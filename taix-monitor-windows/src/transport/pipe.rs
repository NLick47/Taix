use tokio::net::windows::named_pipe::ClientOptions;
use tokio::io::{AsyncWriteExt, BufWriter};
use std::io;

pub struct NamedPipeTransport {
    pipe_name: String,
    writer: Option<BufWriter<tokio::net::windows::named_pipe::NamedPipeClient>>,
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

    pub async fn connect(&mut self) -> io::Result<()> {
        self.disconnect();
        let pipe_path = format!("\\\\.\\pipe\\{}", self.pipe_name);
        let client = ClientOptions::new().open(&pipe_path)?;
        self.writer = Some(BufWriter::new(client));
        Ok(())
    }

    pub async fn send(&mut self, data: &[u8]) -> io::Result<()> {
        let writer = self.writer.as_mut().ok_or_else(|| io::Error::new(io::ErrorKind::NotConnected, "Not connected"))?;
        writer.write_all(data).await?;
        writer.flush().await?;
        Ok(())
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