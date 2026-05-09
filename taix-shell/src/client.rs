use tokio::io::AsyncWriteExt;
use tokio::net::windows::named_pipe::ClientOptions;

use crate::constants::{CLIENT_EXE_NAME, CLIENT_PIPE_NAME};

pub async fn launch_or_wake() -> anyhow::Result<()> {
    match try_wake_existing().await {
        Ok(true) => return Ok(()),
        Ok(false) => {}
        Err(e) => tracing::warn!(target: "taix_shell::client", "wake failed: {}", e),
    }
    spawn_new_process()
}

async fn try_wake_existing() -> anyhow::Result<bool> {
    let mut client = match ClientOptions::new().open(CLIENT_PIPE_NAME) {
        Ok(c) => c,
        Err(e) if e.kind() == std::io::ErrorKind::NotFound => {
            tracing::debug!(target: "taix_shell::client", "no existing client pipe found");
            return Ok(false);
        }
        Err(e) => {
            tracing::warn!(target: "taix_shell::client", "failed to open client pipe: {}", e);
            return Ok(false);
        }
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
