import Foundation

struct Configuration: Sendable {
    let socketPath: String
    let tickInterval: TimeInterval
    let monitorConfig: MonitorConfig
    let iconCacheDirectory: URL
    let persistenceURL: URL

    static let `default` = Configuration(
        socketPath: "/tmp/taix_daemon.sock",
        tickInterval: 60,
        monitorConfig: .default,
        iconCacheDirectory: Configuration.defaultIconCacheDirectory,
        persistenceURL: Configuration.defaultPersistenceURL
    )

    static let appIconsDirectoryName = "AppIcons"

    static var defaultIconCacheDirectory: URL {
        if let override = ProcessInfo.processInfo.environment["TAIX_ICON_DIR"] {
            return URL(fileURLWithPath: override).appendingPathComponent(appIconsDirectoryName, isDirectory: true)
        }
        let support = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first!
        return support
            .appendingPathComponent("Taix", isDirectory: true)
            .appendingPathComponent(appIconsDirectoryName, isDirectory: true)
    }

    static var defaultPersistenceURL: URL {
        let support = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first!
        let directory = support.appendingPathComponent("Taix", isDirectory: true)
        try? FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        return directory.appendingPathComponent("active_session.json")
    }
}
