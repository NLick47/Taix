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
        self.eventBus = EventBus()
        self.transport = TransportClient(socketPath: configuration.socketPath)
        self.persistence = Persistence(url: configuration.persistenceURL)
        self.iconExtractor = IconExtractor(cacheDirectory: configuration.iconCacheDirectory)
        self.appObserver = AppObserver(eventBus: eventBus, iconExtractor: iconExtractor)
        self.gamepadMonitor = GamepadMonitor()
        self.idleDetector = IdleDetector(
            eventBus: eventBus,
            config: configuration.monitorConfig,
            gamepadMonitor: gamepadMonitor
        )
        self.sessionTracker = SessionTracker(
            eventBus: eventBus,
            transport: transport,
            persistence: persistence,
            tickInterval: configuration.tickInterval
        )
    }

    func start() async {
        Logger.info("TaixMonitor starting...")

        await transport.start()
        await appObserver.start()
        await idleDetector.start()
        await gamepadMonitor.start()
        await sessionTracker.start()

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
