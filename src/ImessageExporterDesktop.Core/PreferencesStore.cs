using System.Text.Json;
using System.Text.Json.Serialization;

namespace IMessageExporterDesktop.Core;

public static class PreferencesStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string PreferencesPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "iMessage Exporter Desktop",
            "settings.json");

    public static UserPreferences Load()
    {
        try
        {
            if (File.Exists(PreferencesPath))
            {
                var json = File.ReadAllText(PreferencesPath);
                var preferences = JsonSerializer.Deserialize<UserPreferences>(json, SerializerOptions);
                if (preferences is not null)
                {
                    return WithDefaultOutputPath(preferences);
                }
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // A damaged preference file must never stop the application opening.
        }

        return WithDefaultOutputPath(new UserPreferences());
    }

    public static void Save(UserPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        var directory = Path.GetDirectoryName(PreferencesPath)
            ?? throw new InvalidOperationException("Unable to resolve the settings directory.");
        Directory.CreateDirectory(directory);

        var temporaryPath = PreferencesPath + ".tmp";
        var json = JsonSerializer.Serialize(preferences, SerializerOptions);
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, PreferencesPath, true);
    }

    private static UserPreferences WithDefaultOutputPath(UserPreferences preferences)
    {
        if (!string.IsNullOrWhiteSpace(preferences.OutputBasePath))
        {
            return preferences;
        }

        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return preferences with
        {
            OutputBasePath = Path.Combine(documents, "iMessage Exports")
        };
    }
}
