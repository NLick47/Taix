import Foundation

enum SingleInstance {
    private static var lockFile: FileHandle?
    private static var lockPath: String = "/tmp/taix-monitor.lock"

    static func tryAcquire() -> Bool {
        let fileManager = FileManager.default

        if !fileManager.fileExists(atPath: lockPath) {
            fileManager.createFile(atPath: lockPath, contents: nil, attributes: nil)
        }

        guard let file = FileHandle(forWritingAtPath: lockPath) else {
            Logger.error("Failed to open lock file: \(lockPath)")
            return false
        }

        let fd = file.fileDescriptor
        let result = flock(fd, LOCK_EX | LOCK_NB)

        if result == 0 {
            lockFile = file
            Logger.info("Single instance lock acquired")
            return true
        } else {
            Logger.error("Another instance is already running (lock file: \(lockPath))")
            file.closeFile()
            return false
        }
    }

    static func release() {
        if let file = lockFile {
            flock(file.fileDescriptor, LOCK_UN)
            file.closeFile()
            lockFile = nil
            Logger.info("Single instance lock released")
        }
    }
}
