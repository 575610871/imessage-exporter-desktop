using System.IO;
using System.Windows;

namespace Suoh.App;

public partial class App : Application
{
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
            else MessageBox.Show("程序启动失败：" + exception.Message + "\n请确认便携包已完整解压。", "苏芳");
            Shutdown(1);
        }
    }
}
