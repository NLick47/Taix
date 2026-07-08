use std::fs::{File, OpenOptions};
use std::io;

pub struct NamedPipeTransport {
    pipe_name: String,
    writer: Option<File>,
}

impl NamedPipeTransport {
    pub fn new(pipe_name: impl Into<String>) -> Self {
        Self {
            pipe_name: pipe_name.into(),
            writer: None,
        }
    }

    pub fn is_connected(&self) -> bool {
        self.writer.is_some()
    }

    pub fn connect(&mut self) -> io::Result<()> {
        // 断开旧连接后才创建新连接
        self.disconnect();
        let pipe_path = format!("\\\\.\\pipe\\{}", self.pipe_name);
        // 同步打开命名管道；内部调用 CreateFileW，无 FILE_FLAG_OVERLAPPED
        let f = OpenOptions::new().write(true).open(&pipe_path)?;
        self.writer = Some(f);
        Ok(())
    }

    pub fn send(&mut self, data: &[u8]) -> io::Result<()> {
        use std::io::Write;
        self.writer
            .as_mut()
            .ok_or_else(|| io::Error::new(io::ErrorKind::NotConnected, "Not connected"))?
            .write_all(data)?;
        self.writer
            .as_mut()
            .ok_or_else(|| io::Error::new(io::ErrorKind::NotConnected, "Not connected"))?
            .flush()
    }

    pub fn disconnect(&mut self) {
        self.writer = None;
    }
}

impl Drop for NamedPipeTransport {
    fn drop(&mut self) {
        self.disconnect();
    }
}
