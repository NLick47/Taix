fn main() {

    #[cfg(target_os = "windows")]
    {
        let manifest_dir = std::path::Path::new("resources");
        let _ = embed_resource::compile(
            manifest_dir.join("taix-installer.rc"),
            embed_resource::NONE,
        );
    }
}
