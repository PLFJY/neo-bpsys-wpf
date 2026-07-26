using System.Globalization;
using System.Windows;
using System.Windows.Media.Animation;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

internal static class AnimationAdapterHelpers
{
    public static bool Is(string propertyName, params string[] names) =>
        names.Any(name => string.Equals(propertyName, name, StringComparison.OrdinalIgnoreCase));

    public static double ParseDoubleOrDefault(string? value, double fallback = 0D) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? number
            : fallback;

    public static IEasingFunction? CreateEasing(string? easing)
    {
        return easing?.Trim() switch
        {
            "SineInOut" => new SineEase { EasingMode = EasingMode.EaseInOut },
            "CubicOut" => new CubicEase { EasingMode = EasingMode.EaseOut },
            "CubicIn" => new CubicEase { EasingMode = EasingMode.EaseIn },
            "CubicInOut" => new CubicEase { EasingMode = EasingMode.EaseInOut },
            "BackOut" => new BackEase { EasingMode = EasingMode.EaseOut },
            _ => null
        };
    }

    public static Task AnimateDoubleAsync(
        DependencyObject target,
        DependencyProperty property,
        double? from,
        double to,
        int durationMs,
        string? easing,
        CancellationToken cancellationToken)
    {
        if (durationMs <= 0)
        {
            target.SetValue(property, to);
            return Task.CompletedTask;
        }

        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            FillBehavior = FillBehavior.HoldEnd,
            EasingFunction = CreateEasing(easing)
        };
        return BeginAnimationAsync(target, property, animation, cancellationToken);
    }

    public static Task BeginAnimationAsync(
        DependencyObject target,
        DependencyProperty property,
        AnimationTimeline animation,
        CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler? completed = null;
        CancellationTokenRegistration registration = default;
        completed = (_, _) =>
        {
            animation.Completed -= completed;
            registration.Dispose();
            tcs.TrySetResult();
        };
        animation.Completed += completed;
        if (cancellationToken.CanBeCanceled)
        {
            registration = cancellationToken.Register(() =>
            {
                animation.Completed -= completed;
                if (target is IAnimatable animatable)
                {
                    target.Dispatcher.BeginInvoke(() => animatable.BeginAnimation(property, null));
                }

                tcs.TrySetCanceled(cancellationToken);
            });
        }

        ((IAnimatable)target).BeginAnimation(property, animation);
        return tcs.Task;
    }
}
