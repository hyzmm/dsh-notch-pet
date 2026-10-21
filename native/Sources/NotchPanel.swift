import AppKit
import Combine
import SwiftUI

final class PetViewModel: ObservableObject {
    @Published private(set) var payload = PetPayload.empty
    @Published private(set) var connected = false
    @Published private(set) var pinned = false
    @Published private(set) var layoutRevision = 0
    @Published private(set) var celebrating = false
    @Published private(set) var hasNotch = true

    private(set) var expanded = false

    var pointerInside: (() -> Bool)?

    private var collapseWork: DispatchWorkItem?
    private var celebrateWork: DispatchWorkItem?
    private var lastPhase: PetPhase = .idle

    var focus: SessionFocus? { payload.focus }
    var phase: PetPhase {
        PetPhase(raw: payload.focus?.phase ?? payload.overall)
    }
    var activeCount: Int { payload.activeCount ?? 0 }
    var originURL: URL? {
        guard let origin = payload.origin, !origin.isEmpty else { return nil }
        return URL(string: origin)
    }

    func apply(_ payload: PetPayload) {
        let nextPhase = PetPhase(raw: payload.focus?.phase ?? payload.overall)
        self.payload = payload
        if nextPhase == .done, lastPhase != .done {
            celebrate()
        }
        lastPhase = nextPhase
    }

    func setConnected(_ connected: Bool) {
        self.connected = connected
    }

    func setHasNotch(_ value: Bool) {
        hasNotch = value
    }

    func beginPeek() {
        setExpanded(true)
        scheduleCollapse(after: 2.8)
    }

    func setHovering(_ hovering: Bool) {
        if hovering {
            collapseWork?.cancel()
            setExpanded(true)
            return
        }
        guard !pinned else { return }
        scheduleCollapse(after: 0.32)
    }

    func togglePinned() {
        pinned.toggle()
        collapseWork?.cancel()
        if pinned {
            setExpanded(true)
        } else {
            scheduleCollapse(after: 0.45)
        }
    }

    private func setExpanded(_ value: Bool) {
        guard expanded != value else { return }
        expanded = value
        layoutRevision &+= 1
    }

    private func scheduleCollapse(after delay: TimeInterval) {
        collapseWork?.cancel()
        let work = DispatchWorkItem { [weak self] in
            guard let self, !self.pinned else { return }
            if self.pointerInside?() == true {
                self.scheduleCollapse(after: 0.4)
                return
            }
            self.setExpanded(false)
        }
        collapseWork = work
        DispatchQueue.main.asyncAfter(deadline: .now() + delay, execute: work)
    }

    private func celebrate() {
        celebrateWork?.cancel()
        celebrating = true
        let work = DispatchWorkItem { [weak self] in
            self?.celebrating = false
        }
        celebrateWork = work
        DispatchQueue.main.asyncAfter(deadline: .now() + 1.7, execute: work)
    }
}

final class NotchPanel: NSPanel {
    override var canBecomeKey: Bool { false }
    override var canBecomeMain: Bool { false }
}

final class NotchBackdropView: NSView {
    var expanded = false { didSet { updatePath() } }
    var hasNotch = true { didSet { updatePath() } }

    private let shapeLayer = CAShapeLayer()

    override init(frame: NSRect) {
        super.init(frame: frame)
        wantsLayer = true
        layer = CALayer()
        layer?.backgroundColor = NSColor.clear.cgColor
        shapeLayer.fillColor = NSColor(red: 0.018, green: 0.022, blue: 0.032, alpha: 1).cgColor
        shapeLayer.actions = ["path": NSNull(), "bounds": NSNull(), "position": NSNull()]
        shapeLayer.contentsScale = NSScreen.main?.backingScaleFactor ?? 2
        layer?.addSublayer(shapeLayer)
    }

    required init?(coder: NSCoder) {
        fatalError("init(coder:) is not supported")
    }

    override func layout() {
        super.layout()
        updatePath()
    }

    func syncLayout() {
        needsLayout = true
        layoutSubtreeIfNeeded()
    }

