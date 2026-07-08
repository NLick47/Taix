use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::Arc;
use std::thread;

use anyhow::Result;
use image::GenericImageView;
use serde::Deserialize;
use tao::event_loop::{ControlFlow, EventLoop, EventLoopBuilder, EventLoopProxy};
use tao::event::WindowEvent;
use tao::window::WindowBuilder;
use tao::window::Icon;
#[cfg(target_os = "windows")]
use tao::platform::windows::WindowExtWindows;
use wry::WebViewBuilder;

use crate::platform;
use crate::platform::restore_backup;
use crate::sfx;

const WINDOW_WIDTH: f64 = 660.0;
const WINDOW_HEIGHT: f64 = 480.0;

#[derive(Debug)]
enum InstEvent {
    Progress { step: u32, total: u32, message: String },
    StatusText(String),
    Extracting,
    Complete(String),
    Error(String),
    StatusResponse {
        has_payload: bool,
        existing_install: Option<String>,
        default_dir: String,
    },
    DirSelected(Option<String>),
    Close,
    WorkerDone,
}

#[derive(Deserialize)]
struct IpcMessage {
    cmd: String,
    payload: Option<serde_json::Value>,
}

fn frontend_html() -> &'static str {
    include_str!("../../frontend/index.html")
}

fn load_icon() -> Option<Icon> {
    let png_data = include_bytes!("../../resources/app_icon.png");
    let img = image::load_from_memory(png_data).ok()?;
    let (w, h) = img.dimensions();
    let rgba = img.to_rgba8().into_raw();
    Icon::from_rgba(rgba, w, h).ok()
}

pub fn run_gui() {
    let event_loop: EventLoop<InstEvent> =
        EventLoopBuilder::<InstEvent>::with_user_event().build();
    let proxy = event_loop.create_proxy();

    let window = WindowBuilder::new()
        .with_title("Taix 安装程序")
        .with_inner_size(tao::dpi::LogicalSize::new(WINDOW_WIDTH, WINDOW_HEIGHT))
        .with_resizable(false)
        .with_decorations(true)
        .with_window_icon(load_icon())
        .build(&event_loop)
        .expect("Failed to create window");

    #[cfg(target_os = "windows")]
    apply_window_style(&window);

    #[cfg(target_os = "windows")]
    let hwnd = window.hwnd() as isize;
    #[cfg(not(target_os = "windows"))]
    let hwnd = 0isize;

    let worker_running = Arc::new(AtomicBool::new(false));

    let webview = WebViewBuilder::new()
        .with_background_color((247, 247, 250, 255))
        .with_html(frontend_html())
        .with_navigation_handler(|uri| {
            let url = uri.to_string();
            if url.starts_with("https://") || url.starts_with("http://") {
                #[cfg(target_os = "windows")]
                unsafe {
                    use windows::Win32::UI::Shell::ShellExecuteW;
                    use windows::Win32::UI::WindowsAndMessaging::SW_SHOW;
                    let _ = ShellExecuteW(
                        None,
                        None,
                        &windows::core::HSTRING::from(&url),
                        None,
                        None,
                        SW_SHOW,
                    );
                }
                false
            } else {
                true
            }
        })
        .with_ipc_handler({
            let proxy = proxy.clone();
            let worker_running = worker_running.clone();
            let hwnd_ipc = hwnd;
            move |req| {
                let body = req.body();
                let parsed: IpcMessage = match serde_json::from_str(body) {
                    Ok(m) => m,
                    Err(_) => return,
                };
                handle_ipc(parsed, &proxy, &worker_running, hwnd_ipc);
            }
        })
        .build(&window)
        .expect("Failed to create webview");

    event_loop.run(move |event, _target, control_flow| {
        *control_flow = ControlFlow::Wait;

        match event {
            tao::event::Event::WindowEvent {
                event: WindowEvent::CloseRequested,
                ..
            } => {
                if worker_running.load(Ordering::SeqCst) {
                    let _ = webview.evaluate_script(
                        "window.onRustMessage(JSON.stringify({type:'confirm_close'}))",
                    );
                } else {
                    *control_flow = ControlFlow::Exit;
                }
            }
            tao::event::Event::WindowEvent {
                event: WindowEvent::Moved(_),
                ..
            } => {}
            tao::event::Event::UserEvent(evt) => match evt {
                InstEvent::Close => {
                    *control_flow = ControlFlow::Exit;
                }
                InstEvent::WorkerDone => {
                    worker_running.store(false, Ordering::SeqCst);
                }
                InstEvent::Progress { step, total, message } => {
                    let json = serde_json::json!({
                        "type": "progress",
                        "step": step,
                        "total": total,
                        "message": message
                    });
                    let _ = webview.evaluate_script(&format!(
                        "window.onRustMessage({})",
                        json.to_string()
                    ));
                }
                InstEvent::StatusText(msg) => {
                    let json = serde_json::json!({
                        "type": "status_text",
                        "message": msg
                    });
                    let _ = webview.evaluate_script(&format!(
                        "window.onRustMessage({})",
                        json.to_string()
                    ));
                }
                InstEvent::Extracting => {
                    let _ = webview.evaluate_script(
                        "window.onRustMessage(JSON.stringify({type:'extracting'}))",
                    );
                }
                InstEvent::Complete(msg) => {
                    let json = serde_json::json!({
                        "type": "complete",
                        "message": msg
                    });
                    let _ = webview.evaluate_script(&format!(
                        "window.onRustMessage({})",
                        json.to_string()
                    ));
                }
                InstEvent::Error(msg) => {
                    let json = serde_json::json!({
                        "type": "error",
                        "message": msg
                    });
                    let _ = webview.evaluate_script(&format!(
                        "window.onRustMessage({})",
                        json.to_string()
                    ));
                }
                InstEvent::StatusResponse {
                    has_payload,
                    existing_install,
                    default_dir,
                } => {
                    let json = serde_json::json!({
                        "type": "status",
                        "has_payload": has_payload,
                        "existing_install": existing_install,
                        "default_dir": default_dir
                    });
                    let _ = webview.evaluate_script(&format!(
                        "window.onRustMessage({})",
                        json.to_string()
                    ));
                }
                InstEvent::DirSelected(path) => {
                    let json = serde_json::json!({
                        "type": "dir_selected",
                        "path": path
                    });
                    let _ = webview.evaluate_script(&format!(
                        "window.onRustMessage({})",
                        json.to_string()
                    ));
                }
            },
            _ => {}
        }
    });
}

