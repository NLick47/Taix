use axum::{
    extract::ws::{Message, WebSocket, WebSocketUpgrade},
    extract::State,
    response::IntoResponse,
    routing::get,
    Router,
};
use serde::Deserialize;
use sqlx::SqlitePool;
use std::path::PathBuf;
use std::sync::Arc;
use tokio::sync::{broadcast, RwLock, Semaphore};

use crate::models::request::AddUrlBrowseTimeRequest;
use crate::services::web_data::WebDataService;

pub struct SentryState {
    pub pool: SqlitePool,
    pub is_sleep: RwLock<bool>,
    pub tx: broadcast::Sender<String>,
    pub web_favicons_dir: PathBuf,
    pub semaphore: Arc<Semaphore>,
}

#[derive(Debug, Deserialize)]
struct WebBrowseData {
    #[serde(rename = "Url")]
    url: String,
    #[serde(rename = "Title")]
    title: Option<String>,
    #[serde(rename = "Icon")]
    icon: Option<String>,
    #[serde(rename = "Duration")]
    duration: i64,
    #[serde(rename = "ActiveTime")]
    active_time: i64,
}

pub fn router(state: Arc<SentryState>) -> Router {
    Router::new()
        .route("/TaiWebSentry", get(ws_handler))
        .with_state(state)
}

async fn ws_handler(
    ws: WebSocketUpgrade,
    State(state): State<Arc<SentryState>>,
) -> impl IntoResponse {
    ws.on_upgrade(move |socket| handle_socket(socket, state))
}

async fn handle_socket(mut socket: WebSocket, state: Arc<SentryState>) {
    let mut rx = state.tx.subscribe();

    loop {
        tokio::select! {
            msg = socket.recv() => {
                match msg {
                    Some(Ok(Message::Text(text))) => {
                        if text == "ping" || text == "pong" {
                            continue;
                        }

                        if text == "sleep" {
                            *state.is_sleep.write().await = true;
                            let _ = state.tx.send("sleep".to_string());
                            continue;
                        }

                        if text == "wake" {
                            *state.is_sleep.write().await = false;
                            let _ = state.tx.send("wake".to_string());
                            continue;
                        }

                        match serde_json::from_str::<WebBrowseData>(&text) {
                            Ok(data) => {
                                let semaphore = state.semaphore.clone();
                                let state = state.clone();
                                tokio::spawn(async move {
                                    let Ok(_permit) = semaphore.acquire().await else { return; };
                                    handle_browse_data(state, data).await;
                                });
                            }
                            Err(e) => {
                                tracing::debug!("Failed to parse browse data: {}", e);
                            }
                        }
                    }
                    Some(Ok(Message::Close(_))) | None => break,
                    _ => {}
                }
            }
            Ok(msg) = rx.recv() => {
                let _ = socket.send(Message::Text(msg)).await;
            }
        }
    }
}

async fn handle_browse_data(state: Arc<SentryState>, data: WebBrowseData) {
    let date_time = chrono::DateTime::from_timestamp(data.active_time, 0)
        .map(|dt| dt.with_timezone(&chrono::Local).naive_local());
    let req = AddUrlBrowseTimeRequest {
        url: data.url,
        title: data.title,
        duration: data.duration,
        date_time,
        icon_url: data.icon,
    };

    let favicons_dir = &state.web_favicons_dir;
    let _ = WebDataService::add_url_browse_time(
        &state.pool,
        req,
        Some(favicons_dir),
    ).await;
}
