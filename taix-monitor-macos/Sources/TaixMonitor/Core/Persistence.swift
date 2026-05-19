import Foundation

actor Persistence {
    private let url: URL
    
    init(url: URL) {
        self.url = url
    }
    
    func save(snapshot: SessionSnapshot) async {
        do {
            let data = try JSONEncoder().encode(snapshot)
            try data.write(to: url, options: .atomic)
            Logger.debug("Session persisted: \(snapshot.bundleIdentifier) @ \(snapshot.accumulatedSeconds)s")
        } catch {
            Logger.error("Failed to persist session: \(error)")
        }
    }
    
    func load() async -> SessionSnapshot? {
        guard let data = try? Data(contentsOf: url) else { return nil }
        do {
            let snapshot = try JSONDecoder().decode(SessionSnapshot.self, from: data)
            Logger.info("Session restored: \(snapshot.bundleIdentifier) @ \(snapshot.accumulatedSeconds)s")
            return snapshot
        } catch {
            Logger.error("Failed to decode persisted session: \(error)")
            return nil
        }
    }
    
    func clear() async {
        do {
            try FileManager.default.removeItem(at: url)
            Logger.debug("Session persistence cleared")
        } catch {
            Logger.error("Failed to clear persistence: \(error)")
        }
    }
}
