import Foundation

actor SessionTracker {
    private enum TimerState {
        case idle
        case tracking(app: AppInfo, window: WindowInfo?, start: Date, accumulatedMs: TimeInterval, lastPeriodicCheck: Date)
        case suspended(app: AppInfo?, window: WindowInfo?, accumulatedMs: TimeInterval)
    }

    private let eventBus: EventBus
    private let transport: TransportClient
    private let persistence: Persistence
    private let tickInterval: TimeInterval
    private var state: TimerState = .idle
    private var tickTask: Task<Void, Never>?
    private var eventTask: Task<Void, Never>?

    init(
        eventBus: EventBus,
        transport: TransportClient,
        persistence: Persistence,
        tickInterval: TimeInterval
    ) {
        self.eventBus = eventBus
        self.transport = transport
        self.persistence = persistence
        self.tickInterval = tickInterval
    }

    func start() async {
        await restoreSessionIfNeeded()

        eventTask = Task { [weak self] in
            guard let self else { return }
            for await event in await self.eventBus.subscribe() {
                guard !Task.isCancelled else { break }
                await self.handle(event: event)
            }
        }

        tickTask = Task { [weak self] in
            while !Task.isCancelled, let self = self {
                try? await Task.sleep(nanoseconds: UInt64(self.tickInterval * 1_000_000_000))
                await self.periodicCheck()
            }
        }
    }

    func stop() async {
        tickTask?.cancel()
        eventTask?.cancel()
        tickTask = nil
        eventTask = nil
        await flushFinal()
    }

    private func handle(event: MonitorEvent) async {
        switch event.kind {
        case .foregroundChanged:
            await flushFinal()
            if let app = event.app {
                await beginSession(app: app, window: event.window)
            }
        case .idleDetected:
            // 注意：flushFinal 在 suspendSession 之前调用，会将当前会话结束
            // suspendSession 只是保存状态，不会重复 flush
            await flushFinal()
            await suspendSession()
        case .activityResumed:
            await resumeSession()
        default:
            break
        }
    }

    private func beginSession(app: AppInfo, window: WindowInfo?) async {
        let now = Date()
        state = .tracking(app: app, window: window, start: now, accumulatedMs: 0, lastPeriodicCheck: now)
        await saveSnapshot()

        Logger.info("Session started: \(app.name) [\(app.bundleIdentifier ?? "no-bundle-id")]")

        let event = MonitorEvent(
            kind: .foregroundChanged,
            timestamp: now,
            app: app,
            duration: 0,
            window: window
        )
        await transport.send(event)
    }

    private func suspendSession() async {
        switch state {
        case .tracking(let app, let window, _, let accumulatedMs, _):
            state = .suspended(app: app, window: window, accumulatedMs: accumulatedMs)
            await saveSnapshot()
            Logger.info("Session suspended: \(app.name)")
        case .idle:
            state = .suspended(app: nil, window: nil, accumulatedMs: 0)
        case .suspended:
            break
        }
    }

    private func resumeSession() async {
        let now = Date()
        switch state {
        case .suspended(let app, let window, let accumulatedMs):
            if let app = app {
                let window = window ?? WindowInfo(title: "", windowClass: "")
                state = .tracking(app: app, window: window, start: now, accumulatedMs: accumulatedMs, lastPeriodicCheck: now)
                await saveSnapshot()
                Logger.info("Session resumed: \(app.name)")
            } else {
                state = .idle
            }
        default:
            break
        }
    }

    private func periodicCheck() async {
        let now = Date()
        switch state {
        case .tracking(let app, _, let start, let accumulatedMs, let lastPeriodicCheck):
            if now.timeIntervalSince(lastPeriodicCheck) >= tickInterval {
                await saveSnapshot()
                state = .tracking(app: app, window: nil, start: start, accumulatedMs: accumulatedMs, lastPeriodicCheck: now)
                Logger.debug("Periodic checkpoint: \(app.name)")
            }
        case .idle:
            await persistence.clear()
        case .suspended:
            await persistence.clear()
        }
    }

    private func flushFinal() async {
        let now = Date()
        switch state {
        case .tracking(let app, _, let start, var accumulatedMs, _):
            if let event = computeFlush(start: start, end: now, accumulatedMs: &accumulatedMs, app: app, isFinal: true) {
                Logger.info("Session ended: \(app.name) [duration: \(event.duration ?? 0)s]")
                await transport.send(event)
            }
            state = .idle
            await persistence.clear()
        default:
            break
        }
    }

    private func computeFlush(
        start: Date,
        end: Date,
        accumulatedMs: inout TimeInterval,
        app: AppInfo,
        isFinal: Bool
    ) -> MonitorEvent? {
        let durationMs = end.timeIntervalSince(start) * 1000
        guard durationMs > 0 else { return nil }

        let maxFlushDurationMs: TimeInterval = 3600_000
        let cappedDurationMs = min(durationMs, maxFlushDurationMs)

        let totalMs = cappedDurationMs + accumulatedMs
        var durationSecs = Int64(totalMs / 1000)
        let remainder = totalMs.truncatingRemainder(dividingBy: 1000)

        if isFinal {
            if remainder >= 500 {
                durationSecs += 1
            }
            accumulatedMs = 0
        } else {
            accumulatedMs = remainder
        }

        guard durationSecs > 0 else { return nil }

        return MonitorEvent(
            kind: .sessionEnded,
            timestamp: start,
            app: app,
            duration: Double(durationSecs),
            window: nil
        )
    }

    private func saveSnapshot() async {
        switch state {
        case .tracking(let app, _, let start, let accumulatedMs, _):
            let snapshot = SessionSnapshot(
                bundleIdentifier: app.bundleIdentifier ?? "",
                executablePath: app.executablePath,
                startTime: start,
                accumulatedSeconds: accumulatedMs / 1000,
                appName: app.name
            )
            await persistence.save(snapshot: snapshot)
        case .suspended(let app, _, let accumulatedMs):
            guard let app = app else { return }
            let snapshot = SessionSnapshot(
                bundleIdentifier: app.bundleIdentifier ?? "",
                executablePath: app.executablePath,
                startTime: Date(),
                accumulatedSeconds: accumulatedMs / 1000,
                appName: app.name
            )
            await persistence.save(snapshot: snapshot)
        default:
            break
        }
    }

    private func restoreSessionIfNeeded() async {
        guard let snapshot = await persistence.load() else { return }
        await persistence.clear()

        let elapsed = Date().timeIntervalSince(snapshot.startTime)
        if elapsed > 300 {
            Logger.info("Session snapshot expired (\(Int(elapsed))s), skipping recovery")
            return
        }

        let app = AppInfo(
            name: snapshot.appName ?? snapshot.bundleIdentifier,
            bundleIdentifier: snapshot.bundleIdentifier,
            executablePath: snapshot.executablePath,
            iconPath: nil,
            displayName: snapshot.appName
        )

        state = .suspended(app: app, window: nil, accumulatedMs: snapshot.accumulatedSeconds * 1000)
        Logger.info("Session recovered: \(app.name) [\(Int(snapshot.accumulatedSeconds))s, pending user activity]")
    }
}
