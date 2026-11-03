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
        target.Element is not BackgroundTintControlHost host ? null :
        AnimationAdapterHelpers.Is(propertyName, "TintColor") ? host.TintColorValue :
        AnimationAdapterHelpers.Is(propertyName, "TintStrength") ? host.TintStrengthValue :
        AnimationAdapterHelpers.Is(propertyName, "TextureStrength") ? host.TextureStrengthValue : null;

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

        if (AnimationAdapterHelpers.Is(propertyName, "TintColor")) host.TintColorValue = value;
        else if (AnimationAdapterHelpers.Is(propertyName, "TintStrength")) host.TintStrengthValue = AnimationAdapterHelpers.ParseDoubleOrDefault(value, host.TintStrengthValue);
        else if (AnimationAdapterHelpers.Is(propertyName, "TextureStrength")) host.TextureStrengthValue = AnimationAdapterHelpers.ParseDoubleOrDefault(value, host.TextureStrengthValue);
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
        if (AnimationAdapterHelpers.Is(propertyName, "TintColor"))
        {
            if (!string.IsNullOrWhiteSpace(from)) SetValue(target, propertyName, from, context);
            SetValue(target, propertyName, to, context);
            return Task.CompletedTask;
        }
        if (target.Element is not BackgroundTintControlHost host) return Task.CompletedTask;
        var start = AnimationAdapterHelpers.Is(propertyName, "TintStrength") ? host.TintStrengthValue : host.TextureStrengthValue;
        return AnimateStrengthAsync(host, propertyName, string.IsNullOrWhiteSpace(from) ? start : AnimationAdapterHelpers.ParseDoubleOrDefault(from, start), AnimationAdapterHelpers.ParseDoubleOrDefault(to, start), durationMs, context.CancellationToken);
    }

    public void ResetValue(
        FrontedAnimationTarget target,
        string propertyName,
        object? baseValue,
        FrontedAnimationExecutionContext context)
    {
        if (target.Element is not BackgroundTintControlHost host) return;
        if (AnimationAdapterHelpers.Is(propertyName, "TintColor")) host.TintColorValue = baseValue as string;
        else if (AnimationAdapterHelpers.Is(propertyName, "TintStrength") && baseValue is double tint) host.TintStrengthValue = tint;
        else if (AnimationAdapterHelpers.Is(propertyName, "TextureStrength") && baseValue is double texture) host.TextureStrengthValue = texture;
    }

    private static async Task AnimateStrengthAsync(BackgroundTintControlHost host, string propertyName, double from, double to, int durationMs, CancellationToken cancellationToken)
    {
        if (durationMs <= 0) { SetStrength(host, propertyName, to); return; }

        // 染色强度每次变化都会触发 CPU 像素处理，不能按 60fps 逐帧重算。
        // 这里按约 50ms 一个关键帧量化，最终值仍精确落到目标值；运行中的宿主会在后台合并计算，
        // Designer/测试宿主保持同步，视觉变化由“平滑”变为“分步”，但不会冻结 UI 线程。
        var steps = Math.Clamp((int)Math.Ceiling(durationMs / 50D), 1, 12);
        var stepDelay = Math.Max(1, durationMs / steps);
        for (var step = 1; step <= steps; step++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var progress = step / (double)steps;
            SetStrength(host, propertyName, from + ((to - from) * progress));
            if (step < steps)
            {
                await Task.Delay(stepDelay, cancellationToken);
            }
        }
    }

    private static void SetStrength(BackgroundTintControlHost host, string propertyName, double value)
    {
        if (AnimationAdapterHelpers.Is(propertyName, "TintStrength")) host.TintStrengthValue = value;
        else host.TextureStrengthValue = value;
    }
}
