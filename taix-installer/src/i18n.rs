pub struct I18n {
    lang: String,
}

impl I18n {
    pub fn new(lang: Option<&str>) -> Self {
        let lang = lang.unwrap_or("zh").to_string();
        Self { lang }
    }

    /// 安装步骤消息
    pub fn step_check_process(&self) -> &'static str {
        if self.lang == "en" {
            "Checking running processes..."
        } else {
            "检查运行中的进程..."
        }
    }

    pub fn step_extract(&self) -> &'static str {
        if self.lang == "en" {
            "Extracting files..."
        } else {
            "解压文件..."
        }
    }

    pub fn step_shortcut(&self) -> &'static str {
        if self.lang == "en" {
            "Creating shortcuts..."
        } else {
            "创建快捷方式..."
        }
    }

    pub fn step_register_startup(&self) -> &'static str {
        if self.lang == "en" {
            "Registering startup..."
        } else {
            "注册开机启动..."
        }
    }

    pub fn step_stop_process(&self) -> &'static str {
        if self.lang == "en" {
            "Stopping running processes..."
        } else {
            "停止运行中的进程..."
        }
    }

    pub fn step_backup(&self) -> &'static str {
        if self.lang == "en" {
            "Backing up current version..."
        } else {
            "备份当前版本..."
        }
    }

    pub fn step_extract_update(&self) -> &'static str {
        if self.lang == "en" {
            "Extracting update files..."
        } else {
            "解压更新文件..."
        }
    }

    pub fn step_verify(&self) -> &'static str {
        if self.lang == "en" {
            "Verifying installation..."
        } else {
            "验证安装..."
        }
    }

    pub fn step_restart(&self) -> &'static str {
        if self.lang == "en" {
            "Restarting services..."
        } else {
            "重启服务..."
        }
    }

    pub fn step_remove_shortcut(&self) -> &'static str {
        if self.lang == "en" {
            "Removing shortcuts..."
        } else {
            "删除快捷方式..."
        }
    }

    pub fn step_unregister(&self) -> &'static str {
        if self.lang == "en" {
            "Unregistering startup..."
        } else {
            "注销开机启动..."
        }
    }

    pub fn step_delete_files(&self) -> &'static str {
        if self.lang == "en" {
            "Deleting program files..."
        } else {
            "删除程序文件..."
        }
    }

    /// 错误消息
    pub fn err_invalid_installer(&self) -> &'static str {
        if self.lang == "en" {
            "Installer is invalid"
        } else {
            "安装程序无效"
        }
    }

    pub fn err_cannot_stop_process(&self, proc_name: &str) -> String {
        if self.lang == "en" {
            format!("Cannot stop process: {}\nPlease close it manually and retry", proc_name)
        } else {
            format!("无法停止进程: {}\n请手动关闭后重试", proc_name)
        }
    }

    pub fn err_missing_file(&self, file: &str) -> String {
        if self.lang == "en" {
            format!("Missing core file: {}", file)
        } else {
            format!("缺少核心文件: {}", file)
        }
    }

    pub fn err_no_install(&self) -> &'static str {
        if self.lang == "en" {
            "Taix installation not found"
        } else {
            "未找到 Taix 安装"
        }
    }

    pub fn err_install_dir_not_exist(&self) -> &'static str {
        if self.lang == "en" {
            "Taix installation directory does not exist"
        } else {
            "Taix 安装目录不存在"
        }
    }

    /// 恢复备份提示
    pub fn msg_restore_backup(&self) -> &'static str {
        if self.lang == "en" {
            "Update failed, restoring backup..."
        } else {
            "更新失败，正在恢复备份..."
        }
    }

    /// 完成消息
    pub fn complete_install(&self, dir: &str) -> String {
        if self.lang == "en" {
            format!("Installation complete!\nInstall location: {}", dir)
        } else {
            format!("安装完成!\n安装目录: {}", dir)
        }
    }

    pub fn complete_update(&self, dir: &str) -> String {
        if self.lang == "en" {
            format!("Update complete!\nInstall location: {}", dir)
        } else {
            format!("更新完成!\n安装目录: {}", dir)
        }
    }

    pub fn complete_uninstall(&self) -> &'static str {
        if self.lang == "en" {
            "Uninstall complete!\nNote: User data has been preserved"
        } else {
            "卸载完成!\n注意: 用户数据已保留"
        }
    }
}
