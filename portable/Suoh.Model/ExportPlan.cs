using System.Globalization;

namespace Suoh.Model;

public enum TranscriptKind { Html, Txt }
public enum MediaHandling { Originals, None, Images, All }

/// <summary>A single immutable job; the password is deliberately not part of this serializable model.</summary>
public sealed record ExportPlan(
    string Source,
    string Destination,
    TranscriptKind Transcript = TranscriptKind.Html,
    MediaHandling Media = MediaHandling.Originals,
    DateOnly? From = null,
    DateOnly? Before = null,
    string Conversation = "",
    string MyName = "",
    bool CallerId = false,
    bool EagerImages = false,
    bool MessageTimestamps = false)
{
    public IReadOnlyList<string> Arguments(bool diagnose = false)
    {
        var args = new List<string> { "--db-path", Source, "--platform", "iOS" };
        if (diagnose) { args.Add("--diagnostics"); return args; }
        args.AddRange(["--format", Transcript == TranscriptKind.Html ? "html" : "txt",
            "--export-path", Destination, "--copy-method", Media switch
            {
                MediaHandling.Originals => "clone", MediaHandling.None => "disabled",
                MediaHandling.Images => "basic", MediaHandling.All => "full",
                _ => throw new ArgumentOutOfRangeException(nameof(Media))
            }, "--no-progress"]);
        if (From is { } from) args.AddRange(["--start-date", from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)]);
        if (Before is { } before) args.AddRange(["--end-date", before.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)]);
        if (!string.IsNullOrWhiteSpace(Conversation)) args.AddRange(["--conversation-filter", Conversation.Trim()]);
        if (CallerId) args.Add("--use-caller-id");
        else if (!string.IsNullOrWhiteSpace(MyName)) args.AddRange(["--custom-name", MyName.Trim()]);
        if (EagerImages && Transcript == TranscriptKind.Html) args.Add("--no-lazy");
        if (MessageTimestamps) args.Add("--use-message-times");
        return args;
    }

    public string? Validate(bool diagnose = false)
    {
        if (!Directory.Exists(Source)) return "请选择存在的 iPhone 本地备份文件夹。";
        if (!File.Exists(Path.Combine(Source, "Manifest.plist")) || !File.Exists(Path.Combine(Source, "Manifest.db")))
            return "请选中同时包含 Manifest.plist 和 Manifest.db 的设备备份根目录，不是 MobileSync 的上一级目录。";
        if (diagnose) return null;
        if (From is { } from && Before is { } before && before <= from)
            return "结束日期必须晚于开始日期；结束日期当天不包含在导出中。";
        if (string.IsNullOrWhiteSpace(Destination)) return "请选择导出位置。";
        try
        {
            var source = PhysicalPath(Source);
            var destination = PhysicalPath(Destination);
            if (destination.Equals(source, StringComparison.OrdinalIgnoreCase) ||
                destination.StartsWith(Path.TrimEndingDirectorySeparator(source) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return "导出位置不能在原备份内部。请选择其他文件夹，以保护原始数据。";
            if (File.Exists(destination) || Directory.Exists(destination))
                return "本次导出目录已经存在，请重新开始以生成一个新目录。";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { return "无法使用这个导出位置：" + e.Message; }
        return null;
    }

    private static string PhysicalPath(string value)
    {
        var full = Path.GetFullPath(value);
        var root = Path.GetPathRoot(full)!;
        var current = root;
        foreach (var segment in full[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (Directory.Exists(current))
                current = new DirectoryInfo(current).ResolveLinkTarget(true)?.FullName ?? current;
        }
        return Path.TrimEndingDirectorySeparator(current);
    }

    public static string NewDestination(string parent) => Path.Combine(Path.GetFullPath(parent.Trim()),
        $"iMessage_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_{Guid.NewGuid().ToString("N")[..6]}");
}
