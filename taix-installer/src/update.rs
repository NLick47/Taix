use anyhow::{bail, Result};
use std::path::PathBuf;
use std::time::Duration;

use crate::platform::{Platform, PROCESSES, TASK_NAME, save_install_location};
use crate::sfx;
use crate::ui::cli;

pub fn run_update(install_dir: Option<PathBuf>, _silent: bool) -> Result<()> {
    if !sfx::has_payload() {
        bail!("安装程序无效");
    }

    let install_dir = if let Some(dir) = install_dir {
        dir
    } else {
        crate::platform::detect_install_dir()
    };

    if !install_dir.exists() {
        bail!("未找到 Taix 安装");
    }

    println!("\n安装目录: {}", install_dir.display());

    cli::show_step(1, 5, "停止运行中的进程...");
    for proc_name in PROCESSES {
        if <() as Platform>::is_process_running(proc_name) {
            <() as Platform>::stop_process(proc_name)?;
        }
    }

    std::thread::sleep(Duration::from_secs(2));

    // 再次检查
    for proc_name in PROCESSES {
        let mut retries = 0;
        while <() as Platform>::is_process_running(proc_name) && retries < 5 {
            <() as Platform>::stop_process(proc_name)?;
            std::thread::sleep(Duration::from_secs(1));
            retries += 1;
        }
    }

    <() as Platform>::stop_scheduled_task(TASK_NAME);

    cli::show_step(2, 5, "备份当前版本...");
    let backup_dir = install_dir.with_extension("bak");
    if backup_dir.exists() {
        let _ = std::fs::remove_dir_all(&backup_dir);
    }

    let critical_files = [
        "Taix.exe",
        "taix-shell.exe",
        "taix-server.exe",
        "taix-monitor-windows.exe",
    ];

    std::fs::create_dir_all(&backup_dir)?;
    for file in critical_files {
        let src = install_dir.join(file);
        if src.exists() {
            let dst = backup_dir.join(file);
            std::fs::copy(&src, &dst)?;
        }
    }

    cli::show_step(3, 5, "解压更新文件...");
    cli::show_extracting();
    sfx::extract_to(&install_dir)?;

    cli::show_step(4, 5, "验证安装...");
    let shell_exe = install_dir.join("taix-shell.exe");
    if !shell_exe.exists() {
        println!("更新失败，正在恢复备份...");
        restore_backup(&backup_dir, &install_dir)?;
        bail!("更新文件不完整");
    }

    cli::show_step(5, 5, "重启服务...");
    <() as Platform>::register_startup(&shell_exe, TASK_NAME)?;
    let _ = <() as Platform>::start_process(&shell_exe);

    std::thread::sleep(Duration::from_secs(2));

    let _ = std::fs::remove_dir_all(&backup_dir);
    save_install_location(&install_dir)?;

    cli::show_complete("更新完成!");
    println!("安装目录: {}", install_dir.display());
    cli::wait_exit();

    Ok(())
}

fn restore_backup(backup_dir: &PathBuf, install_dir: &PathBuf) -> Result<()> {
    for entry in std::fs::read_dir(backup_dir)? {
        let entry = entry?;
        let src = entry.path();
        let dst = install_dir.join(entry.file_name());
        std::fs::copy(&src, &dst)?;
    }
    Ok(())
}
