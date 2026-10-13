import Foundation

/// M4 数据总线心跳（docs/plugin-plan/04-databus.md §4）：
/// 5s 一拍上报当前前台应用；idleDetected（系统空闲→睡眠语义）期间停发，
/// activityResumed 后以唤醒时刻重新计会话起点。
/// 与 Windows monitor 共用同一 JSON：`{"t":"current","p","f","i","s"}`。
actor Heartbeat {
    private let transport: TransportClient
    private let appProvider: () async -> AppInfo?
    private var ticker: Task<Void, Never>?
    private var listener: Task<Void, Never>?
    private var isIdle = false
    private var since = Date()
    private var currentName: String?

    init(transport: TransportClient, appProvider: @escaping () async -> AppInfo?) {
        self.transport = transport
        self.appProvider = appProvider
    }

    func start(events: AsyncStream<MonitorEvent>) {
        guard ticker == nil else { return }

        listener = Task { [weak self] in
            for await event in events {
                guard let self else { break }
                switch event.kind {
                case .idleDetected:
                    await self.setSleeping(true)
                case .activityResumed:
                    await self.setSleeping(false)
                default:
                    break
                }
            }
        }

        ticker = Task { [weak self] in
            while !Task.isCancelled {
                try? await Task.sleep(nanoseconds: 5_000_000_000)
                guard let self else { break }
                await self.beat()
            }
        }
        Logger.info("Heartbeat started (5s)")
    }

    func stop() {
        ticker?.cancel()
        ticker = nil
        listener?.cancel()
        listener = nil
    }

    private func setSleeping(_ sleeping: Bool) {
        if sleeping && !isIdle {
            Logger.debug("Heartbeat paused (idle)")
        }
        if !sleeping && isIdle {
            since = Date() // 唤醒即新会话
            Logger.debug("Heartbeat resumed")
        }
        isIdle = sleeping
    }

    private func beat() async {
        guard !isIdle else { return }
        let app = await appProvider()
        if app?.name != currentName {
            currentName = app?.name
            since = Date() // 应用切换即新会话起点
        }
        await transport.sendCurrent(app: app, since: since)
    }
}
