import AppKit
import Combine
import SwiftUI

struct AccentPreset: Identifiable, Equatable {
    let id: String
    let hex: String
    /// The deeper companion hue used at the bottom of the pet's body gradient.
    /// The default pairs the original cyan with the aurora blue it used to
    /// blend into before accents became configurable.
    let companionHex: String
    /// Optional explicit resting color. The default keeps the signature cyan
    /// at rest, so the notch still matches the original artwork when no task
    /// is running instead of falling back to the old slate blue.
    var idleHex: String?

    var name: String { L10n.accentPresetName(id) }
    var color: Color { Color(hex: hex) ?? Color(red: 0.27, green: 0.82, blue: 0.98) }
    var companion: Color { Color(hex: companionHex) ?? color.dimmed(by: 0.32) }
    var idle: Color { idleHex.flatMap(Color.init(hex:)) ?? color.idleTint }

    static let all: [AccentPreset] = [
        // The original notch pet palette: cyan for live work and resting,
        // blue for depth. Kept first so it is the default.
        AccentPreset(
            id: "glacier",
            hex: "#45D1FA",
            companionHex: "#5B7CFF",
            idleHex: "#45D1FA"
        ),
        AccentPreset(id: "aurora", hex: "#5B7CFF", companionHex: "#3D5BFF"),
        AccentPreset(id: "iris", hex: "#A78BFA", companionHex: "#7C5CFF"),
        AccentPreset(id: "sakura", hex: "#FF8FB8", companionHex: "#FF5C9E"),
        AccentPreset(id: "tangerine", hex: "#FFA65C", companionHex: "#FF7A3D"),
        AccentPreset(id: "mint", hex: "#46E0A6", companionHex: "#21B98A"),
    ]

    static var fallback: AccentPreset { all[0] }
}

/// The resolved colors for one accent choice. The chosen color drives the
/// working and resting states; waiting, success and error keep their semantic
/// colors so the status stays readable whichever accent is selected.
struct PetPalette {
    let primary: Color
    let companion: Color
    let idle: Color
    let waiting: Color
    let done: Color
    let error: Color

    init(primary raw: Color, companion companionRaw: Color? = nil, idle idleRaw: Color? = nil) {
        self.primary = raw
        self.companion = companionRaw ?? raw.dimmed(by: 0.32)
        self.idle = idleRaw ?? raw.idleTint
        self.waiting = Color(red: 1.0, green: 0.71, blue: 0.30)
        self.done = Color(red: 0.35, green: 0.94, blue: 0.68)
        self.error = Color(red: 1.0, green: 0.39, blue: 0.45)
    }

    func color(for phase: PetPhase) -> Color {
        switch phase {
        case .running, .thinking, .tool: return primary
        case .waiting: return waiting
        case .done: return done
        case .error: return error
        case .idle: return idle
        }
    }

    /// The second stop in the pet's body gradient. Working and resting states
    /// blend the accent into its companion, while waiting, success and errors
    /// stay inside their own hue so the status remains unmistakable.
    func secondTone(for phase: PetPhase) -> Color {
        switch phase {
        case .running, .thinking, .tool, .idle: return companion
        case .waiting, .done, .error: return color(for: phase).dimmed(by: 0.32)
        }
    }
}

final class PetTheme: ObservableObject {
    static let shared = PetTheme()
    private static let storageKey = "accentHex"
    private static let migrationKey = "accentDefaultMigration"
    /// 1.1.0 shipped with this blue as the implicit default, and 1.2.0 kept it.
    /// The first launch after the fix moves it to the restored cyan default;
    /// the blue itself remains available as the Aurora preset.
    private static let legacyDefaultHex = "#5B7CFF"
    /// The previous Glacier preset was a slightly greener cyan. Treat it as
    /// the same preset so an existing selection picks up the two-tone body.
    private static let legacyGlacierHex = "#3FD8EC"

    @Published private(set) var chosenColor: Color
    @Published private(set) var palette: PetPalette
    private(set) var hex: String

    private init() {
        let stored = Self.loadStoredHex()
        if let stored, let preset = Self.preset(matching: stored) {
            hex = preset.hex
            chosenColor = preset.color
            palette = Self.palette(for: preset)
        } else if let stored, let color = Color(hex: stored) {
            hex = stored
            chosenColor = color
            palette = PetPalette(primary: color)
        } else {
            let preset = AccentPreset.fallback
            hex = preset.hex
            chosenColor = preset.color
            palette = Self.palette(for: preset)
        }
    }

