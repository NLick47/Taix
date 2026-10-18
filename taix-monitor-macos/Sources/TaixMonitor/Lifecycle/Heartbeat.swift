import Foundation

/// M4 数据总线心跳（docs/plugin-plan/04-databus.md §4）：
/// 5s 一拍上报当前前台应用；idleDetected（系统空闲→睡眠语义）期间停发并上报
/// `{"t":"sleep"}`，activityResumed / 回到前台后以唤醒时刻重新计会话起点并上报 `{"t":"wake"}`。
/// 与 Windows monitor 共用同一 JSON：`{"t":"current","p","f","i","s"}`。
actor Heartbeat {
    private let transport: TransportClient
    private let appProvider: () async -> AppInfo?
    private var ticker: Task<Void, Never>?
    private var listener: Task<Void, Never>?
    private var isIdle = false
    private var since = Date()
    private var currentName: String?
    private var sessionId: String?
    private var lastTrackedApp: AppInfo?

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
                    if await self.setSleeping(true) {
                        await self.transport.sendLifecycle(sleeping: true)
                    }
                case .activityResumed:
                    if await self.setSleeping(false) {
                        await self.transport.sendLifecycle(sleeping: false)
                    }
                case .foregroundChanged, .sessionStarted:
                    // 锁屏/屏保后回到前台不会触发 activityResumed，这里兜底恢复心跳
                    if await self.setSleeping(false) {
                        await self.transport.sendLifecycle(sleeping: false)
                    }
                    if let app = event.app {
                        await self.publish(
                            app: app,
                            since: event.timestamp,
                            sessionId: event.sessionId ?? UUID().uuidString
                        )
                    }
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

    @discardableResult
    private func setSleeping(_ sleeping: Bool) -> Bool {
        guard isIdle != sleeping else { return false }
        isIdle = sleeping
        if sleeping {
            Logger.debug("Heartbeat paused (idle)")
            sessionId = nil
        } else {
            since = Date() // 唤醒即新会话
            sessionId = nil // 等 sessionStarted / foregroundChanged 给新 id
            Logger.debug("Heartbeat resumed")
        }
        return true
    }

    /// 心跳上报的目标应用：`provided`（当前可统计前台）为空时沿用 `last`。
    /// 锁屏/屏保时 appProvider 返回 nil，编码后消息不带 `p` 字段，server 会直接
    /// 丢弃（既不更新应用也不刷新存活时间），sleep_watch=false 下 90s 后会被
    /// 看门狗误判空闲。沿用上一个应用与 Windows monitor 的 last_app 行为对齐
    static func heartbeatTarget(provided: AppInfo?, last: AppInfo?) -> AppInfo? {
        provided ?? last
    }

    private func publish(app: AppInfo, since: Date, sessionId: String) async {
        lastTrackedApp = app
        currentName = app.name
        self.since = since
        self.sessionId = sessionId
        await transport.sendCurrent(app: app, since: since, sessionId: sessionId)
    }

    private func beat() async {
        guard !isIdle else { return }
        let provided = await appProvider()
        if let provided {
            lastTrackedApp = provided
        }
        let app = Self.heartbeatTarget(provided: provided, last: lastTrackedApp)
        guard let app, let sessionId else { return }
        if app.name != currentName {
            currentName = app.name
            since = Date() // 应用切换即新会话起点
            self.sessionId = UUID().uuidString
        }
        await transport.sendCurrent(
            app: app,
            since: since,
            sessionId: self.sessionId ?? sessionId
        )
    }
}
