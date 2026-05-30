import Foundation

enum TransportError: Error {
    case socketCreationFailed
    case connectionFailed
    case notConnected
    case encodingFailed
    case writeFailed
}

private struct LegacyMessage: Codable {
    let t: String
    let p: String?
    let d: Int64?
    let a: Int64?
    let f: String?
    let i: String?
    let desc: String?
    
    init(adapting event: MonitorEvent) {
        let timestamp = Int64(event.timestamp.timeIntervalSince1970)

        switch event.kind {
        case .foregroundChanged:
            self.t = "app"
            self.p = event.app?.name
            self.d = 0
            self.a = timestamp
            self.f = event.app?.executablePath
            self.i = event.app?.iconPath
            self.desc = event.app?.displayName

        case .sessionTick, .sessionEnded:
            self.t = "app"
            self.p = event.app?.name
            self.d = event.duration.map { Int64($0) }
            self.a = timestamp
            self.f = event.app?.executablePath
            self.i = event.app?.iconPath
            self.desc = event.app?.displayName

        case .idleDetected:
            self.t = "sleep"
            self.p = nil
            self.d = nil
            self.a = nil
            self.f = nil
            self.i = nil
            self.desc = nil

        case .activityResumed:
            self.t = "wake"
            self.p = nil
            self.d = nil
            self.a = nil
            self.f = nil
            self.i = nil
            self.desc = nil
        }
    }
}

actor TransportClient {
    private let socketPath: String
    private var fileDescriptor: Int32?
    private var messageContinuation: AsyncStream<Data>.Continuation?
    private var sendTask: Task<Void, Never>?
    
    init(socketPath: String) {
        self.socketPath = socketPath
    }
    
    func start() {
        var continuation: AsyncStream<Data>.Continuation!
        let stream = AsyncStream<Data> { cont in
            continuation = cont
        }
        self.messageContinuation = continuation
        
        sendTask = Task { [weak self] in
            guard let self else { return }
            for await message in stream {
                guard !Task.isCancelled else { break }
                await self.sendWithRetry(message)
            }
        }
    }
    
    func stop() {
        messageContinuation?.finish()
        sendTask?.cancel()
        sendTask = nil
        disconnect()
    }
    
    func send(_ event: MonitorEvent) {
        let legacy = LegacyMessage(adapting: event)
        let encoder = JSONEncoder()
        encoder.outputFormatting = .sortedKeys
        
        guard let data = try? encoder.encode(legacy) else {
            Logger.error("Transport encoding failed")
            return
        }
        var payload = data
        payload.append(10) // newline
        
        if let json = String(data: data, encoding: .utf8) {
            Logger.debug("Transport enqueue: \(json)")
        }
        
        messageContinuation?.yield(payload)
    }
    
    private func sendWithRetry(_ message: Data) async {
        let maxAttempts = 10
        
        for attempt in 1...maxAttempts {
            if fileDescriptor == nil {
                do {
                    try connect()
                } catch {
                    Logger.error("Transport connect failed (attempt \(attempt)): \(error)")
                    if attempt < maxAttempts {
                        try? await Task.sleep(for: .milliseconds(500 * attempt))
                    }
                    continue
                }
            }
            
            do {
                try sendRaw(message)
                Logger.debug("Transport sent \(message.count) bytes")
                return
            } catch {
                Logger.error("Transport send failed (attempt \(attempt)): \(error)")
                disconnect()
                if attempt < maxAttempts {
                    try? await Task.sleep(for: .milliseconds(500 * attempt))
                }
            }
        }
        
        Logger.error("Dropped message after \(maxAttempts) retries")
    }
    
    private func sendRaw(_ data: Data) throws {
        guard let fd = fileDescriptor else {
            throw TransportError.notConnected
        }
        
        let written = data.withUnsafeBytes { buffer in
            guard let baseAddress = buffer.baseAddress else { return -1 }
            return Darwin.send(fd, baseAddress, buffer.count, 0)
        }
        
        guard written == data.count else {
            throw TransportError.writeFailed
        }
    }
    
    private func connect() throws {
        Logger.info("Connecting to Unix socket: \(socketPath)")
        
        var address = sockaddr_un()
        address.sun_family = sa_family_t(AF_UNIX)
        
        let path = socketPath.utf8CString
        guard path.count <= MemoryLayout.size(ofValue: address.sun_path) else {
            Logger.error("Socket path too long: \(socketPath)")
            throw TransportError.connectionFailed
        }
        
        path.withUnsafeBufferPointer { source in
            withUnsafeMutablePointer(to: &address.sun_path.0) { destination in
                destination.withMemoryRebound(to: CChar.self, capacity: path.count) { ptr in
                    memcpy(ptr, source.baseAddress!, path.count - 1)
                }
            }
        }
        
        let fd = socket(AF_UNIX, SOCK_STREAM, 0)
        guard fd >= 0 else {
            Logger.error("Socket creation failed")
            throw TransportError.socketCreationFailed
        }
        
        let result = withUnsafePointer(to: &address) { ptr in
            ptr.withMemoryRebound(to: sockaddr.self, capacity: 1) { addrPtr in
                Darwin.connect(fd, addrPtr, socklen_t(MemoryLayout<sockaddr_un>.size))
            }
        }
        
        guard result == 0 else {
            close(fd)
            Logger.error("Socket connection failed to \(socketPath)")
            throw TransportError.connectionFailed
        }
        
        self.fileDescriptor = fd
        Logger.info("Socket connected successfully")
    }
    
    private func disconnect() {
        if let fd = fileDescriptor {
            close(fd)
            fileDescriptor = nil
            Logger.info("Socket disconnected")
        }
    }
}
