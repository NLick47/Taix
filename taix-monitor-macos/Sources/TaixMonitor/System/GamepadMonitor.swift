import Foundation
import IOKit
import IOKit.hid

private final class GamepadActivityFlag: @unchecked Sendable {
    private let lock = NSLock()
    private var value = false

    func set() {
        lock.lock()
        let wasActive = value
        value = true
        lock.unlock()
        if !wasActive {
            Logger.info("Gamepad input detected")
        }
    }

    func take() -> Bool {
        lock.lock()
        defer {
            value = false
            lock.unlock()
        }
        return value
    }
}

actor GamepadMonitor {
    private var manager: IOHIDManager?
    private let activityFlag = GamepadActivityFlag()

    func start() {
        let hidManager = IOHIDManagerCreate(kCFAllocatorDefault, IOOptionBits(kIOHIDOptionsTypeNone))

        let matching = [
            kIOHIDDeviceUsagePageKey: kHIDPage_GenericDesktop,
            kIOHIDDeviceUsageKey: kHIDUsage_GD_GamePad
        ] as CFDictionary

        IOHIDManagerSetDeviceMatching(hidManager, matching)

        let context = Unmanaged.passUnretained(activityFlag).toOpaque()
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

    func consumeActivity() -> Bool {
        activityFlag.take()
    }
}

private func inputCallback(
    _ context: UnsafeMutableRawPointer?,
    _ result: IOReturn,
    _ sender: UnsafeMutableRawPointer?,
    _ value: IOHIDValue
) {
    guard let context else { return }
    let flag = Unmanaged<GamepadActivityFlag>.fromOpaque(context).takeUnretainedValue()
    flag.set()
}
