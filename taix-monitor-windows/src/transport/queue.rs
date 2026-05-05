use super::pipe::NamedPipeTransport;
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::Arc;
use std::time::Duration;
use tokio::sync::mpsc;
use tracing::{debug, error, info, warn};

// 单条消息最大重试次数，约 25 秒退避
const MAX_SEND_ATTEMPTS: u32 = 10;
const CHANNEL_CAPACITY: usize = 256;

/// 尽力而为的消息队列。当内部 channel 满时会丢弃消息，调用者不应假设消息一定送达。
pub struct MessageQueue {
    tx: mpsc::Sender<Vec<u8>>,
    task: Option<tokio::task::JoinHandle<()>>,
    shutting_down: Arc<AtomicBool>,
}

impl MessageQueue {
    pub fn new(mut transport: NamedPipeTransport) -> Self {
        let (tx, mut rx) = mpsc::channel::<Vec<u8>>(CHANNEL_CAPACITY);
        let shutting_down = Arc::new(AtomicBool::new(false));
        let shutting_down_task = Arc::clone(&shutting_down);

        let task = tokio::spawn(async move {
            while let Some(message) = rx.recv().await {
                if shutting_down_task.load(Ordering::Relaxed) {
                    // 关机快路径，只试一次
                    if let Err(e) = try_send_once(&mut transport, &message).await {
                        debug!(
                            "[MessageQueue] Fast-path send failed during shutdown: {}",
                            e
                        );
                    } else {
                        debug!(
                            "[MessageQueue] Fast-path sent {} bytes during shutdown",
                            message.len()
                        );
                    }
                } else {
                    match send_with_retry(&mut transport, &message).await {
                        Ok(()) => {
                            info!("[Queue] Sent {} bytes to named pipe", message.len());
                        }
                        Err(e) => {
                            error!(
                                "[Queue] Dropped message after {} retries: {}",
                                MAX_SEND_ATTEMPTS, e
                            );
                        }
                    }
                }
            }

            debug!("[MessageQueue] Send loop ended");
        });

        Self {
            tx,
            task: Some(task),
            shutting_down,
        }
    }

    /// 将消息入队。如果内部 channel 已满，消息会被静默丢弃。
    pub fn enqueue(&self, data: Vec<u8>) {
        match self.tx.try_send(data) {
            Ok(()) => {}
            Err(mpsc::error::TrySendError::Full(_)) => {
                tracing::warn!("[MessageQueue] Channel full ({}), dropping message", CHANNEL_CAPACITY);
            }
            Err(mpsc::error::TrySendError::Closed(_)) => {}
        }
    }

    pub async fn shutdown(mut self) {
        // 切换到快路径
        self.shutting_down.store(true, Ordering::Relaxed);
        // drop sender 关闭 channel
        drop(self.tx);
        if let Some(task) = self.task.take() {
            // 快路径超时 30 秒
            let _ = tokio::time::timeout(Duration::from_secs(30), task).await;
        }
    }
}

async fn try_send_once(
    transport: &mut NamedPipeTransport,
    message: &[u8],
) -> std::io::Result<()> {
    if !transport.is_connected() {
        transport.connect().await?;
    }
    transport.send(message).await
}

async fn send_with_retry(
    transport: &mut NamedPipeTransport,
    message: &[u8],
) -> std::io::Result<()> {
    let mut last_err = std::io::Error::new(std::io::ErrorKind::Other, "No send attempts made");

    for attempt in 1..=MAX_SEND_ATTEMPTS {
        if !transport.is_connected() {
            if let Err(e) = transport.connect().await {
                warn!("[Queue] Pipe connect failed (attempt {}): {}", attempt, e);
                last_err = e;
                if attempt < MAX_SEND_ATTEMPTS {
                    let delay = Duration::from_millis(500 * attempt as u64);
                    tokio::time::sleep(delay).await;
                }
                continue;
            }
        }

        match transport.send(message).await {
            Ok(()) => return Ok(()),
            Err(e) => {
                warn!("[Queue] Pipe send failed (attempt {}): {}", attempt, e);
                last_err = e;
                transport.disconnect();
                if attempt < MAX_SEND_ATTEMPTS {
                    let delay = Duration::from_millis(500 * attempt as u64);
                    tokio::time::sleep(delay).await;
                }
            }
        }
    }

    Err(last_err)
}