use cocoa::{
    appkit::{
        NSApp, NSApplication, NSApplicationActivateIgnoringOtherApps, NSMenu, NSMenuItem,
        NSRunningApplication, NSStatusBar, NSStatusItem,
    },
    base::{id, nil, YES},
    foundation::{NSPoint, NSString},
};
use objc::declare::ClassDecl;
use objc::runtime::{Class, Object, Protocol, Sel};
use objc::{class, msg_send, sel, sel_impl};
use objc_foundation::INSObject;
use objc_id::Id;

use crate::config::{Language, TrayConfig};
use crate::i18n::menu_texts;
use crate::platform::TrayCmd;
use crate::shutdown::Shutdown;

use std::ffi::c_void;

/// `NSEventTypeApplicationDefined`
const NS_EVENT_TYPE_APPLICATION_DEFINED: usize = 15;

pub(crate) struct CallbackState {
    cb: Box<dyn Fn() + Send>,
}

pub(crate) enum Callback {}
unsafe impl objc::Message for Callback {}

impl INSObject for Callback {
    fn class() -> &'static Class {
        let cname = "TaixTrayCallback";

        let mut klass = Class::get(cname);
        if klass.is_none() {
            let superclass = class!(NSObject);
            let mut decl = ClassDecl::new(cname, superclass).unwrap();
            decl.add_ivar::<usize>("_cbptr");

            extern "C" fn taix_callback_call(this: &Object, _cmd: Sel) {
                unsafe {
                    let pval: usize = *this.get_ivar("_cbptr");
                    let ptr = pval as *mut c_void;
                    let ptr = ptr as *mut CallbackState;
                    let bcbs: Box<CallbackState> = Box::from_raw(ptr);
                    {
                        (*bcbs.cb)();
                    }
                    std::mem::forget(bcbs);
                }
            }

            unsafe {
                decl.add_method(
                    sel!(call),
                    taix_callback_call as extern "C" fn(&Object, Sel),
                );
            }

            decl.register();
            klass = Class::get(cname);
        }
        klass.unwrap()
    }
}

impl Callback {
    pub(crate) fn from(cb: Box<dyn Fn() + Send>) -> Id<Self> {
        let cbs = CallbackState { cb };
        let bcbs = Box::new(cbs);

        let ptr = Box::into_raw(bcbs);
        let ptr = ptr as *mut c_void as usize;
        let mut oid = <Callback as INSObject>::new();
        (*oid).setptr(ptr);
        oid
    }

    pub(crate) fn setptr(&mut self, uptr: usize) {
        unsafe {
            let obj = &mut *(self as *mut _ as *mut ::objc::runtime::Object);
            obj.set_ivar("_cbptr", uptr);
        }
    }
}

pub(crate) enum MenuAppearanceDelegate {}
unsafe impl objc::Message for MenuAppearanceDelegate {}

impl INSObject for MenuAppearanceDelegate {
    fn class() -> &'static Class {
        let cname = "TaixMenuAppearanceDelegate";

        let mut klass = Class::get(cname);
        if klass.is_none() {
            let superclass = class!(NSObject);
            let mut decl = ClassDecl::new(cname, superclass).unwrap();

            extern "C" fn menu_will_open(_this: &Object, _cmd: Sel, menu: id) {
                unsafe {
                    let app = NSApp();
                    let appearance: id = msg_send![app, effectiveAppearance];
                    let _: () = msg_send![menu, setAppearance: appearance];
                    tracing::debug!(
                        target: "taix_shell::tray",
                        "menuWillOpen: menu appearance pinned to NSApp.effectiveAppearance"
                    );
                }
            }

            unsafe {
                decl.add_method(
                    sel!(menuWillOpen:),
                    menu_will_open as extern "C" fn(&Object, Sel, id),
                );
            }

            if let Some(protocol) = Protocol::get("NSMenuDelegate") {
                decl.add_protocol(protocol);
            }

            decl.register();
            klass = Class::get(cname);
        }
        klass.unwrap()
    }
}

