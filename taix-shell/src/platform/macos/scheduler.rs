use std::path::{Path, PathBuf};
use std::process::Command;

const LAUNCH_AGENT_ID: &str = "com.taix.shell";

fn get_plist_path() -> PathBuf {
    dirs::home_dir()
        .unwrap_or_else(|| PathBuf::from("/tmp"))
        .join("Library")
        .join("LaunchAgents")
        .join(format!("{}.plist", LAUNCH_AGENT_ID))
}

fn build_plist_content(exe_path: &Path, data_dir: Option<&PathBuf>) -> String {
    let exe_str = exe_path.to_string_lossy();
    let data_dir_args = if let Some(dir) = data_dir {
        format!(
            "\n\t\t<string>--data-dir</string>\n\t\t<string>{}</string>",
            dir.to_string_lossy()
        )
    } else {
        String::new()
    };

    let log_dir = dirs::home_dir()
        .unwrap_or_else(|| PathBuf::from("/tmp"))
        .join("Library")
        .join("Logs")
        .join("Taix");

    format!(
        r#"<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>{}</string>
    <key>ProgramArguments</key>
    <array>
        <string>{}</string>
        <string>run</string>{}
    </array>
    <key>RunAtLoad</key>
    <true/>
    <key>KeepAlive</key>
    <true/>
    <key>StandardOutPath</key>
    <string>{}/shell.log</string>
    <key>StandardErrorPath</key>
    <string>{}/shell.err</string>
</dict>
</plist>"#,
        LAUNCH_AGENT_ID, exe_str, data_dir_args, log_dir.display(), log_dir.display()
    )
}

pub fn install(exe_path: &Path, data_dir: Option<&PathBuf>, _task_name: &str) -> Result<(), String> {
    let plist_content = build_plist_content(exe_path, data_dir);
    let plist_path = get_plist_path();

    if let Some(parent) = plist_path.parent() {
        std::fs::create_dir_all(parent)
            .map_err(|e| format!("Failed to create LaunchAgents directory: {}", e))?;
    }

    let log_dir = dirs::home_dir()
        .unwrap_or_else(|| PathBuf::from("/tmp"))
        .join("Library")
        .join("Logs")
        .join("Taix");
    std::fs::create_dir_all(&log_dir)
        .map_err(|e| format!("Failed to create Logs directory: {}", e))?;

    std::fs::write(&plist_path, &plist_content)
        .map_err(|e| format!("Failed to write plist: {}", e))?;

    let output = Command::new("launchctl")
        .args(["load", "-w", plist_path.to_str().ok_or("Invalid plist path")?])
        .output()
        .map_err(|e| format!("Failed to execute launchctl: {}", e))?;

    if output.status.success() {
        Ok(())
    } else {
        let stderr = String::from_utf8_lossy(&output.stderr);
        if stderr.contains("service already loaded") || stderr.contains("already loaded") {
            return Err(format!(
                "'{}' is already running. Run 'uninstall' first if you want to reconfigure.",
                LAUNCH_AGENT_ID
            ));
        }
        Err(format!("launchctl load failed: {}", stderr.trim()))
    }
}

pub fn uninstall(_task_name: &str) -> Result<(), String> {
    let plist_path = get_plist_path();

    if plist_path.exists() {
        let _ = Command::new("launchctl")
            .args(["unload", plist_path.to_str().unwrap_or("")])
            .output();

        std::fs::remove_file(&plist_path)
            .map_err(|e| format!("Failed to remove plist: {}", e))?;
    }

    Ok(())
}
