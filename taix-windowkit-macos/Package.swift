// swift-tools-version:5.9
import PackageDescription

let package = Package(
    name: "TaixWindowKit",
    platforms: [.macOS(.v12)],
    products: [
        .library(name: "TaixWindowKit", type: .dynamic, targets: ["TaixWindowKit"])
    ],
    targets: [
        .target(
            name: "TaixWindowKit",
            linkerSettings: [
                .linkedFramework("AppKit")
            ]
        )
    ]
)