const SHUTDOWN_POLL_SECS: f64 = 0.25;

struct WatcherState {
    shutdown: Shutdown,
    reported_termination: std::sync::atomic::AtomicBool,
    reported_stop: std::sync::atomic::AtomicBool,
}

pub(crate) enum ShutdownWatcher {}
unsafe impl objc::Message for ShutdownWatcher {}

impl INSObject for ShutdownWatcher {
    fn class() -> &'static Class {
        let cname = "TaixShutdownWatcher";

        let mut klass = Class::get(cname);
        if klass.is_none() {
            let superclass = class!(NSObject);
            let mut decl = ClassDecl::new(cname, superclass).unwrap();
            decl.add_ivar::<usize>("_stateptr");

            extern "C" fn tick(this: &Object, _cmd: Sel, _timer: id) {
                unsafe {
                    let ptr: usize = *this.get_ivar("_stateptr");
                    let state = &*(ptr as *const WatcherState);

                    if super::termination_requested() {
                        if !state
                            .reported_termination
                            .swap(true, std::sync::atomic::Ordering::SeqCst)
                        {
                            tracing::info!(
                                target: "taix_shell::signal",
                                "external termination (SIGTERM/SIGINT) observed on the main run loop; translating into a shutdown request"
                            );
                        }
                        state.shutdown.request();
                    }

                    if state.shutdown.is_requested() {
                        if !state
                            .reported_stop
                            .swap(true, std::sync::atomic::Ordering::SeqCst)
                        {
                            tracing::info!(
                                target: "taix_shell::tray",
                                "shutdown requested; stopping the NSApp run loop"
                            );
                        }
                        stop_run_loop();
                    }
                }
            }

            unsafe {
                decl.add_method(sel!(tick:), tick as extern "C" fn(&Object, Sel, id));
            }

            decl.register();
            klass = Class::get(cname);
        }
        klass.unwrap()
    }
}

impl ShutdownWatcher {
    fn new(shutdown: Shutdown) -> Id<Self> {
        let state = WatcherState {
            shutdown,
            reported_termination: std::sync::atomic::AtomicBool::new(false),
            reported_stop: std::sync::atomic::AtomicBool::new(false),
        };
        let ptr = Box::into_raw(Box::new(state)) as usize;
        let mut oid = <ShutdownWatcher as INSObject>::new();
        (*oid).set_stateptr(ptr);
        oid
    }

    fn set_stateptr(&mut self, uptr: usize) {
        unsafe {
            let obj = &mut *(self as *mut _ as *mut ::objc::runtime::Object);
            obj.set_ivar("_stateptr", uptr);
        }
    }
}

unsafe fn schedule_shutdown_watcher(shutdown: Shutdown) {
    let watcher = ShutdownWatcher::new(shutdown);
    let _: id = msg_send![
        class!(NSTimer),
        scheduledTimerWithTimeInterval: SHUTDOWN_POLL_SECS
        target: watcher
        selector: sel!(tick:)
        userInfo: nil
        repeats: YES
    ];
    tracing::debug!(
        target: "taix_shell::tray",
        "shutdown watcher scheduled on the main run loop every {}s",
        SHUTDOWN_POLL_SECS
    );
}

unsafe fn stop_run_loop() {
    let app = NSApp();
    let _: () = msg_send![app, stop: nil];

    let event: id = msg_send![
        class!(NSEvent),
        otherEventWithType: NS_EVENT_TYPE_APPLICATION_DEFINED
        location: NSPoint { x: 0.0, y: 0.0 }
        modifierFlags: 0usize
        timestamp: 0.0f64
        windowNumber: 0isize
        context: nil
        subtype: 0isize
        data1: 0isize
        data2: 0isize
    ];
    if event.is_null() {
        tracing::warn!(
            target: "taix_shell::tray",
            "could not synthesise a wake-up event; NSApp.run() may stay blocked"
        );
        return;
    }
    let _: () = msg_send![app, postEvent: event atStart: YES];
    tracing::debug!(
        target: "taix_shell::tray",
        "NSApp stop: sent and a wake-up event posted"
    );
}

