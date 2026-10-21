import AppKit

enum DSHLauncher {
    static let desktopBundleIdentifier = "com.deepseek.dsh"

    static func open(origin: URL?) {
        if let appURL = NSWorkspace.shared.urlForApplication(withBundleIdentifier: desktopBundleIdentifier) {
            let configuration = NSWorkspace.OpenConfiguration()
            configuration.activates = true
            configuration.createsNewApplicationInstance = false
            NSWorkspace.shared.openApplication(at: appURL, configuration: configuration) { _, error in
                if let error {
                    AppLog.write("open DSH desktop failed: \(error.localizedDescription)")
                }
            }
            return
        }
        if let origin {
            NSWorkspace.shared.open(origin)
        }
    }
}
