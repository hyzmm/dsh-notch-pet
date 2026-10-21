import AppKit
import Combine
import SwiftUI

struct PetView: View {
    @ObservedObject var model: PetViewModel
    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    private var phase: PetPhase { model.phase }
    private var accent: Color { PetTheme.color(for: phase) }
    private var hasProgress: Bool { model.focus?.progress?.fraction != nil }
    private var showsProgress: Bool { phase.isBusy || hasProgress }

    var body: some View {
        GeometryReader { proxy in
            ZStack(alignment: .bottom) {
                shell(width: proxy.size.width)
                glow(in: proxy.size)
                if model.expanded {
                    expandedContent
                        .transition(.opacity.combined(with: .move(edge: .top)))
                } else {
                    collapsedContent
                        .transition(.opacity)
                }
                bottomProgress
            }
            .frame(width: proxy.size.width, height: proxy.size.height)
            .contentShape(Rectangle())
            .contextMenu { menu }
            .animation(
                reduceMotion ? nil : .easeOut(duration: 0.18),
                value: model.expanded
            )
        }
        .frame(minWidth: 300, maxWidth: .infinity, minHeight: 48, maxHeight: .infinity)
        .background(Color.clear)
    }

    private func shell(width: CGFloat) -> some View {
        let shape = NotchShellShape(
            topRadius: model.hasNotch ? 0 : 22,
            bottomRadius: model.expanded ? 25 : 19
        )
        return shape
            .fill(
                LinearGradient(
                    colors: [
                        Color(red: 0.035, green: 0.045, blue: 0.070),
                        Color(red: 0.008, green: 0.010, blue: 0.018),
                    ],
                    startPoint: .top,
                    endPoint: .bottom
                )
            )
            .overlay(
                shape.stroke(
                    LinearGradient(
                        colors: [.clear, .white.opacity(0.11)],
                        startPoint: .top,
                        endPoint: .bottom
                    ),
                    lineWidth: 0.8
                )
            )
    }

    private func glow(in size: CGSize) -> some View {
        RadialGradient(
            colors: [accent.opacity(0.24), accent.opacity(0.04), .clear],
            center: .center,
            startRadius: 0,
            endRadius: model.expanded ? 105 : 54
        )
        .frame(width: model.expanded ? 220 : 120, height: model.expanded ? 140 : 70)
        .position(
            x: model.expanded ? 66 : size.width - 37,
            y: model.expanded ? 82 : 43
        )
        .allowsHitTesting(false)
    }

