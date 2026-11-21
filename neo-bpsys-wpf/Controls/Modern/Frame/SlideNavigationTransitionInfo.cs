#nullable enable

using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace neo_bpsys_wpf.Controls.Modern.Frame;

// Inspired by iNKORE.UI.WPF.Modern SlideNavigationTransitionInfo.
/// <summary>
/// 滑动导航过渡信息。内容从左、右或底部滑入/滑出。
/// </summary>
public sealed class SlideNavigationTransitionInfo : ModernNavigationTransitionInfo
{
    /// <summary>
    /// <see cref="Effect"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty EffectProperty =
        DependencyProperty.Register(
            nameof(Effect),
            typeof(SlideNavigationTransitionEffect),
            typeof(SlideNavigationTransitionInfo),
            new PropertyMetadata(SlideNavigationTransitionEffect.FromBottom));

    /// <summary>
    /// 获取或设置滑动过渡的方向效果。
    /// </summary>
    public SlideNavigationTransitionEffect Effect
    {
        get => (SlideNavigationTransitionEffect)GetValue(EffectProperty);
        set => SetValue(EffectProperty, value);
    }

    internal override Storyboard CreateEnterStoryboard(FrameworkElement element, bool movingBackwards, TimeSpan duration)
    {
        element.RenderTransform = new TranslateTransform();
        var storyboard = new Storyboard();
        if (Effect == SlideNavigationTransitionEffect.FromBottom)
        {
            var middle = ProfileTime(250, duration);
            if (!movingBackwards)
            {
                var translation = new DoubleAnimationUsingKeyFrames
                {
                    KeyFrames = { new DiscreteDoubleKeyFrame(200, TimeSpan.Zero), new DiscreteDoubleKeyFrame(200, middle),
                        new EasingDoubleKeyFrame(0, ProfileTime(600, duration),
                            new ExponentialEase { Exponent = 6, EasingMode = EasingMode.EaseOut }) }
                };
                Storyboard.SetTargetProperty(translation, TranslateYPath);
                storyboard.Children.Add(translation);
            }
            storyboard.Children.Add(EntranceNavigationTransitionInfo.StepOpacity(0, 1, middle));
        }
        else
        {
            var factor = Effect == SlideNavigationTransitionEffect.FromLeft ? 1 : -1;
            var from = (movingBackwards ? 150 : -200) * factor;
            var exit = ProfileTime(150, duration);
            storyboard.Children.Add(EntranceNavigationTransitionInfo.StagedSpline(TranslateXPath, from, 0, exit, duration, DecelerateKeySpline));
            storyboard.Children.Add(EntranceNavigationTransitionInfo.StepOpacity(0, 1, exit));
        }
        return storyboard;
    }

    internal override Storyboard CreateExitStoryboard(FrameworkElement element, bool movingBackwards, TimeSpan duration)
    {
        element.RenderTransform = new TranslateTransform();
        var storyboard = new Storyboard();
        if (Effect == SlideNavigationTransitionEffect.FromBottom)
        {
            if (movingBackwards)
            {
                var translation = new DoubleAnimationUsingKeyFrames
                {
                    KeyFrames = { new DiscreteDoubleKeyFrame(0, TimeSpan.Zero),
                        new EasingDoubleKeyFrame(200, ProfileTime(600, duration),
                            new ExponentialEase { Exponent = 6, EasingMode = EasingMode.EaseIn }) }
                };
                Storyboard.SetTargetProperty(translation, TranslateYPath);
                storyboard.Children.Add(translation);
            }
            storyboard.Children.Add(EntranceNavigationTransitionInfo.StepOpacity(1, 0, ProfileTime(250, duration)));
        }
        else
        {
            var factor = Effect == SlideNavigationTransitionEffect.FromLeft ? 1 : -1;
            var to = (movingBackwards ? -200 : 150) * factor;
            var exit = ProfileTime(150, duration);
            storyboard.Children.Add(EntranceNavigationTransitionInfo.Translate(TranslateXPath, 0, to, exit, AccelerateKeySpline));
            storyboard.Children.Add(EntranceNavigationTransitionInfo.StepOpacity(1, 0, exit));
        }
        return storyboard;
    }

    private static TimeSpan ProfileTime(int milliseconds, TimeSpan duration) => TimeSpan.FromTicks(duration.Ticks * milliseconds / 450);
}
