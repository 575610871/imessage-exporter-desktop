using IMessageExporterDesktopSuohEdition.Model;

var assertions = 0;
void Check(bool result, string message)
{
    assertions++;
    if (!result) throw new Exception(message);
}

Check(typeof(ExportPlan).Namespace == "IMessageExporterDesktopSuohEdition.Model", "Model namespace rename incomplete.");
Check(typeof(ExportPlan).Assembly.GetName().Name == "IMessageExporterDesktopSuohEdition.Model", "Model assembly rename incomplete.");

foreach (var scale in new[] { 1d, 1.25, 1.5, 1.75, 2, 2.5, 3 })
foreach (var resolution in new[] { (1366, 768), (1920, 1080), (2560, 1440), (3840, 2160), (800, 600) })
foreach (var origin in new[] { (0, 0), (-1920, 0), (0, -1080), (-3000, -1500) })
foreach (var taskbar in new[] { (0, 0, 0, 48), (0, 48, 0, 0), (60, 0, 0, 0) })
{
    var work = new ScreenBox(origin.Item1 + taskbar.Item1, origin.Item2 + taskbar.Item2,
        resolution.Item1 - taskbar.Item1 - taskbar.Item3, resolution.Item2 - taskbar.Item2 - taskbar.Item4);
    var box = ScreenLayout.OpeningBounds(work, scale);
    Check(work.Encloses(box), $"Off-screen at scale {scale}: {work} -> {box}");
    Check(Math.Abs(box.X * 2 + box.Width - work.X * 2 - work.Width) <= 1, "Horizontal center mismatch.");
    Check(Math.Abs(box.Y * 2 + box.Height - work.Y * 2 - work.Height) <= 1, "Vertical center mismatch.");
    Check(work.Encloses(ScreenLayout.KeepVisible(new ScreenBox(work.X - 300, work.Y - 600, 6000, 4000), work)), "Oversized/offscreen clamp failed.");
    Check(ScreenLayout.KeepVisible(box, work) == box, "Valid user positioning should not move unnecessarily.");
}

var sandbox = Path.Combine(Path.GetTempPath(), "Suoh-Tests-" + Guid.NewGuid().ToString("N"));
try
{
    var source = Path.Combine(sandbox, "备份 with spaces");
    Directory.CreateDirectory(source);
    File.WriteAllText(Path.Combine(source, "Manifest.plist"), "synthetic test fixture, not a real backup");
    File.WriteAllText(Path.Combine(source, "Manifest.db"), "synthetic");
    var dest = ExportPlan.NewDestination(Path.Combine(sandbox, "exports"));
    var plan = new ExportPlan(source, dest, From: new DateOnly(2020, 1, 1), Before: new DateOnly(2021, 1, 1), Conversation: "someone@example.invalid,+15550000000");
    Check(plan.Validate() is null, "Valid independent destination rejected.");
    Check((plan with { Destination = Path.Combine(source, "output") }).Validate() is not null, "Backup child output allowed.");
    Check((plan with { Destination = source }).Validate() is not null, "Backup overwrite allowed.");
    Check((plan with { Before = plan.From }).Validate() is not null, "Empty date range allowed.");
    Check((plan with { Source = sandbox }).Validate() is not null, "Missing manifests allowed.");
    Check((plan with { Before = plan.From }).Validate(diagnose: true) is null, "Diagnostics incorrectly uses export-only filters.");
    Check(ExportPlan.NewDestination(sandbox) != ExportPlan.NewDestination(sandbox), "Destination must be unique even within a second.");
    foreach (var media in Enum.GetValues<MediaHandling>())
    foreach (var format in Enum.GetValues<TranscriptKind>())
    {
        var command = (plan with { Media = media, Transcript = format, EagerImages = true }).Arguments();
        Check(command.Contains(source), "Unicode/spaced source split into separate arguments.");
        Check(command.Contains("--no-progress"), "Terminal progress not suppressed.");
        Check(command.Contains("--no-lazy") == (format == TranscriptKind.Html), "HTML-only switch mapping wrong.");
        Check(!command.Any(a => a.Contains("password", StringComparison.OrdinalIgnoreCase)), "Credentials belong outside process arguments.");
    }
    Check(plan.Arguments().Contains("2021-01-01"), "Exclusive end date missing.");
    Check(plan.Arguments(true).SequenceEqual(new[] { "--db-path", source, "--platform", "iOS", "--diagnostics" }), "Diagnostic CLI mapping wrong.");
    var prefs = Path.Combine(sandbox, "preferences.json");
    new PortableOptions(source, "result", 2, true).Write(prefs);
    Check(PortableOptions.Read(prefs).Source == source, "Portable preferences round trip failed.");
    Check(!File.ReadAllText(prefs).Contains("password", StringComparison.OrdinalIgnoreCase), "Preferences must not contain secrets.");
    File.WriteAllText(prefs, "broken json");
    Check(PortableOptions.Read(prefs) == new PortableOptions(), "Broken preferences block startup.");
    Directory.CreateDirectory(dest);
    Check(plan.Validate() is not null, "Existing destination accepted.");

    // Symlink/junction aliases must not bypass the backup protection boundary.
    if (OperatingSystem.IsWindows())
    {
        var link = Path.Combine(sandbox, "alias");
        var mklink = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe")
        { UseShellExecute = false, CreateNoWindow = true, ArgumentList = { "/c", "mklink", "/J", link, source } })!;
        mklink.WaitForExit();
        Check(mklink.ExitCode == 0, "Cannot create safety-test junction.");
        Check((plan with { Destination = Path.Combine(link, "output") }).Validate() is not null, "Junction bypassed backup protection.");
        Directory.Delete(link); // Remove junction, not its target.
    }
}
finally { Directory.Delete(sandbox, recursive: true); }
Console.WriteLine($"PASS: {assertions} assertions (420 DPI/work-area combinations, command mapping, safety, preferences).");
