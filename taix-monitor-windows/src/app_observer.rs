use crate::app_manager::AppManager;
use crate::models::AppActiveEvent;
use crate::win32::window::{
    get_foreground_window, get_window_thread_process_id, is_valid_visible_window,
    set_win_event_hook, unhook_win_event,
};

use crate::window_manager::WindowManager;
use parking_lot::Mutex;
use std::sync::atomic::{AtomicU32, Ordering};
use std::sync::Arc;
use std::thread;
use std::time::Instant;
use tokio::sync::broadcast;
use tracing::{debug, info};
use windows::Win32::Foundation::{HWND, LPARAM, WPARAM};
use windows::Win32::UI::WindowsAndMessaging::PostThreadMessageW;

const EVENT_SYSTEM_FOREGROUND: u32 = 0x0003;
const WM_QUIT: u32 = 0x0012;
const WM_WAKE_MSG_LOOP: u32 = 0x8001; // WM_APP + 1

// hook 回调里抓的快照，给 resolver 判断句柄是否回收
#[derive(Debug, Clone)]
#[allow(dead_code)]
struct HwndSnapshot {
    // hwnd 存 usize，用时转 HWND
    hwnd: usize,
    pid: u32,
    tid: u32,
    // 抓拍时间，留作后续过滤旧事件
    captured_at: Instant,
}

impl HwndSnapshot {
    fn as_hwnd(&self) -> HWND {
        HWND(self.hwnd as *mut _)
    }
}

// hook 回调塞快照，消息循环线程接收，避免回调阻塞
static PENDING_TX: Mutex<Option<std::sync::mpsc::Sender<HwndSnapshot>>> =
    Mutex::new(None);
static OBSERVER_THREAD_ID: AtomicU32 = AtomicU32::new(0);

pub struct AppObserver {
    tx: broadcast::Sender<AppActiveEvent>,
    hwnd_tx: Option<std::sync::mpsc::Sender<HwndSnapshot>>,
    thread: Option<thread::JoinHandle<()>>,
}

