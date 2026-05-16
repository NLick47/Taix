use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::Arc;
use std::time::{Duration, Instant};

use crate::config::TrayConfig;
use crate::i18n::tray_texts;
use crate::platform::TrayCmd;
use tray_icon::{MouseButton, MouseButtonState, TrayIconBuilder, TrayIconEvent};
use windows::Win32::UI::WindowsAndMessaging::{
    DispatchMessageW, PeekMessageW, TranslateMessage, MSG, PM_REMOVE, WM_QUIT,
};

const CLICK_DEBOUNCE_MS: u64 = 500;

pub fn run_tray(
    cmd_tx: tokio::sync::mpsc::Sender<TrayCmd>,
    initial_config: TrayConfig,
    shutdown: Arc<AtomicBool>,
) -> anyhow::Result<()> {
    let texts = tray_texts(initial_config.language.resolve());
    let icon = load_icon()?;

    let tray = TrayIconBuilder::new()
        .with_tooltip(texts.tooltip)
        .with_icon(icon)
        .build()?;

    tray.set_visible(initial_config.is_visible)?;

    let mut last_click = Instant::now() - Duration::from_secs(10);

    loop {
        unsafe {
            let mut msg: MSG = std::mem::zeroed();
            while PeekMessageW(&mut msg, None, 0, 0, PM_REMOVE).as_bool() {
                if msg.message == WM_QUIT {
                    return Ok(());
                }
                let _ = TranslateMessage(&msg);
                let _ = DispatchMessageW(&msg);
            }
        }

        while let Ok(event) = TrayIconEvent::receiver().try_recv() {
            match event {
                TrayIconEvent::Click {
                    button: MouseButton::Left,
                    button_state: MouseButtonState::Up,
                    ..
                } => {
                    let now = Instant::now();
                    if now.duration_since(last_click) < Duration::from_millis(CLICK_DEBOUNCE_MS) {
                        continue;
                    }
                    last_click = now;
                    let _ = cmd_tx.blocking_send(TrayCmd::LaunchClient);
                }
                _ => {}
            }
        }

        if shutdown.load(Ordering::Relaxed) {
            break;
        }

        std::thread::sleep(Duration::from_millis(200));
    }

    Ok(())
}

fn load_icon() -> anyhow::Result<tray_icon::Icon> {
    let exe_dir = std::env::current_exe()?
        .parent()
        .ok_or_else(|| anyhow::anyhow!("failed to get exe directory"))?
        .to_path_buf();
    let icon_path = exe_dir.join("resources").join("icons").join("tai32.ico");
    if !icon_path.exists() {
        return Err(anyhow::anyhow!("icon not found: {}", icon_path.display()));
    }
    Ok(tray_icon::Icon::from_path(&icon_path, Some((32, 32)))?)
}
