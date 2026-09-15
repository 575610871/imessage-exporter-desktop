using System.IO;
using System.Windows;

namespace IMessageExporterDesktopSuohEdition;

public partial class App : Application
{
    public const string DisplayName = "iMessage Exporter Desktop Suoh Edition";
    public const string ExecutableName = DisplayName + ".exe";
    public static string DisplayVersion => typeof(App).Assembly.GetName().Version?.ToString(3) ?? "unknown";
    public static string FooterLabel => $"Suoh Edition {DisplayVersion} · 便携版";

    private async void Boot(object sender, StartupEventArgs e)
    {
        var testing = e.Args.Length == 2 && e.Args[0] == "--ui-test";
        try
        {
            var window = new MainWindow(testing);
            MainWindow = window;
            window.Show();
            if (testing) Shutdown(await UiVerification.Run(window, e.Args[1]));
        }
        catch (Exception exception)
        {
            if (testing)
            {
                Directory.CreateDirectory(e.Args[1]);
                File.WriteAllText(Path.Combine(e.Args[1], "ui-failure.txt"), exception.ToString());
            }
            else MessageBox.Show("程序启动失败：" + exception.Message + "\n请确认便携包已完整解压。", DisplayName);
            Shutdown(1);
        }
    }
}
