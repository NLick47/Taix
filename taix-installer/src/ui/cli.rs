use std::io::{self, Write};
use std::path::PathBuf;
use console::{pad_str, Alignment, Term};

/// 显示选择菜单，返回选中项索引
pub fn select_action() -> io::Result<usize> {
    let term = Term::stdout();
    let options = ["更新", "重新安装", "卸载", "取消"];
    let mut selected = 0;

    let _ = term.hide_cursor();

    println!("\n  请选择要执行的操作：\n");

    print_options(&options, selected);
    println!();
    print_help(options.len());

    io::stdout().flush()?;

    loop {
        let key = term.read_key()?;

        match key {
            console::Key::ArrowUp => {
                if selected > 0 {
                    selected -= 1;
                    refresh_menu(&term, &options, selected)?;
                }
            }
            console::Key::ArrowDown => {
                if selected < options.len() - 1 {
                    selected += 1;
                    refresh_menu(&term, &options, selected)?;
                }
            }
            console::Key::Enter => {
                let _ = term.show_cursor();
                return Ok(selected);
            }
            console::Key::Escape => {
                let _ = term.show_cursor();
                return Ok(3); // 取消
            }
            console::Key::Char(c) => {
                if let Some(digit) = c.to_digit(10) {
                    let idx = digit as usize;
                    if idx >= 1 && idx <= options.len() {
                        let _ = term.show_cursor();
                        return Ok(idx - 1);
                    }
                }
            }
            _ => {}
        }
    }
}

fn print_options(options: &[&str], selected: usize) {
    for (i, opt) in options.iter().enumerate() {
        let num = i + 1;
        if i == selected {
            println!("    ▶ [{}] {}  ", num, opt);
        } else {
            println!("      [{}] {}  ", num, opt);
        }
    }
}

fn print_help(option_count: usize) {
    println!("  提示: 按 ↑↓ 选择, 或按数字键 1-{} 直接选择, Enter 确认", option_count);
}

fn refresh_menu(term: &Term, options: &[&str], selected: usize) -> io::Result<()> {
    let lines_to_clear = options.len() + 2;
    term.clear_last_lines(lines_to_clear)?;
    print_options(options, selected);
    println!();
    print_help(options.len());
    io::stdout().flush()?;
    Ok(())
}

pub fn prompt_install_dir(default: &std::path::Path) -> io::Result<PathBuf> {
    let term = Term::stdout();
    let options = ["确认，开始安装", "自定义安装目录"];
    let mut selected = 0;

    let _ = term.hide_cursor();

    println!("\n  默认安装目录:");
    println!("    {}", default.display());
    println!("\n  请选择：\n");

    print_dir_options(&options, selected);
    println!();
    print_dir_help(options.len());

    io::stdout().flush()?;

    loop {
        let key = term.read_key()?;

        match key {
            console::Key::ArrowUp => {
                if selected > 0 {
                    selected -= 1;
                    refresh_dir_menu(&term, &options, selected)?;
                }
            }
            console::Key::ArrowDown => {
                if selected < options.len() - 1 {
                    selected += 1;
                    refresh_dir_menu(&term, &options, selected)?;
                }
            }
            console::Key::Enter => {
                let _ = term.show_cursor();
                if selected == 0 {
                    return Ok(default.to_path_buf());
                }
                // 自定义安装目录
                if let Some(path) = browse_folder() {
                    return Ok(path);
                }
                // 用户取消了对话框，回到菜单重新选择
                println!("  [未选择目录，回到菜单]");
                std::thread::sleep(std::time::Duration::from_millis(800));
                let _ = term.hide_cursor();
                refresh_dir_menu(&term, &options, selected)?;
                continue;
            }
            console::Key::Escape => {
                let _ = term.show_cursor();
                return Ok(default.to_path_buf());
            }
            console::Key::Char(c) => {
                if let Some(digit) = c.to_digit(10) {
                    let idx = digit as usize;
                    if idx >= 1 && idx <= options.len() {
                        let _ = term.show_cursor();
                        if idx == 1 {
                            return Ok(default.to_path_buf());
                        }
                        if let Some(path) = browse_folder() {
                            return Ok(path);
                        }
                        // 用户取消了对话框，回到菜单重新选择
                        println!("  [未选择目录，回到菜单]");
                        std::thread::sleep(std::time::Duration::from_millis(800));
                        let _ = term.hide_cursor();
                        refresh_dir_menu(&term, &options, selected)?;
                        continue;
                    }
                }
            }
            _ => {}
        }
    }
}

fn print_dir_options(options: &[&str], selected: usize) {
    for (i, opt) in options.iter().enumerate() {
        let num = i + 1;
        if i == selected {
            println!("    ▶ [{}] {}  ", num, opt);
        } else {
            println!("      [{}] {}  ", num, opt);
        }
    }
}

fn print_dir_help(option_count: usize) {
    println!("  提示: 按 ↑↓ 选择, 或按数字键 1-{} 直接选择, Enter 确认", option_count);
}

fn refresh_dir_menu(term: &Term, options: &[&str], selected: usize) -> io::Result<()> {
    let lines_to_clear = options.len() + 2;
    term.clear_last_lines(lines_to_clear)?;
    print_dir_options(options, selected);
    println!();
    print_dir_help(options.len());
    io::stdout().flush()?;
    Ok(())
}

fn browse_folder() -> Option<PathBuf> {
    let script = r#"
Add-Type -AssemblyName System.Windows.Forms
$dialog = New-Object System.Windows.Forms.FolderBrowserDialog
$dialog.Description = "选择 Taix 安装目录"
$dialog.ShowNewFolderButton = $true
if ($dialog.ShowDialog() -eq 'OK') { $dialog.SelectedPath } else { '' }
"#;

    let output = std::process::Command::new("powershell")
        .args(["-NoProfile", "-NonInteractive", "-Command", script])
        .output()
        .ok()?;

    if output.status.success() {
        let path = String::from_utf8_lossy(&output.stdout).trim().to_string();
        if !path.is_empty() {
            return Some(std::path::PathBuf::from(path));
        }
    }
    None
}

pub fn show_step(step: usize, total: usize, message: &str) {
    println!("\n  [{}/{}] {}", step, total, message);
}

pub fn show_extracting() {
    println!("  正在解压，请稍候...");
}

pub fn show_complete(message: &str) {
    let content_width = 32;
    let padded = pad_str(message, content_width, Alignment::Center, None);
    println!();
    println!("  ╔════════════════════════════════════╗");
    println!("  ║  {}  ║", padded);
    println!("  ╚════════════════════════════════════╝");
}

pub fn wait_exit() {
    println!("\n  按 Enter 键退出...");
    let _ = io::stdin().read_line(&mut String::new());
}
