using System.Globalization;

namespace IMessageExporterDesktop.Core;

public static class ExportCommandBuilder
{
    public const string PasswordEnvironmentVariable = "IMESSAGE_EXPORTER_BACKUP_PASSWORD";

    public static IReadOnlyList<string> BuildExportArguments(ExportSettings settings, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var arguments = new List<string>
        {
            "--format",
            settings.Format == ExportFormat.Html ? "html" : "txt",
            "--copy-method",
            ToCliValue(settings.AttachmentMode),
            "--db-path",
            settings.BackupPath,
            "--platform",
            "iOS",
            "--export-path",
            outputPath
        };

        if (settings.StartDate is { } startDate)
        {
            arguments.Add("--start-date");
            arguments.Add(startDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        if (settings.EndDate is { } endDate)
        {
            arguments.Add("--end-date");
            arguments.Add(endDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        if (settings.DisableLazyLoading && settings.Format == ExportFormat.Html)
        {
            arguments.Add("--no-lazy");
        }

        if (settings.UseCallerId)
        {
            arguments.Add("--use-caller-id");
        }
        else if (!string.IsNullOrWhiteSpace(settings.CustomName))
        {
            arguments.Add("--custom-name");
            arguments.Add(settings.CustomName.Trim());
        }

        if (settings.IgnoreDiskWarning)
        {
            arguments.Add("--ignore-disk-warning");
        }

        if (!string.IsNullOrWhiteSpace(settings.ConversationFilter))
        {
            arguments.Add("--conversation-filter");
            arguments.Add(settings.ConversationFilter.Trim());
        }

        if (settings.UseMessageTimes)
        {
            arguments.Add("--use-message-times");
        }

        // Terminal animations do not render correctly inside a GUI log window.
        arguments.Add("--no-progress");
        return arguments;
    }

    public static IReadOnlyList<string> BuildDiagnosticsArguments(ExportSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new[]
        {
            "--diagnostics",
            "--db-path",
            settings.BackupPath,
            "--platform",
            "iOS"
        };
    }

    public static string ToDisplayCommand(IEnumerable<string> arguments)
    {
        return "imessage-exporter.exe " + string.Join(" ", arguments.Select(QuoteForDisplay));
    }

    private static string ToCliValue(AttachmentMode mode)
    {
        return mode switch
        {
            AttachmentMode.Disabled => "disabled",
            AttachmentMode.Clone => "clone",
            AttachmentMode.Basic => "basic",
            AttachmentMode.Full => "full",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
    }

    private static string QuoteForDisplay(string value)
    {
        return value.Any(char.IsWhiteSpace)
            ? $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\""
            : value;
    }
}
