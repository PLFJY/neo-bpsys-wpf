using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

public sealed class BackgroundTintAnimatablePropertyAdapter : IAnimatablePropertyAdapter
{
    public bool CanHandle(FrontedAnimationTarget target, string propertyName) =>
        target.Element is BackgroundTintControlHost
        && AnimationAdapterHelpers.Is(propertyName, "TintColor", "TintStrength", "TextureStrength");

    public object? CaptureBaseValue(FrontedAnimationTarget target, string propertyName) =>
        target.Element is BackgroundTintControlHost host && AnimationAdapterHelpers.Is(propertyName, "TintColor")
            ? host.TintColorValue
            : null;

    public void SetValue(
        FrontedAnimationTarget target,
        string propertyName,
        string? value,
        FrontedAnimationExecutionContext context)
    {
        if (target.Element is not BackgroundTintControlHost host)
        {
            return;
        }

        if (!AnimationAdapterHelpers.Is(propertyName, "TintColor"))
        {
            context.Logger?.LogWarning(
                "Background tint property {PropertyName} is not animatable in Phase 4 because it is constructor state.",
                propertyName);
            return;
        }

        host.TintColorValue = value;
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
        if (!AnimationAdapterHelpers.Is(propertyName, "TintColor"))
        {
            SetValue(target, propertyName, to, context);
            return Task.CompletedTask;
        }

        if (!string.IsNullOrWhiteSpace(from))
        {
            SetValue(target, propertyName, from, context);
        }

        SetValue(target, propertyName, to, context);
        return Task.CompletedTask;
    }

    public void ResetValue(
        FrontedAnimationTarget target,
        string propertyName,
        object? baseValue,
        FrontedAnimationExecutionContext context)
    {
        if (target.Element is BackgroundTintControlHost host
            && AnimationAdapterHelpers.Is(propertyName, "TintColor"))
        {
            host.TintColorValue = baseValue as string;
        }
    }
}
