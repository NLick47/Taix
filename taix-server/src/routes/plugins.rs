use axum::{
    extract::{Path, State},
    routing::{get, post},
    Extension, Json, Router,
};
use serde::Deserialize;
use serde_json::{json, Value};
use sqlx::SqlitePool;
use std::collections::HashMap;
use std::sync::Arc;

use crate::error::AppError;
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
}

fn not_found(id: &str) -> Json<ApiResponse<Value>> {
    Json(ApiResponse {
        code: 404,
        message: format!("plugin '{}' not found", id),
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
    if state.manifest_or_rescan(&id).await.is_none() {
        return not_found(&id);
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
