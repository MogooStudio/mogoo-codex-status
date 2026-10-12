import Foundation

public struct ContextMetric: Codable {
    public let usedTokens: Double?
    public let capacityTokens: Double?
    public let usedPercent: Double?
    public let updatedAt: String?
}
public struct LimitMetric: Codable {
    public let name: String?
    public let label: String?
    public let usedPercent: Double?
    public let remainingPercent: Double?
    public let resetsAt: Double?
    public let updatedAt: String?
    public let stale: Bool?
}
public struct StatusState: Codable {
    public let updatedAt: String?
    public let selectedTitle: String?
    public let selectionStatus: String?
    public let thread: ThreadMetric?
    public let context: ContextMetric?
    public let limits: [LimitMetric]?
}
public struct ThreadMetric: Codable {
    public let id: String
    public let title: String?
}
public enum StatusFormat {
    public static func date(_ text: String?) -> Date? {
        guard let text else { return nil }
        let formatter = ISO8601DateFormatter(); formatter.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        return formatter.date(from: text) ?? ISO8601DateFormatter().date(from: text)
    }
    public static func fresh(_ text: String?, now: Date = Date()) -> Bool {
        guard let at = date(text) else { return false }; let age = now.timeIntervalSince(at); return age >= -5 && age < 10
    }
    public static func number(_ value: Double) -> String {
        String(format: "%.1f", locale: Locale(identifier: "en_US_POSIX"), value).replacingOccurrences(of: ".0", with: "")
    }
    public static func percent(_ value: Double?) -> String {
        guard let value, value.isFinite else { return "—" }; return number(value) + "%"
    }
    public static func context(_ metric: ContextMetric?, status: String?) -> String {
        guard let pct = metric?.usedPercent, pct.isFinite else {
            return "上下文 " + (status == "ambiguous" ? "聊天重名" : ["unknown", "unmatched"].contains(status ?? "") ? "未匹配" : "等待上报")
        }
        let tokens = metric?.capacityTokens
        let capacity = tokens != nil && tokens!.isFinite && tokens! > 0 ? number(tokens! / 1000) + "k" : "—"
        return "上下文 " + number(pct.rounded(.toNearestOrAwayFromZero)) + "% / " + capacity
    }
    public static func countdown(_ seconds: Double?, now: Date = Date(), short: Bool = false) -> String {
        guard let seconds, seconds > 0 else { return "未提供" }
        let left = seconds - now.timeIntervalSince1970
        guard left > 0 else { return "待确认重置" }
        let total = Int(left); let days = total / 86400; let hours = (total % 86400) / 3600; let minutes = (total % 3600) / 60
        let h = short ? "h" : "小时"; let m = short ? "m" : "分"
        if days > 0 { return "\(days)天\(hours)\(h)" }
        if total >= 3600 { return "\(hours)\(h)\(minutes)\(m)" }
        return total < 60 ? "<1分钟" : "\(total / 60)分钟"
    }
    public static func nextReset(_ limits: [LimitMetric], now: Date = Date()) -> Int? {
        let future = limits.indices.filter { (limits[$0].resetsAt ?? 0) > now.timeIntervalSince1970 }
        if let next = future.min(by: { limits[$0].resetsAt! < limits[$1].resetsAt! }) { return next }
        return limits.indices.filter { (limits[$0].resetsAt ?? 0) > 0 }.max(by: { limits[$0].resetsAt! < limits[$1].resetsAt! }) ?? limits.indices.first
    }
    public static func resetDate(_ seconds: Double?) -> String {
        guard let seconds, seconds > 0 else { return "未提供" }
        let formatter = DateFormatter(); formatter.locale = Locale(identifier: "zh_CN"); formatter.timeZone = TimeZone(identifier: "Asia/Shanghai"); formatter.dateFormat = "yyyy-MM-dd HH:mm"
        return formatter.string(from: Date(timeIntervalSince1970: seconds)) + "（北京时间）"
    }
}
public enum Docking {
    // Accessibility uses top-left screen coordinates; panel placement uses Cocoa coordinates.
    public static func x(parentLeft: Double, parentWidth: Double, width: Double, desired: Double) -> Double {
        let inset = min(8, max(0, (parentWidth - width) / 2))
        return max(parentLeft + inset, min(parentLeft + parentWidth - width - inset, desired))
    }
    public static func width(parentWidth: Double, compact: Bool, limits: Int) -> Double {
        compact ? 140 : min(limits > 1 ? 560 : 480, max(280, parentWidth - 300))
    }
}
