use axum::{
    extract::State,
    http::{header, Method, Request, StatusCode},
    middleware::Next,
    response::{IntoResponse, Response},
    Json,
};
use serde_json::Value;

use crate::response::ApiResponse;

/// 写接口 Origin 校验：GET/HEAD/OPTIONS 放行；无 Origin 放行；有 Origin 必须命中本地源。
/// /api/events 与 */poll 是消费式 GET，按写接口校验，另看 Sec-Fetch-Site。
pub async fn check(
    State(allowed): State<Vec<String>>,
    req: Request<axum::body::Body>,
    next: Next,
) -> Response {
    let method = req.method().clone();
    let path = req.uri().path();

    if matches!(method, Method::GET | Method::HEAD | Method::OPTIONS) {
        if consumes_plugin_state(path) && cross_site(&req, &allowed) {
            return forbidden(&format!("blocked cross-origin read of {path}"));
        }
        return next.run(req).await;
    }

    match origin_of(&req) {
        None => next.run(req).await,
        Some(origin) if allowed.iter().any(|a| a == &origin) => next.run(req).await,
        Some(origin) => forbidden(&format!(
            "blocked cross-origin {method} write from '{origin}'"
        )),
    }
}

/// 跨站消费 = 指令被偷走，无法恢复
fn consumes_plugin_state(path: &str) -> bool {
    path == "/api/events" || (path.starts_with("/api/plugins/") && path.ends_with("/poll"))
}

fn origin_of(req: &Request<axum::body::Body>) -> Option<String> {
    req.headers()
        .get(header::ORIGIN)
        .and_then(|v| v.to_str().ok())
        .map(|v| v.to_string())
}

/// Origin 白名单 + Sec-Fetch-Site；两者都缺 = 本地进程放行
fn cross_site(req: &Request<axum::body::Body>, allowed: &[String]) -> bool {
    if let Some(origin) = origin_of(req) {
        if !allowed.iter().any(|a| a == &origin) {
            return true;
        }
    }
    match req
        .headers()
        .get("sec-fetch-site")
        .and_then(|v| v.to_str().ok())
    {
        None => false,
        Some(site) => !(site == "same-origin" || site == "none"),
    }
}

fn forbidden(log: &str) -> Response {
    tracing::warn!(target: "taix_server::origin", "{log}");
    (
        StatusCode::FORBIDDEN,
        Json(ApiResponse::<Value> {
            code: 403,
            message: "forbidden origin".to_string(),
            data: None,
        }),
    )
        .into_response()
}
