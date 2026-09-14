using System.IO;
using System.Windows;
using IMessageExporterDesktop.Core;

namespace IMessageExporterDesktop;

public partial class App : Application
{
    private void Application_Startup(object sender, StartupEventArgs e)
    {
        if (e.Args.Any(argument =>
                string.Equals(argument, "--self-test", StringComparison.OrdinalIgnoreCase)))
        {
            Shutdown(RunPackageSelfTest());
            return;
        }

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    private static int RunPackageSelfTest()
    {
        try
        {
            var enginePath = Path.Combine(
                AppContext.BaseDirectory,
                "engine",
                "imessage-exporter.exe");

            if (!File.Exists(enginePath))
            {
                return 2;
            }

            var arguments = ExportCommandBuilder.BuildExportArguments(
                new ExportSettings
                {
                    BackupPath = @"C:\Example\Backup",
                    Format = ExportFormat.Html,
                    AttachmentMode = AttachmentMode.Clone,
                    BackupPassword = "not-an-argument"
                },
                @"C:\Example\Output");

            return arguments.Contains("--cleartext-password") ||
                   arguments.Contains("not-an-argument")
                ? 3
                : 0;
        }
        catch
        {
            return 1;
        }
    }
}
