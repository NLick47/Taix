use std::collections::HashMap;
use std::path::PathBuf;
use std::time::Duration;

use taix_plugin_manifest::{log_once, resolve_entry, Installed};

use crate::constants;
use crate::service_manager::{ServiceManager, SupervisedTarget};
use crate::shutdown::Shutdown;

struct RunningPlugin {
    shutdown: Shutdown,
    handle: std::thread::JoinHandle<()>,
    manifest_mtime: Option<std::time::SystemTime>,
}

pub struct PluginSupervisor {
    data_dir: PathBuf,
    plugins_dir: PathBuf,
    shutdown: Shutdown,
    /// 全量扫描缓存：(扫描时的目录 mtime, 结果)。mtime 未变则每拍只做一次 stat，
    /// 不再重复 read_dir + 解析全部清单。启用集与 entry 探测仍每拍刷新，启停响应不受影响
    scan_cache: std::sync::Mutex<Option<(Option<std::time::SystemTime>, Vec<Installed>)>>,
}

impl PluginSupervisor {
    pub fn new(data_dir: PathBuf, shutdown: Shutdown) -> Self {
        let plugins_dir = std::env::current_exe()
            .ok()
            .and_then(|p| p.parent().map(|d| d.join(constants::PLUGINS_DIR_NAME)))
            .unwrap_or_else(|| PathBuf::from(constants::PLUGINS_DIR_NAME));
        Self {
            data_dir,
            plugins_dir,
            shutdown,
            scan_cache: std::sync::Mutex::new(None),
        }
    }

    pub fn run(&self) {
        let mut running: HashMap<String, RunningPlugin> = HashMap::new();
        tracing::info!(
            target: "taix_shell::plugins",
            "plugin supervisor started dir={:?} registry={:?}",
            self.plugins_dir,
            self.data_dir.join("plugins").join("registry.json")
        );

        loop {
            if self.shutdown.is_requested() {
                self.stop_all(&mut running);
                return;
            }
            self.reconcile(&mut running);
            if !self
                .shutdown
                .wait_for(Duration::from_secs(constants::PLUGIN_REGISTRY_POLL_SECS))
            {
                self.stop_all(&mut running);
                return;
            }
        }
    }

    /// mtime 门控的清单扫描：目录未变时复用上一拍结果（每次仅一次 stat）。
    /// 清单文件的就地升级不改父目录 mtime，靠 reconcile 里逐插件的 json mtime
    /// 停机检查兜底，新清单内容等下一次目录级变化或重启后生效
    fn scan_cached(&self) -> Vec<Installed> {
        let mut cache = self.scan_cache.lock().unwrap_or_else(|e| e.into_inner());
        let last = match cache.as_ref() {
            Some((mtime, _)) => *mtime,
            None => None,
        };
        // mtime 先于扫描读取：扫描期间落进来的安装会留下更新的 mtime，下一拍兜住
        if let Some((mtime, manifests)) =
            taix_plugin_manifest::scan_if_changed(&self.plugins_dir, last)
        {
            *cache = Some((mtime, manifests));
        }
        cache
            .as_ref()
            .map(|(_, manifests)| manifests.clone())
            .unwrap_or_default()
    }

    fn desired(&self) -> HashMap<String, (Installed, PathBuf)> {
        let enabled = read_enabled(&self.data_dir);
        let mut out = HashMap::new();

        for installed in self.scan_cached() {
            if !enabled.get(&installed.manifest.id).copied().unwrap_or(false) {
                continue;
            }
            let dir = installed.dir();
            let entry = installed.manifest.entry.clone();
            let Some(entry_path) = resolve_entry(&dir, &entry) else {
                let key = format!("entry|{}|{}", installed.manifest.id, entry);
                if log_once(&key) {
                    tracing::warn!(
                        target: "taix_shell::plugins",
                        "plugin '{}': entry {} not found in {}, skipping",
                        installed.manifest.id,
                        entry,
                        dir.display()
                    );
                } else {
                    tracing::debug!(
                        target: "taix_shell::plugins",
                        "plugin '{}': entry {} still missing",
                        installed.manifest.id,
                        entry
                    );
                }
                continue;
            };
            out.insert(installed.manifest.id.clone(), (installed, entry_path));
        }
        out
    }

