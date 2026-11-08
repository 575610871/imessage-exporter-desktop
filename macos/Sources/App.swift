import AppKit
import SwiftUI

@main struct SuohApplication {
    @MainActor static func main() {
        if CommandLine.arguments.contains("--model-test") {
            do { try ModelVerification.run(); print("PASS: macOS model checks"); exit(0) }
            catch { fputs("Model check failed: \(error)\n", stderr); exit(1) }
        }
        let app = NSApplication.shared
        let delegate = AppDelegate()
        app.setActivationPolicy(.regular)
        app.delegate = delegate
        withExtendedLifetime(delegate) { app.run() }
    }
}

@MainActor final class AppDelegate: NSObject, NSApplicationDelegate, NSWindowDelegate {
    var window: NSWindow!
    var model: AppModel!
    private var adjusting = false
    private var displayObserver: NSObjectProtocol?

    func applicationDidFinishLaunching(_ notification: Notification) {
        let testIndex = CommandLine.arguments.firstIndex(of: "--ui-test")
        let testing = testIndex != nil
        model = AppModel(testing: testing)
        window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 1120, height: 820),
            styleMask: [.titled, .closable, .miniaturizable, .resizable], backing: .buffered, defer: false)
        window.title = Product.name
        window.isReleasedWhenClosed = false
        window.delegate = self
        window.backgroundColor = NSColor(red: 250/255, green: 247/255, blue: 247/255, alpha: 1)
        window.contentView = NSHostingView(rootView: MainView(model: model))
        window.contentMinSize = NSSize(width: 520, height: 420)
        configureMenu()
        recenter()
        window.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)
        displayObserver = NotificationCenter.default.addObserver(forName: NSApplication.didChangeScreenParametersNotification,
            object: nil, queue: .main) { [weak self] _ in Task { @MainActor in self?.constrain() } }
        DispatchQueue.main.async { [weak self] in self?.recenter() }
        if let index = testIndex, CommandLine.arguments.count > index + 1 {
            let folder = URL(fileURLWithPath: CommandLine.arguments[index + 1])
            Task { @MainActor in
                do {
                    try await UIVerification.run(delegate: self, output: folder)
                    NSApp.terminate(nil)
                } catch {
                    try? FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
                    try? String(describing: error).write(to: folder.appendingPathComponent("ui-failure.txt"), atomically: true, encoding: .utf8)
                    fputs("UI check failed: \(error)\n", stderr)
                    exit(1)
                }
            }
        }
    }
    private var targetScreen: NSScreen? {
        NSScreen.screens.first { $0.frame.contains(NSEvent.mouseLocation) } ?? window?.screen ?? NSScreen.main
    }
    @objc func recenter() {
        guard !adjusting, let screen = targetScreen else { return }
        adjusting = true; defer { adjusting = false }
        let area = screen.visibleFrame.insetBy(dx: 16, dy: 16)
        window.contentMinSize = NSSize(width: min(520, max(1, area.width)), height: min(420, max(1, area.height - 40)))
        let width = min(1120, area.width), height = min(848, area.height)
        window.setFrame(NSRect(x: area.midX - width / 2, y: area.midY - height / 2, width: width, height: height), display: true)
    }
    func constrain() {
        guard !adjusting, window != nil, !window.styleMask.contains(.fullScreen), let screen = window.screen ?? targetScreen else { return }
        adjusting = true; defer { adjusting = false }
        let area = screen.visibleFrame.insetBy(dx: 8, dy: 8)
        window.contentMinSize = NSSize(width: min(520, max(1, area.width)), height: min(420, max(1, area.height - 40)))
        var rect = window.frame
        rect.size.width = min(rect.width, area.width); rect.size.height = min(rect.height, area.height)
        rect.origin.x = min(max(rect.minX, area.minX), area.maxX - rect.width)
        rect.origin.y = min(max(rect.minY, area.minY), area.maxY - rect.height)
        if rect != window.frame { window.setFrame(rect, display: true) }
    }
    func windowDidResize(_ notification: Notification) { constrain() }
    func windowDidChangeScreen(_ notification: Notification) { constrain() }
    func windowDidChangeBackingProperties(_ notification: Notification) { constrain() }
    @objc func showLogs() { model.logsVisible = true }
    @objc func showGuide() { model.guide() }
    private func configureMenu() {
        let bar = NSMenu()
        let appItem = NSMenuItem(); bar.addItem(appItem)
        let menu = NSMenu(); appItem.submenu = menu
        let guide = NSMenuItem(title: "使用指南", action: #selector(showGuide), keyEquivalent: "?"); guide.target = self; menu.addItem(guide)
        menu.addItem(.separator())
        menu.addItem(withTitle: "退出 " + Product.name, action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q")
        let editItem = NSMenuItem(); bar.addItem(editItem)
        let edit = NSMenu(title: "编辑"); editItem.submenu = edit
        for (title, action, key) in [("撤销", "undo:", "z"), ("剪切", "cut:", "x"), ("复制", "copy:", "c"), ("粘贴", "paste:", "v"), ("全选", "selectAll:", "a")] {
            edit.addItem(withTitle: title, action: Selector(action), keyEquivalent: key)
        }
        let windowItem = NSMenuItem(); bar.addItem(windowItem)
        let windowMenu = NSMenu(title: "窗口"); windowItem.submenu = windowMenu
        let center = NSMenuItem(title: "重新居中", action: #selector(recenter), keyEquivalent: "0"); center.target = self; windowMenu.addItem(center)
        let logs = NSMenuItem(title: "任务日志", action: #selector(showLogs), keyEquivalent: "l"); logs.target = self; windowMenu.addItem(logs)
        windowMenu.addItem(withTitle: "最小化", action: #selector(NSWindow.performMiniaturize(_:)), keyEquivalent: "m")
        NSApp.mainMenu = bar; NSApp.windowsMenu = windowMenu
    }
    func windowShouldClose(_ sender: NSWindow) -> Bool {
        guard model.busy else { model.savePreferences(); return true }
        if confirmStop() { model.afterStop = { [weak self] in self?.window.performClose(nil) }; model.stop() }
        return false
    }
    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { true }
    func applicationShouldTerminate(_ sender: NSApplication) -> NSApplication.TerminateReply {
        guard model != nil && model.busy else { model?.savePreferences(); return .terminateNow }
        guard confirmStop() else { return .terminateCancel }
        model.afterStop = { NSApp.reply(toApplicationShouldTerminate: true) }
        model.stop()
        return .terminateLater
    }
    private func confirmStop() -> Bool {
        let alert = NSAlert(); alert.messageText = "任务仍在运行"
        alert.informativeText = "要停止导出并关闭吗？部分导出文件会保留。"
        alert.addButton(withTitle: "停止并关闭"); alert.addButton(withTitle: "继续导出")
        return alert.runModal() == .alertFirstButtonReturn
    }
}
