import AppKit
import Foundation

actor AppObserver {
    private let eventBus: EventBus
    private let iconExtractor: IconExtractor
    private var frontmostApp: NSRunningApplication?
    
    init(eventBus: EventBus, iconExtractor: IconExtractor) {
        self.eventBus = eventBus
        self.iconExtractor = iconExtractor
    }
    
    func start() {
        NSWorkspace.shared.notificationCenter.addObserver(
            forName: NSWorkspace.didActivateApplicationNotification,
            object: nil,
            queue: .main
        ) { [weak self] notification in
            guard let app = notification.userInfo?[NSWorkspace.applicationUserInfoKey] as? NSRunningApplication else { return }
            Task {
                await self?.handle(application: app)
            }
        }
        
        if let app = NSWorkspace.shared.frontmostApplication {
            Task {
                await handle(application: app)
            }
        }
    }
    
    private func handle(application: NSRunningApplication) async {
        let previous = frontmostApp?.localizedName ?? "nil"
        frontmostApp = application
        
        let path = application.bundleURL?.path ?? ""
        let iconPath = await iconExtractor.iconPath(for: path)
        
        let appInfo = AppInfo(
            name: application.localizedName ?? "Unknown",
            bundleIdentifier: application.bundleIdentifier,
            executablePath: path,
            iconPath: iconPath
        )
        
        let windowInfo = WindowInfo(
            title: "",
            windowClass: ""
        )
        
        Logger.info("App switched: \(previous) → \(appInfo.name) [\(appInfo.bundleIdentifier ?? "no-bundle-id")]")
        
        let event = MonitorEvent(
            kind: .foregroundChanged,
            timestamp: Date(),
            app: appInfo,
            duration: nil,
            window: windowInfo
        )
        
        await eventBus.publish(event)
    }
}