impl AppObserver {
    pub fn spawn(app_manager: Arc<AppManager>, window_manager: Arc<WindowManager>) -> Self {
        let (tx, _rx) = broadcast::channel(16);
        let tx2 = tx.clone();

        // hook 回调 -> 消息循环线程 channel，替代 Mutex<Vec>
        let (pending_tx, pending_rx) = std::sync::mpsc::channel::<HwndSnapshot>();
        *PENDING_TX.lock() = Some(pending_tx);

        let (hwnd_tx, hwnd_rx) = std::sync::mpsc::channel::<HwndSnapshot>();

        // resolver 线程，处理耗时操作，避免 hook 线程阻塞
        thread::spawn(move || {
            let result = std::panic::catch_unwind(std::panic::AssertUnwindSafe(|| {
                while let Ok(snapshot) = hwnd_rx.recv() {
                    debug!("[AppObserver] Processing HWND: {:#x}", snapshot.hwnd);
                    let hwnd = snapshot.as_hwnd();

                    // 过滤已关闭窗口
                    if !is_valid_visible_window(hwnd) {
                        debug!(
                            "[AppObserver] HWND {:#x} closed before resolver could process it; skipping.",
                            snapshot.hwnd
                        );
                        continue;
                    }

                    // 校验 HWND，防止句柄重用。PID+TID 作指纹
                    let (current_tid, current_pid) = get_window_thread_process_id(hwnd);
                    if current_pid != snapshot.pid || current_tid != snapshot.tid {
                        info!(
                            "[AppObserver] Handle recycled: expected pid={}/tid={}, got pid={}/tid={}; skipping.",
                            snapshot.pid, snapshot.tid, current_pid, current_tid
                        );
                        continue;
                    }

                    let app = app_manager.get_app_info(hwnd);
                    if app.is_empty() {
                        debug!("[AppObserver] Empty app info for HWND: {:#x}", snapshot.hwnd);
                        continue;
                    }

                    // get_app_info 耗时较长，窗口可能已关闭
                    if !is_valid_visible_window(hwnd) {
                        debug!(
                            "[AppObserver] HWND {:#x} closed during get_app_info; skipping.",
                            snapshot.hwnd
                        );
                        continue;
                    }

                    // 耗时操作后再次校验
                    let (tid2, pid2) = get_window_thread_process_id(hwnd);
                    if pid2 != snapshot.pid || tid2 != snapshot.tid {
                        debug!(
                            "[AppObserver] Handle recycled during get_app_info: expected pid={}/tid={}, got pid={}/tid={}; skipping.",
                            snapshot.pid, snapshot.tid, pid2, tid2
                        );
                        continue;
                    }

                    let window = window_manager.get_window_info(hwnd);
                    if window.is_empty() {
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
                        active_time: chrono::Local::now(),
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

        let thread = thread::spawn(move || {
            unsafe {
                // OBSERVER_THREAD_ID 优先设置，确保 Drop 能唤醒
                let tid = windows::Win32::System::Threading::GetCurrentThreadId();
                OBSERVER_THREAD_ID.store(tid, Ordering::SeqCst);

                let hook = match set_win_event_hook(
                    EVENT_SYSTEM_FOREGROUND,
                    EVENT_SYSTEM_FOREGROUND,
                    Some(win_event_callback),
                ) {
                    Ok(h) => h,
                    Err(e) => {
                        tracing::error!("[AppObserver] SetWinEventHook failed: {}", e);
                        OBSERVER_THREAD_ID.store(0, Ordering::SeqCst);
                        return;
                    }
                };

                // 初始化时抓拍当前前台窗口
                let hwnd = get_foreground_window();
                if !hwnd.0.is_null() {
                    if is_valid_visible_window(hwnd) {
                        let (tid, pid) = get_window_thread_process_id(hwnd);
                        let snapshot = HwndSnapshot {
                            hwnd: hwnd.0 as usize,
                            pid,
                            tid,
                            captured_at: Instant::now(),
                        };
                        if let Err(e) = hwnd_tx2.send(snapshot) {
                            tracing::warn!(
                                "[AppObserver] Failed to send initial hwnd to resolver: {}",
                                e
                            );
                        }
                    }
                }

                info!("[AppObserver] Started");

                let mut msg = windows::Win32::UI::WindowsAndMessaging::MSG::default();
                loop {
                    // drain 回调的快照
                    while let Ok(snapshot) = pending_rx.try_recv() {
                        if is_valid_visible_window(snapshot.as_hwnd()) {
                            // 忽略 send 错误，消息循环保持运行
                            let _ = hwnd_tx2.send(snapshot);
                        }
                    }

                    // 阻塞等待 Windows 消息
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

                    // 自定义唤醒消息跳过 dispatch
                    if msg.message == WM_WAKE_MSG_LOOP {
                        continue;
                    }

                    // 正常消息走默认流程
                    let _ = windows::Win32::UI::WindowsAndMessaging::TranslateMessage(&msg);
                    windows::Win32::UI::WindowsAndMessaging::DispatchMessageW(&msg);
                }

                unhook_win_event(hook);
                OBSERVER_THREAD_ID.store(0, Ordering::SeqCst);
                info!("[AppObserver] Message loop ended");
            }
        });

        Self {
            tx,
            hwnd_tx: Some(hwnd_tx),
            thread: Some(thread),
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
        let tid = OBSERVER_THREAD_ID.load(Ordering::SeqCst);
        if tid != 0 {
            unsafe {
                if let Err(e) = PostThreadMessageW(tid, WM_QUIT, WPARAM(0), LPARAM(0)) {
                    tracing::warn!("[AppObserver] PostThreadMessageW(WM_QUIT) failed: {}", e);
                }
            }
        }

        // 等待消息循环线程结束
        if let Some(t) = self.thread.take() {
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
    // hook 回调里获取进程身份。GetWindowThreadProcessId 是轻量只读 syscall
    let (tid, pid) = get_window_thread_process_id(hwnd);
    let snapshot = HwndSnapshot {
        hwnd: hwnd.0 as usize,
        pid,
        tid,
        captured_at: Instant::now(),
    };
    if let Some(tx) = PENDING_TX.lock().as_ref() {
        // 无界 channel，send 不阻塞
        let _ = tx.send(snapshot);
    }
    let loop_tid = OBSERVER_THREAD_ID.load(Ordering::SeqCst);
    if loop_tid != 0 {
        let _ = PostThreadMessageW(loop_tid, WM_WAKE_MSG_LOOP, WPARAM(0), LPARAM(0));
    }
}
