namespace IMessageExporterDesktop.Core;

public readonly record struct AdaptiveWindowLayout(
    double Width,
    double Height,
    double MinimumWidth,
    double MinimumHeight);

public static class AdaptiveWindowSizer
{
    public const double PreferredWidth = 980;
    public const double PreferredHeight = 860;
    public const double StandardMinimumWidth = 640;
    public const double StandardMinimumHeight = 520;
    public const double CompactLayoutThreshold = 860;
    public const double WorkAreaSafetyMargin = 32;

    public static AdaptiveWindowLayout Calculate(
        double workAreaWidth,
        double workAreaHeight)
    {
        if (!double.IsFinite(workAreaWidth) || workAreaWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(workAreaWidth),
                "Work area width must be a positive finite value.");
        }

        if (!double.IsFinite(workAreaHeight) || workAreaHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(workAreaHeight),
                "Work area height must be a positive finite value.");
        }

        var usableWidth = Math.Max(1, workAreaWidth - WorkAreaSafetyMargin);
        var usableHeight = Math.Max(1, workAreaHeight - WorkAreaSafetyMargin);

        return new AdaptiveWindowLayout(
            Width: Math.Min(PreferredWidth, usableWidth),
            Height: Math.Min(PreferredHeight, usableHeight),
            MinimumWidth: Math.Min(StandardMinimumWidth, usableWidth),
            MinimumHeight: Math.Min(StandardMinimumHeight, usableHeight));
    }

    public static bool ShouldUseCompact(double windowWidth)
    {
        return double.IsFinite(windowWidth) &&
               windowWidth > 0 &&
               windowWidth < CompactLayoutThreshold;
    }
}