/// 创建状态栏图标与右键菜单，并把菜单动作接到 `cmd_tx` / `shutdown` 上。
///
/// status item、菜单、以及 `Callback` 对象都由 AppKit 或进程生命周期持有，
unsafe fn install_status_item(
    cmd_tx: std::sync::mpsc::SyncSender<TrayCmd>,
    language: Language,
    shutdown: Shutdown,
) {
    let status_bar = NSStatusBar::systemStatusBar(nil);
    let status_item: id = msg_send![status_bar, statusItemWithLength: -1.0];

    let title = NSString::alloc(nil).init_str("Taix");
    let button: id = msg_send![status_item, button];
    let _: () = msg_send![button, setTitle: title];

    let menu = NSMenu::new(nil);
    let _: () = msg_send![menu, setAutoenablesItems: false];

    let menu_delegate = <MenuAppearanceDelegate as INSObject>::new();
    let _: () = msg_send![menu, setDelegate: menu_delegate];

    let texts = menu_texts(language);
    let no_key = NSString::alloc(nil).init_str("");
    tracing::info!(
        target: "taix_shell::tray",
        "status item installed language={:?} items=[{:?}, {:?}]",
        language, texts.show, texts.quit
    );

    let show_cb = Callback::from(Box::new(move || {
        tracing::info!(target: "taix_shell::tray", "menu item selected: Show");
        let _ = cmd_tx.send(TrayCmd::LaunchClient);
        tracing::debug!(target: "taix_shell::tray", "LaunchClient handed to the action thread");
    }));
    let show_title = NSString::alloc(nil).init_str(texts.show);
    let show_item = NSMenuItem::alloc(nil)
        .initWithTitle_action_keyEquivalent_(show_title, sel!(call), no_key);
    let _: () = msg_send![show_item, setTarget: show_cb];
    let _: () = msg_send![menu, addItem: show_item];

    let separator: id = msg_send![class!(NSMenuItem), separatorItem];
    let _: () = msg_send![menu, addItem: separator];

    let quit_cb = Callback::from(Box::new(move || {
        tracing::info!(target: "taix_shell::tray", "exit requested from the tray menu");
        shutdown.request();
    }));
    let quit_title = NSString::alloc(nil).init_str(texts.quit);
    let quit_item = NSMenuItem::alloc(nil)
        .initWithTitle_action_keyEquivalent_(quit_title, sel!(call), no_key);
    let _: () = msg_send![quit_item, setTarget: quit_cb];
    let _: () = msg_send![menu, addItem: quit_item];

    status_item.setMenu_(menu);
}

pub fn run_tray(
    cmd_tx: std::sync::mpsc::SyncSender<TrayCmd>,
    config: TrayConfig,
    shutdown: Shutdown,
) -> anyhow::Result<()> {
    tracing::info!(
        target: "taix_shell::tray",
        "run_tray start language={:?} is_visible={} menu_appearance=follow-system",
        config.language, config.is_visible
    );

    // 接管 SIGTERM / SIGINT：让 shell 被外部要求终止时也有机会先收掉子进程
    super::install_termination_handler();

    unsafe {
        let app = NSApp();
        schedule_shutdown_watcher(shutdown.clone());

        if config.is_visible {
            app.activateIgnoringOtherApps_(YES);
            install_status_item(cmd_tx, config.language, shutdown);

            let current_app = NSRunningApplication::currentApplication(nil);
            current_app.activateWithOptions_(NSApplicationActivateIgnoringOtherApps);
            tracing::debug!(target: "taix_shell::tray", "app activated; status item is visible");
        } else {
            tracing::info!(
                target: "taix_shell::tray",
                "IsEnableTray=false: no status item created, the process keeps supervising server/monitor; only SIGTERM/SIGINT can stop it"
            );
        }

        tracing::info!(target: "taix_shell::tray", "entering NSApp.run()");
        app.run();
    }

    tracing::info!(target: "taix_shell::tray", "NSApp.run() returned; run_tray returning");
    Ok(())
}
