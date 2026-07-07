use std::fs::{self, OpenOptions};
use std::io::{BufWriter, Write};
use std::path::PathBuf;
use std::sync::{Mutex, OnceLock};
use std::time::{SystemTime, UNIX_EPOCH};


// ERROR (0) < WARN (1) < INFO (2) < DEBUG (3) < TRACE (4)

#[derive(Clone, Copy, PartialEq, Eq, PartialOrd, Ord)]
struct Lvl(u8);

const E: Lvl = Lvl(0); const W: Lvl = Lvl(1);
const I: Lvl = Lvl(2); const D: Lvl = Lvl(3); const T: Lvl = Lvl(4);

impl Lvl {
    fn parse(s: &str) -> Option<Self> {
        Some(match s {
            "error"|"Error"|"ERROR" => E, "warn"|"Warn"|"WARN" => W,
            "info"|"Info"|"INFO" => I, "debug"|"Debug"|"DEBUG" => D,
            "trace"|"Trace"|"TRACE" => T, _ => return None,
        })
    }
    fn from_tracing(l: &tracing::Level) -> Self {
        match *l {
            tracing::Level::ERROR => E,
            tracing::Level::WARN => W,
            tracing::Level::INFO => I,
            tracing::Level::DEBUG => D,
            _ => T,
        }
    }
    fn as_str(self) -> &'static str {
        match self.0 { 0 => "ERROR", 1 => "WARN", 2 => "INFO", 3 => "DEBUG", _ => "TRACE" }
    }
}


struct Rule { target: Option<String>, level: Lvl }

fn parse_rules(s: &str) -> Vec<Rule> {
    s.split(',').map(|s| s.trim()).filter(|s| !s.is_empty())
        .filter_map(|s| s.find('=').map_or(
            Lvl::parse(s).map(|l| Rule { target: None, level: l }),
            |p| Lvl::parse(s[p+1..].trim()).map(|l|
                Rule { target: Some(s[..p].trim().to_owned()), level: l }
            ),
        ))
        .collect()
}

fn match_lvl(rules: &[Rule], target: &str) -> Option<Lvl> {
    let mut best = None;
    for r in rules {
        if let Some(t) = &r.target {
            if target.starts_with(t) {
                match best {
                    None => best = Some((r.level, t.len())),
                    Some((_, bl)) if t.len() > bl => best = Some((r.level, t.len())),
                    _ => {}
                }
            }
        }
    }
    best.map(|(l, _)| l)
        .or_else(|| rules.iter().find(|r| r.target.is_none()).map(|r| r.level))
}


fn now() -> (u64, u32) {
    SystemTime::now().duration_since(UNIX_EPOCH)
        .map(|d| (d.as_secs(), d.subsec_millis()))
        .unwrap_or((0, 0))
}

/// Howard Hinnant 返回 (y, m, d)
fn ymd(secs: u64) -> (u64, u64, u64) {
    let z = (secs / 86400) as i64 + 719468;
    let era = (if z >= 0 { z } else { z - 146096 }) / 146097;
    let doe = z - era * 146097;
    let yoe = (doe - doe / 1460 + doe / 36524 - doe / 146096) / 365;
    let y = yoe + era * 400;
    let doy = doe - (365 * yoe + yoe / 4 - yoe / 100);
    let mp = (5 * doy + 2) / 153;
    let d = doy - (153 * mp + 2) / 5 + 1;
    let m = if mp < 10 { mp + 3 } else { mp - 9 };
    let y = if m <= 2 { y + 1 } else { y };
    (y as u64, m as u64, d as u64)
}


fn write_ts(w: &mut Vec<u8>, secs: u64, ms: u32) {
    let (y, m, d) = ymd(secs);
    let sec = secs % 86400;
    let h = sec / 3600; let mi = (sec % 3600) / 60; let s = sec % 60;
    let _ = write!(w, "{y:04}-{m:02}-{d:02}T{h:02}:{mi:02}:{s:02}.{ms:03}Z");
}


fn date_str(secs: u64) -> String {
    let (y, m, d) = ymd(secs);
    format!("{y:04}-{m:02}-{d:02}")
}

struct Visitor<'a>(&'a mut Vec<u8>);

