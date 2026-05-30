import Foundation

actor SessionTracker {
    private struct Session {
        let app: AppInfo
        let window: WindowInfo?
        var startTime: Date
        var accumulatedDuration: TimeInterval
        var isSuspended: Bool
        
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
        let session = Session(
            app: app,
            window: window,
            startTime: Date(),
            accumulatedDuration: 0,
            isSuspended: false
        )
        currentSession = session
        await saveSnapshot()
        
        Logger.info("Session started: \(app.name) [\(app.bundleIdentifier ?? "no-bundle-id")]")
        
        let event = MonitorEvent(
            kind: .foregroundChanged,
            timestamp: Date(),
            app: app,
            duration: 0,
            window: window
        )
        await transport.send(event)
    }
    
    private func endSession() async {
        guard let session = currentSession else { return }
        let duration = session.totalDuration
        currentSession = nil
        await persistence.clear()
        
        Logger.info("Session ended: \(session.app.name) [duration: \(String(format: "%.1f", duration))s]")
        
        let event = MonitorEvent(
            kind: .sessionEnded,
            timestamp: Date(),
            app: session.app,
            duration: duration,
            window: session.window
        )
        await transport.send(event)
    }
    
    private func suspendSession() async {
        guard var session = currentSession, !session.isSuspended else { return }
        let elapsed = Date().timeIntervalSince(session.startTime)
        session.accumulatedDuration += elapsed
        session.isSuspended = true
        currentSession = session
        await saveSnapshot()
        
        Logger.info("Session suspended: \(session.app.name) [accumulated: \(String(format: "%.1f", session.accumulatedDuration))s]")
    }
    
    private func resumeSession() async {
        guard var session = currentSession, session.isSuspended else { return }
        session.startTime = Date()
        session.isSuspended = false
        currentSession = session
        await saveSnapshot()
        
        Logger.info("Session resumed: \(session.app.name)")
    }
    
    private func flushTick() async {
        guard let session = currentSession, !session.isSuspended else { return }
        let duration = session.totalDuration
        await saveSnapshot()
        
        Logger.debug("Session tick: \(session.app.name) [duration: \(String(format: "%.1f", duration))s]")
        
        let event = MonitorEvent(
            kind: .sessionTick,
            timestamp: Date(),
            app: session.app,
            duration: duration,
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
            isSuspended: true
        )
    }
}
