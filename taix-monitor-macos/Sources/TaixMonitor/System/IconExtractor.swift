import AppKit
import Foundation

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

        let image = NSWorkspace.shared.icon(forFile: executablePath)

        image.size = NSSize(width: 128, height: 128)

        guard let tiffData = image.tiffRepresentation,
              let bitmap = NSBitmapImageRep(data: tiffData),
              let pngData = bitmap.representation(using: .png, properties: [:]) else {
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

    private func deterministicHash(for string: String) -> String {
        var hash = UInt64(14695981039346656037)
        for byte in string.utf8 {
            hash ^= UInt64(byte)
            hash = hash &* 1099511628211
        }
        return String(format: "%016X", hash)
    }
}
