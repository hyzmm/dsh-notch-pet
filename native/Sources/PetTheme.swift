import AppKit
import Combine
import SwiftUI

struct AccentPreset: Identifiable, Equatable {
    let id: String
    let hex: String

    var name: String { L10n.accentPresetName(id) }
    var color: Color { Color(hex: hex) ?? Color(red: 0.36, green: 0.49, blue: 1) }

    static let all: [AccentPreset] = [
        AccentPreset(id: "aurora", hex: "#5B7CFF"),
        AccentPreset(id: "glacier", hex: "#3FD8EC"),
        AccentPreset(id: "iris", hex: "#A78BFA"),
        AccentPreset(id: "sakura", hex: "#FF8FB8"),
        AccentPreset(id: "tangerine", hex: "#FFA65C"),
        AccentPreset(id: "mint", hex: "#46E0A6"),
    ]

    static var fallback: AccentPreset { all[0] }
}

/// The resolved colors for one accent choice. The chosen color drives the
/// working and resting states; waiting, success and error keep their semantic
/// colors so the status stays readable whichever accent is selected.
struct PetPalette {
    let primary: Color
    let idle: Color
    let waiting: Color
    let done: Color
    let error: Color

    init(primary raw: Color) {
        self.primary = raw
        self.idle = raw.idleTint
        self.waiting = Color(red: 1.0, green: 0.72, blue: 0.32)
        self.done = Color(red: 0.36, green: 0.93, blue: 0.69)
        self.error = Color(red: 1.0, green: 0.40, blue: 0.47)
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

    /// A darker companion to `color(for:)`, used for the pet body gradient.
    func shade(for phase: PetPhase) -> Color {
        color(for: phase).dimmed(by: 0.32)
    }
}

final class PetTheme: ObservableObject {
    static let shared = PetTheme()
    private static let storageKey = "accentHex"

    @Published private(set) var chosenColor: Color
    @Published private(set) var palette: PetPalette
    private(set) var hex: String

    private init() {
        let stored = UserDefaults.standard.string(forKey: Self.storageKey)
            .flatMap(Color.normalizedHex) ?? AccentPreset.fallback.hex
        let color = Color(hex: stored) ?? AccentPreset.fallback.color
        hex = stored
        chosenColor = color
        palette = PetPalette(primary: color)
    }

    var selectedPreset: AccentPreset? {
        AccentPreset.all.first { $0.hex.caseInsensitiveCompare(hex) == .orderedSame }
    }

    var isDefault: Bool {
        hex.caseInsensitiveCompare(AccentPreset.fallback.hex) == .orderedSame
    }

    func select(_ preset: AccentPreset) {
        apply(color: preset.color, hex: preset.hex)
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
        hex = normalized
        chosenColor = color
        palette = PetPalette(primary: color)
        UserDefaults.standard.set(normalized, forKey: Self.storageKey)
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
