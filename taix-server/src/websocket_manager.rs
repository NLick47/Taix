use std::net::SocketAddr;
use std::sync::Arc;
use tokio::net::TcpListener;
use tokio::sync::watch;

use crate::routes::web_sentry::SentryState;

pub async fn run(mut rx: watch::Receiver<bool>, sentry_state: Arc<SentryState>) {
    let ws_addr: SocketAddr = format!("0.0.0.0:{}", crate::constants::DEFAULT_WEBSOCKET_PORT).parse().unwrap();
    let mut is_enabled = *rx.borrow();

    loop {
        if is_enabled {
            match TcpListener::bind(ws_addr).await {
                Ok(listener) => {
                    let app = crate::routes::web_sentry::router(sentry_state.clone());
                    let (shutdown_tx, shutdown_rx) = tokio::sync::oneshot::channel::<()>();
                    let server = axum::serve(listener, app).with_graceful_shutdown(async move {
                        let _ = shutdown_rx.await;
                    });

                    tracing::info!(
                        "WebSocket listening on ws://{}/TaiWebSentry",
                        ws_addr
                    );
                    let server_handle = tokio::spawn(async move {
                        if let Err(e) = server.await {
                            tracing::error!("WebSocket server error: {}", e);
                        }
                    });

                    // 等待配置变为 false 或通道关闭
                    loop {
                        match rx.changed().await {
                            Ok(()) => {
                                is_enabled = *rx.borrow();
                                if !is_enabled {
                                    break;
                                }
                            }
                            Err(_) => {
                                let _ = shutdown_tx.send(());
                                let _ = server_handle.await;
                                return;
                            }
                        }
                    }

                    let _ = shutdown_tx.send(());
                    let _ = server_handle.await;
                    tracing::info!("WebSocket server stopped");
                }
                Err(e) => {
                    tracing::warn!(
                        "WebSocket 端口 {} 绑定失败（{}），将在配置再次变更后重试",
                        ws_addr,
                        e
                    );
                    match rx.changed().await {
                        Ok(()) => is_enabled = *rx.borrow(),
                        Err(_) => return,
                    }
                }
            }
        } else {
            tracing::info!("WebSocket server disabled by config");
            match rx.changed().await {
                Ok(()) => is_enabled = *rx.borrow(),
                Err(_) => return,
            }
        }
    }
}