    private var collapsedContent: some View {
        HStack(alignment: .center, spacing: 8) {
            VStack(alignment: .leading, spacing: 2) {
                HStack(spacing: 5) {
                    Circle()
                        .fill(model.connected ? accent : Color.white.opacity(0.25))
                        .frame(width: 5, height: 5)
                        .shadow(color: accent.opacity(0.7), radius: 3)
                    Text(L10n.phase(phase))
                        .font(.system(size: 9.5, weight: .bold, design: .rounded))
                        .tracking(0.5)
                        .foregroundStyle(.white.opacity(0.84))
                        .lineLimit(1)
                }
                HStack(spacing: 3) {
                    Text(L10n.shortActivity(model.focus))
                        .lineLimit(1)
                    if let focus = model.focus, focus.running == true, (focus.startedAt ?? 0) > 0 {
                        Text("·")
                        ElapsedText(
                            startMilliseconds: focus.startedAt ?? 0,
                            running: true,
                            completedMilliseconds: focus.completedAt ?? 0
                        )
                    }
                }
                .font(.system(size: 8.5, weight: .medium, design: .monospaced))
                .foregroundStyle(.white.opacity(0.48))
                .lineLimit(1)
            }
            .frame(width: 92, alignment: .leading)

            Spacer(minLength: 0)

            PetAvatar(
                size: 30,
                phase: phase,
                celebrating: model.celebrating,
                reduceMotion: reduceMotion
            )
            .offset(y: 4)
            .onTapGesture { model.togglePinned() }
            .accessibilityLabel("\(L10n.phase(phase)), \(L10n.activity(model.focus))")
        }
        .padding(.horizontal, 14)
        .padding(.top, 28)
        .padding(.bottom, 4)
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .bottom)
    }

    private var expandedContent: some View {
        VStack(alignment: .leading, spacing: 0) {
            HStack(alignment: .top, spacing: 13) {
                PetAvatar(
                    size: 58,
                    phase: phase,
                    celebrating: model.celebrating,
                    reduceMotion: reduceMotion
                )
                .onTapGesture { model.togglePinned() }
                .accessibilityLabel("\(L10n.phase(phase)), \(L10n.activity(model.focus))")

                VStack(alignment: .leading, spacing: 6) {
                    HStack(spacing: 8) {
                        Text(model.focus?.title ?? "DSH")
                            .font(.system(size: 13.5, weight: .semibold, design: .rounded))
                            .foregroundStyle(.white.opacity(0.95))
                            .lineLimit(1)
                            .truncationMode(.tail)
                        Spacer(minLength: 4)
                        StatusChip(phase: phase, connected: model.connected, count: model.activeCount, accent: accent)
                    }

                    HStack(spacing: 5) {
                        Text(L10n.activity(model.focus))
                            .font(.system(size: 11.5, weight: .medium, design: .rounded))
                            .foregroundStyle(.white.opacity(0.70))
                            .lineLimit(1)
                            .truncationMode(.tail)
                        if let tool = model.focus?.tool, !tool.isEmpty, phase == .tool {
                            ToolBadge(tool: tool, accent: accent)
                        } else if model.activeCount > 1 {
                            Text(L10n.count(model.activeCount, total: model.payload.totalCount ?? 0))
                                .font(.system(size: 9, weight: .medium, design: .rounded))
                                .foregroundStyle(.white.opacity(0.32))
                                .lineLimit(1)
                        }
                        Spacer(minLength: 4)
                        if let focus = model.focus, focus.running == true, (focus.startedAt ?? 0) > 0 {
                            ElapsedText(
                                startMilliseconds: focus.startedAt ?? 0,
                                running: true,
                                completedMilliseconds: focus.completedAt ?? 0
                            )
                            .font(.system(size: 10, weight: .medium, design: .monospaced))
                            .foregroundStyle(.white.opacity(0.42))
                        }
                    }

                    if showsProgress {
                        progressArea
                    }
                }
                .padding(.top, 2)
            }
            .padding(.horizontal, 18)
            .padding(.top, 34)

            Spacer(minLength: 8)

            Rectangle()
                .fill(Color.white.opacity(0.07))
                .frame(height: 0.6)
                .padding(.horizontal, 18)

            HStack(spacing: 7) {
                Text(L10n.footer(model.focus, activeCount: model.activeCount))
                    .font(.system(size: 10, weight: .regular, design: .rounded))
                    .foregroundStyle(.white.opacity(0.46))
                    .lineLimit(1)
                    .truncationMode(.tail)

                Spacer(minLength: 6)

                if let modelName = model.focus?.model, !modelName.isEmpty {
                    Text(modelName)
                        .font(.system(size: 8.5, weight: .medium, design: .monospaced))
                        .foregroundStyle(.white.opacity(0.28))
                        .lineLimit(1)
                }

                IconButton(
                    systemName: model.pinned ? "pin.fill" : "pin",
                    label: model.pinned ? L10n.unpin : L10n.pin,
                    accent: accent
                ) {
                    model.togglePinned()
                }

                IconButton(
                    systemName: "arrow.up.forward.app",
                    label: L10n.openDSH,
                    accent: accent
                ) {
                    DSHLauncher.open(origin: model.originURL)
                }
            }
            .padding(.horizontal, 18)
            .padding(.top, 9)
            .padding(.bottom, 11)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .bottom)
    }

    private var progressArea: some View {
        VStack(alignment: .leading, spacing: 5) {
            HStack(spacing: 8) {
                Text(L10n.progressLine(model.focus))
                    .font(.system(size: 9, weight: .bold, design: .rounded))
                    .tracking(0.25)
                    .foregroundStyle(accent.opacity(0.92))
                Spacer(minLength: 4)
                if let progress = model.focus?.progress, let total = progress.total, total > 0 {
                    Text("\(progress.completed ?? 0)/\(total)")
                        .font(.system(size: 9, weight: .semibold, design: .monospaced))
                        .monospacedDigit()
                        .foregroundStyle(.white.opacity(0.42))
                }
            }
            ProgressTrack(
                fraction: model.focus?.progress?.fraction,
                accent: accent,
                animated: phase.isBusy,
                reduceMotion: reduceMotion
            )
            .frame(height: 3)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(.top, 2)
    }

    @ViewBuilder
    private var bottomProgress: some View {
        if showsProgress {
            ProgressTrack(
                fraction: model.focus?.progress?.fraction,
                accent: accent,
                animated: phase.isBusy,
                reduceMotion: reduceMotion,
                compact: true
            )
            .frame(height: 2)
            .padding(.horizontal, model.expanded ? 13 : 44)
            .padding(.bottom, 0.8)
            .opacity(0.95)
            .allowsHitTesting(false)
        }
    }

    @ViewBuilder
    private var menu: some View {
        Button(L10n.openDSH) { DSHLauncher.open(origin: model.originURL) }
        Button(model.pinned ? L10n.unpin : L10n.pin) { model.togglePinned() }
        Divider()
        Button(L10n.quitPet) { NSApp.terminate(nil) }
    }

}

