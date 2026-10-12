import AppKit
import SwiftUI
import StatusCore
import Darwin

final class StatusPanel: NSPanel {
    override var canBecomeKey: Bool { false }
    override var canBecomeMain: Bool { false }
}
final class StatusModel: ObservableObject {
    @Published var light = false
    @Published var compact = false
    @Published var width: Double = 480
    @Published var context: ContextMetric?
    @Published var selectionStatus: String?
    @Published var limits: [LimitMetric] = []
    @Published var stateFresh = false
    @Published var shown: [Int] = []
    @Published var showReset = false
    @Published var shortPeriods = false
    @Published var shortReset = false
    @Published var gap: Double = 16
    var dragging = false
    let configPath: String; let dataDir: String; let desktopPid: pid_t
    let preview: Bool
    var window: StatusPanel?
    var parentFrame: NSRect?
    var selectedTitle = ""
    var timer: Timer?
    var dragTimer: Timer?
    var worker: Process?
    var workerInput: Pipe?
    var workerOutput: Pipe?
    var workerError: Pipe?

    init(configPath: String, desktopPid: pid_t, preview: Bool) {
        self.configPath = configPath; self.desktopPid = desktopPid; self.preview = preview
        let config = Store.dictionary(configPath); dataDir = config["dataDir"] as? String ?? ""
        light = ["codex-light", "frost"].contains(config["skin"] as? String ?? "codex")
        compact = config["displayMode"] as? String == "compact"
        width = compact ? 140 : Store.number(config["previewWidth"], fallback: 480)
    }
    var contextText: String { StatusFormat.context(context, status: selectionStatus) }
    var contextDetails: String {
        guard let context else { return selectedTitle + "\n尚未获得此聊天的 Token 用量" }
        let formatter = NumberFormatter(); formatter.numberStyle = .decimal; formatter.locale = Locale(identifier: "en_US")
        let used = formatter.string(from: NSNumber(value: context.usedTokens ?? 0)) ?? "—"
        let capacity = formatter.string(from: NSNumber(value: context.capacityTokens ?? 0)) ?? "—"
        return selectedTitle + "\n上下文已用≈" + StatusFormat.percent(context.usedPercent) + "\n\(used) / \(capacity) Token（最近上报，近似值）\n更新：\(context.updatedAt ?? "未知")"
    }
    func label(_ index: Int, short: Bool = false) -> String {
        let period = limits[index].label ?? "限额"
        var result = short ? period.replacingOccurrences(of: "小时", with: "h").replacingOccurrences(of: "分钟", with: "m") : period
        if limits.filter({ $0.label == limits[index].label }).count > 1 { result += "·" + String((limits[index].name ?? "限额").prefix(6)) + String(index + 1) }
        return result
    }
    var resetText: String {
        guard let index = StatusFormat.nextReset(limits) else { return "" }
        let text = StatusFormat.countdown(limits[index].resetsAt, short: shortReset)
        return label(index, short: shortReset) + (text == "待确认重置" ? " " : "重置 ") + text
    }
    var limitDetails: String {
        if limits.isEmpty { return "尚未获得账号限额数据" }
        return limits.indices.map { index in
            let row = limits[index]
            return label(index) + " · " + (row.name ?? "") + "\n剩余 " + StatusFormat.percent(row.remainingPercent) + " · 已用 " + StatusFormat.percent(row.usedPercent) + "\n重置：" + StatusFormat.resetDate(row.resetsAt) + " · " + StatusFormat.countdown(row.resetsAt) + "\n更新：" + (row.updatedAt ?? "未知") + (row.stale == true || !stateFresh ? "（缓存数据）" : "")
        }.joined(separator: "\n\n")
    }
    var details: String { contextDetails + "\n\n" + limitDetails + "\n\n点击左侧图标展开；按住文字或空白处拖动，松手吸附顶部。" }
    func measured(_ text: String) -> Double { Double((text as NSString).size(withAttributes: [.font: NSFont.systemFont(ofSize: 13, weight: .medium)]).width) }
    func layout() {
        showReset = !limits.isEmpty; shortPeriods = false; shortReset = false; gap = 16
        shown = Array(limits.indices.prefix(2))
        if let nearest = StatusFormat.nextReset(limits), !shown.contains(nearest) { shown = [nearest] + shown.prefix(1) }
        func total() -> Double {
            let quota = (shortPeriods ? "剩余 " : "限额剩余 ") + shown.map { label($0, short: shortPeriods) + StatusFormat.percent(limits[$0].remainingPercent) + (limits[$0].stale == true || !stateFresh ? " 缓存" : "") }.joined(separator: " · ") + (limits.count > shown.count ? "+\(limits.count - shown.count)" : "")
            return measured(contextText) + 26 + measured(quota) + (showReset ? measured(resetText) + gap : 0) + gap + 54 + 8
        }
        if total() > width { shortReset = true }
        if total() > width, let nearest = StatusFormat.nextReset(limits) { shown = [nearest] }
        if total() > width { showReset = false }
        if total() > width { shortPeriods = true; gap = 8 }
    }
    func load(_ title: String) {
        guard let data = FileManager.default.contents(atPath: dataDir + "/state.json"), let state = try? JSONDecoder().decode(StatusState.self, from: data) else {
            context = nil; limits = []; selectionStatus = nil; stateFresh = false; layout(); return
        }
        stateFresh = StatusFormat.fresh(state.updatedAt); selectedTitle = title
        context = stateFresh && (preview || state.selectedTitle == title) ? state.context : nil
        selectionStatus = state.selectionStatus; limits = state.limits ?? []; layout()
    }
    func save(_ changes: [String: Any]) -> Bool {
        var config = Store.dictionary(configPath); for (key, value) in changes { config[key] = value }
        guard let data = try? JSONSerialization.data(withJSONObject: config) else { return false }
        do { try data.write(to: URL(fileURLWithPath: configPath), options: .atomic); return true } catch { return false }
    }
    func changeSkin(_ next: Bool) { if save(["skin": next ? "codex-light" : "codex"]) { light = next } }
    func cycleSkin() { changeSkin(!light) }
    func toggleMode() {
        guard !dragging else { return }
        let next = !compact; var changes: [String: Any] = ["displayMode": next ? "compact" : "full"]
        if let parent = parentFrame, let window {
            let newWidth = Docking.width(parentWidth: parent.width, compact: next, limits: limits.count)
            let x = Docking.x(parentLeft: parent.minX, parentWidth: parent.width, width: newWidth, desired: window.frame.minX)
            changes["offsetRight"] = parent.maxX - x - newWidth; changes["offsetTop"] = 5
        }
        if save(changes) { compact = next; tick() }
    }
    func finishDrag() {
        guard let parent = parentFrame, let window else { return }
        let x = Docking.x(parentLeft: parent.minX, parentWidth: parent.width, width: window.frame.width, desired: window.frame.minX)
        window.setFrameOrigin(NSPoint(x: x, y: parent.maxY - window.frame.height - 5))
        _ = save(["offsetRight": parent.maxX - x - window.frame.width, "offsetTop": 5])
    }
    func beginDrag() {
        dragging = true; dragTimer?.invalidate()
        // AppKit's window-server drag returns immediately and may omit mouseUp.
        // Keep automatic docking paused until the physical left button is released.
        dragTimer = Timer(timeInterval: 0.02, repeats: true) { [weak self] timer in
            guard let self else { timer.invalidate(); return }
            if NSEvent.pressedMouseButtons & 1 == 0 {
                timer.invalidate(); self.dragTimer = nil; self.dragging = false; self.finishDrag()
            }
        }
        RunLoop.main.add(dragTimer!, forMode: .common)
    }
    func refresh() { Store.write(["at": Store.timestamp()], dataDir + "/refresh.json") }
    func runtime(_ visible: Bool, reason: String) {
        Store.write(["visible": visible, "reason": reason, "pid": Int(getpid()), "desktopPid": Int(desktopPid), "width": width, "height": compact ? 28 : 32, "displayMode": compact ? "compact" : "full", "updatedAt": Store.timestamp()], dataDir + "/runtime.json")
    }
    func tick() {
        if preview { load(""); return }
        if desktopPid <= 0 || kill(desktopPid, 0) != 0 { runtime(false, reason: "codex-exited"); NSApplication.shared.terminate(nil); return }
        if FileManager.default.fileExists(atPath: dataDir + "/stop.signal") { try? FileManager.default.removeItem(atPath: dataDir + "/stop.signal"); NSApplication.shared.terminate(nil); return }
        if dragging { runtime(true, reason: "dragging"); return }
        let snapshot = Accessibility.inspect(pid: desktopPid); Store.write(snapshot.json, dataDir + "/selection.json")
        load(snapshot.title)
        guard snapshot.reason == "ready", let frame = snapshot.frame else { window?.orderOut(nil); runtime(false, reason: snapshot.reason); return }
        parentFrame = frame; width = Docking.width(parentWidth: frame.width, compact: compact, limits: limits.count); layout()
        let config = Store.dictionary(configPath)
        let x = Docking.x(parentLeft: frame.minX, parentWidth: frame.width, width: width, desired: frame.maxX - width - Store.number(config["offsetRight"], fallback: 140))
        let height: Double = compact ? 28 : 32
        window?.setFrame(NSRect(x: x, y: frame.maxY - height - 5, width: width, height: height), display: true)
        window?.orderFrontRegardless(); runtime(true, reason: "ready")
    }
    func startWorker() throws {
        let config = Store.dictionary(configPath); let process = Process(); let input = Pipe(); let output = Pipe(); let error = Pipe()
        process.executableURL = URL(fileURLWithPath: config["nodePath"] as? String ?? "")
        process.arguments = ["--no-warnings", (config["pluginRoot"] as? String ?? "") + "/server/worker.mjs", "--config", configPath, "--parent-pid", String(getpid())]
        var environment = ProcessInfo.processInfo.environment; for key in ["CODEX_THREAD_ID", "CODEX_SESSION_ID", "CODEX_APP_TOOLS_PIPE_PATH"] { environment.removeValue(forKey: key) }
        process.environment = environment; process.standardInput = input; process.standardOutput = output; process.standardError = error
        output.fileHandleForReading.readabilityHandler = { handle in if handle.availableData.isEmpty { handle.readabilityHandler = nil } }
        error.fileHandleForReading.readabilityHandler = { handle in if handle.availableData.isEmpty { handle.readabilityHandler = nil } }
        try process.run(); worker = process; workerInput = input; workerOutput = output; workerError = error
    }
    func stop() {
        timer?.invalidate(); dragTimer?.invalidate(); try? workerInput?.fileHandleForWriting.write(contentsOf: Data("stop\n".utf8))
        if let worker {
            let deadline = Date().addingTimeInterval(2.5); while worker.isRunning && Date() < deadline { Thread.sleep(forTimeInterval: 0.05) }
            if worker.isRunning { worker.terminate() }
        }
        workerOutput?.fileHandleForReading.readabilityHandler = nil; workerError?.fileHandleForReading.readabilityHandler = nil
    }
}
