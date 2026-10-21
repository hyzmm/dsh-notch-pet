import Darwin
import Foundation

protocol SocketClientDelegate: AnyObject {
    func socketClient(_ client: SocketClient, didChangeConnected connected: Bool)
    func socketClient(_ client: SocketClient, didReceive payload: PetPayload)
    func socketClientDidGiveUp(_ client: SocketClient)
}

final class SocketClient {
    weak var delegate: SocketClientDelegate?

    private let path: String
    private let queue = DispatchQueue(label: "local.dsh.notchpet.socket")
    private var descriptor: Int32 = -1
    private var source: DispatchSourceRead?
    private var reconnectWork: DispatchWorkItem?
    private var watchdog: DispatchSourceTimer?
    private var buffer = Data()
    private var connected = false
    private var stopped = false
    private var disconnectedSince = Date()
    private var lastMessage = Date()
    private var hasConnectedOnce = false

    private let decoder = JSONDecoder()

    init(path: String) {
        self.path = path
    }

    func start() {
        queue.async { self.connect() }
        startWatchdog()
    }

    func stop() {
        queue.async {
            self.stopped = true
            self.reconnectWork?.cancel()
            self.watchdog?.cancel()
            self.close(notify: false)
        }
    }

    private func startWatchdog() {
        let timer = DispatchSource.makeTimerSource(queue: queue)
        timer.schedule(deadline: .now() + 3, repeating: 3)
        timer.setEventHandler { [weak self] in
            guard let self, !self.stopped else { return }
            if self.connected, Date().timeIntervalSince(self.lastMessage) > 16 {
                AppLog.write("socket heartbeat timeout")
                self.close(notify: true)
                self.scheduleReconnect(after: 0.8)
            }
            if !self.connected,
               self.hasConnectedOnce,
               Date().timeIntervalSince(self.disconnectedSince) > 20 {
                AppLog.write("socket gone for 20s, exiting")
                self.stopped = true
                DispatchQueue.main.async {
                    self.delegate?.socketClientDidGiveUp(self)
                }
            }
        }
        timer.resume()
        watchdog = timer
    }

    private func connect() {
        guard !stopped, !connected else { return }
        let fd = socket(AF_UNIX, SOCK_STREAM, 0)
        guard fd >= 0 else {
            scheduleReconnect(after: 1.2)
            return
        }

        var address = sockaddr_un()
        address.sun_family = sa_family_t(AF_UNIX)
        let maxLength = MemoryLayout.size(ofValue: address.sun_path)
        guard path.utf8.count < maxLength else {
            AppLog.write("socket path too long")
            Darwin.close(fd)
            return
        }
        _ = path.withCString { pointer in
            withUnsafeMutablePointer(to: &address.sun_path) { sunPath in
                sunPath.withMemoryRebound(to: CChar.self, capacity: maxLength) { destination in
                    strncpy(destination, pointer, maxLength - 1)
                }
            }
        }

        let result = withUnsafePointer(to: &address) { pointer in
            pointer.withMemoryRebound(to: sockaddr.self, capacity: 1) {
                Darwin.connect(fd, $0, socklen_t(MemoryLayout<sockaddr_un>.size))
            }
        }

        guard result == 0 else {
            Darwin.close(fd)
            if !hasConnectedOnce {
                AppLog.write("waiting for host socket")
            }
            scheduleReconnect(after: 1.1)
            return
        }

        let flags = fcntl(fd, F_GETFL, 0)
        _ = fcntl(fd, F_SETFL, flags | O_NONBLOCK)
        descriptor = fd
        connected = true
        disconnectedSince = Date()
        lastMessage = Date()
        hasConnectedOnce = true
        buffer.removeAll(keepingCapacity: true)
        AppLog.write("socket connected \(path)")

        let readSource = DispatchSource.makeReadSource(fileDescriptor: fd, queue: queue)
        readSource.setEventHandler { [weak self] in
            self?.readAvailable()
        }
        readSource.setCancelHandler {
            Darwin.close(fd)
        }
        readSource.resume()
        source = readSource

        DispatchQueue.main.async {
            self.delegate?.socketClient(self, didChangeConnected: true)
        }
    }

    private func readAvailable() {
        guard descriptor >= 0 else { return }
        var chunk = [UInt8](repeating: 0, count: 65536)
        let count = read(descriptor, &chunk, chunk.count)
        if count > 0 {
            lastMessage = Date()
            buffer.append(chunk, count: count)
            parseLines()
            return
        }
        if count == 0 {
            AppLog.write("socket closed by host")
            close(notify: true)
            scheduleReconnect(after: 1.2)
            return
        }
        if errno != EAGAIN && errno != EWOULDBLOCK && errno != EINTR {
            AppLog.write("socket read error \(errno)")
            close(notify: true)
            scheduleReconnect(after: 1.2)
        }
    }

    private func parseLines() {
        while let newline = buffer.firstIndex(of: 0x0A) {
            let line = buffer[..<newline]
            buffer.removeSubrange(...newline)
            guard !line.isEmpty else { continue }
            do {
                let payload = try decoder.decode(PetPayload.self, from: Data(line))
                DispatchQueue.main.async {
                    self.delegate?.socketClient(self, didReceive: payload)
                }
            } catch {
                AppLog.write("payload decode failed: \(error.localizedDescription)")
            }
        }
        if buffer.count > 2_000_000 {
            AppLog.write("socket buffer overflow, dropping")
            buffer.removeAll(keepingCapacity: true)
        }
    }

    private func close(notify: Bool) {
        if let source {
            source.cancel()
            self.source = nil
        } else if descriptor >= 0 {
            Darwin.close(descriptor)
        }
        descriptor = -1
        if connected {
            connected = false
            disconnectedSince = Date()
            if notify {
                DispatchQueue.main.async {
                    self.delegate?.socketClient(self, didChangeConnected: false)
                }
            }
        }
    }

    private func scheduleReconnect(after delay: TimeInterval) {
        guard !stopped else { return }
        reconnectWork?.cancel()
        let work = DispatchWorkItem { [weak self] in
            self?.connect()
        }
        reconnectWork = work
        queue.asyncAfter(deadline: .now() + delay, execute: work)
    }
}