    private func updatePath() {
        shapeLayer.frame = bounds
        let topRadius: CGFloat = hasNotch ? 0 : 22
        let bottomRadius: CGFloat = expanded ? 25 : 19
        let width = bounds.width
        let height = bounds.height
        let top = min(topRadius, min(width / 2, height / 2))
        let bottom = min(bottomRadius, min(width / 2, height / 2))
        let path = CGMutablePath()

        // NSView layers use a bottom-left origin, so build the shape in that
        // coordinate space instead of reusing SwiftUI's top-left Path.
        path.move(to: CGPoint(x: top, y: height))
        path.addLine(to: CGPoint(x: width - top, y: height))
        if top > 0 {
            path.addArc(
                center: CGPoint(x: width - top, y: height - top),
                radius: top,
                startAngle: .pi / 2,
                endAngle: 0,
                clockwise: true
            )
        }
        path.addLine(to: CGPoint(x: width, y: bottom))
        path.addArc(
            center: CGPoint(x: width - bottom, y: bottom),
            radius: bottom,
            startAngle: 0,
            endAngle: -.pi / 2,
            clockwise: true
        )
        path.addLine(to: CGPoint(x: bottom, y: 0))
        path.addArc(
            center: CGPoint(x: bottom, y: bottom),
            radius: bottom,
            startAngle: -.pi / 2,
            endAngle: .pi,
            clockwise: true
        )
        if top > 0 {
            path.addLine(to: CGPoint(x: 0, y: height - top))
            path.addArc(
                center: CGPoint(x: top, y: height - top),
                radius: top,
                startAngle: .pi,
                endAngle: .pi / 2,
                clockwise: true
            )
        } else {
            path.addLine(to: CGPoint(x: 0, y: height))
        }
        path.closeSubpath()
        shapeLayer.path = path
    }
}

final class TrackingHostingView<Content: View>: NSHostingView<Content> {
    var onHoverChange: ((Bool) -> Void)?
    private var trackingAreaInstalled = false

    override func updateTrackingAreas() {
        super.updateTrackingAreas()
        guard !trackingAreaInstalled else { return }
        trackingAreaInstalled = true
        let area = NSTrackingArea(
            rect: .zero,
            options: [.mouseEnteredAndExited, .activeAlways, .inVisibleRect],
            owner: self,
            userInfo: nil
        )
        addTrackingArea(area)
    }

    override func mouseEntered(with event: NSEvent) {
        onHoverChange?(true)
    }

    override func mouseExited(with event: NSEvent) {
        onHoverChange?(false)
    }
}

final class PetWindowController: NSWindowController {
    let model = PetViewModel()

    private let socketClient: SocketClient
    private var expandedObservation: AnyCancellable?
    private var screenObserver: NSObjectProtocol?
    private var currentScreen: NSScreen
    private var notchWidth: CGFloat
    private var frameAnimationTimer: Timer?
    private var frameAnimationStart: Date?
    private var frameAnimationFrom: NSRect = .zero
    private var frameAnimationTo: NSRect = .zero
    private var backdropView: NotchBackdropView?

    private var collapsedSize: NSSize {
        NSSize(width: max(360, notchWidth + 220), height: 58)
    }

    private var expandedSize: NSSize {
        NSSize(width: max(390, notchWidth + 220), height: 168)
    }

    init(socketPath: String) {
        let screen = Self.preferredScreen()
        currentScreen = screen
        notchWidth = Self.notchWidth(on: screen)
        let initialSize = NSSize(width: max(360, notchWidth + 220), height: 58)
        let frame = Self.frame(for: initialSize, on: screen)
        let panel = NotchPanel(
            contentRect: frame,
            styleMask: [.borderless, .nonactivatingPanel],
            backing: .buffered,
            defer: false
        )
        socketClient = SocketClient(path: socketPath)
        super.init(window: panel)

        model.setHasNotch(screen.safeAreaInsets.top > 0)
        model.pointerInside = { [weak self] in
            guard let panel = self?.window else { return false }
            return panel.frame.contains(NSEvent.mouseLocation)
        }
        configure(panel)
        installHostingView()

        expandedObservation = model.$layoutRevision
            .sink { [weak self] _ in
                DispatchQueue.main.async {
                    guard let self else { return }
                    self.resize(expanded: self.model.expanded, animated: true)
                }
            }

        socketClient.delegate = self

        screenObserver = NotificationCenter.default.addObserver(
            forName: NSApplication.didChangeScreenParametersNotification,
            object: nil,
            queue: .main
        ) { [weak self] _ in
            self?.screenConfigurationChanged()
        }
    }

