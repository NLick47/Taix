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
        let base = ProcessInfo.processInfo.environment["TAIX_EXE_DIR"] ?? "/Applications/TaixTools"
        return URL(fileURLWithPath: base).appendingPathComponent(appIconsDirectoryName, isDirectory: true)
    }

    static var defaultPersistenceURL: URL {
        let support = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first!
        let directory = support.appendingPathComponent("Taix", isDirectory: true)
        try? FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        return directory.appendingPathComponent("active_session.json")
    }
}
