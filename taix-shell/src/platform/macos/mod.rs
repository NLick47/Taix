pub mod scheduler;
pub mod single_instance;
pub mod tray;

pub use scheduler::{install, uninstall};
pub use single_instance::try_acquire as try_acquire_single_instance;
pub use tray::run_tray;

use crate::config::Theme;

pub fn apply_menu_theme(_theme: Theme) {
}

pub fn is_process_alive(pid: u32) -> bool {
    use std::process::Command;

    let output = Command::new("kill")
        .args(["-0", &pid.to_string()])
        .output();

    match output {
        Ok(output) => output.status.success(),
        Err(_) => false,
    }
}
