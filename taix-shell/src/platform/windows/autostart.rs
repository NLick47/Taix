use std::path::Path;
use std::process::Command;

const VALUE_NAME: &str = "TaixShell";
const RUN_KEY: &str = r"HKCU\Software\Microsoft\Windows\CurrentVersion\Run";

pub fn ensure_autostart(exe_path: &Path) -> Result<(), String> {
    let exe_str = exe_path.to_string_lossy();
    let output = Command::new("reg.exe")
        .args([
            "add",
            RUN_KEY,
            "/v",
            VALUE_NAME,
            "/t",
            "REG_SZ",
            "/d",
            &exe_str,
            "/f",
        ])
        .output()
        .map_err(|e| format!("Failed to run reg.exe: {}", e))?;

    if output.status.success() {
        Ok(())
    } else {
        Err(format!(
            "reg.exe failed: {}",
            String::from_utf8_lossy(&output.stderr)
        ))
    }
}

pub fn remove_autostart() -> Result<(), String> {
    let output = Command::new("reg.exe")
        .args([
            "delete",
            RUN_KEY,
            "/v",
            VALUE_NAME,
            "/f",
        ])
        .output()
        .map_err(|e| format!("Failed to run reg.exe: {}", e))?;

    if output.status.success() {
        Ok(())
    } else {
        let stderr = String::from_utf8_lossy(&output.stderr);
        let stderr_lower = stderr.to_lowercase();
        if stderr_lower.contains("unable to find")
            || stderr_lower.contains("error: 2")
            || stderr_lower.contains("系统找不到")
            || stderr_lower.contains("cannot find")
        {
            return Ok(());
        }
        Err(format!("reg.exe failed: {}", stderr.trim()))
    }
}
