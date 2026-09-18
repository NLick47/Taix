use std::time::Duration;

use windows::core::PCWSTR;
use windows::Win32::Foundation::{HWND, LPARAM, LRESULT, POINT, WPARAM};
use windows::Win32::System::LibraryLoader::GetModuleHandleW;
use windows::Win32::UI::Shell::{
    Shell_NotifyIconW, NIF_ICON, NIF_MESSAGE, NIF_STATE, NIF_TIP, NIM_ADD, NIM_DELETE, NIM_MODIFY,
    NIS_HIDDEN, NOTIFYICONDATAW,
};
use windows::Win32::UI::WindowsAndMessaging::{
    AppendMenuW, CreatePopupMenu, CreateWindowExW, DefWindowProcW, DestroyIcon, DestroyMenu,
    DestroyWindow, DispatchMessageW, GetCursorPos, GetWindowLongPtrW, LoadImageW, PeekMessageW,
    PostMessageW, PostQuitMessage, RegisterClassW, RegisterWindowMessageW, SetForegroundWindow,
    SetWindowLongPtrW, TrackPopupMenu, TranslateMessage, CREATESTRUCTW, CW_USEDEFAULT,
    GWLP_USERDATA, HICON, IMAGE_ICON, LR_DEFAULTSIZE, LR_LOADFROMFILE, MF_SEPARATOR, MF_STRING,
    MSG, PM_REMOVE, TPM_LEFTBUTTON, TPM_NONOTIFY, TPM_RETURNCMD, WM_DESTROY, WM_LBUTTONDOWN,
    WM_NCCREATE, WM_NULL, WM_RBUTTONUP, WM_USER, WNDCLASSW, WS_EX_LAYERED, WS_EX_NOACTIVATE,
    WS_EX_TOOLWINDOW, WS_EX_TRANSPARENT, WS_OVERLAPPED,
};

use crate::config::TrayConfig;
use crate::i18n::{menu_texts, MenuTexts};
use crate::platform::TrayCmd;
use crate::shutdown::Shutdown;

const WM_TRAYICON: u32 = WM_USER + 1;
const ID_TRAYICON: u32 = 1;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
enum TrayAction {
    Show,
    Exit,
}

impl TrayAction {
    const SHOW: u32 = 1001;
    const EXIT: u32 = 1002;

    const ALL: [Self; 2] = [Self::Show, Self::Exit];

    fn id(self) -> u32 {
        match self {
            Self::Show => Self::SHOW,
            Self::Exit => Self::EXIT,
        }
    }

    fn from_id(id: u32) -> Option<Self> {
        Self::ALL.into_iter().find(|action| action.id() == id)
    }

    fn label(self, texts: &MenuTexts) -> &'static str {
        match self {
            Self::Show => texts.show,
            Self::Exit => texts.quit,
        }
    }
}

fn dispatch(action: TrayAction, data: &TrayUserData) {
    tracing::info!(target: "taix_shell::tray", "dispatching tray action {:?}", action);
    match action {
        TrayAction::Show => {
            if data.cmd_tx.try_send(TrayCmd::LaunchClient).is_err() {
                tracing::warn!(target: "taix_shell::tray", "client launch queue full, dropping request");
            } else {
                tracing::debug!(target: "taix_shell::tray", "LaunchClient handed to the action thread");
            }
        }
        TrayAction::Exit => {
            tracing::info!(target: "taix_shell::tray", "exit requested from the tray menu");
            data.shutdown.request();
        }
    }
}

/// 存储于 GWLP_USERDATA 的托盘数据
struct TrayUserData {
    cmd_tx: std::sync::mpsc::SyncSender<TrayCmd>,
    icon: HICON,
    shutdown: Shutdown,
    texts: &'static MenuTexts,
}

impl TrayUserData {
    unsafe fn from_userdata(hwnd: HWND) -> Option<&'static Self> {
        let ptr = GetWindowLongPtrW(hwnd, GWLP_USERDATA);
        if ptr == 0 { None } else { Some(&*(ptr as *const Self)) }
    }
}

/// 延迟获取 TaskbarCreated 已注册消息 ID
fn taskbar_restart_msg() -> u32 {
    use std::sync::OnceLock;
    static MSG_ID: OnceLock<u32> = OnceLock::new();
    *MSG_ID.get_or_init(|| unsafe {
        let name: Vec<u16> = "TaskbarCreated\0".encode_utf16().collect();
        RegisterWindowMessageW(PCWSTR::from_raw(name.as_ptr()))
    })
}

