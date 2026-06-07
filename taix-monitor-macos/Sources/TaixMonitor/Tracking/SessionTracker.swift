import Foundation

actor SessionTracker {
    private struct Session {
        let app: AppInfo
        let window: WindowInfo?
        var startTime: Date
        var accumulatedDuration: TimeInterval
        var isSuspended: Bool
        var lastFlushTime: Date  // 上次 flush 的时间，用于计算增量
        var accumulatedMs: TimeInterval  // 毫秒零头累积，避免整数截断丢失

        var totalDuration: TimeInterval {
            guard !isSuspended else { return accumulatedDuration }
            return accumulatedDuration + Date().timeIntervalSince(startTime)
        }
    }

    private let eventBus: EventBus
    private let transport: TransportClient
    private let persistence: Persistence
    private let tickInterval: TimeInterval
    private var currentSession: Session?
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
                try? await Task.sleep(for: .seconds(self.tickInterval))
                await self.flushTick()
            }
        }
    }

    func stop() async {
        tickTask?.cancel()
        eventTask?.cancel()
        tickTask = nil
        eventTask = nil
        await endSession()
    }

    private func handle(event: MonitorEvent) async {
        switch event.kind {
        case .foregroundChanged:
            await endSession()
            if let app = event.app {
                await beginSession(app: app, window: event.window)
            }
        case .idleDetected:
            await suspendSession()
        case .activityResumed:
            await resumeSession()
        default:
            break
        }
    }

    private func beginSession(app: AppInfo, window: WindowInfo?) async {
        let now = Date()
        let session = Session(
            app: app,
            window: window,
            startTime: now,
            accumulatedDuration: 0,
            isSuspended: false,
            lastFlushTime: now,
            accumulatedMs: 0
        )
        currentSession = session
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

    private func endSession() async {
        guard let session = currentSession else { return }
        var duration = session.totalDuration + session.accumulatedMs / 1000.0
        // 最终刷盘：毫秒零头四舍五入
        let remainderMs = duration.truncatingRemainder(dividingBy: 1.0) * 1000
        if remainderMs >= 500 {
            duration = (duration * 1000).rounded(.down) / 1000 + 1
        }
        let durationSecs = Int64(duration)

        currentSession = nil
        await persistence.clear()

        Logger.info("Session ended: \(session.app.name) [duration: \(durationSecs)s]")

        // 计算会话开始时间：结束时间 - 持续时间
        // 服务端期望收到的是开始时间（a 字段），而不是结束时间
        let startTime = Date().addingTimeInterval(-Double(durationSecs))

        let event = MonitorEvent(
            kind: .sessionEnded,
            timestamp: startTime,
            app: session.app,
            duration: Double(durationSecs),
            window: session.window
        )
        await transport.send(event)
    }

    private func suspendSession() async {
        guard var session = currentSession, !session.isSuspended else { return }
        let now = Date()
        let elapsed = now.timeIntervalSince(session.startTime)
        session.accumulatedDuration += elapsed
        session.isSuspended = true
        session.lastFlushTime = now
        session.accumulatedMs = 0  // 重置毫秒零头
        currentSession = session
        await saveSnapshot()

        Logger.info("Session suspended: \(session.app.name) [accumulated: \(String(format: "%.1f", session.accumulatedDuration))s]")
    }

    private func resumeSession() async {
        guard var session = currentSession, session.isSuspended else { return }
        let now = Date()
        session.startTime = now
        session.isSuspended = false
        session.lastFlushTime = now
        session.accumulatedMs = 0  // 重置毫秒零头
        currentSession = session
        await saveSnapshot()

        Logger.info("Session resumed: \(session.app.name)")
    }

    private func flushTick() async {
        guard var session = currentSession, !session.isSuspended else { return }
        let now = Date()

        let incrementalDuration = now.timeIntervalSince(session.lastFlushTime)

        // 服务端期望收到的是这段时间的开始时刻，而不是结束时刻
        let startTime = session.lastFlushTime

        // 累积毫秒零头，避免整数截断丢失
        let totalMs = incrementalDuration * 1000 + session.accumulatedMs
        let durationSecs = Int64(totalMs / 1000)
        let remainderMs = totalMs.truncatingRemainder(dividingBy: 1000)

        // 周期刷盘保留毫秒零头继续累积
        session.lastFlushTime = now
        session.accumulatedMs = remainderMs
        currentSession = session
        await saveSnapshot()

        Logger.debug("Session tick: \(session.app.name) [incremental: \(durationSecs)s, remainder: \(Int(remainderMs))ms]")

        let event = MonitorEvent(
            kind: .sessionTick,
            timestamp: startTime,
            app: session.app,
            duration: Double(durationSecs),
            window: session.window
        )
        await transport.send(event)
    }

    private func saveSnapshot() async {
        guard let session = currentSession else { return }
        let snapshot = SessionSnapshot(
            bundleIdentifier: session.app.bundleIdentifier ?? "",
            executablePath: session.app.executablePath,
            startTime: session.startTime,
            accumulatedSeconds: session.accumulatedDuration,
            appName: session.app.name
        )
        await persistence.save(snapshot: snapshot)
    }

    private func restoreSessionIfNeeded() async {
        guard let snapshot = await persistence.load() else { return }
        let now = Date()
        let app = AppInfo(
            name: snapshot.appName ?? snapshot.bundleIdentifier,
            bundleIdentifier: snapshot.bundleIdentifier,
            executablePath: snapshot.executablePath,
            iconPath: nil,
            displayName: snapshot.appName
        )
        currentSession = Session(
            app: app,
            window: nil,
            startTime: snapshot.startTime,
            accumulatedDuration: snapshot.accumulatedSeconds,
            isSuspended: true,
            lastFlushTime: now,
            accumulatedMs: 0
        )
    }
}