impl<'a> tracing::field::Visit for Visitor<'a> {
    fn record_debug(&mut self, f: &tracing::field::Field, v: &dyn std::fmt::Debug) {
        if f.name() == "message" { let _ = write!(self.0, "{v:?}"); }
        else { let _ = write!(self.0, " {}={:?}", f.name(), v); }
    }
    fn record_str(&mut self, f: &tracing::field::Field, v: &str) {
        if f.name() == "message" { let _ = write!(self.0, "{v}"); }
        else { let _ = write!(self.0, " {}={}", f.name(), v); }
    }
    fn record_i64(&mut self, f: &tracing::field::Field, v: i64) {
        let _ = write!(self.0, " {}={}", f.name(), v);
    }
    fn record_u64(&mut self, f: &tracing::field::Field, v: u64) {
        let _ = write!(self.0, " {}={}", f.name(), v);
    }
    fn record_bool(&mut self, f: &tracing::field::Field, v: bool) {
        let _ = write!(self.0, " {}={}", f.name(), v);
    }
}

struct Writer {
    file: BufWriter<std::fs::File>,
    log_dir: PathBuf,
    prefix: String,
    day: u64,
    sync: bool,
    stdout: bool,
    max_log_files: u32,
}

impl Writer {
    fn new(name: &str, dir: &std::path::Path, sync: bool, stdout: bool, max_log_files: u32) -> Self {
        let (s, _) = now(); let day = s / 86400;
        let ds = date_str(s);
        let path = dir.join(format!("{name}.{ds}.log"));
        let file = OpenOptions::new().create(true).append(true)
            .open(&path).unwrap_or_else(|_| panic!("open {path:?}"));
        let w = Writer {
            file: BufWriter::new(file), log_dir: dir.to_owned(),
            prefix: name.to_owned(), day, sync, stdout, max_log_files,
        };
        if max_log_files > 0 { w.cleanup(); }
        w
    }
    fn rotate(&mut self, day_now: u64, ds: &str) {
        if day_now != self.day {
            self.day = day_now;
            let path = self.log_dir.join(format!("{}.{}.log", self.prefix, ds));
            if let Ok(f) = OpenOptions::new().create(true).append(true).open(&path) {
                self.file = BufWriter::new(f);
            }
            if self.max_log_files > 0 { self.cleanup(); }
        }
    }
    fn cleanup(&self) {
        let max = self.max_log_files as usize;
        let prefix = format!("{}.", self.prefix);
        let suffix = ".log";
        let mut files: Vec<(u64, PathBuf)> = Vec::new();
        if let Ok(entries) = fs::read_dir(&self.log_dir) {
            for e in entries.flatten() {
                let name = e.file_name();
                let name = name.to_string_lossy();
                if name.starts_with(&prefix) && name.ends_with(suffix) {
                    let date_part = &name[prefix.len()..name.len() - suffix.len()];
                    // YYYY-MM-DD → 10 chars
                    if date_part.len() == 10 {
                        if let (Ok(y), Ok(mo), Ok(d)) = (
                            date_part[..4].parse::<u64>(),
                            date_part[5..7].parse::<u64>(),
                            date_part[8..10].parse::<u64>(),
                        ) {
                            if mo >= 1 && mo <= 12 && d >= 1 && d <= 31 {
                                files.push((y * 10000 + mo * 100 + d, e.path()));
                            }
                        }
                    }
                }
            }
        }
        if files.len() <= max { return; }
        files.sort_by(|a, b| b.0.cmp(&a.0)); // newest first
        for (_, path) in &files[max..] {
            let _ = fs::remove_file(path);
        }
    }
}


struct Buf(Vec<u8>);

impl Buf {
    /// Vec<u8> is reused across events to avoid repeated allocation
    fn with<R>(f: impl FnOnce(&mut Vec<u8>) -> R) -> R {
        std::thread_local! {
            static B: std::cell::RefCell<Buf> = const { std::cell::RefCell::new(Buf(Vec::new())) };
            static INIT: std::cell::Cell<bool> = const { std::cell::Cell::new(false) };
        }
        INIT.with(|init| {
            if !init.replace(true) {
                B.with(|b| b.borrow_mut().0.reserve(256));
            }
        });
        B.with(|b| {
            let mut b = b.borrow_mut();
            b.0.clear();
            f(&mut b.0)
        })
    }
}


struct Sub { rules: Vec<Rule> }