struct NotchShellShape: Shape {
    var topRadius: CGFloat
    var bottomRadius: CGFloat

    func path(in rect: CGRect) -> Path {
        let top = max(0, min(topRadius, rect.height / 2))
        let bottom = max(0, min(bottomRadius, rect.height / 2))
        var path = Path()

        path.move(to: CGPoint(x: rect.minX + top, y: rect.minY))
        path.addLine(to: CGPoint(x: rect.maxX - top, y: rect.minY))
        if top > 0 {
            path.addArc(
                center: CGPoint(x: rect.maxX - top, y: rect.minY + top),
                radius: top,
                startAngle: .degrees(-90),
                endAngle: .degrees(0),
                clockwise: false
            )
        }
        path.addLine(to: CGPoint(x: rect.maxX, y: rect.maxY - bottom))
        path.addArc(
            center: CGPoint(x: rect.maxX - bottom, y: rect.maxY - bottom),
            radius: bottom,
            startAngle: .degrees(0),
            endAngle: .degrees(90),
            clockwise: false
        )
        path.addLine(to: CGPoint(x: rect.minX + bottom, y: rect.maxY))
        path.addArc(
            center: CGPoint(x: rect.minX + bottom, y: rect.maxY - bottom),
            radius: bottom,
            startAngle: .degrees(90),
            endAngle: .degrees(180),
            clockwise: false
        )
        if top > 0 {
            path.addLine(to: CGPoint(x: rect.minX, y: rect.minY + top))
            path.addArc(
                center: CGPoint(x: rect.minX + top, y: rect.minY + top),
                radius: top,
                startAngle: .degrees(180),
                endAngle: .degrees(270),
                clockwise: false
            )
        } else {
            path.addLine(to: CGPoint(x: rect.minX, y: rect.minY))
        }
        path.closeSubpath()
        return path
    }
}

struct StatusChip: View {
    let phase: PetPhase
    let connected: Bool
    let count: Int
    let accent: Color

    var body: some View {
        HStack(spacing: 4) {
            Circle()
                .fill(connected ? accent : Color.white.opacity(0.25))
                .frame(width: 4, height: 4)
            Text(count > 1 ? "\(L10n.phase(phase)) ×\(count)" : L10n.phase(phase))
                .font(.system(size: 8, weight: .bold, design: .rounded))
                .tracking(0.35)
        }
        .foregroundStyle(connected ? accent.opacity(0.95) : Color.white.opacity(0.35))
        .padding(.horizontal, 6)
        .padding(.vertical, 3)
        .background(Capsule().fill(accent.opacity(connected ? 0.10 : 0.04)))
        .overlay(Capsule().stroke(accent.opacity(connected ? 0.22 : 0.08), lineWidth: 0.5))
    }
}

struct ToolBadge: View {
    let tool: String
    let accent: Color

    var body: some View {
        Text(tool)
            .font(.system(size: 8.5, weight: .medium, design: .monospaced))
            .foregroundStyle(accent.opacity(0.9))
            .lineLimit(1)
            .padding(.horizontal, 5)
            .padding(.vertical, 2)
            .background(RoundedRectangle(cornerRadius: 4, style: .continuous).fill(accent.opacity(0.10)))
    }
}

struct IconButton: View {
    let systemName: String
    let label: String
    let accent: Color
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            Image(systemName: systemName)
                .font(.system(size: 9.5, weight: .semibold))
                .foregroundStyle(.white.opacity(0.56))
                .frame(width: 20, height: 20)
                .background(RoundedRectangle(cornerRadius: 6, style: .continuous).fill(.white.opacity(0.055)))
                .overlay(
                    RoundedRectangle(cornerRadius: 6, style: .continuous)
                        .stroke(.white.opacity(0.06), lineWidth: 0.5)
                )
        }
        .buttonStyle(.plain)
        .help(label)
        .accessibilityLabel(label)
    }
}

