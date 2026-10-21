import SwiftUI

struct AccentSettingsView: View {
    @ObservedObject private var theme = PetTheme.shared
    var onClose: () -> Void

    @State private var hue: Double
    @State private var saturation: Double
    @State private var brightness: Double
    @State private var customExpanded: Bool

    private let columns = Array(
        repeating: GridItem(.flexible(), spacing: 8),
        count: 3
    )
    private let minimumBrightness = 0.30

    init(onClose: @escaping () -> Void) {
        self.onClose = onClose
        let theme = PetTheme.shared
        let components = theme.chosenColor.hsbComponents
            ?? HSBComponents(hue: 0.62, saturation: 0.65, brightness: 1)
        _hue = State(initialValue: components.hue)
        _saturation = State(initialValue: min(1, max(0, components.saturation)))
        _brightness = State(initialValue: min(1, max(0.30, components.brightness)))
        _customExpanded = State(initialValue: theme.selectedPreset == nil)
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 13) {
            header
            LazyVGrid(columns: columns, spacing: 8) {
                ForEach(AccentPreset.all) { preset in
                    presetButton(preset)
                }
            }
            customSection
        }
        .padding(16)
        .frame(width: 300)
        .preferredColorScheme(.dark)
    }

    private var header: some View {
        HStack(alignment: .center, spacing: 9) {
            ZStack {
                Circle()
                    .fill(theme.palette.primary.opacity(0.30))
                    .frame(width: 26, height: 26)
                    .blur(radius: 7)
                Circle()
                    .fill(
                        LinearGradient(
                            colors: [theme.chosenColor, theme.palette.primary],
                            startPoint: .topLeading,
                            endPoint: .bottomTrailing
                        )
                    )
                    .frame(width: 13, height: 13)
                    .overlay(Circle().stroke(.white.opacity(0.35), lineWidth: 0.6))
            }
            .frame(width: 26, height: 26)

            VStack(alignment: .leading, spacing: 1) {
                Text(L10n.accentColor)
                    .font(.system(size: 12.5, weight: .semibold, design: .rounded))
                    .foregroundStyle(.white.opacity(0.94))
                Text(L10n.accentHint)
                    .font(.system(size: 9, weight: .regular, design: .rounded))
                    .foregroundStyle(.white.opacity(0.42))
                    .lineLimit(1)
            }

            Spacer(minLength: 6)

            Text(theme.hex.uppercased())
                .font(.system(size: 9, weight: .medium, design: .monospaced))
                .foregroundStyle(.white.opacity(0.34))

            Button(action: onClose) {
                Image(systemName: "xmark")
                    .font(.system(size: 8.5, weight: .bold))
                    .foregroundStyle(.white.opacity(0.55))
                    .frame(width: 18, height: 18)
                    .background(Circle().fill(.white.opacity(0.07)))
            }
            .buttonStyle(.plain)
            .help(L10n.close)
            .accessibilityLabel(L10n.close)
        }
    }

    private func presetButton(_ preset: AccentPreset) -> some View {
        let selected = theme.hex.caseInsensitiveCompare(preset.hex) == .orderedSame
        return Button {
            withAnimation(.easeOut(duration: 0.16)) {
                theme.select(preset)
                syncPicker(to: preset.color)
            }
        } label: {
            VStack(spacing: 6) {
                ZStack {
                    RoundedRectangle(cornerRadius: 9, style: .continuous)
                        .fill(
                            LinearGradient(
                                colors: [
                                    preset.color.opacity(0.98),
                                    preset.color.opacity(0.64),
                                ],
                                startPoint: .topLeading,
                                endPoint: .bottomTrailing
                            )
                        )
                        .frame(height: 28)
                    if selected {
                        Image(systemName: "checkmark")
                            .font(.system(size: 10, weight: .heavy))
                            .foregroundStyle(.white)
                            .shadow(color: .black.opacity(0.45), radius: 2, y: 0.5)
                    }
                }
                Text(preset.name)
                    .font(
                        .system(
                            size: 9,
                            weight: selected ? .semibold : .medium,
                            design: .rounded
                        )
                    )
                    .foregroundStyle(.white.opacity(selected ? 0.90 : 0.52))
                    .lineLimit(1)
                    .minimumScaleFactor(0.85)
            }
            .padding(6)
            .background(
                RoundedRectangle(cornerRadius: 12, style: .continuous)
                    .fill(
                        selected
                            ? preset.color.opacity(0.14)
                            : Color.white.opacity(0.045)
                    )
            )
            .overlay(
                RoundedRectangle(cornerRadius: 12, style: .continuous)
                    .stroke(
                        selected
                            ? preset.color.opacity(0.62)
                            : Color.white.opacity(0.06),
                        lineWidth: selected ? 1 : 0.6
                    )
            )
        }
        .buttonStyle(.plain)
        .accessibilityLabel(preset.name)
        .accessibilityAddTraits(selected ? .isSelected : [])
    }

    private var customSection: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack(spacing: 8) {
                Button {
                    withAnimation(.easeOut(duration: 0.20)) {
                        customExpanded.toggle()
                    }
                } label: {
                    HStack(spacing: 9) {
                        RoundedRectangle(cornerRadius: 8, style: .continuous)
                            .fill(
                                LinearGradient(
                                    colors: [
                                        theme.chosenColor,
                                        theme.chosenColor.opacity(0.55),
                                    ],
                                    startPoint: .topLeading,
                                    endPoint: .bottomTrailing
                                )
                            )
                            .frame(width: 30, height: 22)
                            .overlay(
                                RoundedRectangle(cornerRadius: 8, style: .continuous)
                                    .stroke(.white.opacity(0.18), lineWidth: 0.6)
                            )
                            .shadow(color: theme.chosenColor.opacity(0.30), radius: 4)

                        VStack(alignment: .leading, spacing: 1) {
                            Text(L10n.customColor)
                                .font(.system(size: 10, weight: .medium, design: .rounded))
                                .foregroundStyle(.white.opacity(0.82))
                            Text(L10n.customHint)
                                .font(.system(size: 8.5, weight: .regular, design: .rounded))
                                .foregroundStyle(.white.opacity(0.36))
                                .lineLimit(1)
                        }

                        Spacer(minLength: 4)

                        Image(systemName: "chevron.down")
                            .font(.system(size: 8, weight: .semibold))
                            .foregroundStyle(.white.opacity(0.38))
                            .rotationEffect(.degrees(customExpanded ? 180 : 0))
                    }
                    .contentShape(Rectangle())
                }
                .buttonStyle(.plain)
                .accessibilityLabel(L10n.customColor)

                resetButton
            }

            if customExpanded {
                VStack(spacing: 9) {
                    saturationBrightnessField
                    HStack(spacing: 10) {
                        hueSlider
                        Text(theme.hex.uppercased())
                            .font(.system(size: 8.5, weight: .medium, design: .monospaced))
                            .foregroundStyle(.white.opacity(0.38))
                            .frame(width: 56, alignment: .trailing)
                    }
                }
                .transition(.opacity)
            }
        }
        .padding(.top, 1)
    }

    private var resetButton: some View {
        Button {
            withAnimation(.easeOut(duration: 0.16)) {
                theme.reset()
                syncPicker(to: AccentPreset.fallback.color)
            }
        } label: {
            HStack(spacing: 3) {
                Image(systemName: "arrow.counterclockwise")
                    .font(.system(size: 8, weight: .semibold))
                Text(L10n.reset)
                    .font(.system(size: 9, weight: .medium, design: .rounded))
            }
            .foregroundStyle(.white.opacity(theme.isDefault ? 0.22 : 0.60))
            .padding(.horizontal, 8)
            .padding(.vertical, 5)
            .background(Capsule().fill(.white.opacity(0.055)))
        }
        .buttonStyle(.plain)
        .disabled(theme.isDefault)
        .help(L10n.reset)
    }

    private var saturationBrightnessField: some View {
        GeometryReader { proxy in
            let size = proxy.size
            let radius: CGFloat = 6
            let cursorX = min(size.width - radius, max(radius, size.width * saturation))
            let cursorY = min(
                size.height - radius,
                max(radius, size.height * (1 - normalizedBrightness))
            )
            ZStack(alignment: .topLeading) {
                LinearGradient(
                    colors: [.white, Color(hue: hue, saturation: 1, brightness: 1)],
                    startPoint: .leading,
                    endPoint: .trailing
                )
                LinearGradient(
                    colors: [.clear, .black.opacity(1 - minimumBrightness)],
                    startPoint: .top,
                    endPoint: .bottom
                )
                Circle()
                    .fill(pickerColor)
                    .overlay(Circle().stroke(.white, lineWidth: 1.4))
                    .frame(width: 12, height: 12)
                    .shadow(color: .black.opacity(0.35), radius: 2)
                    .position(x: cursorX, y: cursorY)
            }
            .contentShape(Rectangle())
            .gesture(
                DragGesture(minimumDistance: 0)
                    .onChanged { value in
                        let x = min(size.width, max(0, value.location.x))
                        let y = min(size.height, max(0, value.location.y))
                        saturation = x / max(1, size.width)
                        brightness = minimumBrightness
                            + (1 - y / max(1, size.height)) * (1 - minimumBrightness)
                        theme.setCustom(pickerColor)
                    }
            )
        }
        .frame(height: 86)
        .clipShape(RoundedRectangle(cornerRadius: 9, style: .continuous))
        .overlay(
            RoundedRectangle(cornerRadius: 9, style: .continuous)
                .stroke(.white.opacity(0.10), lineWidth: 0.6)
        )
        .accessibilityLabel(L10n.customColor)
        .accessibilityHint(L10n.customHint)
    }

    private var hueSlider: some View {
        GeometryReader { proxy in
            let size = proxy.size
            let cursorX = min(size.width - 7, max(7, size.width * hue))
            ZStack(alignment: .leading) {
                Capsule()
                    .fill(
                        LinearGradient(
                            colors: hueGradient,
                            startPoint: .leading,
                            endPoint: .trailing
                        )
                    )
                    .frame(height: 9)
                Circle()
                    .fill(
                        Color(
                            hue: hue,
                            saturation: max(0.62, saturation),
                            brightness: max(0.78, brightness)
                        )
                    )
                    .overlay(Circle().stroke(.white, lineWidth: 1.4))
                    .frame(width: 14, height: 14)
                    .shadow(color: .black.opacity(0.35), radius: 2)
                    .position(x: cursorX, y: size.height / 2)
            }
            .contentShape(Rectangle())
            .gesture(
                DragGesture(minimumDistance: 0)
                    .onChanged { value in
                        let x = min(size.width, max(0, value.location.x))
                        hue = min(0.999, x / max(1, size.width))
                        theme.setCustom(pickerColor)
                    }
            )
        }
        .frame(height: 14)
    }

    private var pickerColor: Color {
        Color(
            hue: hue,
            saturation: saturation,
            brightness: brightness,
            opacity: 1
        )
    }

    private var normalizedBrightness: Double {
        (brightness - minimumBrightness) / (1 - minimumBrightness)
    }

    private var hueGradient: [Color] {
        stride(from: 0.0, through: 1.0, by: 1.0 / 12.0).map {
            Color(hue: $0, saturation: 0.95, brightness: 1)
        }
    }

    private func syncPicker(to color: Color) {
        guard let components = color.hsbComponents else { return }
        hue = components.hue
        saturation = components.saturation
        brightness = min(1, max(minimumBrightness, components.brightness))
    }
}
