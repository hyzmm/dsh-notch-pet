import Foundation

enum AppLog {
    private static let queue = DispatchQueue(label: "local.dsh.notchpet.log")
    private static let formatter = ISO8601DateFormatter()
    private static let fileURL: URL = {
        let directory = FileManager.default.homeDirectoryForCurrentUser
            .appendingPathComponent("Library/Logs", isDirectory: true)
        try? FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        return directory.appendingPathComponent("DSHNotchPet.log")
    }()

    static func write(_ message: String) {
        queue.async {
            let line = "\(formatter.string(from: Date())) \(message)\n"
            guard let data = line.data(using: .utf8) else { return }
            if !FileManager.default.fileExists(atPath: fileURL.path) {
                try? data.write(to: fileURL, options: .atomic)
                return
            }
            guard let handle = try? FileHandle(forWritingTo: fileURL) else { return }
            defer { try? handle.close() }
            _ = try? handle.seekToEnd()
            try? handle.write(contentsOf: data)
        }
    }
}