struct ProgressTrack: View {
    let fraction: Double?
    let accent: Color
    var animated: Bool = true
    var reduceMotion: Bool = false
    var compact: Bool = false

    @State private var tick = 0
    private let motionTimer = Timer.publish(every: 1.0 / 60.0, on: .main, in: .common).autoconnect()

    var body: some View {
        GeometryReader { proxy in
            let height = compact ? 2.0 : 3.0
            ZStack(alignment: .leading) {
                Capsule()
                    .fill(Color.white.opacity(compact ? 0.07 : 0.09))

                if let fraction {
                    let width = max(height, proxy.size.width * min(1, max(0, fraction)))
                    let shimmerWidth = min(28, max(10, width * 0.45))
                    let unit = reduceMotion ? 0.35 : cycle(tick, step: 0.00925)
                    let offset = -shimmerWidth + unit * (width + shimmerWidth)
                    Capsule()
                        .fill(
                            LinearGradient(
                                colors: [accent.opacity(0.62), accent, accent.opacity(0.78)],
                                startPoint: .leading,
                                endPoint: .trailing
                            )
                        )
                        .frame(width: width, height: height)
                        .overlay(alignment: .leading) {
                            if animated && !reduceMotion {
                                Capsule()
                                    .fill(
                                        LinearGradient(
                                            colors: [.clear, .white.opacity(0.55), .clear],
                                            startPoint: .leading,
                                            endPoint: .trailing
                                        )
                                    )
                                    .frame(width: shimmerWidth, height: height)
                                    .offset(x: offset)
                            }
                        }
                        .clipShape(Capsule())
                } else {
                    let bandWidth = max(24, proxy.size.width * 0.34)
                    let unit = reduceMotion ? 0.4 : cycle(tick, step: 0.0098)
                    let offset = -bandWidth + unit * (proxy.size.width + bandWidth)
                    Capsule()
                        .fill(
                            LinearGradient(
                                colors: [.clear, accent.opacity(0.62), accent, .clear],
                                startPoint: .leading,
                                endPoint: .trailing
                            )
                        )
                        .frame(width: bandWidth, height: height)
                        .offset(x: offset)
                }
            }
            .clipShape(Capsule())
        }
        .frame(height: compact ? 2 : 3)
        .onReceive(motionTimer) { _ in
            if animated && !reduceMotion { tick &+= 1 }
        }
    }

    private func cycle(_ tick: Int, step: Double) -> CGFloat {
        let value = Double(tick) * step
        return CGFloat(value - floor(value))
    }
}

struct PetAvatar: View {
    let size: CGFloat
    let phase: PetPhase
    let celebrating: Bool
    let reduceMotion: Bool

    @State private var tick = 0
    private let motionTimer = Timer.publish(every: 0.08, on: .main, in: .common).autoconnect()

    private var accent: Color { PetTheme.color(for: phase) }