fn handle_ipc(
    msg: IpcMessage,
    proxy: &EventLoopProxy<InstEvent>,
    worker_running: &Arc<AtomicBool>,
    hwnd: isize,
) {
    match msg.cmd.as_str() {
        "get_status" => {
            let has_payload = sfx::has_payload();
            let existing_install = platform::read_install_location()
                .map(|p| p.to_string_lossy().to_string());
            let default_dir = <() as platform::Platform>::default_install_dir()
                .to_string_lossy()
                .to_string();

            let _ = proxy.send_event(InstEvent::StatusResponse {
                has_payload,
                existing_install,
                default_dir,
            });
        }
        "install" | "update" | "uninstall" => {
            let dir = msg
                .payload
                .and_then(|p| p.get("dir").and_then(|v| v.as_str().map(String::from)));
            let cmd = msg.cmd.clone();
            let proxy = proxy.clone();
            let worker_running = worker_running.clone();
            worker_running.store(true, Ordering::SeqCst);

            thread::spawn(move || {
                let result = match cmd.as_str() {
                    "install" => run_install_with_progress(dir, &proxy),
                    "update" => run_update_with_progress(dir, &proxy),
                    "uninstall" => run_uninstall_with_progress(dir, &proxy),
                    _ => Err(anyhow::anyhow!("未知操作")),
                };

                match result {
                    Ok(msg) => {
                        let _ = proxy.send_event(InstEvent::Complete(msg));
                    }
                    Err(e) => {
                        let _ = proxy.send_event(InstEvent::Error(format!("{}", e)));
                    }
                }
                let _ = proxy.send_event(InstEvent::WorkerDone);
            });
        }
        "browse_dir" => {
            let default = msg
                .payload
                .and_then(|p| p.get("default").and_then(|v| v.as_str().map(String::from)))
                .unwrap_or_default();

            let selected = browse_folder(&default, hwnd);
            let _ = proxy.send_event(InstEvent::DirSelected(selected));
        }
        "open_link" => {
            let url = msg
                .payload
                .and_then(|p| p.get("url").and_then(|v| v.as_str().map(String::from)))
                .unwrap_or_default();
            if !url.is_empty() {
                #[cfg(target_os = "windows")]
                unsafe {
                    use windows::Win32::UI::Shell::ShellExecuteW;
                    use windows::Win32::UI::WindowsAndMessaging::SW_SHOW;
                    let _ = ShellExecuteW(
                        None,
                        None,
                        &windows::core::HSTRING::from(&url),
                        None,
                        None,
                        SW_SHOW,
                    );
                }
            }
        }
        "close" => { let _ = proxy.send_event(InstEvent::Close); }
        "force_close" => { let _ = proxy.send_event(InstEvent::Close); }
        _ => {}
    }
}

