use std::path::PathBuf;

fn main() -> anyhow::Result<()> {
    let mut args = std::env::args().skip(1);
    let icon_file = args.next().map(PathBuf::from);
    if icon_file.is_none() {
        println!("用法：toast_preview <icon.png> [插件 id] [显示名]");
    }
    let id = args.next().unwrap_or_else(|| "preview".to_string());
    let name = args.next().unwrap_or_else(|| "Taix 预览".to_string());

    let identity = match icon_file {
        Some(path) => taix_notify::app_identity(&id, &name, &std::fs::read(path)?),
        None => taix_notify::AppIdentity::default(),
    };
    println!("aumid={:?} icon={:?}", identity.aumid, identity.icon);

    let opts = taix_notify::NotifyOptions {
        aumid: identity.aumid,
        icon: identity.icon,
        sound: Some("default"),
        ..Default::default()
    };
    taix_notify::notify(&name, "已经连续使用 2 小时 15 分钟了", &opts)
}
