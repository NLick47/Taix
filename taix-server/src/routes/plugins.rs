use axum::{
    extract::{Path, State},
    http::{header, StatusCode},
    response::{IntoResponse, Response},
    routing::{get, post},
    Extension, Json, Router,
};
use serde::Deserialize;
use serde_json::{json, Value};
use sqlx::SqlitePool;
use std::collections::HashMap;
use std::sync::Arc;

use crate::error::AppError;
use crate::plugins::manifest;
use crate::plugins::service;
use crate::plugins::PluginState;
use crate::response::ApiResponse;

pub fn router() -> Router<SqlitePool> {
    Router::new()
        .route("/api/plugins", get(list))
        .route("/api/plugins/:id", get(detail))
        .route("/api/plugins/:id/config", get(get_config).put(save_config))
        .route("/api/plugins/:id/enabled", post(set_enabled))
        .route("/api/plugins/:id/commands", post(post_command))
        .route("/api/plugins/:id/poll", get(poll))
        .route("/api/plugins/:id/events", post(report_event))
        .route("/api/plugins/:id/state", get(get_state))
        .route("/api/plugins/:id/records", get(get_records).post(upsert_record))
        .route("/api/plugins/:id/ui/*path", get(plugin_ui))
}

fn not_found(id: &str) -> Json<ApiResponse<Value>> {
    Json(ApiResponse {
        code: 404,
        message: format!("plugin '{}' not found", id),
        data: None,
    })
}

fn disabled(id: &str) -> Json<ApiResponse<Value>> {
    Json(ApiResponse {
        code: 409,
        message: format!("plugin '{}' is disabled", id),
        data: None,
    })
}

fn err(e: AppError) -> Json<ApiResponse<Value>> {
    let code = match e {
        AppError::Business(_) => 400,
        _ => 500,
    };
    Json(ApiResponse {
        code,
        message: e.to_string(),
        data: None,
    })
}

fn ok<T: serde::Serialize>(data: T) -> Json<ApiResponse<Value>> {
    match serde_json::to_value(data) {
        Ok(value) => Json(ApiResponse::ok(value)),
        Err(e) => err(AppError::Internal(format!("序列化响应失败: {}", e))),
    }
}

fn ok_empty() -> Json<ApiResponse<Value>> {
    Json(ApiResponse::ok(Value::Null))
}

async fn list(
    Extension(state): Extension<Arc<PluginState>>,
) -> Json<ApiResponse<Value>> {
    state.rescan().await;
    let manifests = state.list_manifests().await;
    let mut items = Vec::with_capacity(manifests.len());

    for m in &manifests {
        let enabled = service::is_enabled(&state, &m.id).await;
        let config = service::get_config(&state, &m.id).await;
        let status = service::get_status(&state, &m.id).await;
        items.push(json!({
            "id": m.id,
            "name": m.name,
            "version": m.version,
            "display": m.display,
            "entry": m.entry,
            "platforms": m.platforms,
            "enabled": enabled,
            "state": status.as_ref().map(|s| s.state.clone()),
            "configVersion": config.version,
        }));
    }

    Json(ApiResponse::ok(Value::Array(items)))
}

async fn detail(
    Path(id): Path<String>,
    State(pool): State<SqlitePool>,
    Extension(state): Extension<Arc<PluginState>>,
) -> Json<ApiResponse<Value>> {
    let Some(m) = state.manifest_or_rescan(&id).await else {
        return not_found(&id);
    };

    let enabled = service::is_enabled(&state, &id).await;
    let config = service::get_config(&state, &id).await;
    let status = service::get_status(&state, &id).await;
    let records = service::get_records(&pool, &id, None).await.unwrap_or_default();

    Json(ApiResponse::ok(json!({
        "id": m.id,
        "name": m.name,
        "version": m.version,
        "display": m.display,
        "entry": m.entry,
        "platforms": m.platforms,
        "configSchema": m.config_schema,
        "configUi": m.config_ui,
        "enabled": enabled,
        "config": config,
        "configVersion": config.version,
        "status": status,
        "records": records,
    })))
}

