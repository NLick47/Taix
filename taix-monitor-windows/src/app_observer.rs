use crate::app_manager::AppManager;
use crate::models::AppActiveEvent;
use crate::win32::window::{
    get_foreground_window, get_window_thread_process_id, is_valid_visible_window,
    set_win_event_hook, unhook_win_event,
};

use crate::window_manager::WindowManager;
use std::cell::Cell;
use std::sync::atomic::{AtomicU32, Ordering};
use std::sync::Arc;
use std::thread;
use tokio::sync::broadcast;
use tracing::{debug, info};
use windows::Win32::Foundation::{HWND, LPARAM, WPARAM};
use windows::Win32::UI::WindowsAndMessaging::PostThreadMessageW;

const EVENT_SYSTEM_FOREGROUND: u32 = 0x0003;
const WM_QUIT: u32 = 0x0012;
const WM_FOREGROUND_CHANGED: u32 = 0x8002; // WM_APP + 2

#[derive(Debug, Clone)]
struct HwndSnapshot {
    hwnd: usize,
    pid: u32,
    tid: u32,
}

impl HwndSnapshot {
    fn as_hwnd(&self) -> HWND {
        HWND(self.hwnd as *mut _)
    }
}

// Hook 回调通过 thread_local 获取消息循环线程的 tid，避免全局 AtomicU32
thread_local! {
    static LOOP_TID: Cell<u32> = const { Cell::new(0) };
}

pub struct AppObserver {
    tx: broadcast::Sender<AppActiveEvent>,
    hwnd_tx: Option<std::sync::mpsc::Sender<HwndSnapshot>>,
    thread: Option<thread::JoinHandle<()>>,
    resolver_thread: Option<thread::JoinHandle<()>>,
    thread_id: u32,
}