fn run_install_with_progress(
    dir: Option<String>,
    proxy: &EventLoopProxy<InstEvent>,
) -> Result<String> {
    if !sfx::has_payload() {
        anyhow::bail!("安装程序无效");
    }

    let dest = if let Some(ref d) = dir {
        std::path::PathBuf::from(d)
    } else {
        <() as platform::Platform>::default_install_dir()
    };

    fn progress(step: u32, total: u32, msg: &str, proxy: &EventLoopProxy<InstEvent>) {
        let _ = proxy.send_event(InstEvent::Progress {
            step,
            total,
            message: msg.to_string(),
        });
    }

    progress(1, 4, "检查运行中的进程...", proxy);
    // 先停看门狗，避免进程被自动重启
    <() as platform::Platform>::stop_scheduled_task(platform::TASK_NAME);

    for proc_name in platform::PROCESSES {
        if <() as platform::Platform>::is_process_running(proc_name) {
            <() as platform::Platform>::stop_process(proc_name)?;
        }
    }

    std::thread::sleep(std::time::Duration::from_secs(2));
    for proc_name in platform::PROCESSES {
        let mut retries = 0;
        while <() as platform::Platform>::is_process_running(proc_name) && retries < 5 {
            <() as platform::Platform>::stop_process(proc_name)?;
            std::thread::sleep(std::time::Duration::from_secs(1));
            retries += 1;
        }
        if <() as platform::Platform>::is_process_running(proc_name) {
            return Err(anyhow::anyhow!(
                "无法停止进程: {}\n请手动关闭后重试",
                proc_name
            ));
        }
    }

    progress(2, 4, "解压文件...", proxy);
    let _ = proxy.send_event(InstEvent::Extracting);
    sfx::extract_to(&dest)?;

    progress(3, 4, "创建快捷方式...", proxy);
    let client_exe = dest.join("Taix.exe");
    let start_menu_dir = <() as platform::Platform>::start_menu_dir();
    let start_menu_sc = start_menu_dir.join("Taix.lnk");
    if client_exe.exists() {
        <() as platform::Platform>::create_shortcut(&client_exe, &start_menu_sc, "Taix")?;
    }

    progress(4, 4, "注册开机启动...", proxy);
    let shell_exe = dest.join("taix-shell.exe");
    if shell_exe.exists() {
        <() as platform::Platform>::register_startup(&shell_exe, platform::TASK_NAME)?;
    }
    if shell_exe.exists() {
        let _ = <() as platform::Platform>::start_process(&shell_exe);
    }

    platform::save_install_location(&dest)?;

    Ok(format!("安装完成!\n安装目录: {}", dest.display()))
}

