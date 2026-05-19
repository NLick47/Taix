use serde::Deserialize;
use sqlx::SqlitePool;
use std::io;
use std::sync::Arc;
use tokio::io::{AsyncBufReadExt, BufReader};

use crate::constants;
use crate::models::request::UpdateAppDurationRequest;
use crate::routes::web_sentry::SentryState;
use crate::services::app_timer::AppTimerService;

#[derive(Debug, Deserialize)]
struct PipeAppMsg {
    p: String,
    d: i64,
    a: i64,
    f: Option<String>,
    i: Option<String>,
    desc: Option<String>,
}

#[derive(Debug, Deserialize)]
struct PipeMsg {
    t: String,
    #[serde(flatten)]
    extra: Option<PipeAppMsg>,
}

#[cfg(target_os = "windows")]
mod imp {
    use super::*;
    use tokio::net::windows::named_pipe::{NamedPipeServer, ServerOptions};

    const PIPE_NAME: &str = r"\\.\pipe\TaixDaemon";

    pub async fn start(state: Arc<SentryState>, pool: SqlitePool) {
        let mut server = match create_server(true) {
            Ok(s) => s,
            Err(e) => {
                tracing::error!("[Pipe] Failed to create server: {}", e);
                return;
            }
        };

        loop {
            if let Err(e) = server.connect().await {
                tracing::error!("[Pipe] Connect error: {}", e);
                tokio::time::sleep(tokio::time::Duration::from_secs(
                    constants::PIPE_CONNECT_RETRY_INTERVAL_SECS,
                ))
                .await;
                continue;
            }

            let next = match create_server(false) {
                Ok(s) => s,
                Err(e) => {
                    tracing::error!("[Pipe] Failed to create next server: {}", e);
                    tokio::time::sleep(tokio::time::Duration::from_secs(
                        constants::PIPE_CONNECT_RETRY_INTERVAL_SECS,
                    ))
                    .await;
                    loop {
                        if let Ok(s) = create_server(false) {
                            break s;
                        }
                        tokio::time::sleep(tokio::time::Duration::from_secs(
                            constants::PIPE_CREATE_RETRY_INTERVAL_SECS,
                        ))
                        .await;
                    }
                }
            };

            let semaphore = state.semaphore.clone();
            let state = state.clone();
            let pool = pool.clone();
            tokio::spawn(async move {
                let Ok(_permit) = semaphore.acquire().await else { return };
                if let Err(e) = handle_client(server, state, pool).await {
                    tracing::warn!("[Pipe] Client handler error: {}", e);
                }
            });

            server = next;
        }
    }

    fn create_server(first: bool) -> io::Result<NamedPipeServer> {
        let mut opts = ServerOptions::new();
        if first {
            opts.first_pipe_instance(true);
        }
        opts.create(PIPE_NAME)
    }
}

#[cfg(target_family = "unix")]
mod imp {
    use super::*;
    use std::path::Path;
    use tokio::net::UnixListener;

    const SOCKET_PATH: &str = "/tmp/taix_daemon.sock";

    pub async fn start(state: Arc<SentryState>, pool: SqlitePool) {
        let path = Path::new(SOCKET_PATH);

        if path.exists() {
            if let Err(e) = std::fs::remove_file(path) {
                tracing::warn!("[Pipe] Failed to remove old socket: {}", e);
            }
        }

        let listener = match UnixListener::bind(path) {
            Ok(l) => l,
            Err(e) => {
                tracing::error!("[Pipe] Failed to bind Unix socket: {}", e);
                return;
            }
        };

        tracing::info!("[Pipe] Unix socket listening at {}", SOCKET_PATH);

        loop {
            let (stream, _) = match listener.accept().await {
                Ok(s) => s,
                Err(e) => {
                    tracing::error!("[Pipe] Accept error: {}", e);
                    tokio::time::sleep(tokio::time::Duration::from_secs(
                        constants::PIPE_CONNECT_RETRY_INTERVAL_SECS,
                    ))
                    .await;
                    continue;
                }
            };

            let semaphore = state.semaphore.clone();
            let state = state.clone();
            let pool = pool.clone();
            tokio::spawn(async move {
                let Ok(_permit) = semaphore.acquire().await else { return };
                if let Err(e) = handle_client(stream, state, pool).await {
                    tracing::warn!("[Pipe] Client handler error: {}", e);
                }
            });
        }
    }
}

pub use imp::start;

async fn handle_client<T>(
    client: T,
    state: Arc<SentryState>,
    pool: SqlitePool,
) -> io::Result<()>
where
    T: tokio::io::AsyncRead + tokio::io::AsyncWrite + Unpin,
{
    let mut reader = BufReader::new(client);
    let mut line = String::new();

    loop {
        line.clear();
        let n = reader.read_line(&mut line).await?;
        if n == 0 {
            break;
        }

        let trimmed = line.trim();
        if trimmed.is_empty() {
            continue;
        }

        tracing::debug!("[Pipe] Received: {}", trimmed);

        match serde_json::from_str::<PipeMsg>(trimmed) {
            Ok(msg) => match msg.t.as_str() {
                "sleep" => {
                    *state.is_sleep.write().await = true;
                    if let Err(e) = state.tx.send("sleep".to_string()) {
                        tracing::debug!("[Pipe] Broadcast sleep failed (no subscribers): {}", e);
                    }
                    tracing::info!("[Pipe] Broadcast sleep");
                }
                "wake" => {
                    *state.is_sleep.write().await = false;
                    if let Err(e) = state.tx.send("wake".to_string()) {
                        tracing::debug!("[Pipe] Broadcast wake failed (no subscribers): {}", e);
                    }
                    tracing::info!("[Pipe] Broadcast wake");
                }
                "app" => {
                    if let Some(extra) = msg.extra {
                        let now_ts = chrono::Utc::now().timestamp();
                        if extra.a < constants::MIN_VALID_TIMESTAMP
                            || extra.a > now_ts + constants::TIMESTAMP_FUTURE_TOLERANCE_SECS
                        {
                            tracing::warn!(
                                "[Pipe] Invalid timestamp {} for app {}, dropping",
                                extra.a,
                                extra.p
                            );
                            continue;
                        }

                        let date_time = chrono::DateTime::from_timestamp(extra.a, 0)
                            .unwrap_or_else(chrono::Utc::now);

                        let req = UpdateAppDurationRequest {
                            process_name: extra.p,
                            duration: extra.d,
                            start_date_time: date_time,
                            file: extra.f,
                            icon_file: extra.i,
                            description: extra.desc,
                        };

                        if let Err(e) =
                            AppTimerService::update_app_duration(&pool, req).await
                        {
                            tracing::warn!("[Pipe] Failed to update app duration: {}", e);
                        } else {
                            tracing::debug!("[Pipe] App duration updated");
                        }
                    }
                }
                _ => {
                    tracing::debug!("[Pipe] Unknown msg type: {}", msg.t);
                }
            },
            Err(e) => {
                tracing::debug!("[Pipe] Failed to parse: {}, error: {}", trimmed, e);
            }
        }
    }

    Ok(())
}
