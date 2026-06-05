#nullable enable

using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace neo_bpsys_wpf.Controls.Modern.Frame;

// Inspired by iNKORE.UI.WPF.Modern SlideNavigationTransitionInfo.
public sealed class SlideNavigationTransitionInfo : ModernNavigationTransitionInfo
{
    public static readonly DependencyProperty EffectProperty =
        DependencyProperty.Register(
            nameof(Effect),
            typeof(SlideNavigationTransitionEffect),
            typeof(SlideNavigationTransitionInfo),
            new PropertyMetadata(SlideNavigationTransitionEffect.FromBottom));

    public SlideNavigationTransitionEffect Effect
    {
        get => (SlideNavigationTransitionEffect)GetValue(EffectProperty);
        set => SetValue(EffectProperty, value);
    }

    internal override Storyboard CreateEnterStoryboard(FrameworkElement element, bool movingBackwards, TimeSpan duration)
    {
        var storyboard = new Storyboard();
        EnsureTranslateTransform(element);

        if (Effect == SlideNavigationTransitionEffect.FromBottom)
        {
            storyboard.Children.Add(movingBackwards
                ? CreateOpacityAnimation(0, 1, duration, DecelerateKeySpline)
                : CreateTranslateAnimation(TranslateYPath, 48, 0, duration, DecelerateKeySpline));
        }
        else
        {
            var fromLeft = Effect == SlideNavigationTransitionEffect.FromLeft ? !movingBackwards : movingBackwards;
            storyboard.Children.Add(CreateTranslateAnimation(TranslateXPath, fromLeft ? -96 : 96, 0, duration, DecelerateKeySpline));
        }

        storyboard.Children.Add(CreateImmediateOpacityAnimation(1));
        return storyboard;
    }

    internal override Storyboard CreateExitStoryboard(FrameworkElement element, bool movingBackwards, TimeSpan duration)
    {
        var storyboard = new Storyboard();
        EnsureTranslateTransform(element);

        if (Effect == SlideNavigationTransitionEffect.FromBottom)
        {
            if (movingBackwards)
            {
                storyboard.Children.Add(CreateTranslateAnimation(TranslateYPath, 0, 48, duration, AccelerateKeySpline));
            }
        }
        else
        {
            var toLeft = Effect == SlideNavigationTransitionEffect.FromLeft ? movingBackwards : !movingBackwards;
            storyboard.Children.Add(CreateTranslateAnimation(TranslateXPath, 0, toLeft ? -96 : 96, duration, AccelerateKeySpline));
        }

        storyboard.Children.Add(CreateOpacityAnimation(1, 0, TimeSpan.FromMilliseconds(Math.Min(150, duration.TotalMilliseconds)), AccelerateKeySpline));
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
