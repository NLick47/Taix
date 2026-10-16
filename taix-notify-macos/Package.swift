// swift-tools-version:5.9
import PackageDescription

let package = Package(
    name: "TaixNotify",
    platforms: [.macOS(.v12)],
    products: [
        .executable(name: "TaixNotify", targets: ["TaixNotify"])
    ],
    targets: [
        .executableTarget(
            name: "TaixNotify",
            swiftSettings: [
                .unsafeFlags(["-parse-as-library"])
            ],
            linkerSettings: [
                .linkedFramework("UserNotifications")
            ]
        )
    ]
)
