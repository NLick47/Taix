use anyhow::{bail, Context, Result};
use indicatif::{ProgressBar, ProgressStyle};
use std::fs;
use std::io::{Cursor, Read, Seek, SeekFrom, Write};
use std::path::Path;

#[cfg(has_payload)]
const PAYLOAD: &[u8] = include_bytes!(concat!(env!("OUT_DIR"), "/payload.zst"));

#[cfg(not(has_payload))]
const PAYLOAD: &[u8] = &[];

pub fn extract_to(dest: &Path) -> Result<()> {
    if PAYLOAD.is_empty() {
        bail!("No embedded payload. Build with embedded-payload feature.");
    }

    println!("Extracting files to {}...", dest.display());

    let decompressed = decompress_payload(PAYLOAD)?;
    let files = parse_and_write(&decompressed, dest)?;

    println!("Extracted {} files successfully.", files);
    Ok(())
}

fn decompress_payload(data: &[u8]) -> Result<Vec<u8>> {
    zstd::decode_all(data).context("Failed to decompress zstd payload")
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

#[allow(dead_code)]
pub fn list_payload_files() -> Result<Vec<String>> {
    if PAYLOAD.is_empty() {
        bail!("No embedded payload found.");
    }

    let decompressed = decompress_payload(PAYLOAD)?;
    let mut cursor = Cursor::new(&decompressed[..]);

    let mut count_buf = [0u8; 4];
    cursor.read_exact(&mut count_buf)?;
    let file_count = u32::from_le_bytes(count_buf) as usize;

    let mut files = Vec::with_capacity(file_count);
    for _ in 0..file_count {
        let mut path_len_buf = [0u8; 4];
        cursor.read_exact(&mut path_len_buf)?;
        let path_len = u32::from_le_bytes(path_len_buf) as usize;

        let mut path_buf = vec![0u8; path_len];
        cursor.read_exact(&mut path_buf)?;
        files.push(String::from_utf8(path_buf)?);

        let mut size_buf = [0u8; 8];
        cursor.read_exact(&mut size_buf)?;
        let file_size = u64::from_le_bytes(size_buf) as usize;

        cursor.seek(SeekFrom::Current(file_size as i64))?;
    }

    Ok(files)
}

pub fn has_payload() -> bool {
    !PAYLOAD.is_empty()
}
