import Foundation

enum Product {
    static let name = "iMessage Exporter Desktop Suoh Edition"
    static let version = "0.3.0"
    static var engine: URL {
        Bundle.main.bundleURL.appendingPathComponent("Contents/Resources/engine/imessage-exporter")
    }
    static var preferences: URL {
        FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
            .appendingPathComponent("IMessageExporterDesktopSuohEdition/preferences.json")
    }
}

struct PlanError: LocalizedError {
    let message: String
    var errorDescription: String? { message }
    init(_ message: String) { self.message = message }
}

struct ExportPlan {
    var source: URL
    var destination: URL
    var format = "html"
    var media = "clone"
    var from = ""
    var before = ""
    var conversation = ""
    var myName = ""
    var callerID = false
    var eagerImages = false
    var timestamps = false
    // The UI selects iPhone backups; macOS mode is also used by public-fixture integration checks.
    var platform = "iOS"

    func arguments(diagnostics: Bool = false) -> [String] {
        var args = ["--db-path", source.path, "--platform", platform]
        if diagnostics { return args + ["--diagnostics"] }
        args += ["--format", format, "--export-path", destination.path,
                 "--copy-method", media, "--no-progress"]
        if !from.isEmpty { args += ["--start-date", from] }
        if !before.isEmpty { args += ["--end-date", before] }
        if !conversation.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            args += ["--conversation-filter", conversation.trimmingCharacters(in: .whitespacesAndNewlines)]
        }
        if callerID { args += ["--use-caller-id"] }
        else if !myName.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            args += ["--custom-name", myName.trimmingCharacters(in: .whitespacesAndNewlines)]
        }
        if eagerImages && format == "html" { args += ["--no-lazy"] }
        if timestamps { args += ["--use-message-times"] }
        return args
    }

    func validate(diagnostics: Bool = false) throws {
        let fm = FileManager.default
        if platform == "iOS" {
            for name in ["Manifest.plist", "Manifest.db"] {
                guard fm.fileExists(atPath: source.appendingPathComponent(name).path) else {
                    throw PlanError("请选择同时包含 Manifest.plist 和 Manifest.db 的 iPhone 备份根目录。")
                }
            }
        }
        if diagnostics { return }
        guard ["html", "txt"].contains(format), ["clone", "disabled", "basic", "full"].contains(media) else {
            throw PlanError("导出格式或附件方式无效。")
        }
        for date in [from, before] where !date.isEmpty {
            guard Self.isDate(date) else { throw PlanError("日期请填写 YYYY-MM-DD，例如 2026-01-31；留空表示不限。") }
        }
        if !from.isEmpty && !before.isEmpty && before <= from {
            throw PlanError("结束日期必须晚于开始日期；结束当天不包含在导出中。")
        }
        let original = Self.physicalPath(source)
        let output = Self.physicalPath(destination)
        // Case folding is deliberately conservative for case-insensitive APFS volumes.
        let a = original.lowercased(), b = output.lowercased()
        guard b != a && !b.hasPrefix(a.hasSuffix("/") ? a : a + "/") else {
            throw PlanError("导出位置不能在原备份内部，请选择其他文件夹。")
        }
        guard !fm.fileExists(atPath: output) else { throw PlanError("本次导出目录已存在，请重新开始以生成新目录。") }
    }

    private static func physicalPath(_ url: URL) -> String {
        // Foundation can leave the whole path unresolved when the final child does not exist.
        // Resolve each existing ancestor first, then append the new export directory.
        var current = URL(fileURLWithPath: "/", isDirectory: true)
        for component in url.standardizedFileURL.pathComponents.dropFirst() {
            current.appendPathComponent(component)
            if FileManager.default.fileExists(atPath: current.path) {
                current = current.resolvingSymlinksInPath()
            }
        }
        return current.standardizedFileURL.path
    }

    static func isDate(_ value: String) -> Bool {
        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.calendar = Calendar(identifier: .gregorian)
        formatter.timeZone = TimeZone(secondsFromGMT: 0)
        formatter.dateFormat = "yyyy-MM-dd"
        formatter.isLenient = false
        guard value.count == 10, let date = formatter.date(from: value) else { return false }
        return formatter.string(from: date) == value
    }

    static func newDestination(in parent: URL) -> URL {
        let date = DateFormatter()
        date.locale = Locale(identifier: "en_US_POSIX")
        date.dateFormat = "yyyy-MM-dd_HH-mm-ss"
        return parent.appendingPathComponent("iMessage_\(date.string(from: Date()))_\(UUID().uuidString.prefix(6))")
    }

    static func backups() -> [String] {
        let parent = FileManager.default.homeDirectoryForCurrentUser
            .appendingPathComponent("Library/Application Support/MobileSync/Backup")
        let children = (try? FileManager.default.contentsOfDirectory(at: parent, includingPropertiesForKeys: nil)) ?? []
        return children.filter { child in
            ["Manifest.db", "Manifest.plist"].allSatisfy {
                FileManager.default.fileExists(atPath: child.appendingPathComponent($0).path)
            }
        }.map(\.path).sorted()
    }
}

struct Preferences: Codable {
    var source: String
    var output: String
    var media: String
    var format: String
    static func read() -> Preferences? {
        guard let data = try? Data(contentsOf: Product.preferences) else { return nil }
        return try? JSONDecoder().decode(Self.self, from: data)
    }
    func write() throws {
        try FileManager.default.createDirectory(at: Product.preferences.deletingLastPathComponent(), withIntermediateDirectories: true)
        try JSONEncoder().encode(self).write(to: Product.preferences, options: .atomic)
    }
    static func forget() throws {
        if FileManager.default.fileExists(atPath: Product.preferences.path) {
            try FileManager.default.removeItem(at: Product.preferences)
        }
    }
}
