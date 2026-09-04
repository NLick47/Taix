import Foundation

actor Runner {
    private let configuration: Configuration
    private let eventBus: EventBus
    private let transport: TransportClient
    private let persistence: Persistence
    private let iconExtractor: IconExtractor
    private let appObserver: AppObserver
    private let idleDetector: IdleDetector
    private let sessionTracker: SessionTracker
    private let gamepadMonitor: GamepadMonitor
    private var isRunning = false

    init(configuration: Configuration) {
        self.configuration = configuration
        let eventBus = EventBus()
        let transport = TransportClient(socketPath: configuration.socketPath)
        let persistence = Persistence(url: configuration.persistenceURL)
        let iconExtractor = IconExtractor(cacheDirectory: configuration.iconCacheDirectory)
        let appObserver = AppObserver(eventBus: eventBus, iconExtractor: iconExtractor)
        let gamepadMonitor = GamepadMonitor()
        let idleDetector = IdleDetector(
            eventBus: eventBus,
            config: configuration.monitorConfig,
            gamepadMonitor: gamepadMonitor
        )
        let sessionTracker = SessionTracker(
            eventBus: eventBus,
            transport: transport,
            persistence: persistence,
            tickInterval: configuration.tickInterval,
            frontmostProvider: { [weak appObserver] in
                guard let appObserver else { return nil }
                return await appObserver.currentTrackedApp()
            }
        )
        self.eventBus = eventBus
        self.transport = transport
        self.persistence = persistence
        self.iconExtractor = iconExtractor
        self.appObserver = appObserver
        self.idleDetector = idleDetector
        self.sessionTracker = sessionTracker
        self.gamepadMonitor = gamepadMonitor
    }

    func start() async {
        Logger.info("TaixMonitor starting...")

        await transport.start()
        await sessionTracker.start()
        await appObserver.start()
        await idleDetector.start()
        await gamepadMonitor.start()

        Logger.info("TaixMonitor is running")

        isRunning = true
        while isRunning && !Task.isCancelled {
            try? await Task.sleep(nanoseconds: 1_000_000_000)
        }
    }

    func shutdown() async {
        Logger.info("TaixMonitor shutting down...")
        isRunning = false
        await appObserver.stop()
        await idleDetector.stop()
        await gamepadMonitor.stop()
        await sessionTracker.stop()
        await transport.stop()
        Logger.info("TaixMonitor stopped")
    }
}