    required init?(coder: NSCoder) {
        fatalError("init(coder:) is not supported")
    }

    func show() {
        guard let panel = window else { return }
        panel.orderFrontRegardless()
        socketClient.start()
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.15) { [weak self] in
            self?.model.beginPeek()
        }
        AppLog.write("panel shown frame=\(NSStringFromRect(panel.frame))")
    }

    func shutdown() {
        stopFrameAnimation()
        socketClient.stop()
        expandedObservation?.cancel()
        if let screenObserver {
            NotificationCenter.default.removeObserver(screenObserver)
        }
        window?.orderOut(nil)
    }

    private func installHostingView() {
        guard let panel = window else { return }
        let container = NSView(frame: panel.contentRect(forFrameRect: panel.frame))
        container.autoresizingMask = [.width, .height]
        container.wantsLayer = true
        container.layer?.backgroundColor = NSColor.clear.cgColor

        let backdrop = NotchBackdropView(frame: container.bounds)
        backdrop.autoresizingMask = [.width, .height]
        backdrop.expanded = model.expanded
        backdrop.hasNotch = model.hasNotch
        container.addSubview(backdrop)

        let hosting = TrackingHostingView(rootView: PetView(model: model))
        hosting.onHoverChange = { [weak model] hovering in
            model?.setHovering(hovering)
        }
        hosting.frame = container.bounds
        hosting.autoresizingMask = [.width, .height]
        hosting.wantsLayer = true
        hosting.layer?.backgroundColor = NSColor.clear.cgColor
        container.addSubview(hosting)

        panel.contentView = container
        backdropView = backdrop
    }

    private func configure(_ panel: NSPanel) {
        panel.isFloatingPanel = true
        panel.level = NSWindow.Level(rawValue: NSWindow.Level.statusBar.rawValue + 4)
        panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .stationary, .ignoresCycle]
        panel.isOpaque = false
        panel.backgroundColor = .clear
        panel.hasShadow = false
        panel.hidesOnDeactivate = false
        panel.isMovable = false
        panel.isReleasedWhenClosed = false
        panel.animationBehavior = .none
        panel.acceptsMouseMovedEvents = true
        panel.title = "DSH Notch Pet"
        panel.titleVisibility = .hidden
        panel.titlebarAppearsTransparent = true
    }

    private func resize(expanded: Bool, animated: Bool) {
        guard let panel = window else { return }
        let size = expanded ? expandedSize : collapsedSize
        let target = Self.frame(for: size, on: currentScreen)
        backdropView?.expanded = expanded
        backdropView?.hasNotch = currentScreen.safeAreaInsets.top > 0
        backdropView?.syncLayout()
        let shouldAnimate = animated && !NSWorkspace.shared.accessibilityDisplayShouldReduceMotion
        if shouldAnimate {
            animateFrame(to: target)
        } else {
            stopFrameAnimation()
            panel.setFrame(target, display: true)
        }
        AppLog.write("panel resize expanded=\(expanded) animated=\(shouldAnimate) frame=\(NSStringFromRect(target))")
    }

    private func animateFrame(to target: NSRect) {
        guard let panel = window else { return }
        stopFrameAnimation()
        frameAnimationFrom = panel.frame
        frameAnimationTo = target
        frameAnimationStart = Date()
        let timer = Timer(timeInterval: 1.0 / 60.0, repeats: true) { [weak self] timer in
            guard let self else {
                timer.invalidate()
                return
            }
            self.stepFrameAnimation()
        }
        RunLoop.main.add(timer, forMode: .common)
        frameAnimationTimer = timer
        stepFrameAnimation()
    }

    private func stepFrameAnimation() {
        guard let panel = window, let start = frameAnimationStart else {
            stopFrameAnimation()
            return
        }
        let configuredDuration = ProcessInfo.processInfo.environment["DSH_NOTCH_PET_ANIMATION_DURATION"]
            .flatMap(Double.init) ?? 0.2
        let duration = max(0.05, configuredDuration)
        let progress = min(1, Date().timeIntervalSince(start) / duration)
        let eased = 1 - pow(1 - progress, 3)
        let width = frameAnimationFrom.width + (frameAnimationTo.width - frameAnimationFrom.width) * eased
        let height = frameAnimationFrom.height + (frameAnimationTo.height - frameAnimationFrom.height) * eased
        // Re-pin the top edge every tick. AppKit's built-in frame animation can
        // interpolate origin and height independently and briefly expose the
        // desktop above the notch.
        let top = Self.topEdge(on: currentScreen)
        let frame = NSRect(
            x: currentScreen.frame.midX - width / 2,
            y: top - height,
            width: width,
            height: height
        )
        panel.setFrame(frame, display: true)
        backdropView?.syncLayout()
        if progress >= 1 {
            let target = frameAnimationTo
            stopFrameAnimation()
            panel.setFrame(target, display: true)
        }
    }

    private func stopFrameAnimation() {
        frameAnimationTimer?.invalidate()
        frameAnimationTimer = nil
        frameAnimationStart = nil
        frameAnimationFrom = .zero
        frameAnimationTo = .zero
    }

    private func screenConfigurationChanged() {
        stopFrameAnimation()
        currentScreen = Self.preferredScreen()
        notchWidth = Self.notchWidth(on: currentScreen)
        model.setHasNotch(currentScreen.safeAreaInsets.top > 0)
        backdropView?.hasNotch = currentScreen.safeAreaInsets.top > 0
        resize(expanded: model.expanded, animated: false)
    }

    private static func notchWidth(on screen: NSScreen) -> CGFloat {
        guard screen.safeAreaInsets.top > 0,
              let left = screen.auxiliaryTopLeftArea,
              let right = screen.auxiliaryTopRightArea else { return 0 }
        return max(0, screen.frame.width - left.width - right.width)
    }

    private static func topEdge(on screen: NSScreen) -> CGFloat {
        screen.safeAreaInsets.top > 0 ? screen.frame.maxY : screen.frame.maxY - 6
    }

    private static func preferredScreen() -> NSScreen {
        NSScreen.screens.first(where: { $0.safeAreaInsets.top > 0 })
            ?? NSScreen.main
            ?? NSScreen.screens.first
            ?? NSScreen.screens[0]
    }

    private static func frame(for size: NSSize, on screen: NSScreen) -> NSRect {
        let hasNotch = screen.safeAreaInsets.top > 0
        let top = hasNotch ? screen.frame.maxY : screen.frame.maxY - 6
        let width = min(size.width, screen.frame.width - 16)
        return NSRect(
            x: screen.frame.midX - width / 2,
            y: top - size.height,
            width: width,
            height: size.height
        )
    }
}

