use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::Arc;
use std::time::Duration;

use windows::core::PCWSTR;
use windows::Win32::Foundation::{HWND, LPARAM, LRESULT, WPARAM};
use windows::Win32::System::LibraryLoader::GetModuleHandleW;
use windows::Win32::UI::Shell::{
    Shell_NotifyIconW, NIF_ICON, NIF_MESSAGE, NIF_TIP, NIM_ADD, NIM_DELETE,
    NOTIFYICONDATAW,
};
use windows::Win32::UI::WindowsAndMessaging::{
    CreateWindowExW, DefWindowProcW, DestroyIcon, DestroyWindow, DispatchMessageW,
    GetWindowLongPtrW, PeekMessageW, PostQuitMessage, RegisterClassW, RegisterWindowMessageW,
    SetWindowLongPtrW, TranslateMessage, CREATESTRUCTW, CW_USEDEFAULT, GWLP_USERDATA, HICON,
    IMAGE_ICON, LR_DEFAULTSIZE, LR_LOADFROMFILE, LoadImageW, MSG, PM_REMOVE, WM_DESTROY,
    WM_LBUTTONUP, WM_NCCREATE, WM_USER, WNDCLASSW, WS_EX_LAYERED, WS_EX_NOACTIVATE,
    WS_EX_TOOLWINDOW, WS_EX_TRANSPARENT, WS_OVERLAPPED,
};

use crate::config::TrayConfig;
use crate::platform::TrayCmd;

const WM_TRAYICON: u32 = WM_USER + 1;
const ID_TRAYICON: u32 = 1;

/// 存储于 GWLP_USERDATA 的托盘数据
struct TrayUserData {
    cmd_tx: std::sync::mpsc::SyncSender<TrayCmd>,
    icon: HICON,
}

/// 延迟获取 TaskbarCreated 已注册消息 ID（explorer 重启时重建托盘图标）
fn taskbar_restart_msg() -> u32 {
    use std::sync::OnceLock;
    static MSG_ID: OnceLock<u32> = OnceLock::new();
    *MSG_ID.get_or_init(|| unsafe {
        let name: Vec<u16> = "TaskbarCreated\0".encode_utf16().collect();
        RegisterWindowMessageW(PCWSTR::from_raw(name.as_ptr()))
    })
}

/// 窗口过程 —— 借鉴 tray-icon crate 的实现模式
unsafe extern "system" fn tray_wnd_proc(
    hwnd: HWND,
    msg: u32,
    wparam: WPARAM,
    lparam: LPARAM,
) -> LRESULT {
    match msg {
        WM_NCCREATE => {
            let cs = &*(lparam.0 as *const CREATESTRUCTW);
            let userdata = cs.lpCreateParams;
            SetWindowLongPtrW(hwnd, GWLP_USERDATA, userdata as isize);
            return DefWindowProcW(hwnd, msg, wparam, lparam);
        }
        WM_DESTROY => {
            let userdata = GetWindowLongPtrW(hwnd, GWLP_USERDATA) as *mut TrayUserData;
            if !userdata.is_null() {
                // 移除托盘图标
                let mut nid: NOTIFYICONDATAW = std::mem::zeroed();
                nid.cbSize = std::mem::size_of::<NOTIFYICONDATAW>() as u32;
                nid.hWnd = hwnd;
                nid.uID = ID_TRAYICON;
                let _ = Shell_NotifyIconW(NIM_DELETE, &nid);
                // 销毁图标
                let _ = DestroyIcon((*userdata).icon);
                drop(Box::from_raw(userdata));
            }
            PostQuitMessage(0);
            return LRESULT(0);
        }
        _ => {}
    }

    let userdata = GetWindowLongPtrW(hwnd, GWLP_USERDATA);

    // 处理托盘图标回调消息
    if userdata != 0 && msg == WM_TRAYICON {
        let event = lparam.0 as u32;
        if event == WM_LBUTTONUP {
            let data = &*(userdata as *const TrayUserData);
            let _ = data.cmd_tx.send(TrayCmd::LaunchClient);
        }
        return LRESULT(0);
    }

    // 处理 explorer 重启
    let restart = taskbar_restart_msg();
    if userdata != 0 && msg == restart {
        let data = &*(userdata as *const TrayUserData);
        register_tray_icon(hwnd, ID_TRAYICON, data.icon);
        return LRESULT(0);
    }

    DefWindowProcW(hwnd, msg, wparam, lparam)
}