async fn get_config(
    Path(id): Path<String>,
    Extension(state): Extension<Arc<PluginState>>,
) -> Json<ApiResponse<Value>> {
    if state.manifest_or_rescan(&id).await.is_none() {
        return not_found(&id);
    }
    ok(service::get_config(&state, &id).await)
}

#[derive(Deserialize)]
struct SaveConfigRequest {
    values: Value,
}

async fn save_config(
    Path(id): Path<String>,
    Extension(state): Extension<Arc<PluginState>>,
    Json(req): Json<SaveConfigRequest>,
) -> Json<ApiResponse<Value>> {
    if state.manifest_or_rescan(&id).await.is_none() {
        return not_found(&id);
    }

    match service::save_config(&state, &id, req.values).await {
        Ok(version) => Json(ApiResponse::ok(json!({ "configVersion": version }))),
        Err(e) => err(e),
    }
}

#[derive(Deserialize)]
struct SetEnabledRequest {
    enabled: bool,
}

async fn set_enabled(
    Path(id): Path<String>,
    Extension(state): Extension<Arc<PluginState>>,
    Json(req): Json<SetEnabledRequest>,
) -> Json<ApiResponse<Value>> {
    let Some(m) = state.manifest_or_rescan(&id).await else {
        return not_found(&id);
    };
    if req.enabled {
        let Some(dir) = state.plugin_dir_or_rescan(&id).await else {
            return not_found(&id);
        };
        if manifest::resolve_entry(&dir, &m.entry).is_none() {
            return Json(ApiResponse {
                code: 400,
                message: format!("plugin '{}': entry '{}' not found", id, m.entry),
                data: None,
            });
        }
    }

    match service::set_enabled(&state, &id, req.enabled).await {
        // 启停由 shell 轮询 registry 生效
        Ok(()) => ok_empty(),
        Err(e) => err(e),
    }
}

#[derive(Deserialize)]
struct CommandRequest {
    cmd: String,
    #[serde(default)]
    data: Option<Value>,
}

async fn post_command(
    Path(id): Path<String>,
    Extension(state): Extension<Arc<PluginState>>,
    Json(req): Json<CommandRequest>,
) -> Json<ApiResponse<Value>> {
    if state.manifest_or_rescan(&id).await.is_none() {
        return not_found(&id);
    }
    if !service::is_enabled(&state, &id).await {
        return disabled(&id);
    }

    let cmd = req.cmd.trim().to_string();
    if cmd.is_empty() || cmd.len() > 64 {
        return Json(ApiResponse {
            code: 400,
            message: "cmd 不能为空且不超过 64 字符".to_string(),
            data: None,
        });
    }

    let command = service::push_command(&state, &id, cmd, req.data);
    ok(command)
}

async fn poll(
    Path(id): Path<String>,
    Extension(state): Extension<Arc<PluginState>>,
) -> Json<ApiResponse<Value>> {
    if state.manifest_or_rescan(&id).await.is_none() {
        return not_found(&id);
    }
    if !service::is_enabled(&state, &id).await {
        return disabled(&id);
    }

    let config = service::get_config(&state, &id).await;
    let commands = service::take_commands(&state, &id);

    Json(ApiResponse::ok(json!({
        "config": config,
        "configVersion": config.version,
        "commands": commands,
    })))
}

#[derive(Deserialize)]
struct ReportEventRequest {
    state: String,
    #[serde(default)]
    data: Option<Value>,
}

async fn report_event(
    Path(id): Path<String>,
    Extension(state): Extension<Arc<PluginState>>,
    Json(req): Json<ReportEventRequest>,
) -> Json<ApiResponse<Value>> {
    if state.manifest_or_rescan(&id).await.is_none() {
        return not_found(&id);
    }
    if !service::is_enabled(&state, &id).await {
        return disabled(&id);
    }

    let state_name = req.state.trim().to_string();
    if state_name.is_empty() || state_name.len() > 64 {
        return Json(ApiResponse {
            code: 400,
            message: "state 不能为空且不超过 64 字符".to_string(),
            data: None,
        });
    }

    service::set_plugin_state(&state, &id, state_name, req.data).await;
    ok_empty()
}

