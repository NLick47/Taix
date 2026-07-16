use anyhow::{bail, Context, Result};
use std::fs;
use std::io::Write;
use std::path::Path;
use std::process::Command;

fn main() -> Result<()> {
    let args: Vec<String> = std::env::args().collect();
    if args.len() < 3 {
        bail!("Usage: pack-payload <source_dir> <output_path>");
    }

    let source_dir = Path::new(&args[1]);
    let output_path = Path::new(&args[2]);

    if !source_dir.is_dir() {
        bail!("Source directory does not exist: {}", source_dir.display());
    }

    pack_payload(source_dir, output_path)
}

fn pack_payload(source_dir: &Path, output_path: &Path) -> Result<()> {
    // 使用 7z 极限压缩
    // -mx=9 最高压缩级别
    // -mf=BCJ2 可执行文件过滤器
    let temp_7z = output_path.with_extension("7z");

    // 尝试多个可能的 7z 路径
    let seven_zip_paths = [
        "7z",
        "C:\\Program Files\\7-Zip\\7z.exe",
        "C:\\Program Files (x86)\\7-Zip\\7z.exe",
    ];

    let mut success = false;
    let mut last_error = String::new();

    for seven_zip in &seven_zip_paths {
        println!("Trying 7z path: {}", seven_zip);
        let result = Command::new(seven_zip)
            .args([
                "a",
                "-t7z",
                "-mx=9",
                "-mf=BCJ2",
                &format!("{}", temp_7z.display()),
                &format!("{}\\*", source_dir.display()),
            ])
            .output();

        match result {
            Ok(output) => {
                println!("stdout: {}", String::from_utf8_lossy(&output.stdout));
                if !output.stderr.is_empty() {
                    println!("stderr: {}", String::from_utf8_lossy(&output.stderr));
                }
                if output.status.success() {
                    success = true;
                    break;
                } else {
                    last_error = format!(
                        "{} failed with code {:?}: {}",
                        seven_zip,
                        output.status.code(),
                        String::from_utf8_lossy(&output.stderr)
                    );
                }
            }
            Err(e) => {
                last_error = format!("{} not found: {}", seven_zip, e);
            }
        }
    }

    if !success {
        bail!("Failed to run 7z. Tried all paths. Last error: {}", last_error);
    }

    // 读取压缩后的 7z 文件
    let compressed = fs::read(&temp_7z).context("Failed to read compressed file")?;

    println!(
        "Compressed payload size: {} bytes",
        compressed.len()
    );

    // 写入 payload + marker + size
    let mut output = Vec::new();
    output.write_all(&compressed)?;

    // 写入 marker
    const PAYLOAD_MARKER: &[u8] = b"TAIX_PAYLOAD_END";
    output.write_all(PAYLOAD_MARKER)?;

    // 写入 payload 大小
    output.write_all(&(compressed.len() as u64).to_le_bytes())?;

    fs::write(output_path, &output)
        .with_context(|| format!("Failed to write output: {}", output_path.display()))?;

    // 清理临时文件
    let _ = fs::remove_file(&temp_7z);

    println!("Payload written to: {}", output_path.display());
    Ok(())
}
