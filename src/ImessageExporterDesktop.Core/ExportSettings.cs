namespace IMessageExporterDesktop.Core;

public enum ExportFormat
{
    Html,
    Text
}

public enum AttachmentMode
{
    Disabled,
    Clone,
    Basic,
    Full
}

public sealed record ExportSettings
{
    public string BackupPath { get; init; } = string.Empty;
    public ExportFormat Format { get; init; } = ExportFormat.Html;
    public AttachmentMode AttachmentMode { get; init; } = AttachmentMode.Clone;
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public string? ConversationFilter { get; init; }
    public string? CustomName { get; init; }
    public bool UseCallerId { get; init; }
    public bool DisableLazyLoading { get; init; }
    public bool IgnoreDiskWarning { get; init; }
    public bool UseMessageTimes { get; init; }
    public string? BackupPassword { get; init; }
}

public sealed record BackupInfo(
    string Path,
    string FolderName,
    string? DeviceName,
    string? ProductName,
    string? ProductVersion,
    DateTime LastModified)
{
    public string DisplayName
    {
        get
        {
            var name = DeviceName ?? ProductName ?? "iPhone 备份";
            var model = ProductName is not null && !string.Equals(ProductName, name, StringComparison.Ordinal)
                ? $" · {ProductName}"
                : string.Empty;
            var version = string.IsNullOrWhiteSpace(ProductVersion)
                ? string.Empty
                : $" · iOS {ProductVersion}";
            return $"{name}{model}{version} · {LastModified:yyyy-MM-dd HH:mm}";
        }
    }
}

public sealed record UserPreferences
{
    public string LastBackupPath { get; init; } = string.Empty;
    public string OutputBasePath { get; init; } = string.Empty;
    public ExportFormat Format { get; init; } = ExportFormat.Html;
    public AttachmentMode AttachmentMode { get; init; } = AttachmentMode.Clone;
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public bool StartDateEnabled { get; init; }
    public bool EndDateEnabled { get; init; }
    public string ConversationFilter { get; init; } = string.Empty;
    public string CustomName { get; init; } = string.Empty;
    public bool UseCallerId { get; init; }
    public bool DisableLazyLoading { get; init; }
    public bool IgnoreDiskWarning { get; init; }
    public bool UseMessageTimes { get; init; }
    public bool CreateTimestampedFolder { get; init; } = true;
}
