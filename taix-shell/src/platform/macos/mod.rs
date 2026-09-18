pub mod scheduler;
pub mod single_instance;
pub mod tray;

pub use scheduler::{install, uninstall};
pub use single_instance::try_acquire as try_acquire_single_instance;
pub use tray::run_tray;

use cocoa::base::id;
use objc::{class, msg_send, sel, sel_impl};
use std::sync::atomic::{AtomicBool, Ordering};

use crate::config::Language;

static TERMINATION_REQUESTED: AtomicBool = AtomicBool::new(false);

extern "C" fn on_termination_signal(_signal: libc::c_int) {
    TERMINATION_REQUESTED.store(true, Ordering::SeqCst);
}

pub fn install_termination_handler() {
    let handler = on_termination_signal as extern "C" fn(libc::c_int);

    unsafe {
        libc::signal(libc::SIGTERM, handler as libc::sighandler_t);
        libc::signal(libc::SIGINT, handler as libc::sighandler_t);
    }

    tracing::info!(
        target: "taix_shell::signal",
        "SIGTERM/SIGINT handlers installed; external termination will drain children before exiting"
    );
}

/// 是否收到过外部终止请求
pub fn termination_requested() -> bool {
    TERMINATION_REQUESTED.load(Ordering::SeqCst)
}

pub fn system_language() -> Language {
    let (language, observed) = unsafe {
        let languages: id = msg_send![class!(NSLocale), preferredLanguages];
        if languages.is_null() {
            return log_system_language(Language::EnUs, "<NSLocale.preferredLanguages is nil>");
        }

        let first: id = msg_send![languages, firstObject];
        if first.is_null() {
            return log_system_language(Language::EnUs, "<preferredLanguages is empty>");
        }

        let utf8: *const std::os::raw::c_char = msg_send![first, UTF8String];
        if utf8.is_null() {
            return log_system_language(Language::EnUs, "<preferred language is not UTF-8>");
        }

        let code = std::ffi::CStr::from_ptr(utf8).to_string_lossy().into_owned();
        let language = if code.to_ascii_lowercase().starts_with("zh") {
            Language::ZhCn
        } else {
            Language::EnUs
        };
        (language, code)
    };

    log_system_language(language, &observed)
}

fn log_system_language(language: Language, observed: &str) -> Language {
    tracing::info!(
        target: "taix_shell::i18n",
        "NSLocale.preferredLanguages first={:?} → {:?}",
        observed,
        language
    );
    language
}

pub fn is_process_alive(pid: u32) -> bool {
    let alive = unsafe { libc::kill(pid as libc::pid_t, 0) } == 0;
    tracing::debug!(target: "taix_shell::process", "is_process_alive pid={} → {}", pid, alive);
    alive
}
