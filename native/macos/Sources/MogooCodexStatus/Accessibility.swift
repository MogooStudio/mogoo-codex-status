import AppKit
import ApplicationServices
import Darwin
import StatusCore

struct WindowSnapshot {
    var title = ""
    var codexMode = false
    var foreground = false
    var frame: NSRect?
    var reason = "no-window"
    var pid: pid_t = 0
    var source: String?
    var titleCandidates: [String]?
    var windowId: CGWindowID?
    var json: [String: Any] {
        var result: [String: Any] = ["title": title, "codexMode": codexMode, "foreground": foreground, "pid": Int(pid), "hwnd": Int(pid), "reason": reason, "updatedAt": Store.timestamp()]
        if let source { result["source"] = source }
        if let titleCandidates { result["titleCandidates"] = titleCandidates }
        return result
    }
}
enum Accessibility {
    static func codexApplications() -> [NSRunningApplication] {
        NSWorkspace.shared.runningApplications.filter {
            DesktopIdentity.isCodex(bundleIdentifier: $0.bundleIdentifier, bundleName: $0.bundleURL?.lastPathComponent) && !$0.isTerminated
        }
    }
    static func desktopInfo() -> [String: Any] {
        let apps = codexApplications()
        guard let app = apps.first(where: { $0.isActive }) ?? apps.first, let bundle = app.bundleURL else { return ["pid": 0] }
        return ["pid": Int(app.processIdentifier), "bundlePath": bundle.path]
    }
    static func value(_ element: AXUIElement, _ attribute: String) -> CFTypeRef? {
        var value: CFTypeRef?
        guard AXUIElementCopyAttributeValue(element, attribute as CFString, &value) == .success else { return nil }
        return value
    }
    static func element(_ value: CFTypeRef?) -> AXUIElement? {
        guard let value, CFGetTypeID(value) == AXUIElementGetTypeID() else { return nil }
        return unsafeBitCast(value, to: AXUIElement.self)
    }
    static func inspect(pid: pid_t) -> WindowSnapshot {
        var result = WindowSnapshot(); result.pid = pid
        guard let app = NSRunningApplication(processIdentifier: pid), !app.isTerminated else { result.reason = "codex-exited"; return result }
        let front = NSWorkspace.shared.frontmostApplication?.processIdentifier
        result.foreground = front == pid || front == getpid()
        guard AXIsProcessTrusted() else { result.reason = "accessibility-permission-required"; return result }
        let application = AXUIElementCreateApplication(pid); AXUIElementSetMessagingTimeout(application, 0.15)
        guard let window = element(value(application, kAXFocusedWindowAttribute)) ?? element(value(application, kAXMainWindowAttribute)) else { return result }
        if value(window, kAXMinimizedAttribute) as? Bool == true { result.reason = "minimized"; return result }
        var point = CGPoint.zero; var size = CGSize.zero
        if let raw = value(window, kAXPositionAttribute), CFGetTypeID(raw) == AXValueGetTypeID() {
            AXValueGetValue(unsafeBitCast(raw, to: AXValue.self), .cgPoint, &point)
        }
        if let raw = value(window, kAXSizeAttribute), CFGetTypeID(raw) == AXValueGetTypeID() {
            AXValueGetValue(unsafeBitCast(raw, to: AXValue.self), .cgSize, &size)
        }
        guard size.width > 0 && size.height > 0 else { return result }
        let rawFrame = CGRect(origin: point, size: size)
        let windows = CGWindowListCopyWindowInfo([.optionOnScreenOnly, .excludeDesktopElements], kCGNullWindowID) as? [[String: Any]] ?? []
        result.windowId = windows.first(where: { info in
            guard info[kCGWindowOwnerPID as String] as? Int == Int(pid), info[kCGWindowLayer as String] as? Int == 0,
                  let bounds = info[kCGWindowBounds as String] as? [String: Any], let frame = CGRect(dictionaryRepresentation: bounds as CFDictionary) else { return false }
            return abs(frame.minX - rawFrame.minX) < 2 && abs(frame.minY - rawFrame.minY) < 2 && abs(frame.width - rawFrame.width) < 2 && abs(frame.height - rawFrame.height) < 2
        })?[kCGWindowNumber as String] as? CGWindowID
        let primaryTop = NSScreen.screens.first?.frame.maxY ?? size.height
        result.frame = NSRect(x: point.x, y: primaryTop - point.y - size.height, width: size.width, height: size.height)
        let windowTitle = value(window, kAXTitleAttribute) as? String ?? ""
        var queue: [(AXUIElement, Int)] = [(window, 0)]; var seen = 0; let deadline = Date().addingTimeInterval(0.65)
        let modeNames = Set(["切换模式，当前模式：Codex", "Switch mode, current mode: Codex", "与 Codex 协作", "Work with Codex"])
        // Read document titles and mode-button labels only; never AXValue or message text.
        while !queue.isEmpty && seen < 450 && Date() < deadline {
            let (item, depth) = queue.removeFirst(); seen += 1
            let role = value(item, kAXRoleAttribute) as? String ?? ""
            if role == "AXWebArea" && result.title.isEmpty { result.title = value(item, kAXTitleAttribute) as? String ?? "" }
            if [kAXButtonRole, kAXPopUpButtonRole, "AXMenuButton"].contains(role) {
                let labels = [value(item, kAXTitleAttribute) as? String ?? "", value(item, kAXDescriptionAttribute) as? String ?? ""]
                let label = labels.first(where: { !$0.isEmpty }) ?? ""
                if modeNames.contains(label) { result.codexMode = true }
            }
            if depth < 14, let children = value(item, kAXChildrenAttribute) as? [AXUIElement] {
                queue.append(contentsOf: children.prefix(120).map { ($0, depth + 1) })
            }
        }
        if result.title.isEmpty { result.title = windowTitle }
        result.reason = !result.foreground ? "not-foreground" : !result.codexMode ? "not-codex-mode" : "ready"
        return result
    }
}
