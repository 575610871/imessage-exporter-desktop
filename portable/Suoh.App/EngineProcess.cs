using System.Diagnostics;
using System.IO;
using System.Text;
using Suoh.Model;

namespace Suoh.App;

/// <summary>Starts the upstream Rust binary locally, with no shell and no credentials in arguments.</summary>
public sealed class EngineProcess
{
    public const string PasswordVariable = "SUOH_BACKUP_PASSWORD";
    public static string Binary => Path.Combine(AppContext.BaseDirectory, "engine", "imessage-exporter.exe");

    public static IReadOnlyList<string> MissingConverters(MediaHandling mode)
    {
        var required = mode switch
        {
            MediaHandling.Images => new[] { "magick.exe" },
            MediaHandling.All => new[] { "magick.exe", "ffmpeg.exe" },
            _ => Array.Empty<string>()
        };
        var paths = ToolPath().Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        return required.Where(tool => !paths.Any(path => File.Exists(Path.Combine(path.Trim('"'), tool)))).ToArray();
    }

    public static string ToolPath() => string.Join(Path.PathSeparator,
        Path.Combine(AppContext.BaseDirectory, "tools"), AppContext.BaseDirectory,
        Environment.GetEnvironmentVariable("PATH") ?? "");

    public async Task<int> Execute(ExportPlan plan, bool diagnostics, string password, Action<string> log, CancellationToken stop)
    {
        if (!File.Exists(Binary)) throw new FileNotFoundException("缺少 engine\\imessage-exporter.exe。请完整解压便携包，不能只取出 Suoh.exe。", Binary);
        stop.ThrowIfCancellationRequested();
        var scratch = Path.Combine(Path.GetTempPath(), "Suoh-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        try
        {
            var info = new ProcessStartInfo(Binary)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = AppContext.BaseDirectory
            };
            foreach (var arg in plan.Arguments(diagnostics)) info.ArgumentList.Add(arg);
            info.Environment[PasswordVariable] = password;
            info.Environment["SUOH_GUI"] = "1";
            info.Environment["PATH"] = ToolPath();
            info.Environment["TMP"] = scratch;
            info.Environment["TEMP"] = scratch;
            // Upstream uses temporary decrypted databases; isolate each job, then remove only our own folder.
            using var process = Process.Start(info) ?? throw new IOException("无法启动导出引擎。");
            info.Environment.Remove(PasswordVariable);
            process.StandardInput.Close(); // No invisible terminal password prompts.

            async Task Read(StreamReader stream)
            {
                while (await stream.ReadLineAsync().ConfigureAwait(false) is { } line)
                {
                    if (password.Length > 0) line = line.Replace(password, "[密码已隐藏]", StringComparison.Ordinal);
                    log(line.Length > 8000 ? line[..8000] + " …" : line);
                }
            }
            var readOut = Read(process.StandardOutput);
            var readError = Read(process.StandardError);
            try { await process.WaitForExitAsync(stop).ConfigureAwait(false); }
            catch (OperationCanceledException)
            {
                try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { }
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                await Task.WhenAll(readOut, readError).ConfigureAwait(false);
                throw;
            }
            await Task.WhenAll(readOut, readError).ConfigureAwait(false);
            stop.ThrowIfCancellationRequested();
            return process.ExitCode;
        }
        finally
        {
            try { Directory.Delete(scratch, recursive: true); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            { log("临时文件清理未完成，请手动检查：" + scratch); }
        }
    }
}
