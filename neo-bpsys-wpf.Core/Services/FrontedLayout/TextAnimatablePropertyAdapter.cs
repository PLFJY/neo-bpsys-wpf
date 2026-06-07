using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

public sealed class TextAnimatablePropertyAdapter : IAnimatablePropertyAdapter
{
    public bool CanHandle(FrontedAnimationTarget target, string propertyName) =>
        target.Element is Control or TextBlock
        && AnimationAdapterHelpers.Is(propertyName, "TextColor", "Foreground", "FontSize");

    public object? CaptureBaseValue(FrontedAnimationTarget target, string propertyName)
    {
        if (AnimationAdapterHelpers.Is(propertyName, "FontSize"))
        {
            return target.Element switch
            {
                TextBlock textBlock => textBlock.FontSize,
                Control control => control.FontSize,
                _ => null
            };
        }

        return target.Element switch
        {
            TextBlock textBlock => textBlock.Foreground,
            Control control => control.Foreground,
            _ => null
        };
    }

    public void SetValue(
        FrontedAnimationTarget target,
        string propertyName,
        string? value,
        FrontedAnimationExecutionContext context)
    {
        if (AnimationAdapterHelpers.Is(propertyName, "FontSize"))
        {
            SetFontSize(target, AnimationAdapterHelpers.ParseDoubleOrDefault(value, GetFontSize(target)));
            return;
        }

        if (!ColorHelper.TryParseColor(value, out var color))
        {
            context.Logger?.LogWarning("Invalid color value '{Value}' for {PropertyName}.", value, propertyName);
            return;
        }

        SetForeground(target, new SolidColorBrush(color));
    }

    public Task AnimateAsync(
        FrontedAnimationTarget target,
        string propertyName,
        string? from,
        string? to,
        int durationMs,
        string? easing,
        FrontedAnimationExecutionContext context)
    {
        if (AnimationAdapterHelpers.Is(propertyName, "FontSize"))
        {
            var property = target.Element is TextBlock
                ? TextBlock.FontSizeProperty
                : Control.FontSizeProperty;
            return AnimationAdapterHelpers.AnimateDoubleAsync(
                target.Element,
                property,
                string.IsNullOrWhiteSpace(from) ? null : AnimationAdapterHelpers.ParseDoubleOrDefault(from),
                AnimationAdapterHelpers.ParseDoubleOrDefault(to, GetFontSize(target)),
                durationMs,
                easing,
                context.CancellationToken);
        }

        if (!ColorHelper.TryParseColor(to, out var toColor))
        {
            context.Logger?.LogWarning("Invalid color value '{Value}' for {PropertyName}.", to, propertyName);
            return Task.CompletedTask;
        }

        var brush = EnsureForegroundBrush(target);
        if (ColorHelper.TryParseColor(from, out var fromColor))
        {
            brush.Color = fromColor;
        }

        if (durationMs <= 0)
        {
            brush.Color = toColor;
            return Task.CompletedTask;
        }

        var animation = new ColorAnimation
        {
            To = toColor,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            FillBehavior = FillBehavior.HoldEnd,
            EasingFunction = AnimationAdapterHelpers.CreateEasing(easing)
        };
        return AnimationAdapterHelpers.BeginAnimationAsync(
            brush,
            SolidColorBrush.ColorProperty,
            animation,
            context.CancellationToken);
    }

    public void ResetValue(
        FrontedAnimationTarget target,
        string propertyName,
        object? baseValue,
        FrontedAnimationExecutionContext context)
    {
        if (AnimationAdapterHelpers.Is(propertyName, "FontSize") && baseValue is double fontSize)
        {
            target.Element.BeginAnimation(
                target.Element is TextBlock ? TextBlock.FontSizeProperty : Control.FontSizeProperty,
                null);
            SetFontSize(target, fontSize);
            return;
        }

        if (GetForeground(target) is SolidColorBrush brush)
        {
            brush.BeginAnimation(SolidColorBrush.ColorProperty, null);
        }

        SetForeground(target, (Brush?)baseValue);
    }

    private static double GetFontSize(FrontedAnimationTarget target) =>
        target.Element switch
        {
            TextBlock textBlock => textBlock.FontSize,
            Control control => control.FontSize,
            _ => 12D
        };

    private static void SetFontSize(FrontedAnimationTarget target, double fontSize)
    {
        if (target.Element is TextBlock textBlock)
        {
            textBlock.FontSize = fontSize;
        }
        else if (target.Element is Control control)
        {
            control.FontSize = fontSize;
        }
    }

    private static Brush? GetForeground(FrontedAnimationTarget target) =>
        target.Element switch
        {
            TextBlock textBlock => textBlock.Foreground,
            Control control => control.Foreground,
            _ => null
        };

    private static void SetForeground(FrontedAnimationTarget target, Brush? brush)
    {
        if (target.Element is TextBlock textBlock)
        {
            textBlock.Foreground = brush;
        }
        else if (target.Element is Control control)
        {
            control.Foreground = brush;
        }
    }

    private static SolidColorBrush EnsureForegroundBrush(FrontedAnimationTarget target)
    {
        if (GetForeground(target) is SolidColorBrush brush && !brush.IsFrozen)
        {
            return brush;
        }

        var next = new SolidColorBrush(GetForeground(target) is SolidColorBrush existing ? existing.Color : Colors.White);
        SetForeground(target, next);
        return next;
    }
}
