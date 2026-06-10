use anyhow::{Context, Result};
use std::path::{Path, PathBuf};

use super::Platform;

const BUNDLE_ID: &str = "com.taix.shell";

impl Platform for () {
    fn create_shortcut(_target: &Path, _shortcut: &Path, _name: &str) -> Result<()> {
        Ok(())
    }

    fn remove_shortcut(_shortcut: &Path) -> Result<()> {
        Ok(())
    }

    fn register_startup(exe_path: &Path, _name: &str) -> Result<()> {
        let exe_str = exe_path.to_str().context("Invalid exe path")?;

        let plist_content = format!(
            r#"<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>{bundle_id}</string>
    <key>ProgramArguments</key>
    <array>
        <string>{exe_path}</string>
        <string>run</string>
    </array>
    <key>RunAtLoad</key>
    <true/>
    <key>KeepAlive</key>
    <true/>
</dict>
</plist>"#,
            bundle_id = BUNDLE_ID,
            exe_path = exe_str
        );

        let launch_agents_dir = dirs::home_dir()
            .unwrap_or_else(|| PathBuf::from("/tmp"))
            .join("Library")
            .join("LaunchAgents");

        std::fs::create_dir_all(&launch_agents_dir)?;

        let plist_path = launch_agents_dir.join(format!("{}.plist", BUNDLE_ID));
        std::fs::write(&plist_path, &plist_content)
            .with_context(|| format!("Failed to write plist: {}", plist_path.display()))?;

        let _ = std::process::Command::new("launchctl")
            .args(["load", "-w", plist_path.to_str().unwrap_or("")])
            .output();

        Ok(())
    }

    fn unregister_startup(_name: &str) -> Result<()> {
        let launch_agents_dir = dirs::home_dir()
            .unwrap_or_else(|| PathBuf::from("/tmp"))
            .join("Library")
            .join("LaunchAgents");

        let plist_path = launch_agents_dir.join(format!("{}.plist", BUNDLE_ID));

        if plist_path.exists() {
            let _ = std::process::Command::new("launchctl")
                .args(["unload", plist_path.to_str().unwrap_or("")])
                .output();

            std::fs::remove_file(&plist_path)?;
        }

        Ok(())
    }

    fn stop_process(name: &str) -> Result<bool> {
        let output = std::process::Command::new("pkill")
            .args(["-f", name])
            .output()
            .context("Failed to run pkill")?;

        Ok(output.status.success())
    }

    fn start_process(exe_path: &Path) -> Result<()> {
        let exe_str = exe_path.to_str().context("Invalid exe path")?;
        let work_dir = exe_path
            .parent()
            .and_then(|p| p.to_str())
            .unwrap_or(".");

        std::process::Command::new(exe_str)
            .current_dir(work_dir)
            .spawn()
            .with_context(|| format!("Failed to start process: {}", exe_str))?;

        Ok(())
    }

    fn is_process_running(name: &str) -> bool {
        let output = std::process::Command::new("pgrep")
            .args(["-x", name])
            .output();

        match output {
            Ok(output) => output.status.success(),
            Err(_) => false,
        }
    }

    fn default_install_dir() -> PathBuf {
        PathBuf::from("/Applications/Taix.app")
    }

    fn start_menu_dir() -> PathBuf {
        PathBuf::from("/Applications")
    }

    fn desktop_dir() -> PathBuf {
        dirs::desktop_dir().unwrap_or_else(|| {
            dirs::home_dir().unwrap_or_else(|| PathBuf::from("~/Desktop"))
        })
    }
}

#[allow(dead_code)]
pub fn generate_info_plist(version: &str) -> String {
    format!(
        r#"<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDevelopmentRegion</key>
    <string>zh-CN</string>
    <key>CFBundleExecutable</key>
    <string>Taix</string>
    <key>CFBundleIdentifier</key>
    <string>com.taix.app</string>
    <key>CFBundleName</key>
    <string>Taix</string>
    <key>CFBundleDisplayName</key>
    <string>Taix</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>{version}</string>
    <key>CFBundleVersion</key>
    <string>{version}</string>
    <key>LSMinimumSystemVersion</key>
    <string>12.0</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>LSUIElement</key>
    <false/>
</dict>
</plist>"#
    )
}

#[allow(dead_code)]
pub fn create_app_bundle(dest: &Path, _version: &str) -> Result<()> {
    let contents = dest.join("Contents");
    let macos_dir = contents.join("MacOS");
    let resources_dir = contents.join("Resources");

    std::fs::create_dir_all(&macos_dir)
        .with_context(|| format!("Failed to create MacOS dir: {}", macos_dir.display()))?;
    std::fs::create_dir_all(&resources_dir)
        .with_context(|| format!("Failed to create Resources dir: {}", resources_dir.display()))?;

    Ok(())
}
