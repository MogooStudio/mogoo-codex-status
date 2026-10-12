import AppKit
import SwiftUI
import StatusCore

struct Skin {
    let background: Color; let border: Color; let text: Color; let secondary: Color; let warning: Color; let danger: Color
    static func color(_ hex: UInt32) -> Color { Color(red: Double((hex >> 16) & 255) / 255, green: Double((hex >> 8) & 255) / 255, blue: Double(hex & 255) / 255) }
    static func selected(_ light: Bool) -> Skin {
        light ? Skin(background: color(0xfafafa), border: color(0xd9dce2), text: color(0x383a42), secondary: color(0x5c6370), warning: color(0x986801), danger: color(0xa63c45)) :
            Skin(background: color(0x282c34), border: color(0x363d49), text: color(0xd7dae0), secondary: color(0xabb2bf), warning: color(0xe5c07b), danger: color(0xefc0c4))
    }
}
struct StatusView: View {
    @ObservedObject var model: StatusModel
    var skin: Skin { Skin.selected(model.light) }
    var contextColor: Color { let pct = model.context?.usedPercent ?? 0; return pct >= 95 ? skin.danger : pct >= 80 ? skin.warning : skin.text }
    var modeButton: some View {
        Button(action: model.toggleMode) {
            Image(systemName: "waveform.path.ecg").font(.system(size: 12, weight: .semibold)).frame(width: 18, height: 18).foregroundColor(skin.background).background(skin.text, in: Circle())
        }.buttonStyle(.plain).help(model.compact ? "展开完整指标" : "收起为精简模式").accessibilityLabel("切换精简模式")
    }
    var body: some View {
        ZStack {
            RoundedRectangle(cornerRadius: 14).fill(skin.background)
            RoundedRectangle(cornerRadius: 14).stroke(skin.border, lineWidth: 1)
            DragRegion(model: model)
            if model.compact {
                HStack(spacing: 8) { modeButton; Text("Codex Status").fontWeight(.semibold).allowsHitTesting(false) }.help(model.details)
            } else {
                HStack(spacing: 0) {
                    HStack(spacing: 8) { modeButton; (Text("上下文 ").foregroundColor(skin.secondary) + Text(String(model.contextText.dropFirst(4))).foregroundColor(contextColor).fontWeight(.semibold)).fixedSize().help(model.contextDetails).allowsHitTesting(false) }
                    Spacer(minLength: model.gap)
                    quotaGroup.fixedSize().help(model.limitDetails).allowsHitTesting(false)
                    if model.showReset { Spacer(minLength: model.gap); Text(model.resetText).foregroundColor(skin.secondary).fixedSize().help(model.limitDetails).allowsHitTesting(false) }
                    Button(action: model.cycleSkin) { Image(systemName: "paintpalette").frame(width: 24, height: 24).foregroundColor(skin.secondary) }.buttonStyle(.plain).padding(.leading, 8).help("切换深色／浅色").accessibilityLabel("切换皮肤")
                }.padding(.leading, 14).padding(.trailing, 8)
            }
        }
        .font(.system(size: 13)).monospacedDigit().foregroundColor(skin.text)
        .frame(width: model.width, height: model.compact ? 28 : 32)
        .contextMenu {
            Button("Codex 深色") { model.changeSkin(false) }; Button("Codex 浅色") { model.changeSkin(true) }
            Divider(); Button(model.compact ? "展开完整模式" : "切换精简模式", action: model.toggleMode)
            Button("刷新限额", action: model.refresh)
            Divider(); Button("退出状态条") { NSApplication.shared.terminate(nil) }
        }
    }
    var quotaGroup: some View {
        HStack(spacing: 0) {
            Text(model.limits.isEmpty ? "限额 等待查询" : model.shortPeriods ? "剩余 " : "限额剩余 ").foregroundColor(skin.secondary)
            ForEach(Array(model.shown.enumerated()), id: \.offset) { position, index in
                if position > 0 { Text(" · ").foregroundColor(skin.secondary) }
                Text(model.label(index, short: model.shortPeriods)).foregroundColor(skin.secondary)
                let value = model.limits[index].remainingPercent
                Text(StatusFormat.percent(value)).fontWeight(.semibold).foregroundColor((value ?? 100) <= 5 ? skin.danger : (value ?? 100) <= 20 ? skin.warning : skin.text)
                if model.limits[index].stale == true || !model.stateFresh { Text(" 缓存").foregroundColor(skin.secondary) }
            }
            if model.limits.count > model.shown.count { Text("+\(model.limits.count - model.shown.count)").foregroundColor(skin.secondary).padding(.leading, 4) }
        }
    }
}
struct DragRegion: NSViewRepresentable {
    let model: StatusModel
    func makeNSView(context: Context) -> DragSurface { DragSurface(model: model) }
    func updateNSView(_ view: DragSurface, context: Context) {}
}
final class DragSurface: NSView {
    weak var model: StatusModel?
    init(model: StatusModel) { self.model = model; super.init(frame: .zero) }
    required init?(coder: NSCoder) { fatalError("Not used") }
    override func acceptsFirstMouse(for event: NSEvent?) -> Bool { true }
    override func mouseDown(with event: NSEvent) {
        if event.clickCount == 2 { model?.refresh(); return }
        guard let model, let window else { return }
        model.dragging = true
        window.performWindowDrag(with: event)
        model.dragging = false; model.finishDrag()
    }
}
final class StatusHosting: NSHostingView<StatusView> {
    override func acceptsFirstMouse(for event: NSEvent?) -> Bool { true }
}
