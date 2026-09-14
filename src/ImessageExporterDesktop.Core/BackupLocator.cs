using System.Globalization;
using System.Xml.Linq;

namespace IMessageExporterDesktop.Core;

public static class BackupLocator
{
    public static IReadOnlyList<BackupInfo> FindWindowsBackups()
    {
        var roots = CandidateRoots()
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var backupPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in roots)
        {
            TryAddBackup(root, backupPaths);

            if (!Directory.Exists(root))
            {
                continue;
            }

            try
            {
                foreach (var directory in Directory.EnumerateDirectories(root))
                {
                    TryAddBackup(directory, backupPaths);
                }
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
            {
                // A protected or temporarily unavailable root should not prevent
                // other Apple backup locations from being checked.
            }
        }

        return backupPaths
            .Select(CreateBackupInfo)
            .OrderByDescending(backup => backup.LastModified)
            .ToArray();
    }

    public static bool IsBackupRoot(string path)
    {
        return Directory.Exists(path) &&
               File.Exists(Path.Combine(path, "Manifest.plist")) &&
               (File.Exists(Path.Combine(path, "Manifest.db")) ||
                File.Exists(Path.Combine(path, "Manifest.mbdb")));
    }

    private static IEnumerable<string> CandidateRoots()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        yield return Path.Combine(userProfile, "Apple", "MobileSync", "Backup");
        yield return Path.Combine(roaming, "Apple Computer", "MobileSync", "Backup");
        yield return Path.Combine(roaming, "Apple", "MobileSync", "Backup");
        yield return Path.Combine(local, "Apple Computer", "MobileSync", "Backup");
    }

    private static void TryAddBackup(string path, ISet<string> backupPaths)
    {
        try
        {
            if (IsBackupRoot(path))
            {
                backupPaths.Add(Path.GetFullPath(path));
            }
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException or IOException or ArgumentException)
        {
            // Ignore this candidate and continue looking elsewhere.
        }
    }

    private static BackupInfo CreateBackupInfo(string path)
    {
        var metadata = ReadInfoPlist(Path.Combine(path, "Info.plist"));
        var modified = metadata.LastBackupDate ?? Directory.GetLastWriteTime(path);

        return new BackupInfo(
            path,
            Path.GetFileName(Path.TrimEndingDirectorySeparator(path)),
            metadata.DeviceName,
            metadata.ProductName,
            metadata.ProductVersion,
            modified);
    }

    private static BackupMetadata ReadInfoPlist(string path)
    {
        if (!File.Exists(path))
        {
            return new BackupMetadata();
        }

        try
        {
            var document = XDocument.Load(path);
            var dictionary = document.Descendants("dict").FirstOrDefault();
            if (dictionary is null)
            {
                return new BackupMetadata();
            }

            var elements = dictionary.Elements().ToList();
            var values = new Dictionary<string, string>(StringComparer.Ordinal);

            for (var index = 0; index + 1 < elements.Count; index++)
            {
                if (elements[index].Name.LocalName != "key")
                {
                    continue;
                }

                var valueElement = elements[index + 1];
                values[elements[index].Value] = valueElement.Value;
                index++;
            }

            DateTime? lastBackupDate = null;
            if (values.TryGetValue("Last Backup Date", out var rawDate) &&
                DateTime.TryParse(
                    rawDate,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.RoundtripKind,
                    out var parsedDate))
            {
                lastBackupDate = parsedDate.ToLocalTime();
            }

            return new BackupMetadata(
                GetValue(values, "Device Name"),
                GetValue(values, "Product Name"),
                GetValue(values, "Product Version"),
                lastBackupDate);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            // Some old backups use binary plists. The folder remains usable even
            // when friendly metadata cannot be read.
            return new BackupMetadata();
        }
    }

    private static string? GetValue(IReadOnlyDictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;
    }

    private sealed record BackupMetadata(
        string? DeviceName = null,
        string? ProductName = null,
        string? ProductVersion = null,
        DateTime? LastBackupDate = null);
}
