use std::path::{Path, PathBuf};
use std::process::Command;

const SCHTASKS: &str = r"C:\Windows\System32\schtasks.exe";

fn xml_escape(s: &str) -> String {
    s.replace('&', "&amp;")
        .replace('<', "&lt;")
        .replace('>', "&gt;")
        .replace('"', "&quot;")
        .replace('\'', "&apos;")
}

fn build_task_xml(exe_path: &Path, data_dir: Option<&PathBuf>, task_name: &str) -> String {
    let work_dir = exe_path
        .parent()
        .and_then(|p| p.to_str())
        .unwrap_or(".");

    let exe_str = xml_escape(&exe_path.to_string_lossy());
    let work_str = xml_escape(work_dir);

    let args = if let Some(dir) = data_dir {
        format!("--data-dir {}", xml_escape(&dir.to_string_lossy()))
    } else {
        String::new()
    };

    format!(
        r#"<?xml version="1.0" encoding="UTF-16"?>
<Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
  <RegistrationInfo>
    <Description>{} - auto-launch task</Description>
  </RegistrationInfo>
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
      <Delay>PT30S</Delay>
    </LogonTrigger>
  </Triggers>
  <Principals>
    <Principal id="Author">
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>true</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <IdleSettings>
      <StopOnIdleEnd>false</StopOnIdleEnd>
      <RestartOnIdle>false</RestartOnIdle>
    </IdleSettings>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>true</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <WakeToRun>false</WakeToRun>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <RestartOnFailure>
      <Interval>PT1M</Interval>
      <Count>999</Count>
    </RestartOnFailure>
  </Settings>
  <Actions Context="Author">
    <Exec>
      <Command>{}</Command>
      <Arguments>{}</Arguments>
      <WorkingDirectory>{}</WorkingDirectory>
    </Exec>
  </Actions>
</Task>"#,
        xml_escape(task_name), exe_str, args, work_str
    )
}

fn write_utf16_le_xml(path: &Path, content: &str) -> Result<(), String> {
    let mut bytes: Vec<u8> = Vec::new();
    // UTF-16 LE BOM
    bytes.extend_from_slice(&[0xFF, 0xFE]);
    for unit in content.encode_utf16() {
        bytes.extend_from_slice(&unit.to_le_bytes());
    }
    std::fs::write(path, bytes)
        .map_err(|e| format!("Failed to write temp XML file: {}", e))
}

/// 注册 Task Scheduler 任务。
/// `task_name` 为任务在系统中的唯一名称（如 "TaixMonitor" / "TaixServer"）。
pub fn install(exe_path: &Path, data_dir: Option<&PathBuf>, task_name: &str) -> Result<(), String> {
    let xml = build_task_xml(exe_path, data_dir, task_name);
    let temp_xml = std::env::temp_dir().join(format!("{}_task.xml", task_name));

    write_utf16_le_xml(&temp_xml, &xml)?;

    let output = Command::new(SCHTASKS)
        .args(&[
            "/CREATE",
            "/XML",
            temp_xml.to_str().ok_or("Invalid temp path")?,
            "/TN",
            task_name,
        ])
        .output()
        .map_err(|e| format!("Failed to execute schtasks.exe: {}", e))?;

    let _ = std::fs::remove_file(&temp_xml);

    if output.status.success() {
        Ok(())
    } else {
        let stderr = String::from_utf8_lossy(&output.stderr);
        let stdout = String::from_utf8_lossy(&output.stdout);
        // 如果任务已存在，提供明确提示
        if stderr.contains("already exists") || stdout.contains("already exists") {
            return Err(format!(
                "Task '{}' already exists. Run 'uninstall' first if you want to reconfigure.",
                task_name
            ));
        }
        Err(format!(
            "schtasks.exe failed for '{}'. stdout: {}, stderr: {}",
            task_name,
            stdout.trim(),
            stderr.trim()
        ))
    }
}

/// 注销 Task Scheduler 任务。
pub fn uninstall(task_name: &str) -> Result<(), String> {
    let output = Command::new(SCHTASKS)
        .args(&["/DELETE", "/F", "/TN", task_name])
        .output()
        .map_err(|e| format!("Failed to execute schtasks.exe: {}", e))?;

    if output.status.success() {
        Ok(())
    } else {
        let stderr = String::from_utf8_lossy(&output.stderr);
        let stderr_lower = stderr.to_lowercase();
        // 任务不存在视为已卸载成功
        if stderr_lower.contains("cannot find")
            || stderr_lower.contains("not exist")
            || stderr_lower.contains("不存在")
            || stderr_lower.contains("找不到")
        {
            return Ok(());
        }
        Err(format!(
            "schtasks.exe failed for '{}': {}",
            task_name,
            stderr.trim()
        ))
    }
}

#[allow(dead_code)]
pub fn is_installed(task_name: &str) -> bool {
    Command::new(SCHTASKS)
        .args(&["/QUERY", "/TN", task_name, "/FO", "LIST"])
        .output()
        .map(|o| o.status.success())
        .unwrap_or(false)
}
