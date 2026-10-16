import Foundation

@main
struct SleepWatchSelfTest {

    static func makeConfig(sleepWatch: Bool) -> MonitorConfig {
        MonitorConfig(
            inactiveThresholdSecs: 900,
            maxSoundDurationSecs: 7200,
            sleepWatch: sleepWatch
        )
    }

    static func subscribe(_ bus: EventBus) async -> (Task<Void, Never>, Collector) {
        let stream = await bus.subscribe()
        let collector = Collector()
        let task = Task {
            for await event in stream {
                await collector.add(event)
            }
        }
        return (task, collector)
    }

    /// 轮询等待事件数达到 expected，超时返回实际数量
    static func waitCount(
        _ collector: Collector,
        _ expected: Int,
        timeout: TimeInterval = 1.0
    ) async -> Int {
        let deadline = Date().addingTimeInterval(timeout)
        while Date() < deadline {
            let count = await collector.count()
            if count >= expected { return count }
            try? await Task.sleep(nanoseconds: 20_000_000)
        }
        return await collector.count()
    }

    static func main() async {
        var failures = 0
        func check(_ cond: Bool, _ message: String) {
            if cond {
                print("  \u{1B}[32m✓\u{1B}[0m \(message)")
            } else {
                print("  \u{1B}[31m✗\u{1B}[0m \(message)")
                failures += 1
            }
        }

        print("macOS monitor 睡眠监测关闭语义自检")

        print("1) sleepWatch=false：系统/显示器休眠被忽略")
        do {
            let bus = EventBus()
            let detector = IdleDetector(eventBus: bus, config: makeConfig(sleepWatch: false))
            let (task, collector) = await subscribe(bus)
            await detector.handleSystemSleep()
            try? await Task.sleep(nanoseconds: 250_000_000)
            let count = await collector.count()
            let isIdle = await detector.isIdle
            task.cancel()
            check(count == 0, "休眠不发 idleDetected（对齐 Windows）")
            check(!isIdle, "不进入空闲态")
        }

        print("2) sleepWatch=true：保持原行为（休眠即暂停）")
        do {
            let bus = EventBus()
            let detector = IdleDetector(eventBus: bus, config: makeConfig(sleepWatch: true))
            let (task, collector) = await subscribe(bus)
            await detector.handleSystemSleep()
            let count = await waitCount(collector, 1)
            let isIdle = await detector.isIdle
            let first = await collector.first()
            task.cancel()
            check(count == 1, "休眠发一条 idleDetected")
            check(isIdle, "进入空闲态")
            check(first?.kind == .idleDetected, "事件类型为 idleDetected")
        }

        print("3) 重复休眠通知（willSleep + screensDidSleep）只生效一次")
        do {
            let bus = EventBus()
            let detector = IdleDetector(eventBus: bus, config: makeConfig(sleepWatch: true))
            let (task, collector) = await subscribe(bus)
            await detector.handleSystemSleep()
            await detector.handleSystemSleep()
            _ = await waitCount(collector, 1)
            try? await Task.sleep(nanoseconds: 150_000_000)
            let count = await collector.count()
            task.cancel()
            check(count == 1, "重复通知只发一次")
        }

        print("4) AppObserver：系统界面（锁屏/屏保）接管前台的真值表")
        check(
            AppObserver.shouldSuspendForFrontmost(
                bundleIdentifier: "com.apple.screensaver", sleepWatch: true
            ),
            "锁屏/屏保 + 默认开启 → 结束会话"
        )
        check(
            !AppObserver.shouldSuspendForFrontmost(
                bundleIdentifier: "com.apple.screensaver", sleepWatch: false
            ),
            "锁屏/屏保 + 关闭监测 → 保持统计"
        )
        check(
            AppObserver.shouldSuspendForFrontmost(
                bundleIdentifier: "com.apple.loginwindow", sleepWatch: true
            ),
            "登录窗 + 默认开启 → 结束会话"
        )
        check(
            !AppObserver.shouldSuspendForFrontmost(
                bundleIdentifier: "com.google.Chrome", sleepWatch: true
            ),
            "普通应用不触发"
        )
        check(
            !AppObserver.shouldSuspendForFrontmost(bundleIdentifier: nil, sleepWatch: true),
            "无 bundle id 不触发"
        )

        print("5) Heartbeat：锁屏期间沿用最后一个可统计应用（对齐 Windows last_app）")
        let appA = AppInfo(
            name: "A", bundleIdentifier: "a", executablePath: "/a",
            iconPath: nil, displayName: nil
        )
        let appB = AppInfo(
            name: "B", bundleIdentifier: "b", executablePath: "/b",
            iconPath: nil, displayName: nil
        )
        check(
            Heartbeat.heartbeatTarget(provided: nil, last: appA)?.name == "A",
            "frontmost 为系统界面（nil）时沿用上一个应用，p 不为 null"
        )
        check(
            Heartbeat.heartbeatTarget(provided: appB, last: appA)?.name == "B",
            "正常前台时用当前应用"
        )
        check(
            Heartbeat.heartbeatTarget(provided: nil, last: nil) == nil,
            "从未有过可统计应用时保持 nil"
        )

        if failures == 0 {
            print("\n\u{1B}[32mPASS\u{1B}[0m sleepWatch 语义自检全部通过")
        } else {
            print("\n\u{1B}[31mFAIL\u{1B}[0m \(failures) 项不符合预期")
        }
        exit(failures == 0 ? 0 : 1)
    }
}


actor Collector {
    private var events: [MonitorEvent] = []
    func add(_ event: MonitorEvent) { events.append(event) }
    func count() -> Int { events.count }
    func first() -> MonitorEvent? { events.first }
}
