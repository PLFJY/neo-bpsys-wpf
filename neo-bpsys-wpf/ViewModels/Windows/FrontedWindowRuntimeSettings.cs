using neo_bpsys_wpf.Core.Models;
using System.Windows.Media;

namespace neo_bpsys_wpf.ViewModels.Windows;

public sealed class FrontedWindowRuntimeSettings
{
    public WindowSize WindowSize { get; init; } = new(1440, 810);

    public WindowSize ScoreInGameWindowSize { get; init; } = new(480, 152);

    public WindowSize ScoreGlobalWindowSize { get; init; } = new(1440, 195);

    public bool AllowsWindowTransparency { get; init; } = true;

public bool AllowsScoreGlobalWindowTransparency { get; init; } = true;

private static readonly Brush TransparentBlackBrush =
    new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));

public Brush BackgroundBrush => TransparentBlackBrush;

public Brush ScoreGlobalWindowBackgroundBrush => TransparentBlackBrush;
}
