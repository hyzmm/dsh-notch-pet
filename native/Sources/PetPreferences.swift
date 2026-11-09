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
        static let hidePet = "hidePet"
        static let language = "language"
    }

    /// The saved interface language. `.system` follows the OS on every launch;
    /// unsupported system languages resolve to English inside `L10n`.
    @Published private(set) var language: AppLanguage

    /// When true, the panel only exists while at least one agent is working.
    @Published private(set) var hideWhenIdle: Bool

    /// When true, the pawprint status item stays out of the menu bar even
    /// while the panel is ordered out.
    @Published private(set) var hideStatusItem: Bool

    /// When true, the notch keeps the task details but drops the animated
    /// mascot and its glow. Off by default so the pet is visible out of the box.
    @Published private(set) var hidePet: Bool

    private init() {
        let storedLanguage = UserDefaults.standard.string(forKey: Key.language)
        language = AppLanguage(rawValue: storedLanguage ?? "") ?? .system
        hideWhenIdle = UserDefaults.standard.bool(forKey: Key.hideWhenIdle)
        hideStatusItem = UserDefaults.standard.bool(forKey: Key.hideStatusItem)
        hidePet = UserDefaults.standard.bool(forKey: Key.hidePet)
    }

    func setLanguage(_ value: AppLanguage) {
        guard language != value else { return }
        language = value
        UserDefaults.standard.set(value.rawValue, forKey: Key.language)
        AppLog.write("language=\(value.rawValue)")
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

    func setHidePet(_ value: Bool) {
        guard hidePet != value else { return }
        hidePet = value
        UserDefaults.standard.set(value, forKey: Key.hidePet)
        AppLog.write("hidePet=\(value)")
    }
}
