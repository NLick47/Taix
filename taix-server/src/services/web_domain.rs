const SEPARATORS: [&str; 7] = [" - ", " | ", " — ", " – ", " _ ", " · ", " • "];

/// 从 URL 中提取域名，回环地址（localhost/127.0.0.1/::1）保留端口
pub(crate) fn extract_domain(url: &str) -> String {
    let without_scheme = url
        .trim_start_matches("http://")
        .trim_start_matches("https://");
    let host_part = without_scheme
        .split(['/', '?', '#'])
        .next()
        .unwrap_or(without_scheme);

    // IPv6 方括号形式：[::1]:8080
    if let Some(rest) = host_part.strip_prefix('[') {
        let close = match rest.find(']') {
            Some(i) => i,
            None => return host_part.to_string(), // 畸形 URL（缺右括号），按原样处理
        };
        let host = &rest[..close];
        if is_loopback_host(host) {
            let after = &rest[close + 1..];
            if let Some(port) = after.strip_prefix(':') {
                return format!("[{}]:{}", host.to_ascii_lowercase(), port);
            }
            return format!("[{}]", host.to_ascii_lowercase());
        }
        return host.to_ascii_lowercase();
    }

    let (host, port) = match host_part.split_once(':') {
        Some((h, p)) => (h, Some(p)),
        None => (host_part, None),
    };

    if is_loopback_host(host) {
        match port {
            Some(p) if !p.is_empty() && p.chars().all(|c| c.is_ascii_digit()) => {
                format!("{}:{}", host.to_ascii_lowercase(), p)
            }
            _ => host.to_ascii_lowercase(),
        }
    } else {
        host.to_ascii_lowercase()
    }
}

/// 判断是否为回环主机（localhost / 127.0.0.1 / ::1）
fn is_loopback_host(host: &str) -> bool {
    matches!(
        host.to_ascii_lowercase().as_str(),
        "localhost" | "127.0.0.1" | "::1" | "[::1]" | "0:0:0:0:0:0:0:1"
    )
}

/// 从 host[:port] 中剥离端口部分（支持 IPv6 方括号形式）
pub(crate) fn host_without_port(host: &str) -> &str {
    if let Some(rest) = host.strip_prefix('[') {
        return rest.split(']').next().unwrap_or(host);
    }
    host.split(':').next().unwrap_or(host)
}

/// 判断站点域名是否为回环站点（可能带端口）
pub(crate) fn is_loopback_site_domain(domain: &str) -> bool {
    is_loopback_host(domain) || is_loopback_host(host_without_port(domain))
}

/// 从 URL 中提取回环地址的 host:port 分组键；非回环地址或无端口地址返回 None
pub(crate) fn loopback_port_key(url: &str) -> Option<String> {
    let without_scheme = url
        .trim_start_matches("http://")
        .trim_start_matches("https://");
    let host_part = without_scheme.split(['/', '?', '#']).next()?;
    if host_part.is_empty() {
        return None;
    }

    // IPv6 方括号形式：[::1]:8080
    if let Some(rest) = host_part.strip_prefix('[') {
        let close = rest.find(']')?;
        let host = &rest[..close];
        if !is_loopback_host(host) {
            return None;
        }
        let port = rest[close + 1..].strip_prefix(':').unwrap_or("");
        if port.is_empty() || !port.chars().all(|c| c.is_ascii_digit()) {
            return None;
        }
        return Some(format!("[{}]:{}", host.to_ascii_lowercase(), port));
    }

    let (host, port) = host_part.split_once(':').unwrap_or((host_part, ""));
    if !is_loopback_host(host) {
        return None;
    }
    if port.is_empty() || !port.chars().all(|c| c.is_ascii_digit()) {
        return None;
    }
    Some(format!("{}:{}", host.to_ascii_lowercase(), port))
}

/// 从 URL 字符串提取 scheme 和 domain
pub(crate) fn extract_domain_scheme(url_str: &str) -> Option<(String, String)> {
    let stripped = url_str.strip_prefix("https://")
        .or_else(|| url_str.strip_prefix("http://"))?;
    let domain = stripped.split('/').next()?;
    let scheme = if url_str.starts_with("https") {
        "https".to_string()
    } else {
        "http".to_string()
    };
    Some((scheme, domain.to_ascii_lowercase()))
}

