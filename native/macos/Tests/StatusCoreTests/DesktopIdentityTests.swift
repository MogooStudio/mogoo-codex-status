import XCTest
@testable import StatusCore

final class DesktopIdentityTests: XCTestCase {
    func testRenamedCodexBundleUsesStableIdentifier() {
        XCTAssertTrue(DesktopIdentity.isCodex(bundleIdentifier: "com.openai.codex", bundleName: "ChatGPT.app"))
    }

    func testLegacyCodexBundleStillSupported() {
        XCTAssertTrue(DesktopIdentity.isCodex(bundleIdentifier: "com.openai.codex", bundleName: "Codex.app"))
        XCTAssertTrue(DesktopIdentity.isCodex(bundleIdentifier: nil, bundleName: "Codex.app"))
    }

    func testOrdinaryChatGPTIsNotCodex() {
        XCTAssertFalse(DesktopIdentity.isCodex(bundleIdentifier: "com.openai.chatgpt", bundleName: "ChatGPT.app"))
    }
}
