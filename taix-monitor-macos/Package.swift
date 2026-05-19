// swift-tools-version:5.9
import PackageDescription

let package = Package(
    name: "TaixMonitor",
    platforms: [.macOS(.v13)],
    products: [
        .executable(name: "taix-monitor", targets: ["TaixMonitor"])
    ],
    targets: [
        .executableTarget(
            name: "TaixMonitor",
            linkerSettings: [
                .linkedFramework("AppKit"),
                .linkedFramework("CoreAudio"),
                .linkedFramework("IOKit"),
                .linkedFramework("CoreFoundation")
            ]
        )
    ]
)
