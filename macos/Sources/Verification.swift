import AppKit
import Foundation

enum ModelVerification {
    static func require(_ value: @autoclosure () -> Bool, _ reason: String) throws {
        if !value() { throw PlanError(reason) }
    }
    static func run() throws {
        let fm = FileManager.default
        let root = fm.temporaryDirectory.appendingPathComponent("Suoh-model-" + UUID().uuidString)
        try fm.createDirectory(at: root, withIntermediateDirectories: false)
        defer { try? fm.removeItem(at: root) }
        let source = root.appendingPathComponent("备份 with spaces")
        try fm.createDirectory(at: source, withIntermediateDirectories: false)
        for name in ["Manifest.plist", "Manifest.db"] { try Data().write(to: source.appendingPathComponent(name)) }
        var plan = ExportPlan(source: source, destination: root.appendingPathComponent("output"))
        try plan.validate()
        try require(plan.arguments().contains(source.path), "Unicode/spaced path was split")
        try require(!plan.arguments().contains("--cleartext-password"), "Password must not be in arguments")
        try require(plan.arguments(diagnostics: true) == ["--db-path", source.path, "--platform", "iOS", "--diagnostics"], "Diagnostics mapping")
        for media in ["clone", "disabled", "basic", "full"] {
            plan.media = media
            try require(plan.arguments().contains(media), "Media mapping")
        }
        plan.myName = "Test user"; plan.callerID = true; plan.eagerImages = true
        try require(!plan.arguments().contains("--custom-name") && plan.arguments().contains("--use-caller-id"), "Conflicting name flags")
        plan.format = "txt"
        try require(!plan.arguments().contains("--no-lazy"), "HTML-only option on TXT")
        for invalid in ["2026-02-29", "2026-2-01", "2026-13-01", "hello"] {
            try require(!ExportPlan.isDate(invalid), "Invalid date accepted: " + invalid)
        }
        try require(ExportPlan.isDate("2024-02-29"), "Leap-day validation")
        plan.from = "2026-11-01"; plan.before = "2026-11-01"
        try rejects(plan, "Empty date range allowed")
        try plan.validate(diagnostics: true)
        plan.from = ""; plan.before = ""
        plan.destination = source.appendingPathComponent("output")
        try rejects(plan, "Output inside backup accepted")
        let link = root.appendingPathComponent("linked backup")
        try fm.createSymbolicLink(at: link, withDestinationURL: source)
        plan.destination = link.appendingPathComponent("output")
        try rejects(plan, "Symlink bypassed backup protection")
        plan.destination = source
        try rejects(plan, "Backup overwrite allowed")
        try require(ExportPlan.newDestination(in: root) != ExportPlan.newDestination(in: root), "Destination collisions")
        let settings = try JSONEncoder().encode(Preferences(source: source.path, output: root.path, media: "clone", format: "html"))
        try require(!String(decoding: settings, as: UTF8.self).contains("password"), "Password saved in settings")

        // Exercise the actual process wrapper with harmless local scripts, not personal backups.
        plan = ExportPlan(source: source, destination: root.appendingPathComponent("result"))
        let echo = root.appendingPathComponent("test engine")
        let script = "#!/bin/sh\nprintf '%s\\n' \"$SUOH_GUI\" \"$SUOH_BACKUP_PASSWORD\" \"$TMPDIR\" \"$@\"\n"
        try script.write(to: echo, atomically: true, encoding: .utf8)
        try fm.setAttributes([.posixPermissions: 0o700], ofItemAtPath: echo.path)
        let runner = EngineTask(executable: echo), done = DispatchSemaphore(value: 0)
        var result: Result<Int32, Error>?, stopped = true
        runner.run(plan, diagnostics: false, password: "Suoh test secret") { value, cancelled in
            result = value; stopped = cancelled; done.signal()
        }
        try require(done.wait(timeout: .now() + 10) == .success, "Process wrapper timed out")
        let exitCode = try result?.get()
        try require(exitCode == 0 && !stopped, "Process wrapper failed")
        let lines = runner.takeLogs()
        try require(lines.contains("[密码已隐藏]") && !lines.joined().contains("Suoh test secret"), "Password redaction")
        try require(lines.contains(source.path), "ArgumentList did not preserve Unicode/space path")
        if let tmp = lines.first(where: { $0.contains("/Suoh-") }) {
            try require(!fm.fileExists(atPath: tmp), "Decrypted temporary directory survived completion")
        } else { throw PlanError("Job-specific temporary directory was not passed to the child") }
        try "#!/bin/sh\nsleep 30 &\nwait\n".write(to: echo, atomically: true, encoding: .utf8)
        let cancel = EngineTask(executable: echo), cancelledDone = DispatchSemaphore(value: 0)
        var wasCancelled = false
        cancel.run(plan, diagnostics: false, password: "") { _, value in wasCancelled = value; cancelledDone.signal() }
        Thread.sleep(forTimeInterval: 0.3)
        cancel.cancel()
        try require(cancelledDone.wait(timeout: .now() + 10) == .success && wasCancelled, "Process/child cancellation failed")
        print("PASS: argument, date, symlink, settings, Unicode, password, temp cleanup and cancellation checks")
    }
    private static func rejects(_ plan: ExportPlan, _ reason: String) throws {
        do { try plan.validate() } catch { return }
        throw PlanError(reason)
    }
}