unsafe fn show_context_menu(hwnd: HWND, texts: &MenuTexts) -> Option<TrayAction> {
    let menu = match CreatePopupMenu() {
        Ok(menu) => menu,
        Err(e) => {
            tracing::error!(target: "taix_shell::tray", "CreatePopupMenu failed: {}", e);
            return None;
        }
    };

    for (index, action) in TrayAction::ALL.into_iter().enumerate() {
        if index > 0 {
            let _ = AppendMenuW(menu, MF_SEPARATOR, 0, None);
        }

        let label = wide(action.label(texts));
        let _ = AppendMenuW(
            menu,
            MF_STRING,
            action.id() as usize,
            PCWSTR::from_raw(label.as_ptr()),
        );
        tracing::debug!(
            target: "taix_shell::tray",
            "menu item added id={} action={:?} label={:?}",
            action.id(), action, action.label(texts)
        );
    }

    let mut point = POINT::default();
    let _ = GetCursorPos(&mut point);
    tracing::debug!(target: "taix_shell::tray", "popping up context menu at ({}, {})", point.x, point.y);

    let _ = SetForegroundWindow(hwnd);
    let selected = TrackPopupMenu(
        menu,
        TPM_LEFTBUTTON | TPM_NONOTIFY | TPM_RETURNCMD,
        point.x,
        point.y,
        Some(0),
        hwnd,
        None,
    )
    .0 as u32;

    // 这一行是验证「菜单选择不再被 TPM_NONOTIFY 吞掉」的关键证据：id 应当等于
    // 被点中那一项的 ID，而不是恒为 0。
    tracing::info!(
        target: "taix_shell::tray",
        "TrackPopupMenu returned id={} → {:?}",
        selected, TrayAction::from_id(selected)
    );

    let _ = DestroyMenu(menu);
    let _ = PostMessageW(Some(hwnd), WM_NULL, WPARAM(0), LPARAM(0));

    TrayAction::from_id(selected)
}

fn wide(text: &str) -> Vec<u16> {
    text.encode_utf16().chain(Some(0)).collect()
}

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
            tracing::info!(
                target: "taix_shell::tray",
                "WM_DESTROY received; removing tray icon and releasing TrayUserData"
            );
            // 先断开 GWLP_USERDATA 再释放：DestroyWindow 之后系统还会补送一个
            // WM_NCDESTROY，窗口过程那时会再读一次 USERDATA。若不清零，
            // TrayUserData::from_userdata 就会基于已释放的内存造出一个引用
            // —— 即使不解引用也是 UB。
            let userdata = GetWindowLongPtrW(hwnd, GWLP_USERDATA) as *mut TrayUserData;
            SetWindowLongPtrW(hwnd, GWLP_USERDATA, 0);
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

    let Some(data) = TrayUserData::from_userdata(hwnd) else {
        return DefWindowProcW(hwnd, msg, wparam, lparam);
    };

    // 托盘图标回调：左键直接唤起客户端，右键弹菜单
    if msg == WM_TRAYICON {
        match lparam.0 as u32 {
            WM_LBUTTONDOWN => {
                tracing::debug!(target: "taix_shell::tray", "tray icon left-click");
                dispatch(TrayAction::Show, data);
            }
            WM_RBUTTONUP => {
                tracing::debug!(target: "taix_shell::tray", "tray icon right-click; opening context menu");
                match show_context_menu(hwnd, data.texts) {
                    Some(action) => dispatch(action, data),
                    None => tracing::debug!(
                        target: "taix_shell::tray",
                        "context menu dismissed without a selection (id=0)"
                    ),
                }
            }
            other => tracing::trace!(
                target: "taix_shell::tray",
                "unhandled tray icon notification lparam={}",
                other
            ),
        }
        return LRESULT(0);
    }

    // explorer 重启后重新注册图标
    if msg == taskbar_restart_msg() {
        tracing::warn!(
            target: "taix_shell::tray",
            "TaskbarCreated received (explorer restarted); re-registering tray icon"
        );
        register_tray_icon(hwnd, ID_TRAYICON, data.icon, &data.shutdown);
        return LRESULT(0);
    }

    DefWindowProcW(hwnd, msg, wparam, lparam)
}

