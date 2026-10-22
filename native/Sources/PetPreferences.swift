import Combine
import Foundation

/// User-facing behavior switches that are independent of the accent palette.
/// They live in the same `UserDefaults` suite as the accent so a relaunch
/// restores the notch exactly the way the user left it.
final class PetPreferences: ObservableObject {
    static let shared = PetPreferences()

    private enum Key {
        static let hideWhenIdle = "hideWhenIdle"
        static let hideStatusItem = "hideStatusItem"
    }

    /// When true, the panel only exists while at least one agent is working.
    @Published private(set) var hideWhenIdle: Bool

    /// When true, the pawprint status item stays out of the menu bar even
    /// while the panel is ordered out.
    @Published private(set) var hideStatusItem: Bool

    private init() {
        hideWhenIdle = UserDefaults.standard.bool(forKey: Key.hideWhenIdle)
        hideStatusItem = UserDefaults.standard.bool(forKey: Key.hideStatusItem)
    }

    func setHideWhenIdle(_ value: Bool) {
        guard hideWhenIdle != value else { return }
        hideWhenIdle = value
        UserDefaults.standard.set(value, forKey: Key.hideWhenIdle)
        AppLog.write("hideWhenIdle=\(value)")
    }

    func setHideStatusItem(_ value: Bool) {
        guard hideStatusItem != value else { return }
        hideStatusItem = value
        UserDefaults.standard.set(value, forKey: Key.hideStatusItem)
        AppLog.write("hideStatusItem=\(value)")
    }
}
