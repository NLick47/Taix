use std::env;
use std::fs;
use std::path::Path;

fn main() {
    println!("cargo:rustc-check-cfg=cfg(has_payload)");
    println!("cargo:rerun-if-env-changed=PAYLOAD_FILE");
    println!("cargo:rerun-if-changed=build.rs");

    if let Ok(payload_file) = env::var("PAYLOAD_FILE") {
        let payload_path = Path::new(&payload_file);
        if payload_path.exists() {
            let out_dir = env::var("OUT_DIR").expect("OUT_DIR not set");
            let dest_path = Path::new(&out_dir).join("payload.zst");

            fs::copy(payload_path, &dest_path).expect("Failed to copy payload");

            println!(
                "cargo:warning=Embedded payload from: {} ({} bytes)",
                payload_path.display(),
                fs::metadata(payload_path).map(|m| m.len()).unwrap_or(0)
            );
            println!("cargo:rustc-cfg=has_payload");
        } else {
            println!(
                "cargo:warning=PAYLOAD_FILE specified but file not found: {}",
                payload_file
            );
        }
    }
}
