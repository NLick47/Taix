use std::path::Path;

pub use taix_plugin_manifest::{valid_id, ConfigField, Installed, PluginManifest};

/// 解析与校验都在共享 crate，与 shell 同一份实现
pub fn scan(plugins_dir: &Path) -> Vec<PluginManifest> {
    taix_plugin_manifest::scan(plugins_dir)
        .into_iter()
        .map(|installed| installed.manifest)
        .collect()
}
