import Foundation

actor SessionTracker {
    private enum TimerState {
        case idle
        case tracking(app: AppInfo, window: WindowInfo?, start: Date, startTick: TimeInterval, accumulatedMs: TimeInterval, lastPeriodicCheck: Date)
        case suspended
    }

    private let eventBus: EventBus
    private let transport: TransportClient
    private let persistence: Persistence
    private let tickInterval: TimeInterval
    /// 获取当前“可统计”的前台应用（排除锁屏/屏保等系统应用）。
    private let frontmostProvider: () async -> AppInfo?
    private var state: TimerState = .idle
    private var tickTask: Task<Void, Never>?
    private var eventTask: Task<Void, Never>?

    init(
        eventBus: EventBus,
        transport: TransportClient,
        persistence: Persistence,
        tickInterval: TimeInterval,
        frontmostProvider: @escaping () async -> AppInfo?
    ) {
        self.eventBus = eventBus
        self.transport = transport
        self.persistence = persistence
        self.tickInterval = tickInterval
        self.frontmostProvider = frontmostProvider
    }

    private var nowTick: TimeInterval { ProcessInfo.processInfo.systemUptime }

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
            // flushFinal 结算当前会话，suspendSession 再转入挂起态
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
        let tick = nowTick
        state = .tracking(app: app, window: window, start: now, startTick: tick, accumulatedMs: 0, lastPeriodicCheck: now)
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
        // .idleDetected 路径下 flushFinal 已把 .tracking 结算并置回 .idle，此处只命中 .idle
        switch state {
        case .idle:
            state = .suspended
        case .suspended:
            break
        default:
            break
        }
    }

    private func resumeSession() async {
        let now = Date()
        let tick = nowTick
        switch state {
        case .suspended:
            // 恢复后前台仍是某个可统计应用就立即续计，否则要等下次切应用
            if let frontmost = await frontmostProvider() {
                state = .tracking(app: frontmost, window: nil, start: now, startTick: tick, accumulatedMs: 0, lastPeriodicCheck: now)
                await saveSnapshot()
                Logger.info("Session resumed after idle: \(frontmost.name)")
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
        case .tracking(let app, _, let start, let startTick, let accumulatedMs, let lastPeriodicCheck):
            if now.timeIntervalSince(lastPeriodicCheck) >= tickInterval {
                await saveSnapshot()
                state = .tracking(app: app, window: nil, start: start, startTick: startTick, accumulatedMs: accumulatedMs, lastPeriodicCheck: now)
                Logger.debug("Periodic checkpoint: \(app.name)")
            }
        case .idle:
            await persistence.clear()
        case .suspended:
            await persistence.clear()
        }
    }

    private func flushFinal() async {
        let tick = nowTick
        switch state {
        case .tracking(let app, _, let start, let startTick, var accumulatedMs, _):
            if let event = computeFlush(start: start, startTick: startTick, endTick: tick, accumulatedMs: &accumulatedMs, app: app, isFinal: true) {
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
        startTick: TimeInterval,
        endTick: TimeInterval,
        accumulatedMs: inout TimeInterval,
        app: AppInfo,
        isFinal: Bool
    ) -> MonitorEvent? {
        let durationMs = (endTick - startTick) * 1000 + accumulatedMs
        guard durationMs > 0 else { return nil }

        var durationSecs = Int64(durationMs / 1000)
        let remainder = durationMs.truncatingRemainder(dividingBy: 1000)

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
        let now = Date()
        let tick = nowTick
        switch state {
        case .tracking(let app, _, let start, let startTick, let accumulatedMs, _):
            let accruedSeconds = (accumulatedMs + (tick - startTick) * 1000) / 1000
            let snapshot = SessionSnapshot(
                bundleIdentifier: app.bundleIdentifier ?? "",
                executablePath: app.executablePath,
                startTime: start,
                accumulatedSeconds: accumulatedMs / 1000,
                appName: app.name,
                accruedSeconds: accruedSeconds,
                savedAt: now
            )
            await persistence.save(snapshot: snapshot)
        default:
            break
        }
    }

    private func restoreSessionIfNeeded() async {
        guard let snapshot = await persistence.load() else { return }
        await persistence.clear()

        let app = AppInfo(
            name: snapshot.appName ?? snapshot.bundleIdentifier,
            bundleIdentifier: snapshot.bundleIdentifier,
            executablePath: snapshot.executablePath,
            iconPath: nil,
            displayName: snapshot.appName
        )

        let accMs = (snapshot.accruedSeconds ?? snapshot.accumulatedSeconds) * 1000

        // 崩溃前应用仍是当前可统计前台
        if let frontmost = await frontmostProvider(), isSameApp(app, frontmost) {
            let now = Date()
            let tick = nowTick
            state = .tracking(app: app, window: nil, start: now, startTick: tick, accumulatedMs: accMs, lastPeriodicCheck: now)
            await saveSnapshot()
            Logger.info("Session resumed after restart: \(app.name) [\(Int(accMs / 1000))s carried over]")
            return
        }

        var secs = Int64(accMs / 1000)
        if accMs.truncatingRemainder(dividingBy: 1000) >= 500 { secs += 1 }
        if secs > 0 {
            let settledAt = snapshot.savedAt ?? snapshot.startTime
            let event = MonitorEvent(
                kind: .sessionEnded,
                timestamp: settledAt,
                app: app,
                duration: Double(secs),
                window: nil
            )
            await transport.send(event)
            Logger.info("Session settled after restart: \(app.name) [\(secs)s]")
        }
        state = .idle
    }

    private func isSameApp(_ a: AppInfo, _ b: AppInfo) -> Bool {
        if let aBundle = a.bundleIdentifier, let bBundle = b.bundleIdentifier, !aBundle.isEmpty {
            return aBundle == bBundle
        }
        return !a.executablePath.isEmpty && a.executablePath == b.executablePath
    }
}
