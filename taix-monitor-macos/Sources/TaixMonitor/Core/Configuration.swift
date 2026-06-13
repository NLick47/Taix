import Foundation

struct Configuration: Sendable {
    let socketPath: String
    let tickInterval: TimeInterval
    let idleThreshold: TimeInterval
    let iconCacheDirectory: URL
    let persistenceURL: URL

    static let `default` = Configuration(
        socketPath: "/tmp/taix_daemon.sock",
        tickInterval: 60,
        idleThreshold: 900,
        iconCacheDirectory: Configuration.defaultCacheDirectory,
        persistenceURL: Configuration.defaultPersistenceURL
    )

    private static var defaultCacheDirectory: URL {
        let caches = FileManager.default.urls(for: .cachesDirectory, in: .userDomainMask).first!
        return caches.appendingPathComponent("Taix/Icons", isDirectory: true)
    }

    private static var defaultPersistenceURL: URL {
        let support = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first!
        let directory = support.appendingPathComponent("Taix", isDirectory: true)
        try? FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        return directory.appendingPathComponent("active_session.json")
    }
}
