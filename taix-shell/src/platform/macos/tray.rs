use std::sync::atomic::AtomicBool;
use std::sync::Arc;

use cocoa::{
    appkit::{
        NSApp, NSApplication, NSApplicationActivateIgnoringOtherApps, NSMenu,
        NSMenuItem, NSRunningApplication, NSStatusBar, NSStatusItem,
    },
    base::{id, nil, YES},
    foundation::NSString,
};
use objc::{class, msg_send, sel, sel_impl};
use objc::runtime::{Class, Object, Sel};
use objc::declare::ClassDecl;
use objc_foundation::INSObject;
use objc_id::Id;

use crate::config::TrayConfig;
use crate::platform::TrayCmd;

use std::ffi::c_void;

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

pub fn run_tray(
    cmd_tx: std::sync::mpsc::SyncSender<TrayCmd>,
    initial_config: TrayConfig,
    shutdown: Arc<AtomicBool>,
) -> anyhow::Result<()> {
    unsafe {
        let app = NSApp();
        app.activateIgnoringOtherApps_(YES);

        let status_bar = NSStatusBar::systemStatusBar(nil);
        let status_item: id = msg_send![status_bar, statusItemWithLength: -1.0];

        let title = NSString::alloc(nil).init_str("Taix");
        let button: id = msg_send![status_item, button];
        let _: () = msg_send![button, setTitle: title];

        let menu = NSMenu::new(nil);
        let _: () = msg_send![menu, setAutoenablesItems: false];

        let show_title = NSString::alloc(nil).init_str("显示 Taix");
        let no_key = NSString::alloc(nil).init_str("");

        let tx_clone = cmd_tx.clone();
        let cb_obj = Callback::from(Box::new(move || {
            let _ = tx_clone.send(TrayCmd::LaunchClient);
        }));

        let show_item = NSMenuItem::alloc(nil)
            .initWithTitle_action_keyEquivalent_(show_title, sel!(call), no_key);
        let _: () = msg_send![show_item, setTarget: cb_obj];
        let _: () = msg_send![menu, addItem: show_item];

        let separator: id = msg_send![class!(NSMenuItem), separatorItem];
        let _: () = msg_send![menu, addItem: separator];

        let quit_title = NSString::alloc(nil).init_str("退出");

        let quit_cb = Callback::from(Box::new(|| {
            let _ = std::process::Command::new("pkill")
                .args(["-x", "taix-server"])
                .output();
            let _ = std::process::Command::new("pkill")
                .args(["-x", "taix-monitor-macos"])
                .output();
            std::thread::sleep(std::time::Duration::from_millis(500));
            std::process::exit(0);
        }));

        let quit_item = NSMenuItem::alloc(nil)
            .initWithTitle_action_keyEquivalent_(quit_title, sel!(call), no_key);
        let _: () = msg_send![quit_item, setTarget: quit_cb];
        let _: () = msg_send![menu, addItem: quit_item];

        status_item.setMenu_(menu);

        let current_app = NSRunningApplication::currentApplication(nil);
        current_app.activateWithOptions_(NSApplicationActivateIgnoringOtherApps);

        app.run();
    }

    Ok(())
}
