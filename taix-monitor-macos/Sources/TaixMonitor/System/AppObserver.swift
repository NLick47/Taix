import AppKit
import Foundation

/// 系统应用 bundle identifiers，这些应用不追踪
private let excludedBundleIdentifiers: Set<String> = [
    "com.apple.screensaver",           // 屏幕保护程序
    "com.apple.ScreenSaver.Engine",    // 屏幕保护引擎
    "com.apple.loginwindow",           // 登录窗口
    "com.apple.SecurityAgent",         // 安全代理（锁屏时的认证对话框）
    "com.apple.lockscreen",            // 锁屏界面
]

actor AppObserver {
    private let eventBus: EventBus
    private let iconExtractor: IconExtractor
    private var frontmostApp: NSRunningApplication?
    private var observation: Any?

    init(eventBus: EventBus, iconExtractor: IconExtractor) {
        self.eventBus = eventBus
        self.iconExtractor = iconExtractor
    }

    func start() {
        observation = NSWorkspace.shared.notificationCenter.addObserver(
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

    func stop() {
        if let observation {
            NSWorkspace.shared.notificationCenter.removeObserver(observation)
            self.observation = nil
        }
    }

    private func handle(application: NSRunningApplication) async {
        let previous = frontmostApp?.localizedName ?? "nil"
        frontmostApp = application

        // 检查是否为排除的系统应用
        guard !isExcluded(application) else {
            // 如果之前有正常应用在运行，需要结束它的会话
            Logger.debug("App ignored (system): \(application.localizedName ?? "Unknown") [\(application.bundleIdentifier ?? "no-bundle-id")]")
            return
        }

        let path = application.bundleURL?.path ?? ""
        let iconPath = await iconExtractor.iconPath(for: path)

        let displayName = getDisplayName(from: application.bundleURL)

        let appInfo = AppInfo(
            name: application.localizedName ?? "Unknown",
            bundleIdentifier: application.bundleIdentifier,
            executablePath: path,
            iconPath: iconPath,
            displayName: displayName
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

    private func isExcluded(_ application: NSRunningApplication) -> Bool {
        guard let bundleId = application.bundleIdentifier else { return false }
        return excludedBundleIdentifiers.contains(bundleId)
    }

    private func getDisplayName(from bundleURL: URL?) -> String? {
        guard let bundleURL else { return nil }
        let infoPlistURL = bundleURL.appendingPathComponent("Contents/Info.plist")
        guard let info = NSDictionary(contentsOf: infoPlistURL) else { return nil }

        if let displayName = info["CFBundleDisplayName"] as? String, !displayName.isEmpty {
            return displayName
        }
        if let bundleName = info["CFBundleName"] as? String, !bundleName.isEmpty {
            return bundleName
        }
        return nil
    }
}
