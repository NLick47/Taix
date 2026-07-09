import Foundation
import IOKit
import Cocoa

actor IdleDetector {
    private let eventBus: EventBus
    private let config: MonitorConfig
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

    // 声音持续时间追踪
    private var soundStart: Date?

    // 系统睡眠通知
    private var sleepObserver: Any?
    private var wakeObserver: Any?

    init(
        eventBus: EventBus,
        config: MonitorConfig,
        checkInterval: TimeInterval = 5,
        gamepadMonitor: GamepadMonitor? = nil
    ) {
        self.eventBus = eventBus
        self.config = config
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
                try? await Task.sleep(nanoseconds: UInt64(self.checkInterval * 1_000_000_000))
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
        // 睡眠检测关闭时，不进行任何检测
        if !config.sleepWatch {
            return
        }

        let idleSeconds = getIdleTime()
        let audioPlaying = audioMonitor.isAudioPlaying()
        let gamepadActive = await gamepadMonitor?.consumeActivity() ?? false

        Logger.debug("Idle check: \(String(format: "%.1f", idleSeconds))s idle (threshold: \(config.inactiveThresholdSecs)s), audio: \(audioPlaying ? "playing" : "silent"), gamepad: \(gamepadActive ? "active" : "inactive")")

        // 检测锁屏/休眠恢复：idle 在单个 tick 内大幅跳变
        if let lastIdle = lastIdleTime, !isIdle {
            let idleJump = idleSeconds - lastIdle
            if idleJump > resumeJumpThreshold {
                // 系统从锁屏/休眠恢复
                Logger.info("System resume detected (idle jump: \(String(format: "%.1f", lastIdle))s -> \(String(format: "%.1f", idleSeconds))s), entering resume pending state")
                isResumePending = true
                // 先发送 idle 事件暂停计时
                isIdle = true
                soundStart = nil
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
                soundStart = nil
                await publish(kind: .activityResumed)
            }
            lastIdleTime = idleSeconds
            return
        }

        lastIdleTime = idleSeconds

        // 判断用户是否活跃（与 Windows 端一致的逻辑）
        let userActive = idleSeconds < config.inactiveThresholdSecs || gamepadActive

        // 处理状态转换（与 Windows 端一致的声音持续检测逻辑）
        // 注意：soundStart 追踪的是"用户 idle 但声音播放中"的持续时间
        if !userActive && audioPlaying {
            // 用户 idle 但声音播放中，追踪声音开始时间
            if soundStart == nil {
                soundStart = Date()
                Logger.debug("Sound started while idle, tracking duration")
            }
            // 声音持续播放未超过阈值，保持 Wake 状态（不发送 idleDetected）
            if let start = soundStart, Date().timeIntervalSince(start) < config.maxSoundDurationSecs {
                lastIdleTime = idleSeconds
                return
            }
            // 声音持续播放超过阈值，转为 Sleep 状态
            Logger.info("Sound duration exceeded \(config.maxSoundDurationSecs)s, entering idle state")
            soundStart = nil
        } else if userActive {
            // 用户活跃时重置声音追踪
            soundStart = nil
        }

        // 计算有效 idle 状态（用户不活跃、无声音、无手柄、超过阈值）
        let effectiveIdle = !audioPlaying && !gamepadActive && idleSeconds >= config.inactiveThresholdSecs

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
            return config.inactiveThresholdSecs // 返回阈值，避免误判为活动状态
        }

        guard let dict = properties?.takeRetainedValue() as? [String: Any],
              let idleTime = dict["HIDIdleTime"] as? Int64 else {
            Logger.error("Failed to get HIDIdleTime from registry")
            return config.inactiveThresholdSecs
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