impl AppObserver {
    pub fn spawn(app_manager: Arc<AppManager>) -> Self {
        let (tx, _rx) = broadcast::channel(1024);
        let tx2 = tx.clone();

        let (hwnd_tx, hwnd_rx) = std::sync::mpsc::channel::<HwndSnapshot>();

        // resolver 线程
        let resolver_thread = thread::spawn(move || {
            let result = std::panic::catch_unwind(std::panic::AssertUnwindSafe(|| {
                while let Ok(snapshot) = hwnd_rx.recv() {
                    debug!("[AppObserver] Processing HWND: {:#x}", snapshot.hwnd);
                    let hwnd = snapshot.as_hwnd();

                    if !is_valid_visible_window(hwnd) {
                        debug!(
                            "[AppObserver] HWND {:#x} closed before resolver could process it; skipping.",
                            snapshot.hwnd
                        );
                        continue;
                    }

                    let (current_tid, current_pid) = get_window_thread_process_id(hwnd);
                    if current_pid != snapshot.pid || current_tid != snapshot.tid {
                        info!(
                            "[AppObserver] Handle recycled: expected pid={}/tid={}, got pid={}/tid={}; skipping.",
                            snapshot.pid, snapshot.tid, current_pid, current_tid
                        );
                        continue;
                    }

                    let app = match app_manager.get_app_info(hwnd) {
                        Ok(app) => app,
                        Err(e) => {
                            debug!("[AppObserver] Failed to get app info for HWND: {:#x}: {:?}", snapshot.hwnd, e);
                            continue;
                        }
                    };

                    if !is_valid_visible_window(hwnd) {
                        debug!(
                            "[AppObserver] HWND {:#x} closed during get_app_info; skipping.",
                            snapshot.hwnd
                        );
                        continue;
                    }

                    let (tid2, pid2) = get_window_thread_process_id(hwnd);
                    if pid2 != snapshot.pid || tid2 != snapshot.tid {
                        debug!(
                            "[AppObserver] Handle recycled during get_app_info: expected pid={}/tid={}, got pid={}/tid={}; skipping.",
                            snapshot.pid, snapshot.tid, pid2, tid2
                        );
                        continue;
                    }

                    let window = match WindowManager::get_window_info(hwnd) {
                        Some(w) => w,
                        None => {
                            debug!("[AppObserver] Failed to get window info for HWND: {:#x}", snapshot.hwnd);
                            continue;
                        }
                    };
                    if !window.is_valid() {
                        debug!("[AppObserver] Empty window info for HWND: {:#x}", snapshot.hwnd);
                        continue;
                    }

                    info!(
                        "[AppObserver] Foreground changed -> [{}] {} | title: {} | class: {}",
                        app.app_type, app.process, window.title, window.class_name
                    );
                    let event = AppActiveEvent {
                        app,
                        window,
                    };
                    if let Err(e) = tx2.send(event) {
                        tracing::warn!("[AppObserver] Broadcast closed, resolver exiting: {}", e);
                        break;
                    }
                }
            }));

            if let Err(e) = result {
                if let Some(s) = e.downcast_ref::<String>() {
                    tracing::error!("[AppObserver] Resolver thread panicked: {}", s);
                } else if let Some(s) = e.downcast_ref::<&str>() {
                    tracing::error!("[AppObserver] Resolver thread panicked: {}", s);
                } else {
                    tracing::error!("[AppObserver] Resolver thread panicked");
                }
            }
        });

        let hwnd_tx2 = hwnd_tx.clone();
        let thread_id_shared = Arc::new(AtomicU32::new(0));
        let thread_id_clone = Arc::clone(&thread_id_shared);

        let thread = thread::spawn(move || {
            unsafe {
                let tid = windows::Win32::System::Threading::GetCurrentThreadId();
                thread_id_clone.store(tid, Ordering::Relaxed);
                LOOP_TID.with(|t| t.set(tid));

                let hook = match set_win_event_hook(
                    EVENT_SYSTEM_FOREGROUND,
                    EVENT_SYSTEM_FOREGROUND,
                    Some(win_event_callback),
                ) {
                    Ok(h) => h,
                    Err(e) => {
                        tracing::error!("[AppObserver] SetWinEventHook failed: {}", e);
                        LOOP_TID.with(|t| t.set(0));
                        return;
                    }
                };

                // 初始化时抓拍当前前台窗口
                let hwnd = get_foreground_window();
                if !hwnd.0.is_null() && is_valid_visible_window(hwnd) {
                    let (tid_fg, pid_fg) = get_window_thread_process_id(hwnd);
                    let snapshot = HwndSnapshot {
                        hwnd: hwnd.0 as usize,
                        pid: pid_fg,
                        tid: tid_fg,

                    };
                    if let Err(e) = hwnd_tx2.send(snapshot) {
                        tracing::warn!(
                            "[AppObserver] Failed to send initial hwnd to resolver: {}",
                            e
                        );
                    }
                }

                info!("[AppObserver] Started");

                let mut msg = windows::Win32::UI::WindowsAndMessaging::MSG::default();
                loop {
                    let result = windows::Win32::UI::WindowsAndMessaging::GetMessageW(
                        &mut msg,
                        None,
                        0,
                        0,
                    );
                    if result.0 == 0 {
                        break; // WM_QUIT
                    }
                    if result.0 == -1 {
                        tracing::error!("[AppObserver] GetMessage failed");
                        break;
                    }

                    if msg.message == WM_FOREGROUND_CHANGED {
                        let hwnd = HWND(msg.wParam.0 as *mut _);
                        if is_valid_visible_window(hwnd) {
                            let (tid_fg, pid_fg) = get_window_thread_process_id(hwnd);
                            let snapshot = HwndSnapshot {
                                hwnd: hwnd.0 as usize,
                                pid: pid_fg,
                                tid: tid_fg,
        
                            };
                            if let Err(e) = hwnd_tx2.send(snapshot) {
                                tracing::warn!(
                                    "[AppObserver] Failed to send foreground change to resolver: {}",
                                    e
                                );
                            }
                        }
                        continue;
                    }

                    let _ = windows::Win32::UI::WindowsAndMessaging::TranslateMessage(&msg);
                    windows::Win32::UI::WindowsAndMessaging::DispatchMessageW(&msg);
                }

                unhook_win_event(hook);
                LOOP_TID.with(|t| t.set(0));
                info!("[AppObserver] Message loop ended");
            }
        });

        // 等待消息循环线程报告其 tid（最多 5 秒）
        let start = std::time::Instant::now();
        while thread_id_shared.load(Ordering::Relaxed) == 0 {
            if start.elapsed() > std::time::Duration::from_secs(5) {
                panic!("AppObserver message loop thread failed to start within 5 seconds");
            }
            std::thread::yield_now();
        }
        let thread_id = thread_id_shared.load(Ordering::Relaxed);

        Self {
            tx,
            hwnd_tx: Some(hwnd_tx),
            thread: Some(thread),
            resolver_thread: Some(resolver_thread),
            thread_id,
        }
    }

    pub fn subscribe(&self) -> broadcast::Receiver<AppActiveEvent> {
        self.tx.subscribe()
    }
}

impl Drop for AppObserver {
    fn drop(&mut self) {
        // 释放 sender，resolver 线程退出
        if let Some(hwnd_tx) = self.hwnd_tx.take() {
            drop(hwnd_tx);
        }

        // 发送 WM_QUIT 停止消息循环
        if self.thread_id != 0 {
            unsafe {
                if let Err(e) = PostThreadMessageW(self.thread_id, WM_QUIT, WPARAM(0), LPARAM(0)) {
                    tracing::warn!("[AppObserver] PostThreadMessageW(WM_QUIT) failed: {}", e);
                }
            }
        }

        // 等待消息循环线程结束
        if let Some(t) = self.thread.take() {
            let _ = t.join();
        }

        // 等待 resolver 线程结束
        if let Some(t) = self.resolver_thread.take() {
            let _ = t.join();
        }
    }
}

unsafe extern "system" fn win_event_callback(
    _h_win_event_hook: windows::Win32::UI::Accessibility::HWINEVENTHOOK,
    _event: u32,
    hwnd: windows::Win32::Foundation::HWND,
    _id_object: i32,
    _id_child: i32,
    _dw_event_thread: u32,
    _dwms_event_time: u32,
) {
    LOOP_TID.with(|tid| {
        let t = tid.get();
        if t != 0 {
            let _ = PostThreadMessageW(t, WM_FOREGROUND_CHANGED, WPARAM(hwnd.0 as usize), LPARAM(0));
        }
    });
}