pub fn run_tray(
    cmd_tx: std::sync::mpsc::SyncSender<TrayCmd>,
    initial_config: TrayConfig,
    shutdown: Arc<AtomicBool>,
) -> anyhow::Result<()> {
    let icon = load_icon()?;

    // 将 cmd_tx + icon 封装为 heap 对象，窗口过程通过 GWLP_USERDATA 访问
    let userdata = Box::new(TrayUserData {
        cmd_tx: cmd_tx.clone(),
        icon,
    });
    let userdata_ptr = Box::into_raw(userdata);

    let hwnd = create_hidden_window(userdata_ptr as *mut std::ffi::c_void)?;

    // 注册托盘图标
    register_tray_icon(hwnd, ID_TRAYICON, icon);

    if !initial_config.is_visible {
        // 隐藏：NIM_DELETE 再 NIM_ADD 相对繁琐，直接用 NIM_MODIFY + NIF_STATE
        unsafe {
            let mut nid: NOTIFYICONDATAW = std::mem::zeroed();
            nid.cbSize = std::mem::size_of::<NOTIFYICONDATAW>() as u32;
            nid.hWnd = hwnd;
            nid.uID = ID_TRAYICON;
            nid.uFlags = windows::Win32::UI::Shell::NIF_STATE;
            nid.dwState = windows::Win32::UI::Shell::NIS_HIDDEN;
            nid.dwStateMask = windows::Win32::UI::Shell::NIS_HIDDEN;
            let _ = Shell_NotifyIconW(windows::Win32::UI::Shell::NIM_MODIFY, &nid);
        }
    }

    // 消息循环
    unsafe {
        let mut msg: MSG = std::mem::zeroed();
        loop {
            if shutdown.load(Ordering::Relaxed) {
                // 主动销毁窗口，触发 WM_DESTROY → 清理
                let _ = DestroyWindow(hwnd);
                break;
            }

            if PeekMessageW(&mut msg, None, 0, 0, PM_REMOVE).as_bool() {
                if msg.message == WM_DESTROY {
                    // WM_DESTROY 已由窗口过程处理，此处结束消息循环
                    break;
                }
                let _ = TranslateMessage(&msg);
                DispatchMessageW(&msg);
            } else {
                std::thread::sleep(Duration::from_millis(50));
            }
        }
    }

    Ok(())
}

fn load_icon() -> anyhow::Result<HICON> {
    let exe_dir = std::env::current_exe()?
        .parent()
        .ok_or_else(|| anyhow::anyhow!("failed to get exe directory"))?
        .to_path_buf();
    let icon_path = exe_dir.join("resources").join("icons").join("tai32.ico");

    if !icon_path.exists() {
        return Err(anyhow::anyhow!("icon not found: {}", icon_path.display()));
    }

    let path_wide: Vec<u16> = icon_path
        .to_string_lossy()
        .encode_utf16()
        .chain(Some(0))
        .collect();

    unsafe {
        let icon = LoadImageW(
            None,
            PCWSTR::from_raw(path_wide.as_ptr()),
            IMAGE_ICON,
            32,
            32,
            LR_LOADFROMFILE | LR_DEFAULTSIZE,
        )?;
        Ok(HICON(icon.0))
    }
}

fn create_hidden_window(lp_param: *mut std::ffi::c_void) -> anyhow::Result<HWND> {
    let class_name: Vec<u16> = "TaixShellTrayClass\0".encode_utf16().collect();

    unsafe {
        let h_instance = GetModuleHandleW(None)?;

        let wc = WNDCLASSW {
            lpfnWndProc: Some(tray_wnd_proc),
            lpszClassName: PCWSTR::from_raw(class_name.as_ptr()),
            hInstance: h_instance.into(),
            ..std::mem::zeroed()
        };

        if RegisterClassW(&wc) == 0 {
            // 可能已注册，忽略错误（类已存在是预期的）
        }

        // 窗口扩展样式完全匹配 tray-icon crate：
        // WS_EX_NOACTIVATE  — 不接受焦点
        // WS_EX_TRANSPARENT — 鼠标穿透（不可见窗口无需输入）
        // WS_EX_LAYERED     — 分层窗口
        // WS_EX_TOOLWINDOW  — 不显示在任务栏
        let hwnd = CreateWindowExW(
            WS_EX_NOACTIVATE | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW,
            PCWSTR::from_raw(class_name.as_ptr()),
            PCWSTR::null(), // 窗口名设为 NULL
            WS_OVERLAPPED,
            CW_USEDEFAULT,
            0, // height = 0
            CW_USEDEFAULT,
            0, // width = 0
            None,
            None,
            Some(h_instance.into()),
            Some(lp_param), // 通过 lpParam 传递 TrayUserData
        )?;

        Ok(hwnd)
    }
}

fn register_tray_icon(hwnd: HWND, id: u32, icon: HICON) {
    let tooltip: Vec<u16> = "Taix\0".encode_utf16().collect();

    unsafe {
        let mut nid: NOTIFYICONDATAW = std::mem::zeroed();
        nid.cbSize = std::mem::size_of::<NOTIFYICONDATAW>() as u32;
        nid.hWnd = hwnd;
        nid.uID = id;
        nid.uFlags = NIF_ICON | NIF_MESSAGE | NIF_TIP;
        nid.uCallbackMessage = WM_TRAYICON;
        nid.hIcon = icon;
        let tip_len = tooltip.len().min(nid.szTip.len());
        nid.szTip[..tip_len].copy_from_slice(&tooltip[..tip_len]);

        if !Shell_NotifyIconW(NIM_ADD, &nid).as_bool() {
            tracing::error!(target: "taix_shell::tray", "NIM_ADD failed");
        }
    }
}
