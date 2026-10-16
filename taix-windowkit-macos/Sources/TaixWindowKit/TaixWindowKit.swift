import AppKit

private var accessoryControllers: [ObjectIdentifier: TitlebarItemController] = [:]

@_cdecl("taix_window_set_thick_titlebar")
public func taixWindowSetThickTitlebar(_ windowPointer: UnsafeMutableRawPointer,
                                       _ thick: Int32) -> Double {
    guard Thread.isMainThread else { return -2 }

    let window = Unmanaged<NSWindow>.fromOpaque(windowPointer).takeUnretainedValue()

    if thick != 0 {
        window.titleVisibility = .hidden
        window.titlebarAppearsTransparent = true

        if window.toolbar == nil {
            let toolbar = NSToolbar(identifier: "taix-unified-titlebar")
            toolbar.showsBaselineSeparator = false
            window.toolbar = toolbar
        }
    } else if window.toolbar != nil {
        window.toolbar = nil
    }

    return Double(window.frame.height - window.contentLayoutRect.height)
}

@_cdecl("taix_window_set_corner_radius")
public func taixWindowSetCornerRadius(_ windowPointer: UnsafeMutableRawPointer,
                                      _ radius: Double) -> Int32 {
    guard Thread.isMainThread else { return -2 }

    let window = Unmanaged<NSWindow>.fromOpaque(windowPointer).takeUnretainedValue()
    guard let themeFrame = window.contentView?.superview else { return -1 }

    themeFrame.wantsLayer = true
    guard let layer = themeFrame.layer else { return -1 }

    window.isOpaque = false
    layer.cornerRadius = CGFloat(radius)
    layer.cornerCurve = .continuous
    layer.masksToBounds = true
    themeFrame.needsDisplay = true

    return 0
}

@_cdecl("taix_window_set_traffic_light_scale")
public func taixWindowSetTrafficLightScale(_ windowPointer: UnsafeMutableRawPointer,
                                           _ scale: Double) -> Int32 {
    guard Thread.isMainThread else { return -2 }
    guard scale > 0 else { return -1 }

    let window = Unmanaged<NSWindow>.fromOpaque(windowPointer).takeUnretainedValue()
    let buttons: [NSWindow.ButtonType] = [.closeButton, .miniaturizeButton, .zoomButton]

    var applied = false
    for type in buttons {
        guard let button = window.standardWindowButton(type) else { continue }
        button.wantsLayer = true
        guard let layer = button.layer else { continue }
        layer.setAffineTransform(CGAffineTransform(scaleX: scale, y: scale))
        applied = true
    }

    return applied ? 0 : -1
}

@_cdecl("taix_titlebar_item_attach")
public func taixTitlebarItemAttach(_ windowPointer: UnsafeMutableRawPointer,
                                   _ onClick: @convention(c) () -> Void,
                                   _ tooltip: UnsafePointer<CChar>?) -> Int32 {
    guard Thread.isMainThread else { return -2 }

    let window = Unmanaged<NSWindow>.fromOpaque(windowPointer).takeUnretainedValue()
    let key = ObjectIdentifier(window)
    guard accessoryControllers[key] == nil else { return 0 }

    guard let image = pluginEntryImage() else { return -3 }

    let tip = tooltip.map { String(cString: $0) } ?? ""
    let controller = TitlebarItemController(key: key, image: image, tooltip: tip, onClick: { onClick() })
    controller.layoutAttribute = .trailing
    controller.observeClose(of: window)
    window.addTitlebarAccessoryViewController(controller)
    accessoryControllers[key] = controller

    return 0
}

private func pluginEntryImage() -> NSImage? {
    let configuration = NSImage.SymbolConfiguration(pointSize: 15, weight: .regular)
    for name in ["puzzlepiece.extension", "puzzlepiece"] {
        if let image = NSImage(systemSymbolName: name, accessibilityDescription: nil) {
            return image.withSymbolConfiguration(configuration) ?? image
        }
    }
    return nil
}

private final class TitlebarItemController: NSTitlebarAccessoryViewController {
    private let key: ObjectIdentifier
    private let image: NSImage
    private let tooltip: String
    private let onClick: () -> Void

    init(key: ObjectIdentifier, image: NSImage, tooltip: String, onClick: @escaping () -> Void) {
        self.key = key
        self.image = image
        self.tooltip = tooltip
        self.onClick = onClick
        super.init(nibName: nil, bundle: nil)
    }

    /// 窗口关闭时把自己从登记表里清掉，避免引用常驻
    func observeClose(of window: NSWindow) {
        NotificationCenter.default.addObserver(
            self,
            selector: #selector(windowWillClose),
            name: NSWindow.willCloseNotification,
            object: window
        )
    }

    @objc private func windowWillClose() {
        NotificationCenter.default.removeObserver(self, name: NSWindow.willCloseNotification, object: nil)
        accessoryControllers.removeValue(forKey: key)
    }

    override func loadView() {
        let container = NSView(frame: NSRect(x: 0, y: 0, width: 44, height: 28))
        let button = TitlebarIconButton(image: image, tooltip: tooltip, onClick: onClick)
        button.frame = NSRect(x: 8, y: 2, width: 28, height: 24)
        container.addSubview(button)
        view = container
    }

    @available(*, unavailable)
    required init?(coder: NSCoder) {
        fatalError("init(coder:) has not been implemented")
    }
}

private final class TitlebarIconButton: NSButton {
    private let onClick: () -> Void
    private var hovered = false
    private var tracking: NSTrackingArea?

    init(image: NSImage, tooltip: String, onClick: @escaping () -> Void) {
        self.onClick = onClick
        super.init(frame: .zero)

        self.image = image
        imagePosition = .imageOnly
        isBordered = false
        focusRingType = .none
        contentTintColor = .secondaryLabelColor
        toolTip = tooltip
        setAccessibilityLabel(tooltip)

        target = self
        action = #selector(handleClick)

        wantsLayer = true
        layer?.cornerRadius = 6
        layer?.masksToBounds = true

        refreshBackground()
    }

    @available(*, unavailable)
    required init?(coder: NSCoder) {
        fatalError("init(coder:) has not been implemented")
    }

    @objc private func handleClick() {
        onClick()
    }

    override func updateTrackingAreas() {
        super.updateTrackingAreas()

        if let tracking { removeTrackingArea(tracking) }
        let area = NSTrackingArea(rect: .zero,
                                  options: [.mouseEnteredAndExited, .activeInKeyWindow, .inVisibleRect],
                                  owner: self,
                                  userInfo: nil)
        addTrackingArea(area)
        tracking = area
    }

    override func mouseEntered(with event: NSEvent) {
        hovered = true
        refreshBackground()
    }

    override func mouseExited(with event: NSEvent) {
        hovered = false
        refreshBackground()
    }

    override func highlight(_ flag: Bool) {
        super.highlight(flag)
        refreshBackground()
    }

    private func refreshBackground() {
        let color: NSColor
        if isHighlighted {
            color = .labelColor.withAlphaComponent(0.18)
        } else if hovered {
            color = .labelColor.withAlphaComponent(0.10)
        } else {
            color = .clear
        }
        layer?.backgroundColor = color.cgColor
    }
}
