use std::path::Path;

pub use taix_plugin_manifest::{valid_id, ConfigField, Installed, PluginManifest};

/// 解析与校验都在共享 crate，与 shell 同一份实现；目录路径随清单一起保留
pub fn scan(plugins_dir: &Path) -> Vec<Installed> {
    taix_plugin_manifest::scan(plugins_dir)
}
