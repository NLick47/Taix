use anyhow::{bail, Context, Result};
use std::fs;
use std::io::Write;
use std::path::Path;

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
    let mut files = Vec::new();
    collect_files(source_dir, source_dir, &mut files)?;

    println!("Packing {} files from {}", files.len(), source_dir.display());

    let mut raw_payload = Vec::new();

    raw_payload
        .write_all(&(files.len() as u32).to_le_bytes())
        .context("Failed to write file count")?;

    for (relative_path, abs_path) in &files {
        let path_bytes = relative_path.as_bytes();
        raw_payload
            .write_all(&(path_bytes.len() as u32).to_le_bytes())
            .context("Failed to write path length")?;
        raw_payload
            .write_all(path_bytes)
            .context("Failed to write path")?;

        let content = fs::read(abs_path)
            .with_context(|| format!("Failed to read file: {}", abs_path.display()))?;

        raw_payload
            .write_all(&(content.len() as u64).to_le_bytes())
            .context("Failed to write file size")?;
        raw_payload
            .write_all(&content)
            .context("Failed to write file content")?;
    }

    println!("Raw payload size: {} bytes", raw_payload.len());

    // 使用 LZMA2 压缩
    let options = xz2::stream::LzmaOptions::new_preset(9)
        .context("Failed to create LZMA options")?;
    let stream = xz2::stream::Stream::new_lzma_encoder(&options)
        .context("Failed to create LZMA encoder stream")?;

    let mut compressed = Vec::new();
    let mut encoder = xz2::write::XzEncoder::new_stream(compressed, stream);
    encoder.write_all(&raw_payload).context("Failed to compress")?;
    compressed = encoder.finish().context("Failed to finalize compression")?;

    println!(
        "Compressed payload size: {} bytes (ratio: {:.2}x)",
        compressed.len(),
        raw_payload.len() as f64 / compressed.len() as f64
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

    println!("Payload written to: {}", output_path.display());
    Ok(())
}

fn collect_files(base: &Path, current: &Path, files: &mut Vec<(String, std::path::PathBuf)>) -> Result<()> {
    for entry in fs::read_dir(current)? {
        let entry = entry?;
        let path = entry.path();

        if path.is_dir() {
            collect_files(base, &path, files)?;
        } else {
            let relative = path
                .strip_prefix(base)
                .context("Failed to compute relative path")?
                .to_str()
                .context("Invalid UTF-8 in path")?
                .replace('\\', "/");

            files.push((relative, path));
        }
    }
    Ok(())
}