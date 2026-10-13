use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::Arc;
use std::time::Duration;

const POLL_INTERVAL: Duration = Duration::from_millis(500);

#[derive(Clone, Default)]
pub struct Shutdown {
    flag: Arc<AtomicBool>,
    /// 全局退出传播到子循环；插件单停只 request 自己的实例
    parent: Option<Box<Shutdown>>,
}

impl Shutdown {
    pub fn new() -> Self {
        Self::default()
    }

    /// 父 request 时子自动 requested，反之不影响父
    pub fn child(&self) -> Self {
        Self {
            flag: Arc::new(AtomicBool::new(false)),
            parent: Some(Box::new(self.clone())),
        }
    }

    pub fn request(&self) {
        self.flag.store(true, Ordering::Release);
    }

    pub fn is_requested(&self) -> bool {
        self.flag.load(Ordering::Acquire)
            || self.parent.as_ref().is_some_and(|p| p.is_requested())
    }

    pub fn wait_for(&self, total: Duration) -> bool {
        let deadline = std::time::Instant::now() + total;
        loop {
            if self.is_requested() {
                return false;
            }
            let now = std::time::Instant::now();
            if now >= deadline {
                return true;
            }
            std::thread::sleep((deadline - now).min(POLL_INTERVAL));
        }
    }
}
