use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::Arc;
use std::thread;
use std::time::Duration;
use tracing::{debug, error, warn};
use windows::Win32::Media::Audio::{eConsole, eRender, IMMDeviceEnumerator, MMDeviceEnumerator};
use windows::Win32::System::Com::{
    CoCreateInstance, CoInitializeEx, CoUninitialize, CLSCTX_ALL, COINIT_APARTMENTTHREADED,
};
use windows::Win32::Media::Audio::Endpoints::IAudioMeterInformation;

/// 音频播放状态的轻量句柄，可跨线程 Clone
#[derive(Clone)]
pub struct AudioState {
    is_playing: Arc<AtomicBool>,
}

impl AudioState {
    pub fn is_playing(&self) -> bool {
        self.is_playing.load(Ordering::Relaxed)
    }
}

/// 专用 COM 线程，负责轮询音频播放状态。
pub struct AudioMonitor {
    handle: Option<thread::JoinHandle<()>>,
    state: AudioState,
    shutdown_tx: std::sync::mpsc::Sender<()>,
}

impl AudioMonitor {
    pub fn start() -> Self {
        let is_playing = Arc::new(AtomicBool::new(false));
        let (shutdown_tx, shutdown_rx) = std::sync::mpsc::channel::<()>();

        let is_playing_clone = Arc::clone(&is_playing);

        let handle = thread::spawn(move || {
            unsafe {
                let hr = CoInitializeEx(None, COINIT_APARTMENTTHREADED);
                if hr.is_err() {
                    error!(target: "audio_monitor", "CoInitializeEx failed");
                    return;
                }

                let enumerator = match CoCreateInstance::<_, IMMDeviceEnumerator>(
                    &MMDeviceEnumerator,
                    None,
                    CLSCTX_ALL,
                ) {
                    Ok(e) => e,
                    Err(e) => {
                        error!(
                            target: "audio_monitor",
                            "CoCreateInstance(MMDeviceEnumerator) failed: {:?}", e
                        );
                        CoUninitialize();
                        return;
                    }
                };

                loop {
                    let playing = match enumerator.GetDefaultAudioEndpoint(eRender, eConsole) {
                        Ok(device) => {
                            let meter: windows::core::Result<IAudioMeterInformation> =
                                device.Activate(CLSCTX_ALL, None);
                            match meter {
                                Ok(m) => match m.GetPeakValue() {
                                    Ok(peak) => peak > 0.0001,
                                    Err(e) => {
                                        debug!(
                                            target: "audio_monitor",
                                            "GetPeakValue failed: {:?}", e
                                        );
                                        false
                                    }
                                },
                                Err(e) => {
                                    debug!(
                                        target: "audio_monitor",
                                        "Activate(IAudioMeterInformation) failed: {:?}", e
                                    );
                                    false
                                }
                            }
                        }
                        Err(e) => {
                            debug!(
                                target: "audio_monitor",
                                "GetDefaultAudioEndpoint failed: {:?}", e
                            );
                            false
                        }
                    };

                    is_playing_clone.store(playing, Ordering::Relaxed);

                    match shutdown_rx.recv_timeout(Duration::from_secs(5)) {
                        Ok(()) => break,
                        Err(std::sync::mpsc::RecvTimeoutError::Disconnected) => break,
                        Err(std::sync::mpsc::RecvTimeoutError::Timeout) => {}
                    }
                }

                CoUninitialize();
            }
        });

        Self {
            handle: Some(handle),
            state: AudioState { is_playing },
            shutdown_tx,
        }
    }

    pub fn state(&self) -> AudioState {
        self.state.clone()
    }

    pub fn stop(mut self) {
        if let Err(e) = self.shutdown_tx.send(()) {
            warn!(target: "audio_monitor", "Shutdown signal failed: {:?}", e);
        }
        if let Some(h) = self.handle.take() {
            if let Err(e) = h.join() {
                error!(target: "audio_monitor", "Audio thread panicked: {:?}", e);
            }
        }
    }
}
