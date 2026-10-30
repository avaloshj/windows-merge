using System.Windows;
using System.Windows.Media.Animation;

namespace TabDock.App.Controls.Motion;

/// <summary>Acceso desde código a los tokens de <c>Themes/Motion.xaml</c> (o de su variante reducida).</summary>
internal static class MotionTokens
{
    public const string EaseStandard = "Motion.Ease.Standard";
    public const string EaseExit = "Motion.Ease.Exit";
    public const string DurationHover = "Motion.Duration.Hover";
    public const string DurationIndicator = "Motion.Duration.Indicator";
    public const string DurationFrame = "Motion.Duration.Frame";
    public const string DurationEntrance = "Motion.Duration.Entrance";
    public const string DurationCollapse = "Motion.Duration.Collapse";
    public const string StaggerMilliseconds = "Motion.Stagger.Milliseconds";
    public const string StaggerMaxItems = "Motion.Stagger.MaxItems";
    public const string EntranceOffsetX = "Motion.Entrance.OffsetX";
    public const string FrameScaleFrom = "Motion.Frame.ScaleFrom";
    public const string PressScale = "Motion.Press.Scale";
    public const string CollapseAnimateHeight = "Motion.Collapse.AnimateHeight";

    public static TimeSpan Time(string key) =>
        Find(key) is Duration { HasTimeSpan: true } duration ? duration.TimeSpan : TimeSpan.Zero;

    public static double Number(string key, double fallback) => Find(key) is double value ? value : fallback;

    public static bool Flag(string key, bool fallback) => Find(key) is bool value ? value : fallback;

    public static IEasingFunction? Ease(string key) => Find(key) as IEasingFunction;

    private static object? Find(string key) => Application.Current?.TryFindResource(key);
}
