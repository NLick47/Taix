import Foundation

@main
struct TaixMonitor {
    static func main() async {
        setvbuf(stdout, nil, _IOLBF, 0)

        let config = parseArgs()
        guard SingleInstance.tryAcquire(path: config.lockPath) else {
            Logger.error("Another instance is already running, exiting...")
            exit(1)
        }

        let runner = Runner(configuration: config)

        let sigintSource = DispatchSource.makeSignalSource(signal: SIGINT, queue: .global())
        sigintSource.setEventHandler {
            Task {
                await runner.shutdown()
                exit(0)
            }
        }
        signal(SIGINT, SIG_IGN)
        sigintSource.resume()

        let sigtermSource = DispatchSource.makeSignalSource(signal: SIGTERM, queue: .global())
        sigtermSource.setEventHandler {
            Task {
                await runner.shutdown()
                exit(0)
            }
        }
        signal(SIGTERM, SIG_IGN)
        sigtermSource.resume()

        await runner.start()
    }

    static func parseArgs() -> Configuration {
        let args = CommandLine.arguments
        var monitorConfig = MonitorConfig.default
        var socketPath = Configuration.default.socketPath
        var dataDir: URL?
        var lockPath: String?

        var i = 1
        while i < args.count {
            let arg = args[i]

            if arg == "--socket-path" && i + 1 < args.count {
                socketPath = args[i + 1]
                i += 2
            } else if arg == "--data-dir" && i + 1 < args.count {
                dataDir = URL(fileURLWithPath: args[i + 1], isDirectory: true)
                i += 2
            } else if arg == "--lock-path" && i + 1 < args.count {
                lockPath = args[i + 1]
                i += 2
            } else if arg == "--inactive-threshold" && i + 1 < args.count {
                if let mins = Double(args[i + 1]), mins >= 1 && mins <= 60 {
                    monitorConfig = MonitorConfig(
                        inactiveThresholdSecs: mins * 60,
                        maxSoundDurationSecs: monitorConfig.maxSoundDurationSecs,
                        sleepWatch: monitorConfig.sleepWatch
                    )
                }
                i += 2
            } else if arg == "--max-sound-duration" && i + 1 < args.count {
                if let mins = Double(args[i + 1]), mins >= 15 && mins <= 480 && mins.truncatingRemainder(dividingBy: 15) == 0 {
                    monitorConfig = MonitorConfig(
                        inactiveThresholdSecs: monitorConfig.inactiveThresholdSecs,
                        maxSoundDurationSecs: mins * 60,
                        sleepWatch: monitorConfig.sleepWatch
                    )
                }
                i += 2
            } else if arg == "--sleep-watch" && i + 1 < args.count {
                let value = args[i + 1].lowercased()
                monitorConfig = MonitorConfig(
                    inactiveThresholdSecs: monitorConfig.inactiveThresholdSecs,
                    maxSoundDurationSecs: monitorConfig.maxSoundDurationSecs,
                    sleepWatch: value == "true"
                )
                i += 2
            } else {
                i += 1
            }
        }

        if let dataDir {
            return Configuration(
                socketPath: socketPath,
                tickInterval: 60,
                monitorConfig: monitorConfig,
                iconCacheDirectory: dataDir.appendingPathComponent("AppIcons", isDirectory: true),
                persistenceURL: dataDir.appendingPathComponent("active_session.json"),
                lockPath: lockPath
            )
        }
        return Configuration(
            socketPath: socketPath,
            tickInterval: 60,
            monitorConfig: monitorConfig,
            iconCacheDirectory: Configuration.defaultIconCacheDirectory,
            persistenceURL: Configuration.defaultPersistenceURL,
            lockPath: lockPath
        )
    }
}