    var selectedPreset: AccentPreset? {
        Self.preset(matching: hex)
    }

    var isDefault: Bool {
        hex.caseInsensitiveCompare(AccentPreset.fallback.hex) == .orderedSame
    }

    func select(_ preset: AccentPreset) {
        hex = preset.hex
        chosenColor = preset.color
        palette = Self.palette(for: preset)
        UserDefaults.standard.set(preset.hex, forKey: Self.storageKey)
    }

    func setCustom(_ color: Color) {
        guard let hex = color.hexString else { return }
        apply(color: color, hex: hex)
    }

    func reset() {
        select(.fallback)
    }

    private func apply(color: Color, hex newHex: String) {
        let normalized = Color.normalizedHex(newHex) ?? AccentPreset.fallback.hex
        if let preset = Self.preset(matching: normalized) {
            select(preset)
            return
        }
        hex = normalized
        chosenColor = color
        palette = PetPalette(primary: color)
        UserDefaults.standard.set(normalized, forKey: Self.storageKey)
    }

    private static func preset(matching hex: String) -> AccentPreset? {
        AccentPreset.all.first {
            $0.hex.caseInsensitiveCompare(hex) == .orderedSame
        }
    }

    private static func palette(for preset: AccentPreset) -> PetPalette {
        PetPalette(
            primary: preset.color,
            companion: preset.companion,
            idle: preset.idle
        )
    }

    private static func loadStoredHex() -> String? {
        let defaults = UserDefaults.standard
        var stored = defaults.string(forKey: storageKey)
            .flatMap(Color.normalizedHex)
        if defaults.integer(forKey: migrationKey) < 1 {
            if stored == legacyDefaultHex || stored == legacyGlacierHex {
                stored = AccentPreset.fallback.hex
                defaults.set(stored, forKey: storageKey)
            }
            defaults.set(1, forKey: migrationKey)
        }
        return stored
    }
}

extension Color {
    init?(hex: String) {
        guard let normalized = Color.normalizedHex(hex) else { return nil }
        let digits = normalized.dropFirst()
        guard let value = UInt32(digits, radix: 16) else { return nil }
        self.init(
            .sRGB,
            red: Double((value >> 16) & 0xFF) / 255.0,
            green: Double((value >> 8) & 0xFF) / 255.0,
            blue: Double(value & 0xFF) / 255.0,
            opacity: 1
        )
    }

    static func normalizedHex(_ hex: String) -> String? {
        var text = hex.trimmingCharacters(in: .whitespacesAndNewlines)
        if text.hasPrefix("#") { text.removeFirst() }
        guard text.count == 6, UInt32(text, radix: 16) != nil else { return nil }
        return "#\(text.uppercased())"
    }

    var hexString: String? {
        guard let color = NSColor(self).usingColorSpace(.sRGB) else { return nil }
        let red = Int((color.redComponent * 255).rounded())
        let green = Int((color.greenComponent * 255).rounded())
        let blue = Int((color.blueComponent * 255).rounded())
        return String(format: "#%02X%02X%02X", red, green, blue)
    }

    var hsbComponents: HSBComponents? {
        guard let converted = NSColor(self).usingColorSpace(.sRGB) else { return nil }
        var hue: CGFloat = 0
        var saturation: CGFloat = 0
        var brightness: CGFloat = 0
        var alpha: CGFloat = 0
        converted.getHue(&hue, saturation: &saturation, brightness: &brightness, alpha: &alpha)
        return HSBComponents(
            hue: Double(hue),
            saturation: Double(saturation),
            brightness: Double(brightness)
        )
    }

    fileprivate var idleTint: Color {
        guard let components = hsbComponents else { return self }
        let saturation = components.saturation < 0.05
            ? 0
            : max(0.16, components.saturation * 0.55)
        let brightness = max(0.34, min(0.82, components.brightness * 0.80))
        return Color(
            hue: components.hue,
            saturation: saturation,
            brightness: brightness,
            opacity: 1
        )
    }

    fileprivate func dimmed(by amount: Double) -> Color {
        guard let components = hsbComponents else { return self }
        return Color(
            hue: components.hue,
            saturation: min(1, components.saturation * 1.04),
            brightness: max(0.06, components.brightness * (1 - amount)),
            opacity: 1
        )
    }
}

struct HSBComponents {
    let hue: Double
    let saturation: Double
    let brightness: Double
}
