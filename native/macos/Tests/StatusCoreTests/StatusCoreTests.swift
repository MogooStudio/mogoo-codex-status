import XCTest
@testable import StatusCore

final class StatusCoreTests: XCTestCase {
    func state(_ json: String) throws -> StatusState { try JSONDecoder().decode(StatusState.self, from: Data(json.utf8)) }
    func testContextUsesReportedWindowAndRounding() throws {
        let a = try state("{\"context\":{\"usedPercent\":65,\"capacityTokens\":256000},\"limits\":[]}")
        XCTAssertEqual(StatusFormat.context(a.context, status: "matched"), "上下文 65% / 256k")
        let b = try state("{\"context\":{\"usedPercent\":33.94,\"capacityTokens\":475000}}")
        XCTAssertEqual(StatusFormat.context(b.context, status: "matched"), "上下文 34% / 475k")
        XCTAssertEqual(StatusFormat.context(nil, status: "ambiguous"), "上下文 聊天重名")
        XCTAssertEqual(StatusFormat.context(nil, status: "unknown"), "上下文 未匹配")
    }
    func testMissingValuesStayUnknown() throws {
        let a = try state("{\"context\":{\"usedPercent\":65},\"limits\":[{\"label\":\"7天\",\"remainingPercent\":null}]}")
        XCTAssertEqual(StatusFormat.context(a.context, status: "matched"), "上下文 65% / —")
        XCTAssertEqual(StatusFormat.percent(a.limits!.first!.remainingPercent), "—")
        XCTAssertEqual(StatusFormat.countdown(nil), "未提供")
    }
    func testResetOwnerPrefersUpcomingWindow() throws {
        let now = Date(timeIntervalSince1970: 1000)
        let a = try state("{\"limits\":[{\"label\":\"5小时\",\"resetsAt\":900},{\"label\":\"7天\",\"resetsAt\":1600}]}")
        XCTAssertEqual(StatusFormat.nextReset(a.limits!, now: now), 1)
        XCTAssertEqual(StatusFormat.countdown(900, now: now), "待确认重置")
        XCTAssertEqual(StatusFormat.countdown(1040, now: now), "<1分钟")
    }
    func testDockingClampsAtBothEdgesAndCapsWidth() {
        XCTAssertEqual(Docking.x(parentLeft: -1200, parentWidth: 1200, width: 480, desired: -1500), -1192)
        XCTAssertEqual(Docking.x(parentLeft: -1200, parentWidth: 1200, width: 480, desired: 500), -488)
        XCTAssertEqual(Docking.width(parentWidth: 1200, compact: false, limits: 1), 480)
        XCTAssertEqual(Docking.width(parentWidth: 1200, compact: true, limits: 2), 140)
        XCTAssertEqual(Docking.width(parentWidth: 600, compact: false, limits: 2), 300)
    }
    func testFreshnessRejectsOldAndFutureData() {
        let now = Date(timeIntervalSince1970: 1000)
        XCTAssertFalse(StatusFormat.fresh("1970-01-01T00:00:00Z", now: now))
        XCTAssertFalse(StatusFormat.fresh("1970-01-01T00:30:00Z", now: now))
        XCTAssertTrue(StatusFormat.fresh("1970-01-01T00:16:39.000Z", now: now))
    }
}
