use anyhow::{bail, Context, Result};
use std::path::PathBuf;
use std::time::Duration;
use tracing::info;

use crate::platform::{Platform, PROCESSES, save_install_location};
use crate::sfx;
use crate::ui::cli;

pub fn run_update(install_dir: Option<PathBuf>, silent: bool) -> Result<()> {
    if !sfx::has_payload() {
        bail!("No embedded payload. This installer has no files to update.");
    }

    let install_dir = if let Some(dir) = install_dir {
        dir
    } else {
        crate::platform::detect_install_dir()
    };

    if !install_dir.exists() {
        bail!(
            "Taix installation not found at: {}",
            install_dir.display()
        );
    }

    info!("Updating installation at: {}", install_dir.display());

    let total_steps = 5;
    let mut step = 0;

    if !silent {
        if !cli::confirm_update()? {
            println!("更新已取消。");
            return Ok(());
        }
    }

    // 停止所有进程
    step += 1;
    cli::show_step(step, total_steps, "停止运行中的进程...");
    for proc_name in PROCESSES {
        if <() as Platform>::is_process_running(proc_name) {
            info!("Stopping process: {}", proc_name);
            <() as Platform>::stop_process(proc_name)?;
        }
    }

    std::thread::sleep(Duration::from_secs(2));

    // 再次检查
    for proc_name in PROCESSES {
        let mut retries = 0;
        while <() as Platform>::is_process_running(proc_name) && retries < 5 {
            info!("Process {} still running, retrying...", proc_name);
            <() as Platform>::stop_process(proc_name)?;
            std::thread::sleep(Duration::from_secs(1));
            retries += 1;
        }

        if <() as Platform>::is_process_running(proc_name) {
            cli::show_warning(&format!("无法停止进程 {}，可能需要手动关闭", proc_name));
        }
    }

    // 备份当前版本
    step += 1;
    cli::show_step(step, total_steps, "备份当前版本...");
    let backup_dir = install_dir.with_extension("bak");
    if backup_dir.exists() {
        std::fs::remove_dir_all(&backup_dir)
            .with_context(|| format!("Failed to remove old backup: {}", backup_dir.display()))?;
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
            std::fs::copy(&src, &dst)
                .with_context(|| format!("Failed to backup: {}", src.display()))?;
        }
    }
    info!("Backup created at: {}", backup_dir.display());

    // 解压新文件
    step += 1;
    cli::show_step(step, total_steps, "解压更新文件...");
    sfx::extract_to(&install_dir)?;

    // 验证关键文件
    step += 1;
    cli::show_step(step, total_steps, "验证安装...");
    let shell_exe = install_dir.join("taix-shell.exe");
    if !shell_exe.exists() {
        cli::show_warning("更新失败，正在恢复备份...");
        restore_backup(&backup_dir, &install_dir)?;
        bail!("Update failed: taix-shell.exe not found after extraction");
    }

    // 重启服务
    step += 1;
    cli::show_step(step, total_steps, "重启服务...");
    <() as Platform>::start_process(&shell_exe)?;

    std::thread::sleep(Duration::from_secs(2));

    if !<() as Platform>::is_process_running("taix-shell") {
        cli::show_warning("taix-shell 未能自动启动，请手动启动");
    }

    let _ = std::fs::remove_dir_all(&backup_dir);

    // 更新安装路径记录
    save_install_location(&install_dir)?;

    if !silent {
        cli::show_update_complete(&install_dir);
    } else {
        cli::show_success("Taix 更新完成!");
        println!("安装目录: {}", install_dir.display());
    }

    Ok(())
}

fn restore_backup(backup_dir: &PathBuf, install_dir: &PathBuf) -> Result<()> {
    for entry in std::fs::read_dir(backup_dir)? {
        let entry = entry?;
        let src = entry.path();
        let dst = install_dir.join(entry.file_name());
        std::fs::copy(&src, &dst)
            .with_context(|| format!("Failed to restore: {}", src.display()))?;
    }
    info!("Backup restored from: {}", backup_dir.display());
    Ok(())
}