async fn get_state(
    Path(id): Path<String>,
    Extension(state): Extension<Arc<PluginState>>,
) -> Json<ApiResponse<Value>> {
    if state.manifest_or_rescan(&id).await.is_none() {
        return not_found(&id);
    }

    let status = service::get_status(&state, &id).await;
    ok(status)
}

#[derive(Deserialize)]
struct UpsertRecordRequest {
    key: String,
    value: Value,
}

async fn upsert_record(
    Path(id): Path<String>,
    State(pool): State<SqlitePool>,
    Extension(state): Extension<Arc<PluginState>>,
    Json(req): Json<UpsertRecordRequest>,
) -> Json<ApiResponse<Value>> {
    if state.manifest_or_rescan(&id).await.is_none() {
        return not_found(&id);
    }

    let key = req.key.trim().to_string();
    if key.is_empty() || key.len() > 128 {
        return Json(ApiResponse {
            code: 400,
            message: "key 不能为空且不超过 128 字符".to_string(),
            data: None,
        });
    }

    match service::upsert_record(&pool, &id, &key, &req.value).await {
        Ok(()) => ok_empty(),
        Err(e) => err(e),
    }
}

async fn get_records(
    Path(id): Path<String>,
    State(pool): State<SqlitePool>,
    Extension(state): Extension<Arc<PluginState>>,
    axum::extract::Query(params): axum::extract::Query<HashMap<String, String>>,
) -> Json<ApiResponse<Value>> {
    if state.manifest_or_rescan(&id).await.is_none() {
        return not_found(&id);
    }

    // 缺省返回全部
    let key = params.get("key").cloned();

    match service::get_records(&pool, &id, key.as_deref()).await {
        Ok(rows) => ok(rows),
        Err(e) => err(e),
    }
}

/// 插件自带配置 UI 的资源上限，防误放整包资源
const MAX_UI_ASSET_BYTES: u64 = 4 * 1024 * 1024;

/// 相对路径按 / 分段校验，返回段列表供逐段拼接
/// 空段 . .. 反斜杠 冒号一律拒绝
fn safe_ui_segments(path: &str) -> Option<Vec<&str>> {
    if path.is_empty() || path.len() > 256 {
        return None;
    }
    let mut segments = Vec::new();
    for seg in path.split('/') {
        if seg.is_empty()
            || seg == "."
            || seg == ".."
            || seg.contains('\\')
            || seg.contains(':')
            || seg.contains('\0')
        {
            return None;
        }
        segments.push(seg);
    }
    Some(segments)
}

fn ui_content_type(file_name: &str) -> &'static str {
    match file_name
        .rsplit_once('.')
        .map(|(_, ext)| ext.to_ascii_lowercase())
        .as_deref()
    {
        Some("js" | "mjs") => "text/javascript; charset=utf-8",
        Some("css") => "text/css; charset=utf-8",
        Some("json" | "map") => "application/json; charset=utf-8",
        Some("html") => "text/html; charset=utf-8",
        Some("svg") => "image/svg+xml",
        Some("png") => "image/png",
        Some("jpg" | "jpeg") => "image/jpeg",
        Some("gif") => "image/gif",
        Some("webp") => "image/webp",
        Some("woff2") => "font/woff2",
        Some("woff") => "font/woff",
        Some("wasm") => "application/wasm",
        Some("txt") => "text/plain; charset=utf-8",
        _ => "application/octet-stream",
    }
}

fn asset_status(status: StatusCode, msg: &str) -> Response {
    (status, msg.to_string()).into_response()
}