fn run_update_with_progress(
    dir: Option<String>,
    proxy: &EventLoopProxy<InstEvent>,
) -> Result<String> {
    if !sfx::has_payload() {
        anyhow::bail!("安装程序无效");
    }

    let install_dir = if let Some(ref d) = dir {
        std::path::PathBuf::from(d)
    } else {
        platform::detect_install_dir()
    };

    if !install_dir.exists() {
        anyhow::bail!("未找到 Taix 安装");
    }

    fn progress(step: u32, total: u32, msg: &str, proxy: &EventLoopProxy<InstEvent>) {
        let _ = proxy.send_event(InstEvent::Progress {
            step,
            total,
            message: msg.to_string(),
        });
    }

    progress(1, 5, "停止运行中的进程...", proxy);
    // 先停看门狗，避免进程被自动重启
    <() as platform::Platform>::stop_scheduled_task(platform::TASK_NAME);

    for proc_name in platform::PROCESSES {
        if <() as platform::Platform>::is_process_running(proc_name) {
            <() as platform::Platform>::stop_process(proc_name)?;
        }
    }
    std::thread::sleep(std::time::Duration::from_secs(2));
    for proc_name in platform::PROCESSES {
        let mut retries = 0;
        while <() as platform::Platform>::is_process_running(proc_name) && retries < 5 {
            <() as platform::Platform>::stop_process(proc_name)?;
            std::thread::sleep(std::time::Duration::from_secs(1));
            retries += 1;
        }
        if <() as platform::Platform>::is_process_running(proc_name) {
            return Err(anyhow::anyhow!(
                "无法停止进程: {}\n请手动关闭后重试",
                proc_name
            ));
        }
    }

    progress(2, 5, "备份当前版本...", proxy);
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
    for file in &critical_files {
        let src = install_dir.join(file);
        if src.exists() {
            let dst = backup_dir.join(file);
            std::fs::copy(&src, &dst)?;
        }
    }

    progress(3, 5, "解压更新文件...", proxy);
    let _ = proxy.send_event(InstEvent::Extracting);
    sfx::extract_to(&install_dir)?;

    progress(4, 5, "验证安装...", proxy);
    let critical_files = ["Taix.exe", "taix-shell.exe", "taix-server.exe", "taix-monitor-windows.exe"];
    for file in &critical_files {
        if !install_dir.join(file).exists() {
            let _ = proxy.send_event(InstEvent::StatusText("更新失败，正在恢复备份...".into()));
            restore_backup(&backup_dir, &install_dir)?;
            anyhow::bail!("缺少核心文件: {}", file);
        }
    }

    progress(5, 5, "重启服务...", proxy);
    let shell_exe = install_dir.join("taix-shell.exe");
    <() as platform::Platform>::register_startup(&shell_exe, platform::TASK_NAME)?;
    let _ = <() as platform::Platform>::start_process(&shell_exe);

    std::thread::sleep(std::time::Duration::from_secs(2));
    let _ = std::fs::remove_dir_all(&backup_dir);
    platform::save_install_location(&install_dir)?;

    Ok(format!("更新完成!\n安装目录: {}", install_dir.display()))
}

fn run_uninstall_with_progress(
    dir: Option<String>,
    proxy: &EventLoopProxy<InstEvent>,
) -> Result<String> {
    let install_dir = if let Some(ref d) = dir {
        std::path::PathBuf::from(d)
    } else {
        platform::detect_install_dir()
    };

    if !install_dir.exists() {
        anyhow::bail!("Taix 安装目录不存在");
    }

    fn progress(step: u32, total: u32, msg: &str, proxy: &EventLoopProxy<InstEvent>) {
        let _ = proxy.send_event(InstEvent::Progress {
            step,
            total,
            message: msg.to_string(),
        });
    }

    progress(1, 4, "停止运行中的进程...", proxy);
    for proc_name in platform::PROCESSES {
        let _ = <() as platform::Platform>::stop_process(proc_name);
    }
    std::thread::sleep(std::time::Duration::from_secs(1));

    progress(2, 4, "删除快捷方式...", proxy);
    let start_menu_sc = <() as platform::Platform>::start_menu_dir().join("Taix.lnk");
    let desktop_sc = <() as platform::Platform>::desktop_dir().join("Taix.lnk");
    let _ = <() as platform::Platform>::remove_shortcut(&start_menu_sc);
    let _ = <() as platform::Platform>::remove_shortcut(&desktop_sc);

    progress(3, 4, "注销开机启动...", proxy);
    let _ = <() as platform::Platform>::unregister_startup(platform::TASK_NAME);

    #[cfg(target_os = "windows")]
    {
        let reg_path = r"Software\Microsoft\Windows\CurrentVersion\Run";
        let old_keys = ["Taix", "TaixServer", "TaixMonitor", "TaixShell"];
        for key in &old_keys {
            let _ = std::process::Command::new("reg")
                .args(["delete", &format!("HKCU\\{}", reg_path), "/v", key, "/f"])
                .output();
        }
        let legacy_tasks = ["TaixMonitor", "TaixServer"];
        for task in &legacy_tasks {
            let _ = std::process::Command::new("schtasks")
                .args(["/DELETE", "/F", "/TN", task])
                .output();
        }
    }

    progress(4, 4, "删除程序文件...", proxy);
    let exe_files = [
        "Taix.exe",
        "taix-shell.exe",
        "taix-server.exe",
        "taix-monitor-windows.exe",
    ];
    for exe in &exe_files {
        let path = install_dir.join(exe);
        if path.exists() {
            let _ = std::fs::remove_file(&path);
        }
    }
    let resources_dir = install_dir.join("resources");
    if resources_dir.exists() {
        let _ = std::fs::remove_dir_all(&resources_dir);
    }

    platform::remove_install_location()?;

    Ok("卸载完成!\n注意: 用户数据已保留".to_string())
}

