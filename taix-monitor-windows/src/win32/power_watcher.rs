use std::sync::mpsc::{channel, Receiver, Sender};
use std::sync::OnceLock;
use std::thread;
use tracing::{error, info};

use windows::core::PCWSTR;
use windows::Win32::Foundation::{HWND, HINSTANCE, LPARAM, LRESULT, WPARAM};
use windows::Win32::System::LibraryLoader::GetModuleHandleW;
use windows::Win32::System::RemoteDesktop::{
    WTSRegisterSessionNotification, WTSUnRegisterSessionNotification, NOTIFY_FOR_THIS_SESSION,
};
use windows::Win32::UI::WindowsAndMessaging::{
    CreateWindowExW, DefWindowProcW, DispatchMessageW, GetMessageW, PostQuitMessage,
    RegisterClassW, TranslateMessage, WINDOW_EX_STYLE, WM_DESTROY, WM_POWERBROADCAST,
    WM_WTSSESSION_CHANGE, WNDCLASSW, HWND_MESSAGE, MSG,
};

/// 会话锁定事件 (WTS_SESSION_LOCK)
const WTS_SESSION_LOCK: u32 = 0x7;
/// 会话解锁事件 (WTS_SESSION_UNLOCK)
const WTS_SESSION_UNLOCK: u32 = 0x8;

/// 系统挂起事件 (PBT_APMSUSPEND)
const PBT_APMSUSPEND: u32 = 4;
/// 系统自动恢复事件 (PBT_APMRESUMEAUTOMATIC)
const PBT_APMRESUMEAUTOMATIC: u32 = 18;
/// 用户操作触发恢复事件 (PBT_APMRESUMESUSPEND)
const PBT_APMRESUMESUSPEND: u32 = 7;

const WINDOW_CLASS_NAME: &str = "TaixPowerWatcherWindow";

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum PowerEvent {
    /// 会话锁定 (Win+L 等)
    SessionLock,
    /// 会话解锁
    SessionUnlock,
    /// 系统挂起 (盒盖/睡眠)
    Suspend,
    /// 系统恢复
    Resume,
}

static EVENT_SENDER: OnceLock<Sender<PowerEvent>> = OnceLock::new();

pub struct PowerWatcher {
    receiver: Receiver<PowerEvent>,
}

impl PowerWatcher {
    pub fn start() -> Self {
        let (sender, receiver) = channel::<PowerEvent>();

        let _ = EVENT_SENDER.set(sender);

        thread::spawn(move || {
            unsafe {
                if let Err(e) = run_message_loop() {
                    error!(target: "power_watcher", "Message loop failed: {:?}", e);
                }
            }
        });

        Self { receiver }
    }

    pub fn try_recv(&self) -> Option<PowerEvent> {
        self.receiver.try_recv().ok()
    }
}

impl Drop for PowerWatcher {
    fn drop(&mut self) {
        // OnceLock 无法被清空，但 channel 关闭后消息循环的 GetMessageW 会返回错误
        // 隐藏窗口仍然存在，只是不再接收事件
    }
}


unsafe fn run_message_loop() -> Result<(), Box<dyn std::error::Error>> {
    let instance = GetModuleHandleW(None)?;
    let hinstance: HINSTANCE = instance.into();

    let class_name: Vec<u16> = WINDOW_CLASS_NAME
        .encode_utf16()
        .chain(std::iter::once(0))
        .collect();

    let window_class = WNDCLASSW {
        lpfnWndProc: Some(power_watcher_window_proc),
        hInstance: hinstance,
        lpszClassName: PCWSTR(class_name.as_ptr()),
        ..Default::default()
    };

    let _ = RegisterClassW(&window_class);

    // 创建消息窗口
    let hwnd = CreateWindowExW(
        WINDOW_EX_STYLE::default(),
        PCWSTR(class_name.as_ptr()),
        PCWSTR(class_name.as_ptr()),
        Default::default(),
        0,
        0,
        0,
        0,
        Some(HWND_MESSAGE),
        None,
        Some(hinstance),
        None,
    )?;

    // 注册会话通知
    match WTSRegisterSessionNotification(hwnd, NOTIFY_FOR_THIS_SESSION) {
        Ok(()) => {
            info!(target: "power_watcher", "WTSRegisterSessionNotification registered");
        }
        Err(e) => {
            error!(target: "power_watcher", "WTSRegisterSessionNotification failed: {:?}", e);
        }
    }

    info!(target: "power_watcher", "Power watcher started");

    // 消息循环
    let mut msg = MSG::default();
    while GetMessageW(&mut msg, None, 0, 0).into() {
        let _ = TranslateMessage(&msg);
        DispatchMessageW(&msg);
    }

    info!(target: "power_watcher", "Power watcher stopped");
    Ok(())
}


unsafe extern "system" fn power_watcher_window_proc(
    hwnd: HWND,
    msg: u32,
    wparam: WPARAM,
    lparam: LPARAM,
) -> LRESULT {
    match msg {
        WM_WTSSESSION_CHANGE => {
            match wparam.0 as u32 {
                WTS_SESSION_LOCK => {
                    info!(target: "power_watcher", "Session locked");
                    emit_event(PowerEvent::SessionLock);
                }
                WTS_SESSION_UNLOCK => {
                    info!(target: "power_watcher", "Session unlocked");
                    emit_event(PowerEvent::SessionUnlock);
                }
                _ => {}
            }
            LRESULT(0)
        }
        WM_POWERBROADCAST => match wparam.0 as u32 {
            PBT_APMSUSPEND => {
                info!(target: "power_watcher", "System suspending");
                emit_event(PowerEvent::Suspend);
                LRESULT(1) // 返回 TRUE 表示已处理
            }
            PBT_APMRESUMEAUTOMATIC | PBT_APMRESUMESUSPEND => {
                info!(target: "power_watcher", "System resuming");
                emit_event(PowerEvent::Resume);
                LRESULT(1)
            }
            _ => DefWindowProcW(hwnd, msg, wparam, lparam),
        },
        WM_DESTROY => {
            let _ = WTSUnRegisterSessionNotification(hwnd);
            PostQuitMessage(0);
            LRESULT(0)
        }
        _ => DefWindowProcW(hwnd, msg, wparam, lparam),
    }
}

fn emit_event(event: PowerEvent) {
    if let Some(sender) = EVENT_SENDER.get() {
        if sender.send(event).is_err() {
            error!(target: "power_watcher", "Failed to send power event");
        }
    }
}
