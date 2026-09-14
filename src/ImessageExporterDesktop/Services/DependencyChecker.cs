using System.IO;
using IMessageExporterDesktop.Core;

namespace IMessageExporterDesktop.Services;

public static class DependencyChecker
{
    public static IReadOnlyList<string> GetMissingTools(AttachmentMode mode)
    {
        var missing = new List<string>();

        if (mode is AttachmentMode.Basic or AttachmentMode.Full &&
            !CommandExists("magick.exe"))
        {
            missing.Add("ImageMagick（magick.exe）");
        }

        if (mode == AttachmentMode.Full &&
            !CommandExists("ffmpeg.exe"))
        {
            missing.Add("FFmpeg（ffmpeg.exe）");
        }

        return missing;
    }

    private static bool CommandExists(string executableName)
    {
        var candidates = new List<string>
        {
            AppContext.BaseDirectory,
            Path.Combine(AppContext.BaseDirectory, "tools")
        };

        var path = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(path))
        {
            candidates.AddRange(
                path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                    .Select(entry => entry.Trim().Trim('"')));
        }

        return candidates
            .Where(Directory.Exists)
            .Any(directory => File.Exists(Path.Combine(directory, executableName)));
    }
}