    fn reconcile(&self, running: &mut HashMap<String, RunningPlugin>) {
        // 监督线程自己退出（连续找不到 entry / panic）时不能一直占着 running，
        // 否则 entry 恢复了也永远不会重试
        let finished: Vec<String> = running
            .iter()
            .filter(|(_, r)| r.handle.is_finished())
            .map(|(id, _)| id.clone())
            .collect();
        for id in finished {
            tracing::warn!(
                target: "taix_shell::plugins",
                "plugin '{}' supervisor exited; will retry",
                id
            );
            running.remove(&id);
        }

        let desired = self.desired();
        let to_stop: Vec<String> = running
            .iter()
            .filter(|(id, r)| match desired.get(*id) {
                None => true,
                Some((installed, _)) => mtime_of(&installed.json_path) != r.manifest_mtime,
            })
            .map(|(id, _)| id.clone())
            .collect();

        for id in to_stop {
            let Some(r) = running.remove(&id) else { continue };
            tracing::info!(
                target: "taix_shell::plugins",
                "plugin '{}' stopping (disabled / removed / upgraded)",
                id
            );
            r.shutdown.request();
            let _ = r.handle.join();
        }

        for (id, (installed, entry_path)) in desired {
            if running.contains_key(&id) {
                continue;
            }
            let manifest_mtime = mtime_of(&installed.json_path);
            let exe_name = entry_path
                .file_name()
                .map(|n| n.to_string_lossy().to_string())
                .unwrap_or_else(|| installed.manifest.entry.clone());
            let target = SupervisedTarget {
                label: format!("plugin:{}", id),
                exe_path: Some(entry_path.clone()),
                exe_name,
                args: vec![
                    "run".to_string(),
                    "--plugin".to_string(),
                    id.clone(),
                    "--data-dir".to_string(),
                    self.data_dir.to_string_lossy().to_string(),
                ],
                capture_output: false,
            };
            let plugin_shutdown = self.shutdown.child();
            let supervise_shutdown = plugin_shutdown.clone();
            let data_dir = self.data_dir.clone();
            let handle = std::thread::Builder::new()
                .name(format!("sup-plugin-{}", id))
                .spawn(move || {
                    ServiceManager::new(Some(data_dir)).supervise(&target, supervise_shutdown);
                })
                .ok();
            let Some(handle) = handle else {
                tracing::error!(target: "taix_shell::plugins", "plugin '{}': failed to spawn supervisor thread", id);
                continue;
            };
            tracing::info!(target: "taix_shell::plugins", "plugin '{}' supervision started entry={:?}", id, entry_path);
            running.insert(
                id,
                RunningPlugin {
                    shutdown: plugin_shutdown,
                    handle,
                    manifest_mtime,
                },
            );
        }
    }

    fn stop_all(&self, running: &mut HashMap<String, RunningPlugin>) {
        for (id, r) in running.drain() {
            tracing::info!(target: "taix_shell::plugins", "plugin '{}' stopping on shutdown", id);
            r.shutdown.request();
            let _ = r.handle.join();
        }
    }
}

fn mtime_of(path: &std::path::Path) -> Option<std::time::SystemTime> {
    std::fs::metadata(path).and_then(|m| m.modified()).ok()
}

