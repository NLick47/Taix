use anyhow::{bail, Result};
use std::path::PathBuf;

use crate::platform::{Platform, PROCESSES, TASK_NAME, save_install_location, remove_install_location};
use crate::sfx;
use crate::ui::cli;

pub fn run_install(
    install_dir: Option<PathBuf>,
    _silent: bool,
    _desktop_shortcut: bool,
    _defender_exclusion: bool,
) -> Result<()> {
    if !sfx::has_payload() {
        bail!("安装程序无效");
    }

    let dest = if let Some(dir) = install_dir {
        dir
    } else {
        let default = <() as Platform>::default_install_dir();
        cli::prompt_install_dir(&default)?
    };

    cli::show_step(1, 4, "检查运行中的进程...");
    for proc_name in PROCESSES {
        if <() as Platform>::is_process_running(proc_name) {
            <() as Platform>::stop_process(proc_name)?;
        }
    }

    std::thread::sleep(std::time::Duration::from_secs(2));

    for proc_name in PROCESSES {
        let mut retries = 0;
        while <() as Platform>::is_process_running(proc_name) && retries < 5 {
            <() as Platform>::stop_process(proc_name)?;
            std::thread::sleep(std::time::Duration::from_secs(1));
            retries += 1;
        }
    }

    cli::show_step(2, 4, "解压文件...");
    cli::show_extracting();
    sfx::extract_to(&dest)?;

    cli::show_step(3, 4, "创建快捷方式...");
    let client_exe = dest.join("Taix.exe");
    let start_menu_dir = <() as Platform>::start_menu_dir();
    let start_menu_sc = start_menu_dir.join("Taix.lnk");

    if client_exe.exists() {
        <() as Platform>::create_shortcut(&client_exe, &start_menu_sc, "Taix")?;
    }

    cli::show_step(4, 4, "注册开机启动...");
    let shell_exe = dest.join("taix-shell.exe");
    if shell_exe.exists() {
        <() as Platform>::register_startup(&shell_exe, TASK_NAME)?;
    }

    // 启动 taix-shell
    if shell_exe.exists() {
        let _ = <() as Platform>::start_process(&shell_exe);
    }

    save_install_location(&dest)?;

    cli::show_complete("安装完成!");
    println!("安装目录: {}", dest.display());
    cli::wait_exit();

    Ok(())
}

pub fn run_uninstall(install_dir: Option<PathBuf>, _silent: bool) -> Result<()> {
    let install_dir = if let Some(dir) = install_dir {
        dir
    } else {
        crate::platform::detect_install_dir()
    };

    if !install_dir.exists() {
        bail!("Taix 安装目录不存在");
    }

    cli::show_step(1, 4, "停止运行中的进程...");
    for proc_name in PROCESSES {
        <() as Platform>::stop_process(proc_name)?;
    }
    std::thread::sleep(std::time::Duration::from_secs(1));

    cli::show_step(2, 4, "删除快捷方式...");
    let start_menu_sc = <() as Platform>::start_menu_dir().join("Taix.lnk");
    let desktop_sc = <() as Platform>::desktop_dir().join("Taix.lnk");
    let _ = <() as Platform>::remove_shortcut(&start_menu_sc);
    let _ = <() as Platform>::remove_shortcut(&desktop_sc);

    cli::show_step(3, 4, "注销开机启动...");
    let _ = <() as Platform>::unregister_startup(TASK_NAME);

    // 清理旧版注册表项
    #[cfg(target_os = "windows")]
    {
        let reg_path = r"Software\Microsoft\Windows\CurrentVersion\Run";
        let old_keys = ["Taix", "TaixServer", "TaixMonitor", "TaixShell"];
        for key in old_keys {
            let _ = std::process::Command::new("reg")
                .args(["delete", &format!("HKCU\\{}", reg_path), "/v", key, "/f"])
                .output();
        }

        let legacy_tasks = ["TaixMonitor", "TaixServer"];
        for task in legacy_tasks {
            let _ = std::process::Command::new("schtasks")
                .args(["/DELETE", "/F", "/TN", task])
                .output();
        }
    }

    cli::show_step(4, 4, "删除程序文件...");
    let exe_files = [
        "Taix.exe",
        "taix-shell.exe",
        "taix-server.exe",
        "taix-monitor-windows.exe",
    ];

    for exe in exe_files {
        let path = install_dir.join(exe);
        if path.exists() {
            let _ = std::fs::remove_file(&path);
        }
    }

    let resources_dir = install_dir.join("resources");
    if resources_dir.exists() {
        let _ = std::fs::remove_dir_all(&resources_dir);
    }

    remove_install_location()?;

    cli::show_complete("卸载完成!");
    println!("注意: 用户数据已保留");
    cli::wait_exit();

    Ok(())
}
