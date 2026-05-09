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
    let client = ClientOptions::new().open(CLIENT_PIPE_NAME);

    match client {
        Ok(mut c) => {
            c.write_all(b"show\n")
                .await
                .map_err(|e| anyhow::anyhow!("pipe write failed: {}", e))?;
            Ok(true)
        }
        Err(_) => Ok(false),
    }
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
