using IMessageExporterDesktop.Core;

var failures = new List<string>();
var root = Path.Combine(Path.GetTempPath(), "imessage-exporter-desktop-tests", Guid.NewGuid().ToString("N"));
var backup = Path.Combine(root, "backup");
var output = Path.Combine(root, "output");

try
{
    Directory.CreateDirectory(backup);
    File.WriteAllText(Path.Combine(backup, "Manifest.plist"), "<plist><dict /></plist>");
    File.WriteAllBytes(Path.Combine(backup, "Manifest.db"), Array.Empty<byte>());

    var settings = new ExportSettings
    {
        BackupPath = backup,
        Format = ExportFormat.Html,
        AttachmentMode = AttachmentMode.Clone,
        StartDate = new DateTime(2024, 1, 2),
        EndDate = new DateTime(2024, 2, 3),
        ConversationFilter = "friend@example.com,+15551234567",
        CustomName = "Mei",
        DisableLazyLoading = true,
        UseMessageTimes = true,
        BackupPassword = "must-never-be-an-argument"
    };

    var arguments = ExportCommandBuilder.BuildExportArguments(settings, output);

    Check(arguments.Contains("--format") && arguments.Contains("html"), "HTML format mapping failed.");
    Check(arguments.Contains("--copy-method") && arguments.Contains("clone"), "Attachment mapping failed.");
    Check(arguments.Contains("2024-01-02") && arguments.Contains("2024-02-03"), "Date mapping failed.");
    Check(arguments.Contains("--no-progress"), "GUI export must disable terminal progress rendering.");
    Check(!arguments.Contains("--cleartext-password"), "Password flag must never be used.");
    Check(!arguments.Contains(settings.BackupPassword!), "Password leaked into process arguments.");
    Check(ExportValidator.ValidateExport(settings, output).IsValid, "Valid settings were rejected.");

    var unsafeOutput = Path.Combine(backup, "export");
    Check(!ExportValidator.ValidateExport(settings, unsafeOutput).IsValid,
        "An output path inside the backup must be rejected.");

    var invalidDates = settings with
    {
        StartDate = new DateTime(2024, 2, 3),
        EndDate = new DateTime(2024, 2, 3)
    };
    Check(!ExportValidator.ValidateSource(invalidDates).IsValid,
        "An empty date range must be rejected.");

    var diagnostics = ExportCommandBuilder.BuildDiagnosticsArguments(settings);
    Check(diagnostics.SequenceEqual(new[]
    {
        "--diagnostics", "--db-path", backup, "--platform", "iOS"
    }), "Diagnostics argument mapping changed unexpectedly.");

    var roomyLayout = AdaptiveWindowSizer.Calculate(1920, 1040);
    Check(roomyLayout.Width == 980 && roomyLayout.Height == 860,
        "A normal desktop should use the preferred window size.");
    Check(roomyLayout.MinimumWidth == 640 && roomyLayout.MinimumHeight == 520,
        "A normal desktop should keep the standard minimum size.");
    Check(!AdaptiveWindowSizer.ShouldUseCompact(roomyLayout.Width),
        "The preferred window size should retain the two-column layout.");

    var highScaleLayout = AdaptiveWindowSizer.Calculate(683, 344);
    Check(highScaleLayout.Width == 651 && highScaleLayout.Height == 312,
        "A high-DPI small work area must be fitted with a safety margin.");
    Check(highScaleLayout.MinimumWidth <= highScaleLayout.Width &&
          highScaleLayout.MinimumHeight <= highScaleLayout.Height,
        "Minimum dimensions must never exceed the fitted window.");
    Check(AdaptiveWindowSizer.ShouldUseCompact(highScaleLayout.Width),
        "A narrow high-DPI window should switch to one column.");

    var tinyLayout = AdaptiveWindowSizer.Calculate(500, 280);
    Check(tinyLayout.Width == 468 && tinyLayout.Height == 248,
        "Very small work areas must remain usable rather than enforcing fixed minima.");
}
finally
{
    try
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
    catch
    {
        // Cleanup failure does not invalidate the behavior tests.
    }
}

if (failures.Count == 0)
{
    Console.WriteLine("All core self-tests passed.");
    return 0;
}

foreach (var failure in failures)
{
    Console.Error.WriteLine($"FAIL: {failure}");
}

return 1;

void Check(bool condition, string message)
{
    if (!condition)
    {
        failures.Add(message);
    }
}
