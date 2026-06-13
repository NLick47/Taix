// swift-tools-version:5.9
import PackageDescription

let package = Package(
    name: "TaixMonitor",
    platforms: [.macOS(.v12)],
    products: [
        .executable(name: "taix-monitor", targets: ["TaixMonitor"])
    ],
    targets: [
        .executableTarget(
            name: "TaixMonitor",
            swiftSettings: [
                .unsafeFlags(["-parse-as-library"])
            ],
            linkerSettings: [
                .linkedFramework("AppKit"),
                .linkedFramework("CoreAudio"),
                .linkedFramework("IOKit"),
                .linkedFramework("CoreFoundation")
            ]
        )
    ]
)
