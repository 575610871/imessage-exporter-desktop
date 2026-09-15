using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using IMessageExporterDesktopSuohEdition.Model;

namespace IMessageExporterDesktopSuohEdition;

/// <summary>Real WPF/native-window checks on Windows CI. Does not touch personal backups or saved preferences.</summary>
internal static class UiVerification
{
    public static async Task<int> Run(MainWindow view, string output)
    {
        Directory.CreateDirectory(output);
        var observations = new List<object>();
        await Settle(view);
        const string product = "iMessage Exporter Desktop Suoh Edition";
        const string codeRoot = "IMessageExporterDesktopSuohEdition";
        var assembly = typeof(App).Assembly;
        Require(view.Title == product, "Window title does not match the edition name.");
        Require(Path.GetFileName(Environment.ProcessPath) == product + ".exe", "Published executable has the wrong name.");
        Require(assembly.GetName().Name == product, "Desktop assembly has the wrong name.");
        Require(assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product == product, "Product metadata has the wrong name.");
        Require(assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title == product, "Title metadata has the wrong name.");
        Require(typeof(App).Namespace == codeRoot && typeof(MainWindow).Namespace == codeRoot, "Desktop namespace rename is incomplete.");
        Require(typeof(ExportPlan).Namespace == codeRoot + ".Model", "Model namespace rename is incomplete.");
        observations.Add(new { check = "edition-identity", title = view.Title, executable = Environment.ProcessPath,
            desktopNamespace = typeof(App).Namespace, modelNamespace = typeof(ExportPlan).Namespace,
            version = App.DisplayVersion, engine = EngineProcess.Binary, preferences = PortableOptions.FilePath });
        var placement = view.Placement;
        Require(placement.PerMonitorV2, "The window must actually be PerMonitorV2, not merely declare it in source.");
        var expected = ScreenLayout.OpeningBounds(placement.Available, placement.Dpi / 96);
        var actual = placement.Current;
        Require(placement.Available.Encloses(actual), "Startup native rectangle (including caption) is outside the work area.");
        Require(Math.Abs(actual.X - expected.X) <= 2 && Math.Abs(actual.Y - expected.Y) <= 2,
            "Startup is not centered after rendering.");
        observations.Add(new { check = "native-startup", dpi = placement.Dpi, actual, work = placement.Available });
        Capture(view, Path.Combine(output, "01-startup.png"));
        CaptureNativeWindow(view, Path.Combine(output, "00-native-window.png"));

        // Real WPF layout at constrained logical sizes. This is NOT a claim of changing Windows system DPI.
        foreach (var size in new[] { (1120d, 820d), (850d, 680d), (640d, 480d), (651d, 352d), (423d, 208d) })
        {
            view.MinWidth = 1;
            view.MinHeight = 1;
            view.Width = size.Item1;
            view.Height = size.Item2;
            await Settle(view);
            Require(IsInside(view.ExportButton, view.Surface), $"Export action clipped at {size}.");
            Require(IsInside(view.CancelButton, view.Surface), $"Cancel action clipped at {size}.");
            Require(IsInside(view.SettingsTab, view.Surface), $"Settings tab clipped at {size}.");
            Require(view.SettingsPage.ViewportHeight > (size.Item2 < 300 ? 40 : 60), $"Settings cannot scroll at {size}.");
            Require((view.Aside.Visibility == Visibility.Visible) == (view.ActualWidth >= 940), $"Aside breakpoint incorrect at {size}.");
            observations.Add(new { check = "real-WPF-layout", width = view.ActualWidth, height = view.ActualHeight, viewport = view.SettingsPage.ViewportHeight });
            Capture(view, Path.Combine(output, $"layout-{(int)size.Item1}x{(int)size.Item2}.png"));
        }

        view.Advanced.IsExpanded = true;
        view.SettingsPage.ScrollToEnd();
        await Settle(view);
        view.RememberOption.BringIntoView();
        await Settle(view);
        Require(IsInside(view.RememberOption, view.Surface), "Lower settings cannot be reached by scrolling.");
        Capture(view, Path.Combine(output, "advanced-narrow.png"));
        view.Advanced.IsExpanded = false;
        view.SettingsPage.ScrollToTop();

        var work = placement.Available;
        placement.MoveTo(new ScreenBox(work.X - 200, work.Y - 400, 800, 600));
        placement.Constrain();
        await Settle(view);
        Require(placement.Available.Encloses(placement.Current), "Off-screen repair did not keep the native caption inside work area.");
        placement.Recenter();
        await Settle(view);
        Require(placement.Available.Encloses(placement.Current), "Recenter shortcut produced off-screen bounds.");
        Require(view.MediaChoice.Text.Contains("保留原文件"), "Attachment choice must render a friendly label.");
        Require(!File.Exists(PortableOptions.FilePath), "UI tests must not write portable preferences.");
        observations.Add(new { check = "off-screen-repair-and-recenter", actual = placement.Current });
        File.WriteAllText(Path.Combine(output, "ui-checks.json"), JsonSerializer.Serialize(observations, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }

    private static async Task Settle(MainWindow view)
    {
        await view.Dispatcher.InvokeAsync(() => view.UpdateLayout(), DispatcherPriority.ContextIdle);
        await Task.Delay(180);
        await view.Dispatcher.InvokeAsync(() => view.UpdateLayout(), DispatcherPriority.ContextIdle);
    }

    private static bool IsInside(FrameworkElement item, FrameworkElement canvas)
    {
        var rect = item.TransformToAncestor(canvas).TransformBounds(new Rect(new Size(item.ActualWidth, item.ActualHeight)));
        return rect.Width > 0 && rect.Height > 0 && rect.Left >= 0 && rect.Top >= 0 &&
            rect.Right <= canvas.ActualWidth + 1 && rect.Bottom <= canvas.ActualHeight + 1;
    }
    private static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
    private static void CaptureNativeWindow(MainWindow view, string file)
    {
        var hwnd = new WindowInteropHelper(view).Handle;
        var bounds = view.Placement.Current;
        var source = CaptureApi.GetWindowDC(hwnd);
        var memory = CaptureApi.CreateCompatibleDC(source);
        var bitmap = CaptureApi.CreateCompatibleBitmap(source, bounds.Width, bounds.Height);
        var previous = CaptureApi.SelectObject(memory, bitmap);
        try
        {
            if (!CaptureApi.PrintWindow(hwnd, memory, 2)) return; // Some headless sessions do not support native capture.
            var image = Imaging.CreateBitmapSourceFromHBitmap(bitmap, 0, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using var stream = File.Create(file);
            encoder.Save(stream);
        }
        finally
        {
            CaptureApi.SelectObject(memory, previous);
            CaptureApi.DeleteObject(bitmap);
            CaptureApi.DeleteDC(memory);
            CaptureApi.ReleaseDC(hwnd, source);
        }
    }

    private static class CaptureApi
    {
        [DllImport("user32.dll")] internal static extern nint GetWindowDC(nint hwnd);
        [DllImport("user32.dll")] internal static extern int ReleaseDC(nint hwnd, nint dc);
        [DllImport("user32.dll")] internal static extern bool PrintWindow(nint hwnd, nint dc, uint flags);
        [DllImport("gdi32.dll")] internal static extern nint CreateCompatibleDC(nint dc);
        [DllImport("gdi32.dll")] internal static extern nint CreateCompatibleBitmap(nint dc, int width, int height);
        [DllImport("gdi32.dll")] internal static extern nint SelectObject(nint dc, nint obj);
        [DllImport("gdi32.dll")] internal static extern bool DeleteObject(nint obj);
        [DllImport("gdi32.dll")] internal static extern bool DeleteDC(nint dc);
    }

    private static void Capture(MainWindow view, string file)
    {
        var canvas = view.Surface;
        var dpi = VisualTreeHelper.GetDpi(canvas);
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(canvas.ActualWidth * dpi.DpiScaleX),
            (int)Math.Ceiling(canvas.ActualHeight * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bitmap.Render(canvas);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(file);
        encoder.Save(stream);
    }
}
