namespace Suoh.Model;

public static class BackupSearch
{
    public static IReadOnlyList<string> Scan()
    {
        string[] parents = [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Apple", "MobileSync", "Backup"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Apple Computer", "MobileSync", "Backup")];
        var found = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var parent in parents)
        {
            try
            {
                if (!Directory.Exists(parent)) continue;
                foreach (var candidate in Directory.EnumerateDirectories(parent))
                    if (File.Exists(Path.Combine(candidate, "Manifest.db")) && File.Exists(Path.Combine(candidate, "Manifest.plist")))
                        found.Add(candidate);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        return found.ToArray();
    }
}
