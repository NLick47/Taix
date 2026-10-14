use std::collections::HashMap;
use std::path::PathBuf;
use std::time::Duration;

use taix_plugin_manifest::{resolve_entry, scan, Installed};

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

    fn desired(&self) -> HashMap<String, (Installed, PathBuf)> {
        let enabled = read_enabled(&self.data_dir);
        let mut out = HashMap::new();

        for installed in scan(&self.plugins_dir) {
            if !enabled.get(&installed.manifest.id).copied().unwrap_or(false) {
                continue;
            }
            let dir = installed.dir();
            let entry = installed.manifest.entry.clone();
            let Some(entry_path) = resolve_entry(&dir, &entry) else {
                tracing::warn!(
                    target: "taix_shell::plugins",
                    "plugin '{}': entry {} not found in {}, skipping",
                    installed.manifest.id,
                    entry,
                    dir.display()
                );
                continue;
            };
            out.insert(installed.manifest.id.clone(), (installed, entry_path));
        }
        out
    }

    fn reconcile(&self, running: &mut HashMap<String, RunningPlugin>) {
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
            tracing::warn!(target: "taix_shell::plugins", "registry.json 解析失败（{}）", e);
            HashMap::new()
        })
}