@MainActor enum UIVerification {
    static func run(delegate: AppDelegate, output: URL) async throws {
        try FileManager.default.createDirectory(at: output, withIntermediateDirectories: true)
        try await Task.sleep(nanoseconds: 800_000_000)
        let window = delegate.window!
        try ModelVerification.require(window.title == Product.name, "Window title mismatch")
        try ModelVerification.require(window.styleMask.contains(.titled) && window.styleMask.contains(.closable) && window.styleMask.contains(.miniaturizable), "Native title bar controls missing")
        try ModelVerification.require(FileManager.default.isExecutableFile(atPath: Product.engine.path), "Bundled engine missing")
        try ModelVerification.require(Bundle.main.infoDictionary?["CFBundleShortVersionString"] as? String == Product.version, "Bundle version mismatch")
        guard let screen = window.screen else { throw PlanError("No screen for native-window verification") }
        try ModelVerification.require(screen.visibleFrame.contains(window.frame), "Startup outside visible work area")
        try ModelVerification.require(abs(window.frame.midX - screen.visibleFrame.midX) < 2 && abs(window.frame.midY - screen.visibleFrame.midY) < 2, "Startup not centered")
        let preferenceExisted = FileManager.default.fileExists(atPath: Product.preferences.path)
        var checks: [[String: Any]] = [["check": "native-startup", "title": window.title,
            "version": Product.version, "backingScaleFactor": window.backingScaleFactor,
            "frame": NSStringFromRect(window.frame), "visibleFrame": NSStringFromRect(screen.visibleFrame), "engine": Product.engine.path]]
        try capture(window, to: output.appendingPathComponent("startup-content.png"))
        for size in [NSSize(width: 1120, height: 820), NSSize(width: 850, height: 680), NSSize(width: 640, height: 480), NSSize(width: 520, height: 420)] {
            window.setContentSize(size)
            try await Task.sleep(nanoseconds: 250_000_000)
            window.contentView?.layoutSubtreeIfNeeded()
            try ModelVerification.require((window.screen ?? screen).visibleFrame.contains(window.frame), "Resized window outside work area")
            try capture(window, to: output.appendingPathComponent("layout-\(Int(size.width))x\(Int(size.height)).png"))
            checks.append(["check": "SwiftUI-layout", "contentSize": NSStringFromSize(window.contentView!.bounds.size)])
        }
        delegate.model.logsVisible = true
        delegate.model.logs = "示例日志：引擎 5.0.1，所有操作在本机进行。\nUnicode path: /Users/example/备份 with spaces"
        try await Task.sleep(nanoseconds: 250_000_000)
        try capture(window, to: output.appendingPathComponent("logs.png"))
        delegate.model.logsVisible = false
        window.setFrameOrigin(NSPoint(x: screen.visibleFrame.maxX + 5000, y: screen.visibleFrame.maxY + 5000))
        delegate.constrain(); delegate.recenter()
        try await Task.sleep(nanoseconds: 250_000_000)
        try ModelVerification.require((window.screen ?? screen).visibleFrame.contains(window.frame), "Off-screen repair failed")
        try ModelVerification.require(FileManager.default.fileExists(atPath: Product.preferences.path) == preferenceExisted, "UI test wrote preferences")
        checks.append(["check": "off-screen-repair", "frame": NSStringFromRect(window.frame)])
        let data = try JSONSerialization.data(withJSONObject: checks, options: [.prettyPrinted, .sortedKeys])
        try data.write(to: output.appendingPathComponent("ui-checks.json"))
        print(String(decoding: data, as: UTF8.self))
    }
    private static func capture(_ window: NSWindow, to path: URL) throws {
        guard let view = window.contentView, let bitmap = view.bitmapImageRepForCachingDisplay(in: view.bounds) else {
            throw PlanError("Unable to allocate native UI snapshot")
        }
        view.cacheDisplay(in: view.bounds, to: bitmap)
        guard let png = bitmap.representation(using: .png, properties: [:]) else { throw PlanError("PNG capture failed") }
        try png.write(to: path)
    }
}
