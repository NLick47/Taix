use anyhow::{bail, Result};
use std::fs;
use std::io::{Read, Seek, SeekFrom};
use std::path::Path;

const PAYLOAD_MARKER: &[u8] = b"TAIX_PAYLOAD_END";

pub fn extract_to(dest: &Path) -> Result<()> {
    let payload = read_payload_from_exe()?;
    if payload.is_empty() {
        bail!("安装程序无效");
    }

    fs::create_dir_all(dest)?;

    let temp_7z = dest.join(".temp_payload.7z");
    fs::write(&temp_7z, &payload)?;

    let result = sevenz_rust2::decompress_file(&temp_7z, dest);

    let _ = fs::remove_file(&temp_7z);

    result?;

    Ok(())
}

fn read_payload_from_exe() -> Result<Vec<u8>> {
    let exe_path = std::env::current_exe()?;
    let mut file = fs::File::open(&exe_path)?;

    let file_size = file.seek(SeekFrom::End(0))? as usize;
    let metadata_size = PAYLOAD_MARKER.len() + 8;

    if file_size < metadata_size {
        bail!("安装程序无效");
    }

    let marker_offset = file_size - metadata_size;
    file.seek(SeekFrom::Start(marker_offset as u64))?;

    let mut marker_buf = [0u8; 16];
    file.read_exact(&mut marker_buf)?;

    if marker_buf != PAYLOAD_MARKER {
        bail!("安装程序无效");
    }

    let mut size_buf = [0u8; 8];
    file.read_exact(&mut size_buf)?;
    let payload_size = u64::from_le_bytes(size_buf) as usize;

    let payload_start = marker_offset - payload_size;
    file.seek(SeekFrom::Start(payload_start as u64))?;

    let mut payload = vec![0u8; payload_size];
    file.read_exact(&mut payload)?;

    Ok(payload)
}

pub fn has_payload() -> bool {
    read_payload_from_exe().is_ok()
}
