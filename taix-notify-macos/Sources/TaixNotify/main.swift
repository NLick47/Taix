import Foundation
import UserNotifications

@main
struct TaixNotify {
    struct Args {
        var title = ""
        var body = ""
        var sound: String?
    }

    static let usage =
        "usage: TaixNotify --title <标题> [--body <正文>] [--sound <default|系统声名>] | --status | --request"

    static func parse(_ argv: [String]) -> Args? {
        var args = Args()
        var i = 1
        while i < argv.count {
            guard i + 1 < argv.count else { return nil }
            let key = argv[i]
            let value = argv[i + 1]
            switch key {
            case "--title": args.title = value
            case "--body": args.body = value
            case "--sound": args.sound = value
            default: return nil
            }
            i += 2
        }
        return args.title.isEmpty ? nil : args
    }

    static func main() {
        let argv = CommandLine.arguments
        if argv.contains("--help") || argv.contains("-h") {
            print(usage)
            exit(0)
        }
        if argv.contains("--status") {
            guard Bundle.main.bundlePath.hasSuffix(".app") else {
                print("unavailable")
                exit(2)
            }
            UNUserNotificationCenter.current().getNotificationSettings { settings in
                switch settings.authorizationStatus {
                case .authorized: print("authorized")
                case .denied: print("denied")
                case .provisional: print("provisional")
                case .notDetermined: print("notDetermined")
                default: print("unknown")
                }
                exit(0)
            }
            dispatchMain()
        }
        if argv.contains("--request") {
            // 主动申请权限：插件启动/登录时调用，首次询问不再等到第一次提醒才弹。
            // 已有结论（允许/拒绝）时是空操作；给用户 10 分钟点击时间
            guard Bundle.main.bundlePath.hasSuffix(".app") else { exit(2) }
            UNUserNotificationCenter.current().getNotificationSettings { settings in
                switch settings.authorizationStatus {
                case .notDetermined:
                    DispatchQueue.global().asyncAfter(deadline: .now() + 600) { exit(3) }
                    UNUserNotificationCenter.current().requestAuthorization(options: [.alert, .sound]) { _, _ in exit(0) }
                default:
                    exit(0)
                }
            }
            dispatchMain()
        }
        guard let args = parse(argv) else {
            FileHandle.standardError.write(Data((usage + "\n").utf8))
            exit(64)
        }

        // 不在 app bundle 里时 UNUserNotificationCenter 会崩；干净地退出 2 让调用方回退
        guard Bundle.main.bundlePath.hasSuffix(".app") else {
            FileHandle.standardError.write(Data("TaixNotify 未运行在 app bundle 内\n".utf8))
            exit(2)
        }

        let center = UNUserNotificationCenter.current()
        center.getNotificationSettings { settings in
            // 未授权时留 60 秒等用户点「允许」，超时退出码 4（确定没发出去 → 调用方回退）；
            // 已授权时 8 秒兜底退出码 3（按已尝试处理，避免重复推）
            let notDetermined = settings.authorizationStatus == .notDetermined
            let wait: TimeInterval = notDetermined ? 60 : 8
            DispatchQueue.global().asyncAfter(deadline: .now() + wait) { exit(notDetermined ? 4 : 3) }

            switch settings.authorizationStatus {
            case .authorized, .provisional:
                deliver(center, args)
            case .denied:
                exit(2)
            default:
                center.requestAuthorization(options: [.alert, .sound]) { granted, _ in
                    if granted {
                        deliver(center, args)
                    } else {
                        exit(2)
                    }
                }
            }
        }
        dispatchMain()
    }

    static func deliver(_ center: UNUserNotificationCenter, _ args: Args) {
        let content = UNMutableNotificationContent()
        content.title = args.title
        if !args.body.isEmpty {
            content.body = args.body
        }
        if let sound = args.sound {
            content.sound = sound == "default" ? .default : UNNotificationSound(named: UNNotificationSoundName(sound))
        }
        let request = UNNotificationRequest(
            identifier: UUID().uuidString,
            content: content,
            trigger: nil
        )
        center.add(request) { error in
            if let error {
                FileHandle.standardError.write(Data("提交通知失败: \(error)\n".utf8))
                exit(1)
            }
            exit(0)
        }
    }
}
