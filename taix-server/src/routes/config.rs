use axum::{
    extract::Extension,
    routing::{get},
    Json, Router,
};
use serde_json::Value;
use sqlx::SqlitePool;
use std::sync::Arc;
use tokio::sync::watch;

use crate::models::config::ConfigModel;
use crate::response::ApiResponse;
use crate::services::config::ConfigService;

pub fn router() -> Router<SqlitePool> {
    Router::new()
        .route("/api/config", get(get_config).post(save_config))
}

async fn get_config(Extension(service): Extension<Arc<ConfigService>>) -> Json<ApiResponse<Value>> {
    match service.get_or_load().await {
        Ok(config) => match serde_json::to_value(config.as_ref()) {
            Ok(mut value) => {
                to_camel_case_keys(&mut value);
                Json(ApiResponse::ok(value))
            }
            Err(e) => Json(ApiResponse {
                code: 500,
                message: format!("Failed to serialize config: {}", e),
                data: None,
            }),
        },
        Err(e) => Json(ApiResponse {
            code: 500,
            message: e.to_string(),
            data: None,
        }),
    }
}

async fn save_config(
    Extension(service): Extension<Arc<ConfigService>>,
    Extension(web_enabled_tx): Extension<watch::Sender<bool>>,
    Json(mut req): Json<Value>,
) -> Json<ApiResponse<()>> {
    to_pascal_case_keys(&mut req);
    let config: ConfigModel = match serde_json::from_value(req) {
        Ok(c) => c,
        Err(e) => {
            return Json(ApiResponse {
                code: 400,
                message: format!("Invalid config format: {}", e),
                data: None,
            });
        }
    };

    // 读取旧配置，用于比较 is_web_enabled 是否变化
    let old_enabled = match service.get_cached().await {
        Ok(old) => old.general.is_web_enabled,
        Err(_) => {
            // 缓存未预热时回退到加载
            match service.get_or_load().await {
                Ok(old) => old.general.is_web_enabled,
                Err(e) => {
                    tracing::warn!("Failed to load old config before save: {}", e);
                    false
                }
            }
        }
    };

    let new_enabled = config.general.is_web_enabled;

    match service.save(&config).await {
        Ok(_) => {
            if old_enabled != new_enabled {
                let _ = web_enabled_tx.send(new_enabled);
                tracing::info!(
                    "WebSocket enabled config changed: {} -> {}",
                    old_enabled,
                    new_enabled
                );
            }
            Json(ApiResponse::ok_empty())
        }
        Err(e) => Json(ApiResponse {
            code: 500,
            message: e.to_string(),
            data: None,
        }),
    }
}

fn to_camel_case_keys(value: &mut Value) {
    if let Value::Object(map) = value {
        let old_map = std::mem::take(map);
        for (k, mut v) in old_map {
            to_camel_case_keys(&mut v);
            map.insert(to_camel_case(&k), v);
        }
    } else if let Value::Array(arr) = value {
        for v in arr {
            to_camel_case_keys(v);
        }
    }
}

fn to_pascal_case_keys(value: &mut Value) {
    if let Value::Object(map) = value {
        let old_map = std::mem::take(map);
        for (k, mut v) in old_map {
            to_pascal_case_keys(&mut v);
            map.insert(to_pascal_case(&k), v);
        }
    } else if let Value::Array(arr) = value {
        for v in arr {
            to_pascal_case_keys(v);
        }
    }
}

fn to_camel_case(s: &str) -> String {
    if s.is_empty() {
        return s.to_string();
    }
    let mut chars = s.chars();
    let first = chars.next().unwrap().to_lowercase().to_string();
    first + chars.as_str()
}

fn to_pascal_case(s: &str) -> String {
    if s.is_empty() {
        return s.to_string();
    }
    let mut chars = s.chars();
    let first = chars.next().unwrap().to_uppercase().to_string();
    first + chars.as_str()
}
