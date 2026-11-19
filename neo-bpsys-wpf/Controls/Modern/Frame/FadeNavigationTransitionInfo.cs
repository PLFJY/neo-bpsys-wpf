using System.Windows;
using System.Windows.Media.Animation;

namespace neo_bpsys_wpf.Controls.Modern.Frame;

/// <summary>在同一时间线内淡出旧内容并淡入新内容。</summary>
public sealed class FadeNavigationTransitionInfo : ModernNavigationTransitionInfo
{
    internal override Storyboard CreateEnterStoryboard(FrameworkElement element, bool movingBackwards, TimeSpan duration)
    {
        var storyboard = new Storyboard();
        storyboard.Children.Add(EntranceNavigationTransitionInfo.StagedSpline(OpacityPath, 0, 1, TimeSpan.FromTicks(duration.Ticks / 3), duration, DecelerateKeySpline));
        return storyboard;
    }

    internal override Storyboard CreateExitStoryboard(FrameworkElement element, bool movingBackwards, TimeSpan duration)
    {
        var storyboard = new Storyboard();
        storyboard.Children.Add(EntranceNavigationTransitionInfo.Opacity(
            1, 0, TimeSpan.FromTicks(duration.Ticks / 3), AccelerateKeySpline));
        return storyboard;
    }
}
