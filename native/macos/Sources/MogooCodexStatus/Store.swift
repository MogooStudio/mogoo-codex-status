import Foundation

enum Store {
    static func timestamp() -> String {
        let formatter = ISO8601DateFormatter(); formatter.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        return formatter.string(from: Date())
    }
    static func dictionary(_ path: String) -> [String: Any] {
        guard let data = FileManager.default.contents(atPath: path), let value = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else { return [:] }
        return value
    }
    static func write(_ value: [String: Any], _ path: String) {
        guard let data = try? JSONSerialization.data(withJSONObject: value, options: [.sortedKeys]) else { return }
        try? FileManager.default.createDirectory(atPath: (path as NSString).deletingLastPathComponent, withIntermediateDirectories: true)
        try? data.write(to: URL(fileURLWithPath: path), options: .atomic)
    }
    static func number(_ value: Any?, fallback: Double = 0) -> Double { (value as? NSNumber)?.doubleValue ?? fallback }
}
