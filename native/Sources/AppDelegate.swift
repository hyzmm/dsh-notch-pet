import AppKit

final class AppDelegate: NSObject, NSApplicationDelegate {
    private var controller: PetWindowController?
    private var hasSocket = false

    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.setActivationPolicy(.accessory)
        // The notch panel and its accent popover are designed for the dark
        // notch; keep native chrome (popovers, the color panel) in dark mode.
        NSApp.appearance = NSAppearance(named: .darkAqua)
        terminateDuplicateInstances()

        let arguments = CommandLine.arguments
        guard let index = arguments.firstIndex(of: "--socket"), index + 1 < arguments.count else {
            AppLog.write("missing --socket argument")
            NSApp.terminate(nil)
            return
        }
        let socketPath = arguments[index + 1]
        guard FileManager.default.fileExists(atPath: socketPath) else {
            AppLog.write("socket does not exist: \(socketPath)")
            NSApp.terminate(nil)
            return
        }

        hasSocket = true
        let controller = PetWindowController(socketPath: socketPath)
        self.controller = controller
        controller.show()
        AppLog.write("launched socket=\(socketPath) bundle=\(Bundle.main.bundleIdentifier ?? "?")")
    }

    func applicationWillTerminate(_ notification: Notification) {
        controller?.shutdown()
        AppLog.write("terminated")
    }

    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool {
        // The accent popover is an auxiliary window; closing it must not quit
        // the overlay. The socket watchdog owns the app's lifetime instead.
        false
    }

    func applicationSupportsSecureRestorableState(_ app: NSApplication) -> Bool {
        true
    }

    private func terminateDuplicateInstances() {
        guard let bundleIdentifier = Bundle.main.bundleIdentifier else { return }
        for application in NSRunningApplication.runningApplications(withBundleIdentifier: bundleIdentifier) {
            guard application.processIdentifier != ProcessInfo.processInfo.processIdentifier else { continue }
            AppLog.write("terminating duplicate pid=\(application.processIdentifier)")
            application.terminate()
        }
    }
}
