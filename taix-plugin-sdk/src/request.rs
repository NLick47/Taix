use serde_json::{json, Value};

/// 界面靠它在 records 里认领应答
pub const NONCE_FIELD: &str = "nonce";

#[derive(Debug, Clone)]
pub struct Request {
    pub nonce: String,
    pub payload: Value,
}

impl Request {
    pub fn parse(data: Option<&Value>) -> Option<Self> {
        let data = data?.as_object()?;
        let nonce = data.get(NONCE_FIELD)?.as_str()?.to_string();
        let mut payload = data.clone();
        payload.remove(NONCE_FIELD);
        Some(Self {
            nonce,
            payload: Value::Object(payload),
        })
    }

    pub fn str_field(&self, key: &str) -> Option<&str> {
        self.payload.get(key)?.as_str()
    }
}

#[derive(Debug, Clone)]
pub struct Reply {
    pub nonce: String,
    pub ok: bool,
    pub error: Option<String>,
    pub data: Value,
}

impl Reply {
    pub fn ok(nonce: impl Into<String>, data: Value) -> Self {
        Self {
            nonce: nonce.into(),
            ok: true,
            error: None,
            data,
        }
    }

    pub fn err(nonce: impl Into<String>, error: impl Into<String>) -> Self {
        Self {
            nonce: nonce.into(),
            ok: false,
            error: Some(error.into()),
            data: Value::Null,
        }
    }

    /// 不带时间戳；records 行自带 updatedAt
    pub fn to_value(&self) -> Value {
        json!({
            NONCE_FIELD: self.nonce,
            "ok": self.ok,
            "error": self.error,
            "data": self.data,
        })
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn request_requires_nonce() {
        assert!(Request::parse(Some(&json!({ "script": "return nil" }))).is_none());
        assert!(Request::parse(None).is_none());
        assert!(Request::parse(Some(&json!("not an object"))).is_none());
    }

    #[test]
    fn request_splits_payload() {
        let req = Request::parse(Some(&json!({ "nonce": "n1", "script": "return nil" }))).unwrap();
        assert_eq!(req.nonce, "n1");
        assert_eq!(req.str_field("script"), Some("return nil"));
        assert!(req.payload.get("nonce").is_none(), "payload 不应带 nonce");
    }

    #[test]
    fn reply_envelope_shape() {
        let ok = Reply::ok("n1", json!({ "action": { "title": "x" } })).to_value();
        assert_eq!(ok["nonce"], json!("n1"));
        assert_eq!(ok["ok"], json!(true));
        assert_eq!(ok["error"], json!(null));
        assert_eq!(ok["data"]["action"]["title"], json!("x"));
        assert!(ok.get("at").is_none(), "应答里不重复塞时间戳（用 records 行的 updatedAt）");

        let err = Reply::err("n2", "脚本编译失败: ...").to_value();
        assert_eq!(err["ok"], json!(false));
        assert_eq!(err["data"], json!(null));
        assert!(err["error"].as_str().unwrap().contains("编译失败"));
    }
}