/// 插件自带配置 UI 的静态资源，只读 <插件目录>/ui/ 下的普通文件
/// 动态 import 是同源 GET，origin_check 对 GET 放行
async fn plugin_ui(
    Path((id, path)): Path<(String, String)>,
    Extension(state): Extension<Arc<PluginState>>,
) -> Response {
    let Some(dir) = state.plugin_dir_or_rescan(&id).await else {
        return asset_status(StatusCode::NOT_FOUND, "plugin not found");
    };

    let Some(segments) = safe_ui_segments(&path) else {
        return asset_status(StatusCode::BAD_REQUEST, "invalid asset path");
    };

    let root = dir.join("ui");
    let mut full = root.clone();
    for seg in &segments {
        full.push(seg);
    }

    // 段级检查之外再做一次规范化兜底：符号链接 / 平台路径怪癖都不许逃出 ui 根
    let (Ok(real), Ok(real_root)) = (full.canonicalize(), root.canonicalize()) else {
        return asset_status(StatusCode::NOT_FOUND, "asset not found");
    };
    if !real.starts_with(&real_root) {
        return asset_status(StatusCode::BAD_REQUEST, "invalid asset path");
    }

    // 先看大小再读，避免为超大文件做整块分配；读完再查一次防读期间变大
    match tokio::fs::metadata(&real).await {
        Ok(meta) if meta.len() > MAX_UI_ASSET_BYTES => {
            return asset_status(StatusCode::PAYLOAD_TOO_LARGE, "asset too large");
        }
        Ok(_) => {}
        Err(_) => return asset_status(StatusCode::NOT_FOUND, "asset not found"),
    }
    let data = match tokio::fs::read(&real).await {
        Ok(data) if data.len() as u64 <= MAX_UI_ASSET_BYTES => data,
        Ok(_) => return asset_status(StatusCode::PAYLOAD_TOO_LARGE, "asset too large"),
        Err(_) => return asset_status(StatusCode::NOT_FOUND, "asset not found"),
    };

    // 按规范化后的文件名判类型：Windows 会吃掉结尾的点号，请求里的名字可能与实际不符
    let file_name = real.file_name().and_then(|n| n.to_str()).unwrap_or("");
    let content_type = ui_content_type(file_name);
    (
        StatusCode::OK,
        [
            (header::CONTENT_TYPE, content_type),
            (header::CACHE_CONTROL, "no-store"),
            (header::X_CONTENT_TYPE_OPTIONS, "nosniff"),
        ],
        data,
    )
        .into_response()
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn ui_segments_reject_traversal() {
        for bad in [
            "",
            ".",
            "..",
            "../plugin.json",
            "a/../../plugin.json",
            "a//b.js",
            "/etc/passwd",
            "a\\b.js",
            "C:config.js",
            "chunk.js:stream",
        ] {
            assert!(safe_ui_segments(bad).is_none(), "应拒绝: {bad:?}");
        }
    }

    #[test]
    fn ui_segments_accept_nested_assets() {
        assert_eq!(safe_ui_segments("config.js").unwrap(), vec!["config.js"]);
        assert_eq!(
            safe_ui_segments("chunks/vendor-abc.js").unwrap(),
            vec!["chunks", "vendor-abc.js"]
        );
    }

    #[test]
    fn ui_content_types_cover_module_assets() {
        assert_eq!(ui_content_type("config.js"), "text/javascript; charset=utf-8");
        assert_eq!(ui_content_type("chunk.mjs"), "text/javascript; charset=utf-8");
        assert_eq!(ui_content_type("style.CSS"), "text/css; charset=utf-8");
        assert_eq!(ui_content_type("logo.svg"), "image/svg+xml");
        assert_eq!(ui_content_type("noext"), "application/octet-stream");
    }

    #[test]
    fn ui_segments_reject_overlong_path() {
        assert!(safe_ui_segments(&"a".repeat(257)).is_none());
    }

    use axum::body::Body;
    use axum::http::Request;
    use http_body_util::BodyExt;
    use tower::ServiceExt;

    /// 隔离的插件目录 + 内存库，挂真实路由覆盖路由注册与响应头
    async fn ui_test_app(root: &std::path::Path) -> axum::Router {
        let plugins_dir = root.join("plugins");
        let plugin_dir = plugins_dir.join("reminder");
        std::fs::create_dir_all(plugin_dir.join("ui")).unwrap();
        std::fs::write(
            plugin_dir.join("plugin.json"),
            r#"{"id":"reminder","name":"提醒","version":"0.1.0","entry":"taix-plugin-reminder","configUi":"config.js"}"#,
        )
        .unwrap();
        std::fs::write(plugin_dir.join("ui").join("config.js"), "export function mount() {}").unwrap();

        let data_dir = root.join("data");
        std::fs::create_dir_all(&data_dir).unwrap();
        let state = Arc::new(PluginState::init(&plugins_dir, &data_dir).await.unwrap());
        let pool = SqlitePool::connect("sqlite::memory:").await.unwrap();
        router().with_state(pool).layer(Extension(state))
    }

    async fn get(app: &axum::Router, uri: &str) -> (StatusCode, axum::http::HeaderMap, Vec<u8>) {
        let resp = app
            .clone()
            .oneshot(Request::builder().uri(uri).body(Body::empty()).unwrap())
            .await
            .unwrap();
        let status = resp.status();
        let headers = resp.headers().clone();
        let body = resp.into_body().collect().await.unwrap().to_bytes().to_vec();
        (status, headers, body)
    }

    #[tokio::test]
    async fn ui_route_serves_module_with_hardening_headers() {
        let root = std::env::temp_dir().join(format!("taix-ui-route-{}", std::process::id()));
        let _ = std::fs::remove_dir_all(&root);
        let app = ui_test_app(&root).await;

        let (status, headers, body) = get(&app, "/api/plugins/reminder/ui/config.js").await;
        assert_eq!(status, StatusCode::OK);
        assert_eq!(
            headers.get(header::CONTENT_TYPE).unwrap(),
            "text/javascript; charset=utf-8"
        );
        assert_eq!(headers.get(header::CACHE_CONTROL).unwrap(), "no-store");
        assert_eq!(headers.get(header::X_CONTENT_TYPE_OPTIONS).unwrap(), "nosniff");
        assert_eq!(body, b"export function mount() {}");

        std::fs::remove_dir_all(&root).ok();
    }

    #[tokio::test]
    async fn ui_route_rejects_traversal_and_missing_assets() {
        let root = std::env::temp_dir().join(format!("taix-ui-route-bad-{}", std::process::id()));
        let _ = std::fs::remove_dir_all(&root);
        let app = ui_test_app(&root).await;

        // 穿越写法必须被拒，且不得读到 ui/ 之外的文件
        for uri in [
            "/api/plugins/reminder/ui/%2e%2e/plugin.json",
            "/api/plugins/reminder/ui/..%2Fplugin.json",
            "/api/plugins/reminder/ui/a%5Cb.js",
        ] {
            let (status, _, body) = get(&app, uri).await;
            assert!(
                status == StatusCode::BAD_REQUEST || status == StatusCode::NOT_FOUND,
                "{uri} -> {status}"
            );
            assert!(!String::from_utf8_lossy(&body).contains("\"entry\""), "{uri} 越权读到清单");
        }

        let (status, _, _) = get(&app, "/api/plugins/reminder/ui/missing.js").await;
        assert_eq!(status, StatusCode::NOT_FOUND);

        let (status, _, _) = get(&app, "/api/plugins/nope/ui/config.js").await;
        assert_eq!(status, StatusCode::NOT_FOUND);

        std::fs::remove_dir_all(&root).ok();
    }

    #[tokio::test]
    async fn detail_reports_config_ui() {
        let root = std::env::temp_dir().join(format!("taix-ui-detail-{}", std::process::id()));
        let _ = std::fs::remove_dir_all(&root);
        let app = ui_test_app(&root).await;

        let (status, _, body) = get(&app, "/api/plugins/reminder").await;
        assert_eq!(status, StatusCode::OK);
        let text = String::from_utf8_lossy(&body);
        assert!(
            text.contains("\"configUi\":\"config.js\""),
            "详情应带 configUi: {text}"
        );

        std::fs::remove_dir_all(&root).ok();
    }
}