fn browse_folder(default: &str, hwnd: isize) -> Option<String> {
    #[allow(non_snake_case)]
    unsafe {
        use windows::Win32::System::Com::{CoInitializeEx, COINIT_APARTMENTTHREADED, CoCreateInstance, CLSCTX_INPROC_SERVER};
        use windows::Win32::UI::Shell::{
            IFileDialog, IShellItem, FOS_PICKFOLDERS, FOS_PATHMUSTEXIST,
            SIGDN_FILESYSPATH, SHCreateItemFromParsingName,
        };
        use windows::Win32::Foundation::HWND;
        use windows::core::HSTRING;

        let _ = CoInitializeEx(None, COINIT_APARTMENTTHREADED);

        let dialog: IFileDialog = CoCreateInstance(
            &windows::Win32::UI::Shell::FileOpenDialog,
            None,
            CLSCTX_INPROC_SERVER,
        )
        .ok()?;

        dialog.SetOptions(FOS_PICKFOLDERS | FOS_PATHMUSTEXIST).ok()?;

        if !default.is_empty() {
            if let Ok(item) = SHCreateItemFromParsingName::<_, _, IShellItem>(
                &HSTRING::from(default),
                None,
            ) {
                let _ = dialog.SetFolder(&item);
            }
        }

        dialog.SetTitle(&HSTRING::from("选择 Taix 安装目录")).ok()?;

        let parent = HWND(hwnd as *mut std::ffi::c_void);
        dialog.Show(Some(parent)).ok()?;

        let item = dialog.GetResult().ok()?;
        let path = item.GetDisplayName(SIGDN_FILESYSPATH).ok()?;
        Some(path.to_string().ok()?)
    }
}

#[cfg(target_os = "windows")]
fn apply_window_style(window: &tao::window::Window) {
    use windows::Win32::Graphics::Dwm::{DwmSetWindowAttribute, DWMWINDOWATTRIBUTE};

    let hwnd = window.hwnd() as *mut std::ffi::c_void;
    let hwnd = windows::Win32::Foundation::HWND(hwnd);

    unsafe {
        // DWMWA_USE_IMMERSIVE_DARK_MODE (20) = 0 → light theme
        let light: i32 = 0;
        let _ = DwmSetWindowAttribute(
            hwnd,
            DWMWINDOWATTRIBUTE(20),
            &light as *const _ as *const std::ffi::c_void,
            std::mem::size_of::<i32>() as u32,
        );

        // DWMWA_WINDOW_CORNER_PREFERENCE (33) → DWMWCP_ROUND (2)
        let corner_pref: i32 = 2;
        let _ = DwmSetWindowAttribute(
            hwnd,
            DWMWINDOWATTRIBUTE(33),
            &corner_pref as *const _ as *const std::ffi::c_void,
            std::mem::size_of::<i32>() as u32,
        );
    }
}