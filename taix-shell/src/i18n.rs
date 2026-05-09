use crate::config::Language;

pub struct TrayTexts {
    pub tooltip: &'static str,
}

pub fn tray_texts(lang: Language) -> TrayTexts {
    match lang {
        Language::ZhCn => TrayTexts {
            tooltip: "Taix",
        },
        _ => TrayTexts {
            tooltip: "Taix",
        },
    }
}