fn read_enabled(data_dir: &std::path::Path) -> HashMap<String, bool> {
    let path = data_dir.join("plugins").join("registry.json");
    let Ok(content) = std::fs::read_to_string(&path) else {
        return HashMap::new();
    };
    #[derive(serde::Deserialize)]
    #[serde(rename_all = "camelCase")]
    struct RegistryEnabled {
        #[serde(default)]
        enabled: HashMap<String, bool>,
    }
    let content = content.trim_start_matches('\u{FEFF}');
    serde_json::from_str::<RegistryEnabled>(content)
        .map(|r| r.enabled)
        .unwrap_or_else(|e| {
            // registry 损坏也是每 5s 重扫一次，同样要抑制
            if log_once("registry|parse") {
                tracing::warn!(target: "taix_shell::plugins", "registry.json 解析失败（{}）", e);
            } else {
                tracing::debug!(target: "taix_shell::plugins", "registry.json 仍无法解析");
            }
            HashMap::new()
        })
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::path::Path;
    use taix_plugin_manifest::scan;

    fn temp_dir(tag: &str) -> PathBuf {
        let dir =
            std::env::temp_dir().join(format!("taix-shell-reg-{}-{}", tag, std::process::id()));
        let _ = std::fs::remove_dir_all(&dir);
        std::fs::create_dir_all(dir.join("plugins")).unwrap();
        dir
    }

    fn write_registry(dir: &Path, content: &str) {
        std::fs::write(dir.join("plugins").join("registry.json"), content).unwrap();
    }

    #[test]
    fn missing_registry_means_nothing_enabled() {
        let dir = temp_dir("missing");
        assert!(read_enabled(&dir).is_empty(), "无 registry.json 时全部视为未启用");
        std::fs::remove_dir_all(&dir).ok();
    }

    #[test]
    fn enabled_flags_are_read_from_registry() {
        let dir = temp_dir("flags");
        write_registry(&dir, r#"{"enabled":{"demo":true,"reminder":false},"configs":{}}"#);
        let got = read_enabled(&dir);
        assert_eq!(got.get("demo"), Some(&true));
        assert_eq!(got.get("reminder"), Some(&false));
        assert_eq!(got.get("unknown"), None, "未登记插件按未启用处理");
        std::fs::remove_dir_all(&dir).ok();
    }

    #[test]
    fn malformed_registry_degrades_to_empty() {
        let dir = temp_dir("malformed");
        for bad in ["", "not json", "{}", "[]", r#"{"enabled":null}"#] {
            write_registry(&dir, bad);
            let got = read_enabled(&dir);
            assert!(got.is_empty(), "损坏的 registry 应降级为空而不是 panic: {bad:?}");
        }
        std::fs::remove_dir_all(&dir).ok();
    }

    #[test]
    fn bom_prefixed_registry_is_tolerated() {
        let dir = temp_dir("bom");
        write_registry(&dir, "\u{FEFF}{\"enabled\":{\"demo\":true}}");
        assert_eq!(
            read_enabled(&dir).get("demo"),
            Some(&true),
            "server 原子写可能带 BOM，shell 必须能读"
        );
        std::fs::remove_dir_all(&dir).ok();
    }

    #[test]
    fn entry_resolution_follows_manifest_rules() {
        let dir = temp_dir("entry");
        let plugin = dir.join("plugins").join("demo");
        std::fs::create_dir_all(&plugin).unwrap();
        std::fs::write(plugin.join("taix-plugin-demo"), b"").unwrap();
        std::fs::write(
            plugin.join("plugin.json"),
            r#"{"id":"demo","name":"Demo","version":"1.0.0","entry":"taix-plugin-demo"}"#,
        )
        .unwrap();

        let found = scan(&dir.join("plugins"));
        assert_eq!(found.len(), 1, "合法清单应被扫到");
        let entry = resolve_entry(&found[0].dir(), &found[0].manifest.entry);
        assert!(entry.is_some(), "entry 应能解析到真实文件");

        let missing = resolve_entry(&found[0].dir(), "taix-plugin-nope");
        assert!(missing.is_none(), "缺失的可执行文件应返回 None，由上层告警跳过");
        std::fs::remove_dir_all(&dir).ok();
    }

    #[test]
    fn supervisor_plugins_dir_falls_back_without_panicking() {
        let dir = temp_dir("ctor");
        let sup = PluginSupervisor::new(dir.clone(), Shutdown::new());
        assert!(
            sup.plugins_dir.ends_with(constants::PLUGINS_DIR_NAME),
            "插件目录应指向 <exe 旁>/plugins，实际 {:?}",
            sup.plugins_dir
        );
        std::fs::remove_dir_all(&dir).ok();
    }

    #[test]
    fn scan_cached_reuses_unchanged_dir() {
        let dir = temp_dir("scan-cache");
        let sup = PluginSupervisor {
            data_dir: dir.clone(),
            plugins_dir: dir.join("plugins"),
            shutdown: Shutdown::new(),
            scan_cache: std::sync::Mutex::new(None),
        };

        // 空目录：记录 mtime，重复调用复用缓存
        assert!(sup.scan_cached().is_empty());
        assert!(sup.scan_cached().is_empty());

        // 新增插件目录 → mtime 变化 → 重新扫描
        let demo = dir.join("plugins").join("demo");
        std::fs::create_dir_all(&demo).unwrap();
        std::fs::write(
            demo.join("plugin.json"),
            r#"{"id":"demo","name":"Demo","version":"1.0.0","entry":"taix-plugin-demo"}"#,
        )
        .unwrap();
        let found = sup.scan_cached();
        assert_eq!(found.len(), 1);
        assert_eq!(found[0].manifest.name, "Demo");

        // 清单就地编辑不改父目录 mtime：缓存复用，保持旧内容（停机检查由逐插件 mtime 兜底）
        std::fs::write(
            demo.join("plugin.json"),
            r#"{"id":"demo","name":"Demo2","version":"1.1.0","entry":"taix-plugin-demo"}"#,
        )
        .unwrap();
        let cached = sup.scan_cached();
        assert_eq!(cached[0].manifest.name, "Demo", "未变化应复用缓存");

        // 下一次目录级变化（新增插件）触发重扫，就地编辑的内容随之生效
        let other = dir.join("plugins").join("other");
        std::fs::create_dir_all(&other).unwrap();
        std::fs::write(
            other.join("plugin.json"),
            r#"{"id":"other","name":"Other","version":"1.0.0","entry":"taix-plugin-other"}"#,
        )
        .unwrap();
        let fresh = sup.scan_cached();
        assert_eq!(fresh.len(), 2);
        assert!(
            fresh.iter().any(|i| i.manifest.name == "Demo2"),
            "重扫应读到就地编辑后的新清单"
        );

        std::fs::remove_dir_all(&dir).ok();
    }
}
