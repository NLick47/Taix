use std::collections::HashMap;
use std::convert::Infallible;
use std::sync::Arc;
use std::time::Duration;

use axum::{
    extract::{Extension, Query},
    http::StatusCode,
    response::{
        sse::{Event, KeepAlive, Sse},
        IntoResponse, Response,
    },
    routing::get,
    Json, Router,
};
use serde_json::Value;
use sqlx::SqlitePool;
use tokio_stream::wrappers::{BroadcastStream, ReceiverStream};
use tokio_stream::StreamExt;

use crate::constants;
use crate::plugins::{manifest, PluginCommand, PluginState};
use crate::response::ApiResponse;

/// sub=webui 只收广播；sub=<id> 另收定向 config 与指令流
/// 先订阅广播再取队列，极端并发可能多投一次，插件侧 seq 窗口兜底
pub fn router() -> Router<SqlitePool> {
    Router::new().route("/api/events", get(sse_handler))
}

async fn sse_handler(
    Extension(state): Extension<Arc<PluginState>>,
    Query(params): Query<HashMap<String, String>>,
) -> Response {
    let Some(sub) = params.get("sub").cloned().filter(|s| valid_sub(s)) else {
        return (
            StatusCode::BAD_REQUEST,
            Json(ApiResponse {
                code: 400,
                message: "缺少或非法的 sub 参数（插件 id 或 webui）".to_string(),
                data: None::<serde_json::Value>,
            }),
        )
            .into_response();
    };

    let keep_alive =
        KeepAlive::default().interval(Duration::from_secs(constants::PLUGIN_SSE_KEEPALIVE_SECS));

    let rx = state.tx.subscribe();
    let stream_sub = sub.clone();
    let broadcast = BroadcastStream::new(rx).filter_map(move |msg| match msg {
        Ok(text) => {
            event_for(&text, &stream_sub).map(|data| Ok::<_, Infallible>(Event::default().data(data)))
        }
        Err(tokio_stream::wrappers::errors::BroadcastStreamRecvError::Lagged(n)) => {
            tracing::warn!(
                target: "taix_server::plugins",
                "sse subscriber '{}' lagged by {}, events dropped",
                stream_sub,
                n
            );
            None
        }
    });

    if sub == "webui" {
        return Sse::new(broadcast).keep_alive(keep_alive).into_response();
    }

    let full = command_stream(state, sub).merge(broadcast);
    Sse::new(full).keep_alive(keep_alive).into_response()
}

/// 该插件唯一的指令出口；取走即已投递（至多一次）
fn command_stream(
    state: Arc<PluginState>,
    id: String,
) -> impl tokio_stream::Stream<Item = Result<Event, Infallible>> {
    let (tx, rx) = tokio::sync::mpsc::channel(constants::PLUGIN_COMMAND_QUEUE_MAX);
    tokio::spawn(async move {
        // 先拿通知器；Notify 留存许可，唤醒不会丢
        let notifier = state.command_notifier(&id);
        loop {
            for command in state.take_pending(&id) {
                if tx.send(Ok(command_event(&id, &command))).await.is_err() {
                    return; // 订阅者断开，泵退出
                }
            }
            notifier.notified().await;
        }
    });
    ReceiverStream::new(rx)
}

fn command_event(id: &str, command: &PluginCommand) -> Event {
    let data = serde_json::json!({
        "type": "command",
        "id": id,
        "seq": command.seq,
        "cmd": command.cmd,
        "data": command.data,
    });
    Event::default().data(data.to_string())
}

fn valid_sub(sub: &str) -> bool {
    sub == "webui" || manifest::valid_id(sub)
}

fn event_for(text: &str, sub: &str) -> Option<String> {
    let v: Value = serde_json::from_str(text).ok()?;
    match v.get("type")?.as_str()? {
        "current" | "plugin-state" => Some(text.to_string()),
        "config" => {
            if sub != "webui" && v.get("id").and_then(Value::as_str) == Some(sub) {
                Some(text.to_string())
            } else {
                None
            }
        }
        _ => None,
    }
}
