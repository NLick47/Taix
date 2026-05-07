use super::pipe::NamedPipeTransport;
use std::time::Duration;
use tokio::sync::mpsc;
use tracing::{debug, error, info, warn};

// 单条消息最大重试次数，约 25 秒退避
const MAX_SEND_ATTEMPTS: u32 = 10;
/// channel 容量：同时存在的未处理消息数上限。
const CHANNEL_CAPACITY: usize = 256;

/// 带背压的消息队列。当内部 channel 满时，`enqueue` 会同步阻塞（等待消费者），
/// 确保调用者明确感知到背压，而不是静默丢弃消息。
pub struct MessageQueue {
    tx: mpsc::Sender<Vec<u8>>,
    task: Option<tokio::task::JoinHandle<()>>,
}

impl MessageQueue {
    pub fn new(mut transport: NamedPipeTransport) -> Self {
        let (tx, mut rx) = mpsc::channel::<Vec<u8>>(CHANNEL_CAPACITY);

        let task = tokio::spawn(async move {
            while let Some(message) = rx.recv().await {
                match send_with_retry(&mut transport, &message).await {
                    Ok(()) => {
                        info!(target: "message_queue", "Sent {} bytes to named pipe", message.len());
                    }
                    Err(e) => {
                        error!(
                            target: "message_queue",
                            "Dropped message after {} retries: {}",
                            MAX_SEND_ATTEMPTS, e
                        );
                    }
                }
            }
            debug!(target: "message_queue", "Send loop ended");
        });

        Self {
            tx,
            task: Some(task),
        }
    }

    /// 将消息入队。使用 `async` 语义，若 channel 满则等待，绝不静默丢弃。
    pub async fn enqueue(&self, data: Vec<u8>) -> Result<(), mpsc::error::SendError<Vec<u8>>> {
        self.tx.send(data).await
    }

    pub async fn shutdown(mut self) {
        // drop sender 关闭 channel，让消费任务优雅退出
        drop(self.tx);
        if let Some(task) = self.task.take() {
            if let Err(e) = tokio::time::timeout(Duration::from_secs(30), task).await {
                warn!(target: "message_queue", "Shutdown timed out waiting for send loop: {}", e);
            }
        }
    }
}

async fn send_with_retry(
    transport: &mut NamedPipeTransport,
    message: &[u8],
) -> std::io::Result<()> {
    let mut last_err = std::io::Error::new(std::io::ErrorKind::Other, "No send attempts made");

    for attempt in 1..=MAX_SEND_ATTEMPTS {
        if !transport.is_connected() {
            if let Err(e) = transport.connect().await {
                warn!(target: "message_queue", "Pipe connect failed (attempt {}): {}", attempt, e);
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
                warn!(target: "message_queue", "Pipe send failed (attempt {}): {}", attempt, e);
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
