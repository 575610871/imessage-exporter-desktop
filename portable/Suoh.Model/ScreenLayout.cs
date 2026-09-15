namespace Suoh.Model;

/// <summary>All coordinates in this file are physical screen pixels, never WPF DIPs.</summary>
public readonly record struct ScreenBox(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
    public bool Encloses(ScreenBox box) => box.X >= X && box.Y >= Y && box.Right <= Right && box.Bottom <= Bottom;
}

public static class ScreenLayout
{
    public static ScreenBox OpeningBounds(ScreenBox workArea, double scale)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(workArea.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(workArea.Height);
        if (!double.IsFinite(scale) || scale <= 0) throw new ArgumentOutOfRangeException(nameof(scale));
        var inset = Math.Min((int)Math.Ceiling(16 * scale), Math.Min(workArea.Width, workArea.Height) / 8);
        var width = Math.Min((int)Math.Round(1120 * scale), workArea.Width - inset * 2);
        var height = Math.Min((int)Math.Round(820 * scale), workArea.Height - inset * 2);
        return new(workArea.X + (workArea.Width - width) / 2,
            workArea.Y + (workArea.Height - height) / 2, width, height);
    }

    public static ScreenBox KeepVisible(ScreenBox requested, ScreenBox workArea)
    {
        var width = Math.Clamp(requested.Width, 1, workArea.Width);
        var height = Math.Clamp(requested.Height, 1, workArea.Height);
        return new(Math.Clamp(requested.X, workArea.X, workArea.Right - width),
            Math.Clamp(requested.Y, workArea.Y, workArea.Bottom - height), width, height);
    }
}
