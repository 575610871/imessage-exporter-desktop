using System.Diagnostics;
using System.Text;
using IMessageExporterDesktop.Core;

namespace IMessageExporterDesktop.Services;

public sealed class ExporterRunner
{
    public string EnginePath =>
        Path.Combine(AppContext.BaseDirectory, "engine", "imessage-exporter.exe");

    public async Task<int> RunAsync(
        ExportSettings settings,
        string? outputPath,
        bool diagnostics,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(progress);

        if (!File.Exists(EnginePath))
        {
            throw new FileNotFoundException(
                "没有找到导出核心。请重新安装应用，或重新解压完整的便携版。",
                EnginePath);
        }

        var arguments = diagnostics
            ? ExportCommandBuilder.BuildDiagnosticsArguments(settings)
            : ExportCommandBuilder.BuildExportArguments(
                settings,
                outputPath ?? throw new ArgumentNullException(nameof(outputPath)));

        var startInfo = new ProcessStartInfo
        {
            FileName = EnginePath,
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        // Never inherit a stale password from the parent environment.
        startInfo.Environment.Remove(ExportCommandBuilder.PasswordEnvironmentVariable);
        if (!string.IsNullOrEmpty(settings.BackupPassword))
        {
            startInfo.Environment[ExportCommandBuilder.PasswordEnvironmentVariable] =
                settings.BackupPassword;
        }

        progress.Report($"[本地命令] {ExportCommandBuilder.ToDisplayCommand(arguments)}");

        using var process = new Process
        {
            StartInfo = startInfo
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("无法启动本地导出核心。");
        }

        var standardOutputTask = PumpLinesAsync(process.StandardOutput, progress);
        var standardErrorTask = PumpLinesAsync(process.StandardError, progress);

        using var cancellationRegistration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
                // The process exited between the state check and Kill.
            }
        });

        await Task.WhenAll(
            process.WaitForExitAsync(),
            standardOutputTask,
            standardErrorTask).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        return process.ExitCode;
    }

    private static async Task PumpLinesAsync(
        StreamReader reader,
        IProgress<string> progress)
    {
        while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                progress.Report(line);
            }
        }
    }
}
