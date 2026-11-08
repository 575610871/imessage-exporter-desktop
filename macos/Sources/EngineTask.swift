import Foundation
import Darwin

/// Owns one local child process. Arguments never contain the backup password.
final class EngineTask: @unchecked Sendable {
    private let executable: URL
    private let lock = NSLock()
    private var process: Process?
    private var cancelled = false
    private var pending: [String] = []
    init(executable: URL = Product.engine) { self.executable = executable }

    func takeLogs() -> [String] {
        lock.lock(); defer { lock.unlock() }
        let result = pending; pending.removeAll(keepingCapacity: true)
        return result
    }
    private func log(_ line: String, password: String) {
        var clean = password.isEmpty ? line : line.replacingOccurrences(of: password, with: "[密码已隐藏]")
        if clean.count > 8000 { clean = String(clean.prefix(8000)) + " …" }
        lock.lock(); defer { lock.unlock() }
        pending.append(clean)
        if pending.count > 3000 { pending.removeFirst(pending.count - 3000) }
    }
    static func toolPath() -> String {
        let local = Bundle.main.bundleURL.deletingLastPathComponent().appendingPathComponent("tools").path
        return ([local, "/opt/homebrew/bin", "/usr/local/bin", "/usr/bin", "/bin"] +
                [(ProcessInfo.processInfo.environment["PATH"] ?? "")]).joined(separator: ":")
    }
    static func hasTool(_ name: String) -> Bool {
        toolPath().split(separator: ":").contains {
            FileManager.default.isExecutableFile(atPath: String($0) + "/" + name)
        }
    }

    func run(_ plan: ExportPlan, diagnostics: Bool, password: String,
             completion: @escaping (Result<Int32, Error>, Bool) -> Void) {
        DispatchQueue.global(qos: .userInitiated).async { [self] in
            let fm = FileManager.default
            let scratch = fm.temporaryDirectory.appendingPathComponent("Suoh-" + UUID().uuidString, isDirectory: true)
            let outcome: Result<Int32, Error>
            do {
                try fm.createDirectory(at: scratch, withIntermediateDirectories: false)
                defer {
                    do { try fm.removeItem(at: scratch) }
                    catch { log("临时文件清理未完成，请手动检查：" + scratch.path, password: password) }
                }
                guard fm.isExecutableFile(atPath: executable.path) else {
                    throw PlanError("找不到应用内的导出引擎，请重新完整解压 .app 应用。")
                }
                let child = Process(), pipe = Pipe()
                child.executableURL = executable
                child.arguments = plan.arguments(diagnostics: diagnostics)
                child.standardInput = FileHandle.nullDevice
                child.standardOutput = pipe
                child.standardError = pipe
                child.currentDirectoryURL = Bundle.main.bundleURL.deletingLastPathComponent()
                var env = ProcessInfo.processInfo.environment
                env["SUOH_GUI"] = "1"
                env["SUOH_BACKUP_PASSWORD"] = password
                env["TMPDIR"] = scratch.path + "/"
                env["PATH"] = Self.toolPath()
                child.environment = env
                lock.lock()
                if cancelled { lock.unlock(); throw PlanError("任务已取消。") }
                do { try child.run(); process = child; lock.unlock() }
                catch { lock.unlock(); throw error }
                // NSTask launch parameters are immutable after run(); release the local copy only.
                env.removeValue(forKey: "SUOH_BACKUP_PASSWORD")
                var buffer = Data()
                while true {
                    let chunk = pipe.fileHandleForReading.availableData
                    if chunk.isEmpty { break }
                    buffer.append(chunk)
                    while let newline = buffer.firstIndex(of: 10) {
                        log(String(decoding: buffer[..<newline], as: UTF8.self), password: password)
                        buffer.removeSubrange(...newline)
                    }
                    if buffer.count > 65536 {
                        log(String(decoding: buffer, as: UTF8.self), password: password)
                        buffer.removeAll(keepingCapacity: true)
                    }
                }
                if !buffer.isEmpty { log(String(decoding: buffer, as: UTF8.self), password: password) }
                child.waitUntilExit()
                outcome = .success(child.terminationStatus)
            } catch {
                outcome = .failure(PlanError(password.isEmpty ? error.localizedDescription :
                    error.localizedDescription.replacingOccurrences(of: password, with: "[密码已隐藏]")))
            }
            // Complete only after the scoped temporary directory has been cleaned up.
            lock.lock(); let stopped = cancelled; process = nil; lock.unlock()
            completion(outcome, stopped)
        }
    }

    func cancel() {
        lock.lock(); cancelled = true; let child = process; lock.unlock()
        guard let child, child.isRunning else { return }
        let pid = child.processIdentifier
        DispatchQueue.global().async {
            // Stop media converters before their parent; do not signal unrelated processes by name.
            let children = Self.descendants(of: pid)
            for childPID in children.reversed() { Darwin.kill(childPID, SIGTERM) }
            if child.isRunning { child.terminate() }
            DispatchQueue.global().asyncAfter(deadline: .now() + 3) {
                // The parent remains unreaped while running, so its PID cannot be reused here.
                if child.isRunning {
                    for childPID in Self.descendants(of: pid).reversed() { Darwin.kill(childPID, SIGKILL) }
                    Darwin.kill(pid, SIGKILL)
                }
            }
        }
    }
    private static func descendants(of parent: Int32, depth: Int = 0) -> [Int32] {
        guard depth < 16 else { return [] }
        let find = Process(), output = Pipe()
        find.executableURL = URL(fileURLWithPath: "/usr/bin/pgrep")
        find.arguments = ["-P", String(parent)]
        find.standardOutput = output; find.standardError = FileHandle.nullDevice
        do {
            try find.run()
            let bytes = output.fileHandleForReading.readDataToEndOfFile(); find.waitUntilExit()
            let children = String(decoding: bytes, as: UTF8.self).split(whereSeparator: \.isWhitespace).compactMap { Int32($0) }
            return children.flatMap { [$0] + descendants(of: $0, depth: depth + 1) }
        } catch { return [] }
    }
}
