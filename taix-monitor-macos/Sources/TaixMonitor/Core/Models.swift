import Foundation

struct AppInfo: Codable, Sendable {
    let name: String
    let bundleIdentifier: String?
    let executablePath: String
    let iconPath: String?
    let displayName: String?
}

struct WindowInfo: Codable, Sendable {
    let title: String
    let windowClass: String
}

struct SessionSnapshot: Codable, Sendable {
    let bundleIdentifier: String
    let executablePath: String
    let startTime: Date
    let accumulatedSeconds: TimeInterval
    let appName: String?
}

struct MonitorEvent: Codable, Sendable {
    let kind: EventKind
    let timestamp: Date
    let app: AppInfo?
    let duration: TimeInterval?
    let window: WindowInfo?

    enum EventKind: String, Codable, Sendable {
        case foregroundChanged
        case sessionTick
        case sessionEnded
        case idleDetected
        case activityResumed
    }
}

struct MonitorConfig: Sendable {
    let inactiveThresholdSecs: TimeInterval
    let maxSoundDurationSecs: TimeInterval
    let sleepWatch: Bool

    static let `default` = MonitorConfig(
        inactiveThresholdSecs: 900,
        maxSoundDurationSecs: 7200,
        sleepWatch: true
    )
}
