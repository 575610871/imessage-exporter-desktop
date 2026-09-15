using System.Text.Json;

namespace IMessageExporterDesktopSuohEdition.Model;

/// <summary>Only these opt-in values can be persisted. No credentials, contact filters or geometry.</summary>
public sealed record PortableOptions(string Source = "", string OutputFolder = "", int Media = 0, bool TextOnly = false)
{
    public static string FilePath => Path.Combine(AppContext.BaseDirectory, "data", "preferences.json");

    public static PortableOptions Read(string? file = null)
    {
        try { return JsonSerializer.Deserialize<PortableOptions>(File.ReadAllText(file ?? FilePath)) ?? new(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }

    public void Write(string? file = null)
    {
        file ??= FilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
        var staging = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(staging, JsonSerializer.Serialize(this));
            File.Move(staging, file, true);
        }
        finally { if (File.Exists(staging)) File.Delete(staging); }
    }

    public static void Forget() { if (File.Exists(FilePath)) File.Delete(FilePath); }
}
