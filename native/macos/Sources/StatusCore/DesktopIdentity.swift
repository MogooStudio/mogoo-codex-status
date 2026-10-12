import Foundation

public enum DesktopIdentity {
    public static func isCodex(bundleIdentifier: String?, bundleName: String?) -> Bool {
        bundleIdentifier == "com.openai.codex" || (bundleIdentifier == nil && bundleName == "Codex.app")
    }
}
