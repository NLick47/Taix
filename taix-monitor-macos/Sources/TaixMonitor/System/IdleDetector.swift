import CoreGraphics
import Foundation

actor IdleDetector {
    private let eventBus: EventBus
    private let threshold: TimeInterval
    private let checkInterval: TimeInterval
    private var monitorTask: Task<Void, Never>?
    private var isIdle: Bool = false
    
    init(eventBus: EventBus, threshold: TimeInterval, checkInterval: TimeInterval = 5) {
        self.eventBus = eventBus
        self.threshold = threshold
        self.checkInterval = checkInterval
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
        let idleSeconds = TimeInterval(
            CGEventSource.secondsSinceLastEventType(.hidSystemState)
        )
        
        Logger.debug("Idle check: \(String(format: "%.1f", idleSeconds))s idle (threshold: \(threshold)s)")
        
        if idleSeconds >= threshold && !isIdle {
            isIdle = true
            Logger.info("System entered idle state after \(String(format: "%.1f", idleSeconds))s")
            await publish(kind: .idleDetected)
        } else if idleSeconds < checkInterval && isIdle {
            isIdle = false
            Logger.info("System activity resumed")
            await publish(kind: .activityResumed)
        }
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
