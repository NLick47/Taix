import Foundation

@main
struct TaixMonitor {
    static func main() async {
        let runner = Runner(configuration: .default)
        
        let sigintSource = DispatchSource.makeSignalSource(signal: SIGINT, queue: .global())
        sigintSource.setEventHandler {
            Task {
                await runner.shutdown()
                exit(0)
            }
        }
        signal(SIGINT, SIG_IGN)
        sigintSource.resume()
        
        let sigtermSource = DispatchSource.makeSignalSource(signal: SIGTERM, queue: .global())
        sigtermSource.setEventHandler {
            Task {
                await runner.shutdown()
                exit(0)
            }
        }
        signal(SIGTERM, SIG_IGN)
        sigtermSource.resume()
        
        await runner.start()
    }
}
