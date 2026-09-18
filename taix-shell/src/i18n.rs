use std::sync::OnceLock;

use crate::config::Language;

pub struct MenuTexts {
    pub show: &'static str,
    pub quit: &'static str,
}

const ZH_CN: MenuTexts = MenuTexts {
    show: "显示 Taix",
    quit: "退出",
};

const EN_US: MenuTexts = MenuTexts {
    show: "Show Taix",
    quit: "Quit",
};

pub fn menu_texts(language: Language) -> &'static MenuTexts {
    let resolved = resolve(language);
    let texts = match resolved {
        Language::EnUs => &EN_US,
        _ => &ZH_CN,
    };
    tracing::debug!(
        target: "taix_shell::i18n",
        "menu_texts requested={:?} resolved={:?} show={:?} quit={:?}",
        language, resolved, texts.show, texts.quit
    );
    texts
}


fn resolve(language: Language) -> Language {
    match language {
        Language::Auto => *SYSTEM_LANGUAGE.get_or_init(|| {
            let detected = crate::platform::system_language();
            tracing::info!(
                target: "taix_shell::i18n",
                "Language=Auto resolved from system locale → {:?}",
                detected
            );
            detected
        }),
        concrete => concrete,
    }
}

static SYSTEM_LANGUAGE: OnceLock<Language> = OnceLock::new();
