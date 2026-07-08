use super::pipe::NamedPipeTransport;
use std::collections::VecDeque;
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::{Arc, Mutex};
use std::thread;
use std::time::{Duration, Instant};
use tracing::{debug, warn};

const MAX_QUEUED: usize = 2048;
const CONSUMER_INTERVAL: Duration = Duration::from_millis(100);
const MAX_SEND_ATTEMPTS: u32 = 10;
/// pending 中的消息超过此时间则丢弃，防止 daemon 长期不可用时积压
const PENDING_TTL: Duration = Duration::from_secs(300);

#[derive(Clone)]
struct TimedMessage {
    data: Vec<u8>,
    enqueued_at: Instant,
}

pub struct MessageQueue {
    inner: Arc<Inner>,
    _consumer: Option<thread::JoinHandle<()>>,
}

struct Inner {
    buf: Mutex<VecDeque<TimedMessage>>,
    running: AtomicBool,
    pipe_name: String,
}

impl MessageQueue {
    pub fn new(pipe_name: &str) -> Self {
        let inner = Arc::new(Inner {
            buf: Mutex::new(VecDeque::with_capacity(256)),
            running: AtomicBool::new(true),
            pipe_name: pipe_name.to_owned(),
        });

        // 消费者线程：后台 flush 到命名管道
        let inner_clone = Arc::clone(&inner);
        let _consumer = thread::spawn(move || {
            let mut transport = NamedPipeTransport::new(&inner_clone.pipe_name);
            let mut pending: Vec<TimedMessage> = Vec::new();

            while inner_clone.running.load(Ordering::Relaxed) {
                // 1. 从共享缓冲取消息
                let batch = {
                    let mut buf = inner_clone.buf.lock().unwrap_or_else(|e| e.into_inner());
                    if buf.is_empty() && pending.is_empty() {
                        drop(buf);
                        thread::sleep(CONSUMER_INTERVAL);
                        continue;
                    }
                    // pending 中超时消息丢弃
                    let now = Instant::now();
                    pending.retain(|m| now.duration_since(m.enqueued_at) < PENDING_TTL);
                    // 把 pending 中没发成功的放回队列头部
                    let mut all: VecDeque<TimedMessage> = pending.drain(..).collect();
                    all.append(&mut *buf);
                    all.into_iter().collect::<Vec<_>>()
                };

                // 2. 尝试发送所有消息
                match send_batch(&mut transport, &batch) {
                    Ok(()) => {
                        pending.clear();
                    }
                    Err(unsent) => {
                        pending = unsent;
                        // 连接失败，等一会再试
                        thread::sleep(Duration::from_millis(500));
                    }
                }
            }

            // 退出前最后一次尝试
            if !pending.is_empty() {
                let batch: Vec<TimedMessage> = {
                    let mut buf = inner_clone.buf.lock().unwrap_or_else(|e| e.into_inner());
                    let mut all: VecDeque<TimedMessage> = pending.drain(..).collect();
                    all.append(&mut *buf);
                    all.into_iter().collect()
                };
                let _ = send_batch(&mut transport, &batch);
            }
        });

        Self {
            inner,
            _consumer: Some(_consumer),
        }
    }

    /// 主线程调用：将消息加入缓冲
    pub fn enqueue(&self, data: Vec<u8>) {
        let mut buf = self.inner.buf.lock().unwrap_or_else(|e| e.into_inner());
        if buf.len() >= MAX_QUEUED {
            buf.pop_front();
            warn!(target: "message_queue", "Queue full, dropped oldest message");
        }
        buf.push_back(TimedMessage {
            data,
            enqueued_at: Instant::now(),
        });
    }
}

/// 尝试发送一批消息。成功返回 Ok(())，失败返回 Err(未发送的消息)。
fn send_batch(
    transport: &mut NamedPipeTransport,
    batch: &[TimedMessage],
) -> Result<(), Vec<TimedMessage>> {
    if batch.is_empty() {
        return Ok(());
    }

    // 如果没有连接，先连接
    if !transport.is_connected() {
        if let Err(e) = transport.connect() {
            debug!(target: "message_queue", "Pipe connect failed: {}", e);
            return Err(batch.to_vec());
        }
        debug!(target: "message_queue", "Pipe connected");
    }

    // 逐条发送，每条失败时重试 MAX_SEND_ATTEMPTS 次
    let mut unsent: Vec<TimedMessage> = Vec::new();
    for (idx, msg) in batch.iter().enumerate() {
        let mut ok = false;
        for attempt in 1..=MAX_SEND_ATTEMPTS {
            match transport.send(&msg.data) {
                Ok(()) => {
                    ok = true;
                    break;
                }
                Err(e) => {
                    warn!(
                        target: "message_queue",
                        "Pipe send failed (attempt {}): {}", attempt, e
                    );
                    transport.disconnect();
                    if attempt < MAX_SEND_ATTEMPTS {
                        thread::sleep(Duration::from_millis(500 * attempt as u64));
                        // 尝试重新连接
                        if let Err(e) = transport.connect() {
                            warn!(target: "message_queue", "Reconnect failed: {}", e);
                        }
                    }
                }
            }
        }
        if !ok {
            // 当前消息失败后，后续所有消息也都发不出去了，全部保留
            unsent.extend(batch[idx..].iter().cloned());
            break;
        }
    }

    if unsent.is_empty() {
        Ok(())
    } else {
        Err(unsent)
    }
}