/// 清洗 HTML title，提取核心品牌名（去掉常见分隔符后的标语后缀）
pub(crate) fn clean_title(title: &str) -> Option<String> {
    let title = title.trim();
    if title.is_empty() {
        return None;
    }

    let mut result = title;
    for sep in &SEPARATORS {
        if let Some(idx) = result.find(sep) {
            result = &result[..idx];
        }
    }

    let result = result.trim();
    if result.is_empty() {
        None
    } else {
        Some(result.to_string())
    }
}

/// 常见无意义的标题片段
pub(crate) fn is_generic_title_word(part: &str) -> bool {
    let lower = part.to_ascii_lowercase();
    matches!(
        lower.as_str(),
        "home" | "首页" | "homepage" | "index" | "default" | "untitled" | "无标题"
            | "new tab" | "新标签页" | "空白页" | "app" | "localhost" | "127.0.0.1" | "::1"
    )
}

/// 从原始页面标题中提取第一个有意义的部分（按常见分隔符拆分）
fn meaningful_page_title(raw_title: Option<&str>) -> Option<String> {
    const MAX_TITLE_LEN: usize = 64;

    let raw_title = raw_title?;
    let mut parts: Vec<&str> = vec![raw_title];
    for sep in SEPARATORS {
        parts = parts.iter().flat_map(|p| p.split(sep)).collect();
    }
    for part in parts {
        let part = part.trim();
        if part.is_empty() || part.chars().count() > MAX_TITLE_LEN {
            continue;
        }
        if is_generic_title_word(part) {
            continue;
        }
        return Some(part.to_string());
    }
    None
}

/// 判断站点标题是否为占位符（空、域名本身或回环主机名），可被更有意义的页面标题替换
pub(crate) fn is_placeholder_site_title(title: &str, domain: &str) -> bool {
    let t = title.trim().to_ascii_lowercase();
    if t.is_empty() {
        return true;
    }
    let d = domain.to_ascii_lowercase();
    t == d || (is_loopback_site_domain(domain) && t == host_without_port(domain).to_ascii_lowercase())
}

/// 提取站点显示名称：
/// - 回环站点优先使用页面标题中有意义的部分（如 "Gradio"），否则回退 host:port
/// - 其他站点保持域名推断逻辑
pub(crate) fn extract_site_name(domain: &str, raw_title: Option<&str>) -> String {
    if is_loopback_site_domain(domain) {
        if let Some(title) = meaningful_page_title(raw_title) {
            let title_lower = title.to_ascii_lowercase();
            let host_lower = host_without_port(domain).to_ascii_lowercase();
            if title_lower != domain.to_ascii_lowercase() && title_lower != host_lower {
                return title;
            }
        }
        return domain.to_string();
    }
    infer_name_from_domain(domain)
}

/// 从域名推断站点名称：取主品牌名，如有有意义的子域名则附加在后
fn infer_name_from_domain(domain: &str) -> String {
    let lower = domain.to_lowercase();

    let without_prefix = lower.trim_start_matches("www.");

    let parts: Vec<&str> = without_prefix.split('.').collect();
    if parts.len() >= 2 {
        let brand = parts[parts.len() - 2];
        if brand.len() > 1 {
            let brand_name = capitalize_first(brand);

            // 如有额外子域名（非品牌部分），将其作为后缀，排除常见无意义前缀
            if parts.len() > 2 {
                let subdomain = parts[0];
                let meaningless = ["m", "app", "chat", "web", "my", "account", "login", "mail"];
                if !meaningless.contains(&subdomain) && subdomain != brand {
                    return format!("{} {}", brand_name, capitalize_first(subdomain));
                }
            }

            return brand_name;
        }
    }

    domain.to_string()
}

fn capitalize_first(s: &str) -> String {
    let mut chars = s.chars();
    match chars.next() {
        Some(first) => first.to_uppercase().collect::<String>() + chars.as_str(),
        None => s.to_string(),
    }
}
