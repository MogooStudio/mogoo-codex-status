// swift-tools-version: 5.9
import PackageDescription
let package = Package(
    name: "MogooCodexStatus",
    platforms: [.macOS(.v13)],
    products: [.executable(name: "MogooCodexStatus", targets: ["MogooCodexStatus"])],
    targets: [
        .target(name: "StatusCore"),
        .executableTarget(name: "MogooCodexStatus", dependencies: ["StatusCore"]),
        .testTarget(name: "StatusCoreTests", dependencies: ["StatusCore"])
    ]
)