pub fn run_tray(
    cmd_tx: std::sync::mpsc::SyncSender<TrayCmd>,
    config: TrayConfig,
    shutdown: Shutdown,
) -> anyhow::Result<()> {
    tracing::info!(
        target: "taix_shell::tray",
        "run_tray start theme={:?} language={:?} is_visible={}",
        config.theme, config.language, config.is_visible
    );

    // 菜单主题是进程级设置，且必须在任何菜单创建之前应用。
    super::apply_menu_theme(config.theme);

    let icon = match load_icon() {
        Ok(icon) => icon,
        Err(e) => {
            tracing::error!(target: "taix_shell::tray", "failed to load tray icon: {:#}", e);
            return Err(e);
        }
    };
    tracing::debug!(target: "taix_shell::tray", "tray icon loaded");

    // 计划任务启动时托盘区域可能还没就绪，先等一小会儿
    let deadline = std::time::Instant::now() + Duration::from_secs(1);
    while std::time::Instant::now() < deadline {
        if shutdown.is_requested() {
            tracing::info!(
                target: "taix_shell::tray",
                "shutdown during taskbar readiness wait; aborting tray startup"
            );
            return Ok(());
        }
        std::thread::sleep(Duration::from_millis(100));
    }

    let userdata = Box::new(TrayUserData {
        cmd_tx,
        icon,
        shutdown: shutdown.clone(),
        texts: menu_texts(config.language),
    });
    let userdata_ptr = Box::into_raw(userdata);

    let hwnd = match create_hidden_window(userdata_ptr as *mut std::ffi::c_void) {
        Ok(hwnd) => hwnd,
        Err(e) => {
            tracing::error!(target: "taix_shell::tray", "CreateWindowExW failed: {:#}", e);
            return Err(e);
        }
    };
    tracing::info!(target: "taix_shell::tray", "hidden tray window created hwnd={:?}", hwnd);

    register_tray_icon(hwnd, ID_TRAYICON, icon, &shutdown);

    if config.is_visible {
        tracing::info!(target: "taix_shell::tray", "tray icon is visible; awaiting user interaction");
    } else {
        set_icon_hidden(hwnd, ID_TRAYICON);
    }

    tracing::info!(target: "taix_shell::tray", "entering message loop");
    unsafe {
        let mut msg: MSG = std::mem::zeroed();
        loop {
            if shutdown.is_requested() {
                // 主动销毁窗口，触发 WM_DESTROY → 清理图标与 userdata
                tracing::info!(
                    target: "taix_shell::tray",
                    "shutdown observed in message loop; destroying the tray window"
                );
                let _ = DestroyWindow(hwnd);
                break;
            }

            if PeekMessageW(&mut msg, None, 0, 0, PM_REMOVE).as_bool() {
                if msg.message == WM_DESTROY {
                    tracing::debug!(
                        target: "taix_shell::tray",
                        "message loop saw WM_DESTROY; breaking out"
                    );
                    break;
                }
                let _ = TranslateMessage(&msg);
                DispatchMessageW(&msg);
            } else {
                std::thread::sleep(Duration::from_millis(50));
            }
        }
    }

    tracing::info!(target: "taix_shell::tray", "message loop finished; run_tray returning");
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

    let path_wide = wide(&icon_path.to_string_lossy());

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

/// 隐藏已注册的托盘图标（`IsEnableTray=false` 时使用）。
///
/// 进程继续运行以维持对 server / monitor 的监督，只是不在托盘区出现。
fn set_icon_hidden(hwnd: HWND, id: u32) {
    tracing::info!(
        target: "taix_shell::tray",
        "IsEnableTray=false: tray icon hidden, but the process keeps supervising server/monitor"
    );
    unsafe {
        let mut nid: NOTIFYICONDATAW = std::mem::zeroed();
        nid.cbSize = std::mem::size_of::<NOTIFYICONDATAW>() as u32;
        nid.hWnd = hwnd;
        nid.uID = id;
        nid.uFlags = NIF_STATE;
        nid.dwState = NIS_HIDDEN;
        nid.dwStateMask = NIS_HIDDEN;
        let _ = Shell_NotifyIconW(NIM_MODIFY, &nid);
    }
}

/// 注册托盘图标失败后最多重试几次。
const MAX_REGISTER_ATTEMPTS: u32 = 10;

/// 注册托盘图标，失败时指数退避重试（应对计划任务启动时托盘区域未就绪）
fn register_tray_icon(hwnd: HWND, id: u32, icon: HICON, shutdown: &Shutdown) {
    let tooltip = wide("Taix");

    for attempt in 1..=MAX_REGISTER_ATTEMPTS {
        let ok = unsafe {
            let mut nid: NOTIFYICONDATAW = std::mem::zeroed();
            nid.cbSize = std::mem::size_of::<NOTIFYICONDATAW>() as u32;
            nid.hWnd = hwnd;
            nid.uID = id;
            nid.uFlags = NIF_ICON | NIF_MESSAGE | NIF_TIP;
            nid.uCallbackMessage = WM_TRAYICON;
            nid.hIcon = icon;
            let tip_len = tooltip.len().min(nid.szTip.len());
            nid.szTip[..tip_len].copy_from_slice(&tooltip[..tip_len]);
            Shell_NotifyIconW(NIM_ADD, &nid).as_bool()
        };

        if ok {
            tracing::info!(
                target: "taix_shell::tray",
                "NIM_ADD ok hwnd={:?} id={} attempt={}/{}",
                hwnd, id, attempt, MAX_REGISTER_ATTEMPTS
            );
            return;
        }

        if attempt == MAX_REGISTER_ATTEMPTS {
            break;
        }

        let delay = if attempt <= 5 {
            Duration::from_secs(1)
        } else {
            Duration::from_secs(2)
        };
        tracing::warn!(target: "taix_shell::tray", "NIM_ADD failed (attempt {}/{}), retrying in {}ms", attempt, MAX_REGISTER_ATTEMPTS, delay.as_millis());

        if !shutdown.wait_for(delay) {
            tracing::info!(target: "taix_shell::tray", "NIM_ADD retry aborted: shutdown requested");
            return;
        }
    }

    tracing::error!(target: "taix_shell::tray", "NIM_ADD failed after {} attempts", MAX_REGISTER_ATTEMPTS);
}