extension PetWindowController: SocketClientDelegate {
    func socketClient(_ client: SocketClient, didChangeConnected connected: Bool) {
        model.setConnected(connected)
    }

    func socketClient(_ client: SocketClient, didReceive payload: PetPayload) {
        model.apply(payload)
    }

    func socketClientDidGiveUp(_ client: SocketClient) {
        AppLog.write("host unavailable, terminating")
        NSApp.terminate(nil)
    }
}

enum PetTheme {
    static let accent = Color(red: 0.34, green: 0.47, blue: 1.0)
    static let cyan = Color(red: 0.27, green: 0.82, blue: 0.98)
    static let mint = Color(red: 0.35, green: 0.94, blue: 0.68)
    static let amber = Color(red: 1.0, green: 0.71, blue: 0.30)
    static let red = Color(red: 1.0, green: 0.39, blue: 0.45)
    static let idle = Color(red: 0.48, green: 0.57, blue: 0.78)

    static func color(for phase: PetPhase) -> Color {
        switch phase {
        case .running, .thinking, .tool: return cyan
        case .waiting: return amber
        case .done: return mint
        case .error: return red
        case .idle: return idle
        }
    }

    static func gradient(for phase: PetPhase) -> LinearGradient {
        let base = color(for: phase)
        return LinearGradient(
            colors: [base, phase == .idle ? accent.opacity(0.75) : accent],
            startPoint: .topLeading,
            endPoint: .bottomTrailing
        )
    }
}