impl tracing::Subscriber for Sub {
    fn enabled(&self, meta: &tracing::Metadata<'_>) -> bool {
        let el = Lvl::from_tracing(meta.level());
        match_lvl(&self.rules, meta.target()).map_or(false, |rl| el <= rl)
    }
    fn new_span(&self, _: &tracing::span::Attributes<'_>) -> tracing::Id { tracing::Id::from_u64(0) }
    fn record(&self, _: &tracing::Id, _: &tracing::span::Record<'_>) {}
    fn record_follows_from(&self, _: &tracing::Id, _: &tracing::Id) {}
    fn enter(&self, _: &tracing::Id) {}
    fn exit(&self, _: &tracing::Id) {}
    fn clone_span(&self, _: &tracing::Id) -> tracing::Id { tracing::Id::from_u64(0) }
    fn drop_span(&self, _: tracing::Id) {}
    fn register_callsite(&self, _: &'static tracing::Metadata<'static>) -> tracing::subscriber::Interest {
        tracing::subscriber::Interest::sometimes()
    }
    fn event(&self, ev: &tracing::Event<'_>) {
        let mut guard = match LOGGER.get().and_then(|l| l.lock().ok()) { Some(g) => g, None => return };
        let w = &mut *guard;
        let meta = ev.metadata();
        let (s, ms) = now();
        let day_now = s / 86400;
        let ds = date_str(s);
        w.rotate(day_now, &ds);
        Buf::with(|buf| {
            write_ts(buf, s, ms);
            let _ = write!(buf, " {} {}: {}:{}",
                           Lvl::from_tracing(meta.level()).as_str(),
                           meta.target(), meta.file().unwrap_or("?"), meta.line().unwrap_or(0));
            { let mut v = Visitor(buf); ev.record(&mut v); }
            buf.push(b'\n');
            let _ = w.file.write_all(buf);
            if w.sync { let _ = w.file.flush(); }
            if w.stdout {
                let _ = std::io::stdout().lock().write_all(buf);
            }
        });
    }
}


static LOGGER: OnceLock<Mutex<Writer>> = OnceLock::new();


pub enum PanicMode { LogOnly, SyncFile }

pub struct LoggingGuard;

impl Drop for LoggingGuard {
    fn drop(&mut self) {
        if let Some(l) = LOGGER.get() {
            if let Ok(mut g) = l.lock() { let _ = g.file.flush(); }
        }
    }
}

pub fn init(name: &str, default_filter: &str, panic_mode: PanicMode, max_log_files: u32) -> LoggingGuard {
    let filter = std::env::var("RUST_LOG").unwrap_or_else(|_| default_filter.to_owned());
    let rules = parse_rules(&filter);

    let exe = std::env::current_exe()
        .ok().and_then(|p| p.parent().map(|p| p.to_path_buf()))
        .unwrap_or_else(|| PathBuf::from("."));
    let log_dir = exe.join("Logs");
    let _ = fs::create_dir_all(&log_dir);

    let sync = matches!(panic_mode, PanicMode::SyncFile);
    let stdout = cfg!(debug_assertions) || sync;
    let _ = LOGGER.set(Mutex::new(Writer::new(name, &log_dir, sync, stdout, max_log_files)));
    let _ = tracing::subscriber::set_global_default(Sub { rules });

    install_panic_hook(name, &log_dir, panic_mode);
    LoggingGuard
}

fn install_panic_hook(name: &str, log_dir: &std::path::Path, mode: PanicMode) {
    let n = name.to_owned(); let d = log_dir.to_path_buf();
    match mode {
        PanicMode::LogOnly => {
            let prev = std::panic::take_hook();
            std::panic::set_hook(Box::new(move |info| {
                tracing::error!(target: "panic", "{info}");
                prev(info);
            }));
        }
        PanicMode::SyncFile => {
            std::panic::set_hook(Box::new(move |info| {
                let payload = info.payload().downcast_ref::<&str>()
                    .map(|s| s.to_string())
                    .or_else(|| info.payload().downcast_ref::<String>().cloned())
                    .unwrap_or_else(|| "Unknown panic payload".to_string());
                let loc = info.location()
                    .map(|l| format!("{}:{}:{}", l.file(), l.line(), l.column()))
                    .unwrap_or_else(|| "unknown location".to_string());
                let (s, ms) = now();
                let ds = date_str(s);
                let buf = Buf::with(|buf| {
                    write_ts(buf, s, ms);
                    let _ = write!(buf, " ERROR {n}: PANIC at {loc}\n    payload: {payload}\n");
                    buf.clone()
                });
                let path = d.join(format!("{n}.{ds}.log"));
                if let Ok(mut f) = OpenOptions::new().create(true).append(true).open(&path) {
                    let _ = f.write_all(&buf); let _ = f.flush();
                }
                let _ = std::io::stderr().write_all(&buf);
            }));
        }
    }
}


