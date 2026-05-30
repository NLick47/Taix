import Foundation
import IOKit
import IOKit.hid

actor GamepadMonitor {
    private var manager: IOHIDManager?
    private var isActive: Bool = false

    init() {}

    func start() {
        let hidManager = IOHIDManagerCreate(kCFAllocatorDefault, IOOptionBits(kIOHIDOptionsTypeNone))

        let matching = [
            kIOHIDDeviceUsagePageKey: kHIDPage_GenericDesktop,
            kIOHIDDeviceUsageKey: kHIDUsage_GD_GamePad
        ] as CFDictionary

        IOHIDManagerSetDeviceMatching(hidManager, matching)

        let context = Unmanaged.passUnretained(self).toOpaque()
        IOHIDManagerRegisterInputValueCallback(hidManager, inputCallback, context)

        IOHIDManagerScheduleWithRunLoop(hidManager, CFRunLoopGetMain(), CFRunLoopMode.defaultMode.rawValue)
        IOHIDManagerOpen(hidManager, IOOptionBits(kIOHIDOptionsTypeNone))

        manager = hidManager
    }

    func stop() {
        guard let manager else { return }
        IOHIDManagerClose(manager, IOOptionBits(kIOHIDOptionsTypeNone))
        IOHIDManagerUnscheduleFromRunLoop(manager, CFRunLoopGetMain(), CFRunLoopMode.defaultMode.rawValue)
        IOHIDManagerRegisterInputValueCallback(manager, nil, nil)
        self.manager = nil
    }

    func markActive() {
        if !isActive {
            Logger.info("Gamepad input detected")
        }
        isActive = true
    }

    func consumeActivity() -> Bool {
        let wasActive = isActive
        isActive = false
        return wasActive
    }
}

private func inputCallback(
    _ context: UnsafeMutableRawPointer?,
    _ result: IOReturn,
    _ sender: UnsafeMutableRawPointer?,
    _ value: IOHIDValue
) {
    guard let context else { return }
    let monitor = Unmanaged<GamepadMonitor>.fromOpaque(context).takeUnretainedValue()
    Task { await monitor.markActive() }
}
