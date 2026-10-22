import AppKit
import Combine

/// A pawprint in the menu bar, shown while the notch panel is hidden by the
/// "Only while working" preference. It is the quickest way back into settings
/// after the panel disappears, and the "Hide menu bar icon" preference can
/// suppress it entirely.
final class StatusItemController: NSObject, NSMenuDelegate {
    private let item: NSStatusItem
    private let preferences = PetPreferences.shared
    private let hideWhenIdleItem = NSMenuItem()
    private var menuObservation: AnyCancellable?
    private var visibilityObservation: AnyCancellable?
    private var wantsVisible = false

    var onOpenSettings: (() -> Void)?
    var onOpenDSH: (() -> Void)?
    var onQuit: (() -> Void)?

    override init() {
        item = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength)
        super.init()

        if let button = item.button {
            let image = NSImage(
                systemSymbolName: "pawprint.fill",
                accessibilityDescription: L10n.appName
            )
            image?.isTemplate = true
            button.image = image
            button.toolTip = L10n.appName
        }

        hideWhenIdleItem.title = L10n.onlyWhileWorking
        hideWhenIdleItem.action = #selector(toggleHideWhenIdle)

        let menu = NSMenu()
        menu.delegate = self
        menu.addItem(
            NSMenuItem(
                title: L10n.openDSH,
                action: #selector(openDSH),
                keyEquivalent: ""
            )
        )
        menu.addItem(
            NSMenuItem(
                title: L10n.settings,
                action: #selector(openSettings),
                keyEquivalent: ""
            )
        )
        menu.addItem(.separator())
        menu.addItem(hideWhenIdleItem)
        menu.addItem(.separator())
        menu.addItem(
            NSMenuItem(
                title: L10n.quitPet,
                action: #selector(quit),
                keyEquivalent: ""
            )
        )
        for menuItem in menu.items where menuItem.action != nil {
            menuItem.target = self
        }
        item.menu = menu
        item.isVisible = false

        // @Published emits before the stored property changes, so use the
        // emitted value here instead of reading `preferences` back.
        menuObservation = preferences.$hideWhenIdle
            .sink { [weak self] hideWhenIdle in
                self?.hideWhenIdleItem.state = hideWhenIdle ? .on : .off
            }
        visibilityObservation = preferences.$hideStatusItem
            .sink { [weak self] hideStatusItem in
                self?.refreshVisibility(hideStatusItem: hideStatusItem)
            }
    }

    /// Requests the menu bar item while the panel is hidden. The user's
    /// "Hide menu bar icon" preference still wins.
    func setVisible(_ visible: Bool) {
        wantsVisible = visible
        refreshVisibility(hideStatusItem: preferences.hideStatusItem)
    }

    func menuWillOpen(_ menu: NSMenu) {
        refreshMenuState()
    }

    private func refreshMenuState() {
        hideWhenIdleItem.state = preferences.hideWhenIdle ? .on : .off
    }

    private func refreshVisibility(hideStatusItem: Bool) {
        let visible = wantsVisible && !hideStatusItem
        guard item.isVisible != visible else { return }
        item.isVisible = visible
        AppLog.write(
            "status item \(visible ? "shown" : "hidden") "
                + "requested=\(wantsVisible) hideStatusItem=\(hideStatusItem)"
        )
    }

    @objc private func openSettings() {
        // Let the status menu finish closing before the panel appears and the
        // menu bar item hides itself.
        DispatchQueue.main.async { [weak self] in
            self?.onOpenSettings?()
        }
    }

    @objc private func openDSH() {
        onOpenDSH?()
    }

    @objc private func toggleHideWhenIdle() {
        DispatchQueue.main.async { [weak self] in
            guard let self else { return }
            self.preferences.setHideWhenIdle(!self.preferences.hideWhenIdle)
        }
    }

    @objc private func quit() {
        onQuit?()
    }
}
