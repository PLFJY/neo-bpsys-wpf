using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using System.Globalization;
using System.Windows.Media.Effects;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 将 GaussianBlurRadius 行为属性应用到既有的运行时效果宿主。
/// </summary>
public sealed class GaussianBlurAnimatablePropertyAdapter : IAnimatablePropertyAdapter
{
    /// <inheritdoc />
    public bool CanHandle(FrontedAnimationTarget target, string propertyName) =>
        AnimationAdapterHelpers.Is(propertyName, "GaussianBlurRadius")
        && FrontedEffectHostFactory.FindEffectHost(target.Element) is not null;

    /// <inheritdoc />
    public object? CaptureBaseValue(FrontedAnimationTarget target, string propertyName)
    {
        var effect = GetHost(target).Effect;
        return effect is BlurEffect blur
            ? new BlurBaseline(true, blur.Radius, blur.RenderingBias)
            : new BlurBaseline(false, 0D, RenderingBias.Performance);
    }

    /// <inheritdoc />
    public void SetValue(FrontedAnimationTarget target, string propertyName, string? value, FrontedAnimationExecutionContext context)
    {
        if (!TryParseRadius(value, out var radius))
        {
            return;
        }

        var blur = EnsureBlur(GetHost(target));
        blur.BeginAnimation(BlurEffect.RadiusProperty, null);
        blur.Radius = radius;
    }

    /// <inheritdoc />
    public Task AnimateAsync(
        FrontedAnimationTarget target,
        string propertyName,
        string? from,
        string? to,
        int durationMs,
        string? easing,
        FrontedAnimationExecutionContext context)
    {
        var blur = EnsureBlur(GetHost(target));
        var fromRadius = TryParseRadius(from, out var parsedFrom) ? parsedFrom : (double?)null;
        var toRadius = TryParseRadius(to, out var parsedTo) ? parsedTo : blur.Radius;
        return AnimationAdapterHelpers.AnimateDoubleAsync(
            blur,
            BlurEffect.RadiusProperty,
            fromRadius,
            toRadius,
            durationMs,
            easing,
            context.CancellationToken);
    }

    /// <inheritdoc />
    public void ResetValue(FrontedAnimationTarget target, string propertyName, object? baseValue, FrontedAnimationExecutionContext context)
    {
        var host = GetHost(target);
        if (host.Effect is BlurEffect current)
        {
            current.BeginAnimation(BlurEffect.RadiusProperty, null);
        }

        if (baseValue is BlurBaseline { HasBlur: true } baseline)
        {
            host.Effect = new BlurEffect
            {
                Radius = baseline.Radius,
                RenderingBias = baseline.RenderingBias
            };
            return;
        }

        host.Effect = null;
    }

    private static FrontedEffectHost GetHost(FrontedAnimationTarget target) =>
        FrontedEffectHostFactory.FindEffectHost(target.Element)
        ?? throw new InvalidOperationException("Gaussian blur target has no stable effect host.");

    private static BlurEffect EnsureBlur(FrontedEffectHost host)
    {
        if (host.Effect is BlurEffect blur)
        {
            return blur;
        }

        blur = new BlurEffect { RenderingBias = RenderingBias.Performance };
        host.Effect = blur;
        return blur;
    }

    private static bool TryParseRadius(string? value, out double radius)
    {
        radius = 0D;
        return !string.IsNullOrWhiteSpace(value)
               && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
               && double.IsFinite(parsed)
               && parsed >= 0D
               && (radius = parsed) >= 0D;
    }

    private sealed record BlurBaseline(bool HasBlur, double Radius, RenderingBias RenderingBias);
}
