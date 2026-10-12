import AppKit
import ScreenCaptureKit
import Vision
import ImageIO
import CryptoKit

final class WindowTitleReader {
    private var key = ""
    private var nextRead = Date.distantPast
    private var task: Task<Void, Never>?
    private var digest: String?
    private var generation: UInt64 = 0
    private(set) var titles: [String]?

    func refresh(pid: pid_t, windowId: CGWindowID) {
        precondition(Thread.isMainThread)
        guard #available(macOS 14, *), CGPreflightScreenCaptureAccess() else { return }
        let nextKey = "\(pid):\(windowId)"
        if nextKey != key { cancel(); key = nextKey; nextRead = .distantPast }
        guard task == nil, Date() >= nextRead else { return }
        nextRead = Date().addingTimeInterval(1.5)
        generation &+= 1
        let requestGeneration = generation
        task = Task { @MainActor [weak self] in
            guard let self else { return }
            defer { if self.generation == requestGeneration { self.task = nil } }
            do {
                let content = try await SCShareableContent.excludingDesktopWindows(true, onScreenWindowsOnly: true)
                guard let window = content.windows.first(where: { $0.windowID == windowId && $0.owningApplication?.processID == pid }) else { return }
                let filter = SCContentFilter(desktopIndependentWindow: window)
                let configuration = SCStreamConfiguration()
                // 只采样标题栏；聊天正文位于该区域之外，图像仅在内存中处理。
                configuration.sourceRect = CGRect(x: 0, y: 0, width: window.frame.width, height: 44)
                configuration.width = Int(window.frame.width * 2)
                configuration.height = 88
                configuration.showsCursor = false
                let image = try await SCScreenshotManager.captureImage(contentFilter: filter, configuration: configuration)
                try Task.checkCancellation()
                let pixels = NSMutableData()
                guard let destination = CGImageDestinationCreateWithData(pixels, "public.png" as CFString, 1, nil) else { return }
                CGImageDestinationAddImage(destination, image, nil)
                guard CGImageDestinationFinalize(destination) else { return }
                let data = pixels as Data
                let fingerprint = SHA256.hash(data: data).map { String(format: "%02x", $0) }.joined()
                guard self.generation == requestGeneration else { return }
                if fingerprint == self.digest { return }
                var candidates: [String] = []
                for left in [0.19, 0.07] {
                    let request = VNRecognizeTextRequest()
                    request.recognitionLevel = .accurate
                    request.recognitionLanguages = ["zh-Hans", "en-US"]
                    request.usesLanguageCorrection = false
                    request.regionOfInterest = CGRect(x: left, y: 0, width: 0.42, height: 1)
                    try VNImageRequestHandler(data: data).perform([request])
                    for observation in request.results ?? [] {
                        guard let candidate = observation.topCandidates(1).first, candidate.confidence >= 0.4, candidate.string.count >= 4 else { continue }
                        let clipped = observation.boundingBox.maxX > 0.97
                        candidates.append(candidate.string + (clipped ? "…" : ""))
                    }
                }
                try Task.checkCancellation()
                guard self.generation == requestGeneration else { return }
                self.digest = fingerprint
                self.titles = Array(Set(candidates)).sorted { $0.count > $1.count }
            } catch {
                if self.generation == requestGeneration && !Task.isCancelled { self.titles = []; self.digest = nil }
            }
        }
    }

    func cancel() { precondition(Thread.isMainThread); generation &+= 1; task?.cancel(); task = nil; titles = nil; digest = nil; key = "" }
}