    var body: some View {
        let time = Double(tick) * 0.08
        let bob = bobOffset(time: time)
        let blink = blinkScale(time: time)
        let eyeTravel = phase.isBusy && !reduceMotion ? sin(time * 1.7) * size * 0.018 : 0
        let tilt: Double = {
            switch phase {
            case .waiting: return -6
            case .error: return 7
            default: return 0
            }
        }()

        ZStack {
            Circle()
                .fill(
                    RadialGradient(
                        colors: [
                            accent.opacity(phase.isBusy ? 0.34 : 0.20),
                            accent.opacity(0.08),
                            .clear,
                        ],
                        center: .center,
                        startRadius: 0,
                        endRadius: size * 0.46
                    )
                )
                .frame(width: size * 0.95, height: size * 0.88)
                .scaleEffect(celebrating ? 1.16 : 1)

            ZStack {
                if size > 34 {
                    Capsule()
                        .fill(accent.opacity(0.75))
                        .frame(width: 1.2, height: size * 0.14)
                        .offset(y: -size * 0.43)
                    Circle()
                        .fill(accent)
                        .frame(width: size * 0.075, height: size * 0.075)
                        .offset(y: -size * 0.51)
                }

                if size > 34 {
                    HStack(spacing: size * 0.34) {
                        foot
                        foot
                    }
                    .offset(y: size * 0.29)
                    .opacity(0.72)
                }

                RoundedRectangle(cornerRadius: size * 0.32, style: .continuous)
                    .fill(
                        LinearGradient(
                            colors: [
                                accent.opacity(0.98),
                                accent.opacity(0.82),
                                PetTheme.accent.opacity(0.88),
                            ],
                            startPoint: .topLeading,
                            endPoint: .bottomTrailing
                        )
                    )
                    .frame(width: size * 0.76, height: size * 0.68)
                    .overlay(
                        RoundedRectangle(cornerRadius: size * 0.32, style: .continuous)
                            .stroke(.white.opacity(0.22), lineWidth: 0.7)
                    )
                    .overlay(
                        Ellipse()
                            .fill(.white.opacity(0.20))
                            .frame(width: size * 0.26, height: size * 0.10)
                            .offset(x: -size * 0.13, y: -size * 0.20),
                        alignment: .top
                    )

                RoundedRectangle(cornerRadius: size * 0.13, style: .continuous)
                    .fill(
                        LinearGradient(
                            colors: [
                                Color(red: 0.025, green: 0.045, blue: 0.085),
                                Color(red: 0.008, green: 0.014, blue: 0.035),
                            ],
                            startPoint: .top,
                            endPoint: .bottom
                        )
                    )
                    .overlay(
                        RoundedRectangle(cornerRadius: size * 0.13, style: .continuous)
                            .stroke(.white.opacity(0.10), lineWidth: 0.5)
                    )
                    .frame(width: size * 0.56, height: size * 0.27)
                    .offset(x: eyeTravel * 0.4, y: size * 0.008)

                HStack(spacing: size * 0.105) {
                    eye
                    eye
                }
                .scaleEffect(y: blink ? 0.14 : 1, anchor: .center)
                .offset(x: eyeTravel, y: size * 0.004)

                if phase == .done || celebrating {
                    SmileShape()
                        .stroke(.white.opacity(0.55), style: StrokeStyle(lineWidth: max(0.7, size * 0.018), lineCap: .round))
                        .frame(width: size * 0.13, height: size * 0.055)
                        .offset(y: size * 0.145)
                } else {
                    Capsule()
                        .fill(.white.opacity(0.35))
                        .frame(width: size * 0.07, height: max(0.7, size * 0.012))
                        .offset(y: size * 0.155)
                }
            }
            .offset(y: bob)
            .rotationEffect(.degrees(tilt), anchor: .bottom)
            .scaleEffect(celebrating ? 1.10 : 1, anchor: .bottom)
        }
        .frame(width: size, height: size)
        .animation(.spring(response: 0.35, dampingFraction: 0.62), value: celebrating)
        .onReceive(motionTimer) { _ in
            if !reduceMotion { tick &+= 1 }
        }
    }

    private var eye: some View {
        Capsule()
            .fill(.white.opacity(0.93))
            .frame(width: size * 0.060, height: size * 0.078)
    }

    private var foot: some View {
        Capsule()
            .fill(accent.opacity(0.75))
            .frame(width: size * 0.15, height: size * 0.065)
    }

    private func bobOffset(time: TimeInterval) -> CGFloat {
        guard !reduceMotion else { return 0 }
        switch phase {
        case .waiting:
            return sin(time * 1.8) * size * 0.012
        case .error:
            return 0
        case .done:
            return abs(sin(time * 5.5)) * size * 0.025
        case .idle:
            return sin(time * 1.35) * size * 0.016
        default:
            return sin(time * 3.8) * size * 0.026
        }
    }

    private func blinkScale(time: TimeInterval) -> Bool {
        guard !reduceMotion else { return false }
        let period: Double = phase.isBusy ? 2.7 : 4.1
        let position = time.truncatingRemainder(dividingBy: period)
        return position > period - 0.2
    }
}

struct ElapsedText: View {
    let startMilliseconds: Double
    let running: Bool
    let completedMilliseconds: Double

    var body: some View {
        if running {
            TimelineView(.periodic(from: .now, by: 1)) { timeline in
                Text(value(at: timeline.date)).monospacedDigit()
            }
        } else {
            Text(value(at: Date())).monospacedDigit()
        }
    }

    private func value(at date: Date) -> String {
        guard startMilliseconds > 0 else { return "" }
        let now = date.timeIntervalSince1970 * 1000
        let end = running ? now : (completedMilliseconds > 0 ? completedMilliseconds : now)
        return L10n.elapsed(max(0, (end - startMilliseconds) / 1000))
    }
}

struct SmileShape: Shape {
    func path(in rect: CGRect) -> Path {
        var path = Path()
        path.move(to: CGPoint(x: rect.minX, y: rect.minY))
        path.addQuadCurve(
            to: CGPoint(x: rect.maxX, y: rect.minY),
            control: CGPoint(x: rect.midX, y: rect.maxY)
        )
        return path
    }
}
