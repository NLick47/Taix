import Foundation
import ImageIO
import UniformTypeIdentifiers

actor IconExtractor {
    private let cacheDirectory: URL

    init(cacheDirectory: URL) {
        self.cacheDirectory = cacheDirectory
        try? FileManager.default.createDirectory(
            at: cacheDirectory,
            withIntermediateDirectories: true
        )
    }

    func iconPath(for executablePath: String) -> String? {
        guard FileManager.default.fileExists(atPath: executablePath) else {
            return nil
        }

        let hash = deterministicHash(for: executablePath)
        let iconURL = cacheDirectory.appendingPathComponent("\(hash).png")

        if FileManager.default.fileExists(atPath: iconURL.path) {
            return iconURL.path
        }

        guard let icnsPath = findIconPath(for: executablePath),
              let cgImage = loadIcon(from: icnsPath) else {
            Logger.debug("Failed to extract icon for: \(executablePath)")
            return nil
        }

        guard let pngData = createPNGData(from: cgImage) else {
            Logger.debug("Failed to encode icon for: \(executablePath)")
            return nil
        }

        do {
            try pngData.write(to: iconURL)
            Logger.debug("Icon extracted: \(executablePath) → \(iconURL.path)")
            return iconURL.path
        } catch {
            Logger.error("Icon write failed: \(error)")
            return nil
        }
    }

    private func findIconPath(for executablePath: String) -> String? {
        if executablePath.hasSuffix(".app") {
            let infoPlistPath = (executablePath as NSString).appendingPathComponent("Contents/Info.plist")
            guard let info = NSDictionary(contentsOf: URL(fileURLWithPath: infoPlistPath)),
                  let iconFileName = info["CFBundleIconFile"] as? String else {
                let defaultIconPath = (executablePath as NSString).appendingPathComponent("Contents/Resources/AppIcon.icns")
                if FileManager.default.fileExists(atPath: defaultIconPath) {
                    return defaultIconPath
                }
                return nil
            }

            let iconName = iconFileName.hasSuffix(".icns") ? iconFileName : "\(iconFileName).icns"
            let iconPath = (executablePath as NSString).appendingPathComponent("Contents/Resources/\(iconName)")

            if FileManager.default.fileExists(atPath: iconPath) {
                return iconPath
            }
        }

        return nil
    }

    private func loadIcon(from path: String) -> CGImage? {
        let url = URL(fileURLWithPath: path)
        guard let source = CGImageSourceCreateWithURL(url as CFURL, nil) else {
            return nil
        }

        let options: [CFString: Any] = [
            kCGImageSourceCreateThumbnailFromImageIfAbsent: true,
            kCGImageSourceThumbnailMaxPixelSize: 128,
            kCGImageSourceCreateThumbnailWithTransform: true
        ]

        return CGImageSourceCreateThumbnailAtIndex(source, 0, options as CFDictionary)
    }

    private func createPNGData(from cgImage: CGImage) -> Data? {
        let data = NSMutableData()
        guard let destination = CGImageDestinationCreateWithData(
            data,
            UTType.png.identifier as CFString,
            1,
            nil
        ) else {
            return nil
        }

        CGImageDestinationAddImage(destination, cgImage, nil)
        CGImageDestinationFinalize(destination)

        return data as Data
    }

    private func deterministicHash(for string: String) -> String {
        var hash = UInt64(14695981039346656037)
        for byte in string.utf8 {
            hash ^= UInt64(byte)
            hash = hash &* 1099511628211
        }
        return String(format: "%016X", hash)
    }
}
