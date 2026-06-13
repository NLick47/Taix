import Foundation

actor EventBus {
    private var continuations: [UUID: AsyncStream<MonitorEvent>.Continuation] = [:]
    
    func subscribe() -> AsyncStream<MonitorEvent> {
        let id = UUID()
        return AsyncStream(bufferingPolicy: .bufferingNewest(100)) { continuation in
            self.continuations[id] = continuation
            continuation.onTermination = { [weak self] _ in
                Task {
                    await self?.removeContinuation(id: id)
                }
            }
        }
    }
    
    func publish(_ event: MonitorEvent) {
        for continuation in continuations.values {
            continuation.yield(event)
        }
    }
    
    private func removeContinuation(id: UUID) {
        continuations.removeValue(forKey: id)
    }
}
