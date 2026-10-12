import AppKit
import SwiftUI
import ApplicationServices
import Darwin

var options: [String: String] = [:]
var index = 1
while index < CommandLine.arguments.count {
    let arg = CommandLine.arguments[index]
    if arg.hasPrefix("--") {
        let key = String(arg.dropFirst(2)); let hasValue = index + 1 < CommandLine.arguments.count && !CommandLine.arguments[index + 1].hasPrefix("--")
        options[key] = hasValue ? CommandLine.arguments[index + 1] : "true"; if hasValue { index += 1 }
    }; index += 1
}
if options["desktop-info"] != nil {
    let data = try JSONSerialization.data(withJSONObject: Accessibility.desktopInfo()); print(String(decoding: data, as: UTF8.self)); exit(0)
}
if let file = options["inspect"] {
    let info = Accessibility.desktopInfo(); let pid = pid_t(info["pid"] as? Int ?? 0)
    Store.write(Accessibility.inspect(pid: pid).json, file); exit(0)
}
guard let configPath = options["config"], let dataDir = Store.dictionary(configPath)["dataDir"] as? String, !dataDir.isEmpty else { exit(2) }
try FileManager.default.createDirectory(atPath: dataDir, withIntermediateDirectories: true)
let preview = options["render"] != nil || options["preview"] != nil
let owner = pid_t(options["desktop-pid"].flatMap(Int.init) ?? (Accessibility.desktopInfo()["pid"] as? Int ?? 0))
var lock: Int32 = -1
if !preview {
    lock = Darwin.open(dataDir + "/status.lock", O_CREAT | O_RDWR, S_IRUSR | S_IWUSR)
    if lock < 0 || flock(lock, LOCK_EX | LOCK_NB) != 0 { exit(0) }
    try? FileManager.default.removeItem(atPath: dataDir + "/stop.signal")
}
let application = NSApplication.shared
application.setActivationPolicy(.accessory)
let model = StatusModel(configPath: configPath, desktopPid: owner, preview: preview)
let panel = StatusPanel(contentRect: NSRect(x: 80, y: 80, width: model.width, height: model.compact ? 28 : 32), styleMask: [.borderless, .nonactivatingPanel], backing: .buffered, defer: false)
panel.isOpaque = false; panel.backgroundColor = .clear; panel.hasShadow = false; panel.level = .floating; panel.hidesOnDeactivate = false
panel.isReleasedWhenClosed = false; panel.isRestorable = false; panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]; panel.title = "Codex Status"
let hosting = StatusHosting(rootView: StatusView(model: model)); panel.contentView = hosting; model.window = panel
final class Delegate: NSObject, NSApplicationDelegate {
    let model: StatusModel
    init(_ model: StatusModel) { self.model = model }
    func applicationShouldSaveSecureApplicationState(_ app: NSApplication) -> Bool { false }
    func applicationShouldRestoreSecureApplicationState(_ app: NSApplication) -> Bool { false }
    func applicationWillTerminate(_ notification: Notification) { model.stop() }
}
let delegate = Delegate(model); application.delegate = delegate
if preview {
    model.tick(); panel.orderFrontRegardless(); hosting.layoutSubtreeIfNeeded()
    RunLoop.current.run(until: Date().addingTimeInterval(0.25))
    if let file = options["render"] {
        if let bitmap = hosting.bitmapImageRepForCachingDisplay(in: hosting.bounds) {
            hosting.cacheDisplay(in: hosting.bounds, to: bitmap)
            if let png = bitmap.representation(using: .png, properties: [:]) { try png.write(to: URL(fileURLWithPath: file)); exit(0) }
        }
        exit(3)
    }
} else {
    _ = AXIsProcessTrustedWithOptions([kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String: true] as CFDictionary)
    do { try model.startWorker() } catch { Store.write(["type": "worker-start-failed", "at": Store.timestamp()], dataDir + "/error.json"); exit(1) }
    model.timer = Timer(timeInterval: 0.5, repeats: true) { _ in model.tick() }; RunLoop.main.add(model.timer!, forMode: .common); model.tick()
}
application.run()
if lock >= 0 { flock(lock, LOCK_UN); Darwin.close(lock) }
