use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::Arc;
use std::time::Duration;

const POLL_INTERVAL: Duration = Duration::from_millis(500);

#[derive(Clone, Default)]
pub struct Shutdown {
    flag: Arc<AtomicBool>,
}

impl Shutdown {
    pub fn new() -> Self {
        Self::default()
    }

    pub fn request(&self) {
        self.flag.store(true, Ordering::Release);
    }

    pub fn is_requested(&self) -> bool {
        self.flag.load(Ordering::Acquire)
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
