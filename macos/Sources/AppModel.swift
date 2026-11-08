import AppKit
import SwiftUI

@MainActor final class AppModel: ObservableObject {
    @Published var source = ""
    @Published var output = ""
    @Published var format = "html"
    @Published var media = "clone"
    @Published var password = ""
    @Published var from = ""
    @Published var before = ""
    @Published var conversation = ""
    @Published var myName = ""
    @Published var callerID = false
    @Published var eager = false
    @Published var timestamps = false
    @Published var remember = false
    @Published var openAfter = false
    @Published var logsVisible = false
    @Published var logs = ""
    @Published var busy = false
    @Published var finding = false
    @Published var status = "准备就绪 · 选择备份后即可开始"
    @Published var elapsed = ""
    @Published var result: URL?
    @Published var backups: [String] = []
    var afterStop: (() -> Void)?
    let testing: Bool
    private var task: EngineTask?
    private var history: [String] = []
    private var start: Date?
    private var timer: Timer?

    init(testing: Bool) {
        self.testing = testing
        output = testing ? "/Users/example/Documents/iMessage Exports" :
            FileManager.default.urls(for: .documentDirectory, in: .userDomainMask)[0].appendingPathComponent("iMessage Exports").path
        if !testing, let saved = Preferences.read() {
            source = saved.source; output = saved.output
            media = ["clone", "disabled", "basic", "full"].contains(saved.media) ? saved.media : "clone"
            format = saved.format == "txt" ? "txt" : "html"
            remember = true
        }
        timer = Timer.scheduledTimer(withTimeInterval: 0.12, repeats: true) { [weak self] _ in
            Task { @MainActor in self?.flush() }
        }
    }
    func pickSource() { if let value = pickFolder(title: "选择 iPhone 备份根目录", initial: source) { source = value } }
    func pickOutput() { if let value = pickFolder(title: "选择导出文件夹的保存位置", initial: output) { output = value } }
    private func pickFolder(title: String, initial: String) -> String? {
        let panel = NSOpenPanel()
        panel.title = title; panel.canChooseDirectories = true; panel.canChooseFiles = false
        panel.allowsMultipleSelection = false; panel.canCreateDirectories = true
        if FileManager.default.fileExists(atPath: initial) { panel.directoryURL = URL(fileURLWithPath: initial) }
        return panel.runModal() == .OK ? panel.url?.path : nil
    }
    func findBackups() {
        guard !busy && !finding && !testing else { return }
        finding = true; status = "正在查找本机 Finder / iTunes 备份…"
        DispatchQueue.global(qos: .userInitiated).async { [weak self] in
            let found = ExportPlan.backups()
            DispatchQueue.main.async {
                guard let self else { return }
                self.backups = found; self.finding = false
                if found.count == 1 { self.source = found[0] }
                self.status = found.isEmpty ? "没有找到备份，可点击“浏览”选择。若遇到权限错误，请检查系统的完全磁盘访问权限。" : "找到 \(found.count) 个备份，请确认所选目录。"
            }
        }
    }
    func run(diagnostics: Bool = false) {
        guard !busy && !finding && !testing else { return }
        do {
            guard !source.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { throw PlanError("请先选择 iPhone 本地备份。") }
            guard diagnostics || !output.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { throw PlanError("请先选择导出位置。") }
            let destination = ExportPlan.newDestination(in: URL(fileURLWithPath: output.trimmingCharacters(in: .whitespacesAndNewlines)))
            let plan = ExportPlan(source: URL(fileURLWithPath: source.trimmingCharacters(in: .whitespacesAndNewlines)), destination: destination,
                format: format, media: media, from: from.trimmingCharacters(in: .whitespacesAndNewlines),
                before: before.trimmingCharacters(in: .whitespacesAndNewlines), conversation: conversation, myName: myName,
                callerID: callerID, eagerImages: eager, timestamps: timestamps)
            try plan.validate(diagnostics: diagnostics)
            if !diagnostics && media == "full" && !EngineTask.hasTool("ffmpeg") {
                throw PlanError("完整媒体转换需要 FFmpeg。可选择“保留原文件”，或将完整工具放到应用旁的 tools 文件夹 / PATH。")
            }
            guard FileManager.default.isExecutableFile(atPath: Product.engine.path) else { throw PlanError("应用内缺少引擎，请重新完整解压应用。") }
            if !diagnostics { try FileManager.default.createDirectory(at: destination, withIntermediateDirectories: true) }
            savePreferences()
            let secret = password; password = ""
            let engine = EngineTask(); task = engine
            busy = true; result = nil; history = []; logs = ""; start = Date(); logsVisible = true
            status = diagnostics ? "正在检查备份…" : "正在导出…具体进展请查看任务日志。"
            append(["备份：" + plan.source.path] + (diagnostics ? [] : ["输出：" + destination.path]))
            engine.run(plan, diagnostics: diagnostics, password: secret) { [weak self] outcome, stopped in
                DispatchQueue.main.async {
                    guard let self else { return }
                    self.flush(); self.busy = false; self.start = nil; self.task = nil
                    var success = false
                    if stopped { self.status = "已取消。部分导出文件会保留，不代表完整导出。" }
                    else {
                        switch outcome {
                        case .success(0):
                            success = true
                            self.status = diagnostics ? "检查完成，请查看任务日志。" : "导出完成 · 可打开结果文件夹查看。"
                            self.append(["引擎正常完成，请留意日志中的附件缺失等警告。"])
                        case .success(let code): self.status = "任务未完成（退出码 \(code)），请查看日志并确认备份密码。"
                        case .failure(let error): self.status = "任务出错，请查看日志。"; self.append([error.localizedDescription])
                        }
                    }
                    if !diagnostics && FileManager.default.fileExists(atPath: destination.path) { self.result = destination }
                    if let close = self.afterStop { self.afterStop = nil; close() }
                    else if success && !diagnostics && self.openAfter { self.openResult() }
                }
            }
        } catch { problem(error.localizedDescription) }
    }
    func stop() { status = "正在停止导出引擎…"; task?.cancel() }
    private func append(_ lines: [String]) {
        guard !lines.isEmpty else { return }
        history.append(contentsOf: lines)
        if history.count > 3000 { history.removeFirst(history.count - 3000) }
        logs = history.joined(separator: "\n")
    }
    func flush() {
        append(task?.takeLogs() ?? [])
        if let start {
            let seconds = max(0, Int(Date().timeIntervalSince(start)))
            elapsed = String(format: "%02d:%02d:%02d", seconds / 3600, seconds / 60 % 60, seconds % 60)
        }
    }
    func savePreferences() {
        guard remember && !testing else { return }
        do { try Preferences(source: source, output: output, media: media, format: format).write() }
        catch { append(["无法保存目录设置，不影响导出：" + error.localizedDescription]) }
    }
    func forgetPreferences() {
        guard !remember && !testing else { return }
        do { try Preferences.forget() } catch { problem("无法删除已保存的设置：" + error.localizedDescription) }
    }
    func saveLog() {
        flush()
        let panel = NSSavePanel(); panel.nameFieldStringValue = "imessage-exporter-desktop-suoh-edition-log.txt"
        if panel.runModal() == .OK, let url = panel.url {
            do { try logs.write(to: url, atomically: true, encoding: .utf8) }
            catch { problem("无法保存日志：" + error.localizedDescription) }
        }
    }
    func openResult() { if let result { NSWorkspace.shared.open(result) } }
    func problem(_ text: String) {
        status = text
        let alert = NSAlert(); alert.messageText = "请检查"; alert.informativeText = text; alert.runModal()
    }
    func guide() {
        let alert = NSAlert(); alert.messageText = Product.name
        alert.informativeText = "1. 使用 Finder 将 iPhone 备份到 Mac。\n2. 选择包含 Manifest.plist 与 Manifest.db 的设备目录。\n3. 选择导出位置、格式和附件方式；加密备份填写本地备份密码。\n4. 点击开始导出，HTML 可用浏览器打开。\n\n所有导出在本机完成，不直接连接手机或下载 iCloud。\n\n每次启动居中。⌘0 重新居中，⌘L 查看日志。\n\n仅勾选记住设置时，目录和格式保存到本机 Application Support；密码不保存、不出现在命令行。\n\n正常结束或取消后会清理本任务临时文件。意外退出可能留下临时内容。\n\nSuoh Edition \(Product.version) · 原引擎 ReagentX/imessage-exporter 5.0.1 · GPL-3.0-or-later"
        alert.runModal()
    }
}
