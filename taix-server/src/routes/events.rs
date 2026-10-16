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

    if sub != "webui" && state.manifest_or_rescan(&sub).await.is_none() {
        return (
            StatusCode::NOT_FOUND,
            Json(ApiResponse {
                code: 404,
                message: format!("plugin '{}' not found", sub),
                data: None::<serde_json::Value>,
            }),
        )
            .into_response();
    }

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

/// 指令出口：每个订阅者用自己的游标读日志，断线重连按游标补投
fn command_stream(
    state: Arc<PluginState>,
    id: String,
) -> impl tokio_stream::Stream<Item = Result<Event, Infallible>> {
    let (tx, rx) = tokio::sync::mpsc::channel(constants::PLUGIN_COMMAND_QUEUE_MAX);
    // 单订阅者的缓冲上限；指令日志另有上限，泵阻塞不影响其它订阅者
    tokio::spawn(async move {
        let notifier = state.command_notifier(&id);
        // 起始游标 0 = 补投日志里保留下来的指令；插件端按 seq 去重
        let mut cursor = 0u64;
        loop {
            for command in state.commands_after(&id, cursor) {
                cursor = cursor.max(command.seq);
                if tx.send(Ok(command_event(&id, &command))).await.is_err() {
                    return; // 订阅者断开，泵退出；指令仍在日志里，重连后补投
                }
            }
            // 死实例残留的泵可能抢走一次性唤醒，超时兜底重扫，指令最迟 10s 内投出
            let _ = tokio::time::timeout(Duration::from_secs(10), notifier.notified()).await;
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
