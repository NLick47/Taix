use anyhow::{bail, Context, Result};
use std::path::{Path, PathBuf};

use super::Platform;

impl Platform for () {
    fn create_shortcut(target: &Path, shortcut: &Path, _name: &str) -> Result<()> {
        create_shortcut_via_powershell(target, shortcut)
    }

    fn remove_shortcut(shortcut: &Path) -> Result<()> {
        if shortcut.exists() {
            std::fs::remove_file(shortcut)
                .with_context(|| format!("Failed to remove shortcut: {}", shortcut.display()))?;
        }
        Ok(())
    }

    fn register_startup(exe_path: &Path, _name: &str) -> anyhow::Result<()> {
        let exe_str = exe_path.to_str().context("Invalid exe path")?;

        let shell_output = std::process::Command::new(exe_str)
            .arg("install")
            .output()
            .context("Failed to run taix-shell install")?;

        if !shell_output.status.success() {
            bail!(
                "Failed to register startup (taix-shell install): {}",
                String::from_utf8_lossy(&shell_output.stderr)
            );
        }
        Ok(())
    }

    fn unregister_startup(name: &str) -> anyhow::Result<()> {
        let install_dir = Self::default_install_dir();
        let shell_exe = install_dir.join("taix-shell.exe");

        if shell_exe.exists() {
            let output = std::process::Command::new(&shell_exe)
                .arg("uninstall")
                .output();

            if let Ok(output) = output {
                if output.status.success() {
                    return Ok(());
                }
            }
        }

        let _ = std::process::Command::new("schtasks")
            .args(["/DELETE", "/F", "/TN", name])
            .output();

        Ok(())
    }

    fn stop_scheduled_task(name: &str) {
        let _ = std::process::Command::new("schtasks")
            .args(["/END", "/TN", name])
            .output();
    }

    fn stop_process(name: &str) -> Result<bool> {
        let output = std::process::Command::new("taskkill")
            .args(["/F", "/IM", &format!("{}.exe", name)])
            .output()
            .context("Failed to run taskkill")?;

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
        let output = std::process::Command::new("tasklist")
            .args(["/FI", &format!("IMAGENAME eq {}.exe", name), "/NH"])
            .output();

        match output {
            Ok(output) => {
                let stdout = String::from_utf8_lossy(&output.stdout);
                stdout.contains(&format!("{}.exe", name))
            }
            Err(_) => false,
        }
    }

    fn default_install_dir() -> PathBuf {
        let program_files = std::env::var("ProgramFiles")
            .unwrap_or_else(|_| r"C:\Program Files".to_string());
        PathBuf::from(program_files).join("Taix")
    }

    fn start_menu_dir() -> PathBuf {
        dirs::data_dir()
            .unwrap_or_else(|| PathBuf::from(r"C:\Users\Default\AppData\Roaming"))
            .join("Microsoft")
            .join("Windows")
            .join("Start Menu")
            .join("Programs")
    }

    fn desktop_dir() -> PathBuf {
        dirs::desktop_dir().unwrap_or_else(|| PathBuf::from(r"C:\Users\Default\Desktop"))
    }
}

fn create_shortcut_via_powershell(target: &Path, shortcut: &Path) -> Result<()> {
    let target_str = target.to_str().context("Invalid target path")?;
    let shortcut_str = shortcut.to_str().context("Invalid shortcut path")?;
    let work_dir = target.parent().and_then(|p| p.to_str()).unwrap_or(".");

    // 显式设置 IconLocation 为 EXE 自身，避免 Windows 图标缓存
    let script = format!(
        r#"
$ws = New-Object -ComObject WScript.Shell
$sc = $ws.CreateShortcut('{}')
$sc.TargetPath = '{}'
$sc.WorkingDirectory = '{}'
$sc.IconLocation = '{}'
$sc.Save()
"#,
        shortcut_str, target_str, work_dir, target_str
    );

    let output = std::process::Command::new("powershell")
        .args(["-NoProfile", "-NonInteractive", "-Command", &script])
        .output()
        .context("Failed to run PowerShell")?;

    if !output.status.success() {
        bail!(
            "Failed to create shortcut: {}",
            String::from_utf8_lossy(&output.stderr)
        );
    }

    Ok(())
}

#[allow(dead_code)]
pub fn remove_defender_exclusion(path: &Path) -> Result<()> {
    let path_str = path.to_str().context("Invalid path")?;

    let _ = std::process::Command::new("powershell")
        .args([
            "-NoProfile",
            "-Command",
            &format!("Remove-MpPreference -ExclusionPath '{}'", path_str),
        ])
        .output();

    Ok(())
}

#[allow(dead_code)]
pub fn is_elevated() -> bool {
    let output = std::process::Command::new("net")
        .args(["session"])
        .output();

    match output {
        Ok(output) => output.status.success(),
        Err(_) => false,
    }
}
