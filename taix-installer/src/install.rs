use anyhow::{bail, Context, Result};
use std::path::PathBuf;
use tracing::info;

use crate::platform::{Platform, PROCESSES, TASK_NAME, save_install_location, remove_install_location};
use crate::sfx;
use crate::ui::cli;

pub fn run_install(
    install_dir: Option<PathBuf>,
    silent: bool,
    desktop_shortcut: bool,
    defender_exclusion: bool,
) -> Result<()> {
    if !sfx::has_payload() {
        bail!("No embedded payload. This installer has no files to install.");
    }

    let dest = if let Some(dir) = install_dir {
        dir
    } else if silent {
        <() as Platform>::default_install_dir()
    } else {
        let default = <() as Platform>::default_install_dir();
        cli::prompt_install_dir(default)?
    };

    let total_steps = 5 + usize::from(desktop_shortcut);
    let mut step = 0;

    if !silent {
        if !cli::confirm_install()? {
            println!("安装已取消。");
            return Ok(());
        }
    }

    // 检查运行中的进程
    step += 1;
    cli::show_step(step, total_steps, "检查运行中的进程...");
    for proc_name in PROCESSES {
        if <() as Platform>::is_process_running(proc_name) {
            info!("Process {} is running, stopping...", proc_name);
            <() as Platform>::stop_process(proc_name)?;
            std::thread::sleep(std::time::Duration::from_secs(1));
        }
    }

    // 解压文件
    step += 1;
    cli::show_step(step, total_steps, "解压文件...");
    sfx::extract_to(&dest)?;

    // 创建开始菜单快捷方式
    step += 1;
    cli::show_step(step, total_steps, "创建快捷方式...");
    let client_exe = dest.join("Taix.exe");
    let start_menu_dir = <() as Platform>::start_menu_dir();
    let start_menu_sc = start_menu_dir.join("Taix.lnk");

    if client_exe.exists() {
        <() as Platform>::create_shortcut(&client_exe, &start_menu_sc, "Taix")?;
    }

    if desktop_shortcut {
        step += 1;
        cli::show_step(step, total_steps, "创建桌面快捷方式...");
        let desktop_sc = <() as Platform>::desktop_dir().join("Taix.lnk");
        if client_exe.exists() {
            <() as Platform>::create_shortcut(&client_exe, &desktop_sc, "Taix")?;
        }
    }

    // 注册开机启动
    step += 1;
    cli::show_step(step, total_steps, "注册开机启动...");
    let shell_exe = dest.join("taix-shell.exe");
    if shell_exe.exists() {
        <() as Platform>::register_startup(&shell_exe, TASK_NAME)?;
    }

    // 添加 Defender 排除
    if defender_exclusion {
        step += 1;
        cli::show_step(step, total_steps, "添加 Windows Defender 排除...");
        #[cfg(target_os = "windows")]
        crate::platform::windows::add_defender_exclusion(&dest)?;
    }

    // 启动 taix-shell
    step += 1;
    cli::show_step(step, total_steps, "启动 Taix...");
    if shell_exe.exists() {
        <() as Platform>::start_process(&shell_exe)?;
    }

    // 保存安装路径
    save_install_location(&dest)?;

    if !silent {
        cli::show_install_complete(&dest);
    } else {
        cli::show_success("Taix 安装完成!");
        println!("安装目录: {}", dest.display());
    }

    Ok(())
}

pub fn run_uninstall(install_dir: Option<PathBuf>, silent: bool) -> Result<()> {
    if !silent {
        if !cli::confirm_uninstall()? {
            println!("卸载已取消。");
            return Ok(());
        }
    }

    let install_dir = if let Some(dir) = install_dir {
        dir
    } else {
        crate::platform::detect_install_dir()
    };

    if !install_dir.exists() {
        bail!("Taix 安装目录不存在: {}", install_dir.display());
    }

    let total_steps = 4;
    let mut step = 0;

    // 停止所有进程
    step += 1;
    cli::show_step(step, total_steps, "停止运行中的进程...");
    for proc_name in PROCESSES {
        <() as Platform>::stop_process(proc_name)?;
    }
    std::thread::sleep(std::time::Duration::from_secs(1));

    // 删除快捷方式
    step += 1;
    cli::show_step(step, total_steps, "删除快捷方式...");
    let start_menu_sc = <() as Platform>::start_menu_dir().join("Taix.lnk");
    let desktop_sc = <() as Platform>::desktop_dir().join("Taix.lnk");
    <() as Platform>::remove_shortcut(&start_menu_sc)?;
    <() as Platform>::remove_shortcut(&desktop_sc)?;

    // 注销开机启动
    step += 1;
    cli::show_step(step, total_steps, "注销开机启动...");
    <() as Platform>::unregister_startup(TASK_NAME)?;

    // 清理旧版注册表项和任务计划
    #[cfg(target_os = "windows")]
    {
        let reg_path = r"Software\Microsoft\Windows\CurrentVersion\Run";
        let old_keys = ["Taix", "TaixServer", "TaixMonitor", "TaixShell"];
        for key in old_keys {
            let output = std::process::Command::new("reg")
                .args(["delete", &format!("HKCU\\{}", reg_path), "/v", key, "/f"])
                .output();
            if let Ok(output) = output {
                if output.status.success() {
                    info!("Cleaned legacy registry entry: {}", key);
                }
            }
        }

        let legacy_tasks = ["TaixMonitor", "TaixServer"];
        for task in legacy_tasks {
            let _ = std::process::Command::new("schtasks")
                .args(["/DELETE", "/F", "/TN", task])
                .output();
        }
    }

    // 删除程序文件 (保留用户数据)
    step += 1;
    cli::show_step(step, total_steps, "删除程序文件...");
    let exe_files = [
        "Taix.exe",
        "taix-shell.exe",
        "taix-server.exe",
        "taix-monitor-windows.exe",
    ];

    for exe in exe_files {
        let path = install_dir.join(exe);
        if path.exists() {
            std::fs::remove_file(&path)
                .with_context(|| format!("Failed to remove: {}", path.display()))?;
            info!("Removed: {}", path.display());
        }
    }

    let resources_dir = install_dir.join("resources");
    if resources_dir.exists() {
        let _ = std::fs::remove_dir_all(&resources_dir);
    }

    // 删除安装路径记录
    remove_install_location()?;

    if !silent {
        cli::show_uninstall_complete(&install_dir);
    } else {
        cli::show_success("Taix 卸载完成!");
        println!("注意: 用户数据已保留。");
    }

    Ok(())
}
