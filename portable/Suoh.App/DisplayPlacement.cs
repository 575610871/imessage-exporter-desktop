using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Suoh.Model;

namespace Suoh.App;

public sealed class DisplayPlacement
{
    private readonly Window _view;
    private nint _hwnd;
    private nint _initialDisplay;
    private bool _moving, _scheduled;

    public DisplayPlacement(Window view)
    {
        _view = view;
        view.SourceInitialized += OnNativeWindowCreated;
        view.ContentRendered += (_, _) => view.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,
            new Action(() => CenterOn(_initialDisplay)));
        view.StateChanged += (_, _) => ScheduleVisibilityCheck();
    }

    public double Dpi => Math.Max(96u, Native.GetDpiForWindow(_hwnd));
    public bool PerMonitorV2 => Native.AreDpiAwarenessContextsEqual(Native.GetWindowDpiAwarenessContext(_hwnd), new nint(-4));
    public ScreenBox Current { get { Native.GetWindowRect(_hwnd, out var rect); return rect.Box; } }
    public ScreenBox Available => ReadWorkArea(Native.MonitorFromWindow(_hwnd, 2));

    private void OnNativeWindowCreated(object? sender, EventArgs args)
    {
        _hwnd = new WindowInteropHelper(_view).Handle;
        HwndSource.FromHwnd(_hwnd)?.AddHook(ObserveMessages);
        Native.GetCursorPos(out var cursor);
        _initialDisplay = Native.MonitorFromPoint(cursor, 2);
        CenterOn(_initialDisplay);
        var caption = 0x00492C99; // COLORREF, #992C49
        var foreground = 0x00FFFFFF;
        // Native caption tint is optional. Older Windows ignores these attributes.
        Native.DwmSetWindowAttribute(_hwnd, 35, ref caption, 4);
        Native.DwmSetWindowAttribute(_hwnd, 36, ref foreground, 4);
    }

    public void Recenter()
    {
        _view.WindowState = WindowState.Normal;
        CenterOn(Native.MonitorFromWindow(_hwnd, 2));
    }

    private void CenterOn(nint display)
    {
        if (_hwnd == 0 || _moving) return;
        _moving = true;
        try
        {
            var work = ReadWorkArea(display);
            // Cross to the destination first, so GetDpiForWindow reports that display's actual DPI.
            Native.SetWindowPos(_hwnd, 0, work.X + work.Width / 2, work.Y + work.Height / 2,
                0, 0, 0x0015); // NOACTIVATE | NOZORDER | NOSIZE
            AdjustMinimums(work);
            MoveTo(ScreenLayout.OpeningBounds(work, Dpi / 96));
        }
        finally { _moving = false; }
    }

    public void Constrain()
    {
        if (_hwnd == 0 || _moving || _view.WindowState != WindowState.Normal) return;
        _moving = true;
        try
        {
            AdjustMinimums(Available);
            MoveTo(ScreenLayout.KeepVisible(Current, Available));
        }
        finally { _moving = false; }
    }

    private void AdjustMinimums(ScreenBox work)
    {
        _view.MinWidth = Math.Min(520, Math.Max(1, work.Width * 96 / Dpi - 32));
        _view.MinHeight = Math.Min(420, Math.Max(1, work.Height * 96 / Dpi - 32));
    }

    internal void MoveTo(ScreenBox bounds) => Native.SetWindowPos(_hwnd, 0,
        bounds.X, bounds.Y, bounds.Width, bounds.Height, 0x0014);

    private void ScheduleVisibilityCheck()
    {
        if (_scheduled || _moving) return;
        _scheduled = true;
        // Run AFTER WPF handles WM_DPICHANGED and scales layout/non-client metrics itself.
        _view.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
        {
            _scheduled = false;
            Constrain();
        }));
    }

    private nint ObserveMessages(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message is 0x02E0 or 0x007E or 0x001A or 0x0232) ScheduleVisibilityCheck();
        // WM_DPICHANGED, DISPLAYCHANGE, SETTINGCHANGE, EXITSIZEMOVE. Leave handled=false.
        return 0;
    }

    private static ScreenBox ReadWorkArea(nint display)
    {
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!Native.GetMonitorInfo(display, ref info)) throw new System.ComponentModel.Win32Exception();
        return info.Work.Box;
    }

    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Rect
    {
        public int Left, Top, Right, Bottom;
        public readonly ScreenBox Box => new(Left, Top, Right - Left, Bottom - Top);
    }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public Rect Full, Work; public uint Flags; }
    private static class Native
    {
        [DllImport("user32.dll")] internal static extern bool GetCursorPos(out Point p);
        [DllImport("user32.dll")] internal static extern nint MonitorFromPoint(Point p, uint flags);
        [DllImport("user32.dll")] internal static extern nint MonitorFromWindow(nint hwnd, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern bool GetMonitorInfo(nint display, ref MonitorInfo info);
        [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(nint hwnd);
        [DllImport("user32.dll")] internal static extern bool GetWindowRect(nint hwnd, out Rect rect);
        [DllImport("user32.dll")] internal static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")] internal static extern nint GetWindowDpiAwarenessContext(nint hwnd);
        [DllImport("user32.dll")] internal static extern bool AreDpiAwarenessContextsEqual(nint a, nint b);
        [DllImport("dwmapi.dll")] internal static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);
    }
}
