#nullable enable

using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace neo_bpsys_wpf.Controls.Modern.Frame;

// Inspired by iNKORE.UI.WPF.Modern EntranceNavigationTransitionInfo.
/// <summary>
/// 进入式导航过渡信息。新内容从底部滑入，后退时使用淡入过渡。
/// </summary>
public sealed class EntranceNavigationTransitionInfo : ModernNavigationTransitionInfo
{
    internal override Storyboard CreateEnterStoryboard(FrameworkElement element, bool movingBackwards, TimeSpan duration)
    {
        var storyboard = new Storyboard();

        if (movingBackwards)
        {
            storyboard.Children.Add(CreateOpacityAnimation(0, 1, duration, DecelerateKeySpline));
        }
        else
        {
            EnsureTranslateTransform(element);
            storyboard.Children.Add(CreateTranslateAnimation(TranslateYPath, 48, 0, duration, DecelerateKeySpline));
            storyboard.Children.Add(CreateImmediateOpacityAnimation(1));
        }

        return storyboard;
    }

    internal override Storyboard CreateExitStoryboard(FrameworkElement element, bool movingBackwards, TimeSpan duration)
    {
        var storyboard = new Storyboard();

        if (movingBackwards)
        {
            EnsureTranslateTransform(element);
            storyboard.Children.Add(CreateTranslateAnimation(TranslateYPath, 0, 48, duration, AccelerateKeySpline));
            storyboard.Children.Add(CreateOpacityAnimation(1, 0, duration, AccelerateKeySpline));
        }
        else
        {
            storyboard.Children.Add(CreateOpacityAnimation(1, 0, TimeSpan.FromMilliseconds(Math.Min(150, duration.TotalMilliseconds)), AccelerateKeySpline));
        }

        return storyboard;
    }

    private static void EnsureTranslateTransform(FrameworkElement element)
    {
        element.RenderTransform = new TranslateTransform();
    }

    private static DoubleAnimationUsingKeyFrames CreateTranslateAnimation(PropertyPath path, double from, double to, TimeSpan duration, KeySpline keySpline)
    {
        var animation = new DoubleAnimationUsingKeyFrames
        {
            KeyFrames =
            {
                new DiscreteDoubleKeyFrame(from, TimeSpan.Zero),
                new SplineDoubleKeyFrame(to, duration, keySpline)
            }
        };

        Storyboard.SetTargetProperty(animation, path);
        return animation;
    }

    private static DoubleAnimationUsingKeyFrames CreateOpacityAnimation(double from, double to, TimeSpan duration, KeySpline keySpline)
    {
        var animation = new DoubleAnimationUsingKeyFrames
        {
            KeyFrames =
            {
                new DiscreteDoubleKeyFrame(from, TimeSpan.Zero),
                new SplineDoubleKeyFrame(to, duration, keySpline)
            }
        };

        Storyboard.SetTargetProperty(animation, OpacityPath);
        return animation;
    }

    private static DoubleAnimation CreateImmediateOpacityAnimation(double value)
    {
        var animation = new DoubleAnimation(value, TimeSpan.Zero);
        Storyboard.SetTargetProperty(animation, OpacityPath);
        return animation;
    }
}
