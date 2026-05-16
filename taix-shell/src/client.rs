use std::sync::atomic::{AtomicBool, Ordering};
use std::time::Duration;

use tokio::io::AsyncWriteExt;
use tokio::net::windows::named_pipe::ClientOptions;

use crate::constants::{CLIENT_EXE_NAME, CLIENT_PIPE_NAME};

static IS_LAUNCHING: AtomicBool = AtomicBool::new(false);

pub async fn launch_or_wake() -> anyhow::Result<()> {
    match try_wake_existing().await {
        Ok(true) => return Ok(()),
        Ok(false) => {}
        Err(e) => tracing::warn!(target: "taix_shell::client", "wake failed: {}", e),
    }

    if IS_LAUNCHING.swap(true, Ordering::SeqCst) {
        tracing::info!(target: "taix_shell::client", "client is already launching, skip duplicate request");
        return Ok(());
    }

    let result = spawn_new_process();

    tokio::spawn(async move {
        tokio::time::sleep(Duration::from_secs(3)).await;
        IS_LAUNCHING.store(false, Ordering::SeqCst);
    });

    result
}

async fn try_wake_existing() -> anyhow::Result<bool> {
    const MAX_RETRIES: usize = 3;
    const RETRY_DELAY_MS: u64 = 300;

    for attempt in 0..MAX_RETRIES {
        match try_wake_once().await {
            Ok(true) => return Ok(true),
            Ok(false) if attempt + 1 < MAX_RETRIES => {
                tracing::debug!(target: "taix_shell::client", "wake attempt {}: pipe not ready, retry in {}ms", attempt + 1, RETRY_DELAY_MS);
                tokio::time::sleep(Duration::from_millis(RETRY_DELAY_MS)).await;
            }
            Ok(false) => return Ok(false),
            Err(e) if attempt + 1 < MAX_RETRIES => {
                tracing::debug!(target: "taix_shell::client", "wake attempt {} failed: {}, retry in {}ms", attempt + 1, e, RETRY_DELAY_MS);
                tokio::time::sleep(Duration::from_millis(RETRY_DELAY_MS)).await;
            }
            Err(e) => {
                tracing::warn!(target: "taix_shell::client", "wake failed after {} attempts: {}", MAX_RETRIES, e);
                return Ok(false);
            }
        }
    }
    Ok(false)
}

async fn try_wake_once() -> anyhow::Result<bool> {
    let mut client = match ClientOptions::new().open(CLIENT_PIPE_NAME) {
        Ok(c) => c,
        Err(e) if e.kind() == std::io::ErrorKind::NotFound => {
            return Ok(false);
        }
        Err(e) => return Err(e.into()),
    };

    if let Err(e) = client.write_all(b"show\n").await {
        tracing::warn!(target: "taix_shell::client", "pipe write failed: {}", e);
        return Ok(false);
    }

    if let Err(e) = client.flush().await {
        tracing::warn!(target: "taix_shell::client", "pipe flush failed: {}", e);
        return Ok(false);
    }

    if let Err(e) = client.shutdown().await {
        tracing::debug!(target: "taix_shell::client", "pipe shutdown failed: {}", e);
    }

    tracing::info!(target: "taix_shell::client", "wake signal sent to existing client");
    Ok(true)
}

fn spawn_new_process() -> anyhow::Result<()> {
    let exe_dir = std::env::current_exe()?
        .parent()
        .ok_or_else(|| anyhow::anyhow!("failed to get exe directory"))?
        .to_path_buf();
    let client_exe = exe_dir.join(CLIENT_EXE_NAME);
    if !client_exe.exists() {
        return Err(anyhow::anyhow!(
            "client executable not found: {}",
            client_exe.display()
        ));
    }
    std::process::Command::new(&client_exe)
        .spawn()
        .map_err(|e| anyhow::anyhow!("failed to spawn client: {}", e))?;
    Ok(())
}
