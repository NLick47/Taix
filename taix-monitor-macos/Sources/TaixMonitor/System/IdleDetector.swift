import Foundation
import IOKit

actor IdleDetector {
    private let eventBus: EventBus
    private let threshold: TimeInterval
    private let checkInterval: TimeInterval
    private var monitorTask: Task<Void, Never>?
    private var isIdle: Bool = false
    private let audioMonitor: AudioMonitor
    private var gamepadMonitor: GamepadMonitor?

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
        monitorTask = Task { [weak self] in
            while !Task.isCancelled {
                guard let self else { break }
                await self.check()
                try? await Task.sleep(for: .seconds(self.checkInterval))
            }
        }
    }

    func stop() {
        monitorTask?.cancel()
        monitorTask = nil
    }

    private func check() async {
        let idleSeconds = getIdleTime()
        let audioPlaying = audioMonitor.isAudioPlaying()
        let gamepadActive = await gamepadMonitor?.consumeActivity() ?? false

        Logger.debug("Idle check: \(String(format: "%.1f", idleSeconds))s idle (threshold: \(threshold)s), audio: \(audioPlaying ? "playing" : "silent"), gamepad: \(gamepadActive ? "active" : "inactive")")

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
