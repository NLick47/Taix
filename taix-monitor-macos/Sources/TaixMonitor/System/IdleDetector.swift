import Foundation
import IOKit
import Cocoa

actor IdleDetector {
    private let eventBus: EventBus
    private let threshold: TimeInterval
    private let checkInterval: TimeInterval
    private var monitorTask: Task<Void, Never>?
    private var isIdle: Bool = false
    private let audioMonitor: AudioMonitor
    private var gamepadMonitor: GamepadMonitor?

    // 锁屏/休眠检测
    private var lastIdleTime: TimeInterval?
    private let resumeJumpThreshold: TimeInterval = 60  // idle 单次跳变超过 60 秒视为系统恢复
    private let resumeIdleThreshold: TimeInterval = 30  // 恢复后判定用户活跃的 idle 阈值
    private var isResumePending: Bool = false

    // 系统睡眠通知
    private var sleepObserver: Any?
    private var wakeObserver: Any?

    init(
        eventBus: EventBus,
        threshold: TimeInterval,
        checkInterval: TimeInterval = 5,
        gamepadMonitor: GamepadMonitor? = nil
    ) {
        self.eventBus = eventBus
        self.threshold = threshold
        self.checkInterval = checkInterval
        self.audioMonitor = AudioMonitor()
        self.gamepadMonitor = gamepadMonitor
    }

    func start() {
        // 监听系统睡眠通知
        sleepObserver = NSWorkspace.shared.notificationCenter.addObserver(
            forName: NSWorkspace.willSleepNotification,
            object: nil,
            queue: .main
        ) { [weak self] _ in
            Task {
                await self?.handleSystemSleep()
            }
        }

        // 监听系统唤醒通知
        wakeObserver = NSWorkspace.shared.notificationCenter.addObserver(
            forName: NSWorkspace.didWakeNotification,
            object: nil,
            queue: .main
        ) { [weak self] _ in
            Task {
                await self?.handleSystemWake()
            }
        }

        monitorTask = Task { [weak self] in
            while !Task.isCancelled {
                guard let self else { break }
                await self.check()
                try? await Task.sleep(for: .seconds(self.checkInterval))
            }
        }
    }

    func stop() {
        // 移除睡眠通知监听
        if let observer = sleepObserver {
            NSWorkspace.shared.notificationCenter.removeObserver(observer)
            sleepObserver = nil
        }
        if let observer = wakeObserver {
            NSWorkspace.shared.notificationCenter.removeObserver(observer)
            wakeObserver = nil
        }
        monitorTask?.cancel()
        monitorTask = nil
    }

    private func handleSystemSleep() async {
        // 系统即将睡眠，立即暂停计时
        guard !isIdle else { return }
        isIdle = true
        isResumePending = true  // 标记为待恢复状态，等待唤醒后用户活动
        Logger.info("System will sleep, pausing session timer")
        await publish(kind: .idleDetected)
    }

    private func handleSystemWake() async {
        // 系统唤醒，进入待恢复状态等待用户活动
        Logger.info("System woke from sleep, waiting for user activity")
        // isResumePending 已经在睡眠时设置，check() 方法会检测用户活动并恢复
    }

    private func check() async {
        let idleSeconds = getIdleTime()
        let audioPlaying = audioMonitor.isAudioPlaying()
        let gamepadActive = await gamepadMonitor?.consumeActivity() ?? false

        Logger.debug("Idle check: \(String(format: "%.1f", idleSeconds))s idle (threshold: \(threshold)s), audio: \(audioPlaying ? "playing" : "silent"), gamepad: \(gamepadActive ? "active" : "inactive")")

        // 检测锁屏/休眠恢复：idle 在单个 tick 内大幅跳变
        if let lastIdle = lastIdleTime, !isIdle {
            let idleJump = idleSeconds - lastIdle
            if idleJump > resumeJumpThreshold {
                // 系统从锁屏/休眠恢复
                Logger.info("System resume detected (idle jump: \(String(format: "%.1f", lastIdle))s -> \(String(format: "%.1f", idleSeconds))s), entering resume pending state")
                isResumePending = true
                // 先发送 idle 事件暂停计时
                isIdle = true
                await publish(kind: .idleDetected)
                lastIdleTime = idleSeconds
                return
            }
        }

        // 恢复后等待用户操作确认
        if isResumePending {
            let userActive = idleSeconds < resumeIdleThreshold || gamepadActive || audioPlaying
            if userActive {
                Logger.info("User activity detected after resume, exiting resume cooldown")
                isResumePending = false
                isIdle = false
                await publish(kind: .activityResumed)
            }
            lastIdleTime = idleSeconds
            return
        }

        lastIdleTime = idleSeconds

        // 如果有音频播放或手柄输入，不算空闲
        let effectiveIdle = !audioPlaying && !gamepadActive && idleSeconds >= threshold

        if effectiveIdle && !isIdle {
            isIdle = true
            Logger.info("System entered idle state after \(String(format: "%.1f", idleSeconds))s")
            await publish(kind: .idleDetected)
        } else if !effectiveIdle && isIdle {
            isIdle = false
            let reason = audioPlaying ? "audio playing" : (gamepadActive ? "gamepad input" : "user activity")
            Logger.info("System activity resumed (\(reason))")
            await publish(kind: .activityResumed)
        }
    }

    private func getIdleTime() -> TimeInterval {
        let service = IOServiceGetMatchingService(kIOMainPortDefault, IOServiceMatching("IOHIDSystem"))
        defer { IOObjectRelease(service) }

        var properties: Unmanaged<CFMutableDictionary>?
        let result = IORegistryEntryCreateCFProperties(service, &properties, kCFAllocatorDefault, 0)

        guard result == KERN_SUCCESS else {
            Logger.error("Failed to get IORegistry properties: \(result)")
            return threshold // 返回阈值，避免误判为活动状态
        }

        guard let dict = properties?.takeRetainedValue() as? [String: Any],
              let idleTime = dict["HIDIdleTime"] as? Int64 else {
            Logger.error("Failed to get HIDIdleTime from registry")
            return threshold
        }

        return Double(idleTime) / 1_000_000_000 // nanoseconds to seconds
    }

    private func publish(kind: MonitorEvent.EventKind) async {
        let event = MonitorEvent(
            kind: kind,
            timestamp: Date(),
            app: nil,
            duration: nil,
            window: nil
        )
        await eventBus.publish(event)
    }
}
