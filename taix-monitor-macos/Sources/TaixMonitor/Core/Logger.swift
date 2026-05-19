import Foundation

enum Logger {
    private static let dateFormatter: DateFormatter = {
        let formatter = DateFormatter()
        formatter.dateFormat = "yyyy-MM-dd HH:mm:ss.SSS"
        return formatter
    }()
    
    static func info(_ message: String) {
        let timestamp = dateFormatter.string(from: Date())
        print("[\(timestamp)] [INFO] \(message)")
    }
    
    static func debug(_ message: String) {
        let timestamp = dateFormatter.string(from: Date())
        print("[\(timestamp)] [DEBUG] \(message)")
    }
    
    static func error(_ message: String) {
        let timestamp = dateFormatter.string(from: Date())
        print("[\(timestamp)] [ERROR] \(message)")
    }
}
