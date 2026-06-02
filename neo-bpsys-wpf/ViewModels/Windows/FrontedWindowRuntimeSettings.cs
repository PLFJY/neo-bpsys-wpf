using neo_bpsys_wpf.Core.Models;
using System.Windows.Media;

namespace neo_bpsys_wpf.ViewModels.Windows;

public sealed class FrontedWindowRuntimeSettings
{
    public WindowSize WindowSize { get; init; } = new(1440, 810);

    public WindowSize ScoreInGameWindowSize { get; init; } = new(480, 152);

    public WindowSize ScoreGlobalWindowSize { get; init; } = new(1440, 195);

    public bool AllowsWindowTransparency { get; init; }

    public bool AllowsScoreGlobalWindowTransparency { get; init; }

    public Brush BackgroundBrush => AllowsWindowTransparency
        ? Brushes.Transparent
        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00FF00"));

    public Brush ScoreGlobalWindowBackgroundBrush => AllowsScoreGlobalWindowTransparency
        ? Brushes.Transparent
        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00FF00"));
}
