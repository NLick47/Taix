use anyhow::{bail, Context, Result};
use indicatif::{ProgressBar, ProgressStyle};
use std::fs;
use std::io::{Cursor, Read, Seek, SeekFrom, Write};
use std::path::Path;

const PAYLOAD_MARKER: &[u8] = b"TAIX_PAYLOAD_END";

pub fn extract_to(dest: &Path) -> Result<()> {
    let payload = read_payload_from_exe()?;
    if payload.is_empty() {
        bail!("No payload found in this installer.");
    }

    println!("Extracting files to {}...", dest.display());

    // 解压 LZMA payload
    let mut decompressed = Vec::new();
    xz2::read::XzDecoder::new(&payload[..])
        .read_to_end(&mut decompressed)
        .context("Failed to decompress payload")?;

    let files = parse_and_write(&decompressed, dest)?;

    println!("Extracted {} files successfully.", files);
    Ok(())
}

fn read_payload_from_exe() -> Result<Vec<u8>> {
    // 获取当前 exe 路径
    let exe_path = std::env::current_exe()
        .context("Failed to get current exe path")?;

    let mut file = fs::File::open(&exe_path)
        .with_context(|| format!("Failed to open exe: {}", exe_path.display()))?;

    // 从文件末尾查找 marker
    let file_size = file.seek(SeekFrom::End(0))? as usize;

    // marker 格式: [PAYLOAD_MARKER (16字节)] + [payload_size (8字节 u64)]
    let metadata_size = PAYLOAD_MARKER.len() + 8;

    if file_size < metadata_size {
        bail!("Installer too small, no payload found");
    }

    let marker_offset = file_size - metadata_size;

    file.seek(SeekFrom::Start(marker_offset as u64))?;

    // 读取 marker
    let mut marker_buf = [0u8; 16];
    file.read_exact(&mut marker_buf)?;

    if marker_buf != PAYLOAD_MARKER {
        bail!("Payload marker not found, this installer has no payload");
    }

    // 读取 payload 大小
    let mut size_buf = [0u8; 8];
    file.read_exact(&mut size_buf)?;
    let payload_size = u64::from_le_bytes(size_buf) as usize;

    // 读取 payload
    let payload_start = marker_offset - payload_size;
    file.seek(SeekFrom::Start(payload_start as u64))?;

    let mut payload = vec![0u8; payload_size];
    file.read_exact(&mut payload)?;

    Ok(payload)
}

fn parse_and_write(data: &[u8], dest: &Path) -> Result<usize> {
    let mut cursor = Cursor::new(data);

    let mut count_buf = [0u8; 4];
    cursor.read_exact(&mut count_buf)?;
    let file_count = u32::from_le_bytes(count_buf) as usize;

    let pb = ProgressBar::new(file_count as u64);
    pb.set_style(
        ProgressStyle::with_template("[{elapsed_precise}] {bar:50.cyan/blue} {pos}/{len} ({eta})")
            .unwrap()
            .progress_chars("█▓░"),
    );

    for _ in 0..file_count {
        let mut path_len_buf = [0u8; 4];
        cursor.read_exact(&mut path_len_buf)?;
        let path_len = u32::from_le_bytes(path_len_buf) as usize;

        let mut path_buf = vec![0u8; path_len];
        cursor.read_exact(&mut path_buf)?;
        let relative_path = String::from_utf8(path_buf).context("Invalid UTF-8 in file path")?;

        let mut size_buf = [0u8; 8];
        cursor.read_exact(&mut size_buf)?;
        let file_size = u64::from_le_bytes(size_buf) as usize;

        let mut content = vec![0u8; file_size];
        cursor.read_exact(&mut content)?;

        let target_path = dest.join(&relative_path);
        if let Some(parent) = target_path.parent() {
            fs::create_dir_all(parent)
                .with_context(|| format!("Failed to create directory: {}", parent.display()))?;
        }

        let mut file = fs::File::create(&target_path)
            .with_context(|| format!("Failed to create file: {}", target_path.display()))?;
        file.write_all(&content)
            .with_context(|| format!("Failed to write file: {}", target_path.display()))?;

        pb.inc(1);
    }

    pb.finish_with_message("done");
    Ok(file_count)
}

pub fn has_payload() -> bool {
    read_payload_from_exe().is_ok()
}