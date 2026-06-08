using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

public sealed class FrontedAnimationRuntime(
    IFrontedAnimationTargetResolver targetResolver,
    IAnimatablePropertyAdapterRegistry adapterRegistry) : IFrontedAnimationRuntime
{
    private readonly object _gate = new();
    private readonly Dictionary<FrameworkElement, RuntimeSession> _sessions = new(ReferenceEqualityComparer.Instance);

    public FrontedAnimationRuntime()
        : this(new FrontedAnimationTargetResolver(), new FrontedAnimatablePropertyAdapterRegistry())
    {
    }

    public async Task ExecuteAsync(
        IReadOnlyList<FrontedGraphActionRequest> actions,
        FrontedAnimationExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        foreach (var action in actions)
        {
            await ExecuteAsync(action, context, cancellationToken);
        }
    }

    public Task ExecuteAsync(
        FrontedGraphActionRequest action,
        FrontedAnimationExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        return InvokeOnDispatcherAsync(context.Root, () => ExecuteOnDispatcherAsync(action, context, cancellationToken));
    }

    public void ResetTarget(Guid behaviorGuid, FrontedAnimationExecutionContext context)
    {
        InvokeOnDispatcher(context.Root, () =>
        {
            var session = GetSession(context.Root);
            foreach (var entry in session.BaseValues.Values
                         .Where(entry => entry.Target.BehaviorGuid == behaviorGuid)
                         .ToArray())
            {
                CancelConflict(session, entry.Target.Element, entry.PropertyName);
                entry.Adapter.ResetValue(entry.Target, entry.PropertyName, entry.BaseValue, context);
                session.BaseValues.Remove(new RuntimePropertyKey(entry.Target.Element, Normalize(entry.PropertyName)));
            }
        });
    }

    public void ResetAll(FrontedAnimationExecutionContext context)
    {
        InvokeOnDispatcher(context.Root, () =>
        {
            var session = GetSession(context.Root);
            foreach (var entry in session.BaseValues.Values.ToArray())
            {
                CancelConflict(session, entry.Target.Element, entry.PropertyName);
                entry.Adapter.ResetValue(entry.Target, entry.PropertyName, entry.BaseValue, context);
            }

            session.BaseValues.Clear();
        });
    }

    public void Release(FrameworkElement root)
    {
        InvokeOnDispatcher(root, () =>
        {
            lock (_gate)
            {
                if (!_sessions.TryGetValue(root, out var session))
                {
                    return;
                }

                // Cancel all in-flight animations
                foreach (var (key, cts) in session.Conflicts.ToArray())
                {
                    try
                    {
                        cts.Cancel();
                        cts.Dispose();
                    }
                    catch (ObjectDisposedException)
                    {
                        // Already disposed
                    }
                }

                foreach (var overlay in session.Overlays.Values.ToArray())
                {
                    if (overlay.Parent is Panel panel)
                    {
                        panel.Children.Remove(overlay);
                    }
                }

                session.Conflicts.Clear();
                session.BaseValues.Clear();
                session.Overlays.Clear();
                _sessions.Remove(root);
            }
        });
    }

    private async Task ExecuteOnDispatcherAsync(
        FrontedGraphActionRequest action,
        FrontedAnimationExecutionContext context,
        CancellationToken cancellationToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, cancellationToken);
        var effectiveContext = context.WithCancellationToken(linkedCts.Token);
        var controlTarget = targetResolver.Resolve(FrontedAnimationTargetReference.Parse(action.Target), effectiveContext);
        if (controlTarget is null)
        {
            effectiveContext.Logger?.LogWarning("Fronted animation action skipped because target {Target} was not resolved.", action.Target);
            return;
        }

        var target = ResolveTargetLayer(controlTarget, action.TargetLayer, action.PropertyName, effectiveContext);
        if (target is null)
        {
            effectiveContext.Logger?.LogWarning(
                "Fronted animation property {PropertyName} is unsupported for target layer {TargetLayer} on target {Target}.",
                action.PropertyName,
                action.TargetLayer,
                controlTarget.Name ?? controlTarget.BehaviorGuid.ToString());
            return;
        }

        if (action.RequestType == FrontedGraphActionRequestType.ResetProperty)
        {
            ResetActionTarget(target, action.PropertyName, effectiveContext);
            return;
        }

        var adapter = adapterRegistry.Resolve(target, action.PropertyName);
        if (adapter is null)
        {
            effectiveContext.Logger?.LogWarning(
                "Fronted animation property {PropertyName} is unsupported for target layer {TargetLayer} on target {Target}.",
                action.PropertyName,
                target.TargetLayer,
                target.Name ?? target.BehaviorGuid.ToString());
            return;
        }

        CaptureBaseValue(target, action.PropertyName, adapter, effectiveContext);
        var session = GetSession(effectiveContext.Root);
        CancelConflict(session, target.Element, action.PropertyName);

        if (action.RequestType == FrontedGraphActionRequestType.SetProperty)
        {
            adapter.SetValue(target, action.PropertyName, action.Values.GetValueOrDefault("Value"), effectiveContext);
            return;
        }

        var conflictKey = new RuntimePropertyKey(target.Element, Normalize(action.PropertyName));
        var conflictCts = CancellationTokenSource.CreateLinkedTokenSource(effectiveContext.CancellationToken);
        session.Conflicts[conflictKey] = conflictCts;
        try
        {
            await adapter.AnimateAsync(
                target,
                action.PropertyName,
                action.Values.GetValueOrDefault("From"),
                action.Values.GetValueOrDefault("To"),
                Math.Max(0, action.DurationMs ?? 0),
                action.Values.GetValueOrDefault("Easing"),
                effectiveContext.WithCancellationToken(conflictCts.Token));
        }
        catch (OperationCanceledException)
        {
            effectiveContext.Logger?.LogDebug(
                "Fronted animation {Target}.{PropertyName} was cancelled.",
                target.Name ?? target.BehaviorGuid.ToString(),
                action.PropertyName);
        }
        finally
        {
            // Only remove if we still own the conflict entry.
            // If a newer animation for the same property has replaced our CTS,
            // we must not remove the new entry.
            if (session.Conflicts.TryGetValue(conflictKey, out var current)
                && ReferenceEquals(current, conflictCts))
            {
                session.Conflicts.Remove(conflictKey);
            }
            conflictCts.Dispose();
        }
    }

    private void ResetActionTarget(
        FrontedAnimationTarget target,
        string propertyName,
        FrontedAnimationExecutionContext context)
    {
        var session = GetSession(context.Root);
        var entries = string.Equals(propertyName, "All", StringComparison.OrdinalIgnoreCase)
            ? session.BaseValues.Values.Where(entry => ReferenceEquals(entry.Target.Element, target.Element)).ToArray()
            : session.BaseValues.Values
                .Where(entry => ReferenceEquals(entry.Target.Element, target.Element)
                                && string.Equals(entry.PropertyName, propertyName, StringComparison.OrdinalIgnoreCase))
                .ToArray();

        foreach (var entry in entries)
        {
            CancelConflict(session, entry.Target.Element, entry.PropertyName);
            entry.Adapter.ResetValue(entry.Target, entry.PropertyName, entry.BaseValue, context);
            session.BaseValues.Remove(new RuntimePropertyKey(entry.Target.Element, Normalize(entry.PropertyName)));
        }
    }

    private FrontedAnimationTarget? ResolveTargetLayer(
        FrontedAnimationTarget controlTarget,
        FrontedAnimationTargetLayer requestedLayer,
        string propertyName,
        FrontedAnimationExecutionContext context)
    {
        var layer = requestedLayer == FrontedAnimationTargetLayer.Auto
            ? ResolveAutoLayer(controlTarget.Element, propertyName)
            : requestedLayer;

        var element = layer switch
        {
            FrontedAnimationTargetLayer.Control => controlTarget.Element,
            FrontedAnimationTargetLayer.Content => ResolveContentElement(controlTarget.Element),
            FrontedAnimationTargetLayer.OverlayAbove => EnsureOverlay(controlTarget, true, context),
            FrontedAnimationTargetLayer.OverlayBelow => EnsureOverlay(controlTarget, false, context),
            _ => controlTarget.Element
        };

        if (element is null)
        {
            return null;
        }

        return new FrontedAnimationTarget
        {
            Element = element,
            BehaviorGuid = controlTarget.BehaviorGuid,
            Name = controlTarget.Name,
            DisplayName = controlTarget.DisplayName,
            TargetLayer = layer,
            ControlElement = controlTarget.Element
        };
    }

    private static FrontedAnimationTargetLayer ResolveAutoLayer(FrameworkElement controlElement, string propertyName)
    {
        if (AnimationAdapterHelpers.Is(
            propertyName,
            "Opacity",
            "Visibility",
            "Width",
            "Height",
            "VisualOffsetX",
            "VisualOffsetY",
            "ScaleX",
            "ScaleY",
            "Rotation",
            "TintColor",
            "TintStrength",
            "TextureStrength"))
        {
            return FrontedAnimationTargetLayer.Control;
        }

        if (AnimationAdapterHelpers.Is(propertyName, "TextColor", "Foreground", "FontSize"))
        {
            return ResolveContentElement(controlElement) is not null
                ? FrontedAnimationTargetLayer.Content
                : FrontedAnimationTargetLayer.Control;
        }

        if (AnimationAdapterHelpers.Is(propertyName, "FillColor", "StrokeColor", "StrokeThickness"))
        {
            return ResolveContentElement(controlElement) is Shape
                ? FrontedAnimationTargetLayer.Content
                : FrontedAnimationTargetLayer.OverlayAbove;
        }

        return FrontedAnimationTargetLayer.Control;
    }

    private static FrameworkElement? ResolveContentElement(FrameworkElement controlElement)
    {
        if (controlElement is Shape or BackgroundTintControlHost)
        {
            return controlElement;
        }

        if (controlElement is Border { Child: TextBlock textBlock })
        {
            return textBlock;
        }

        if (controlElement is Border { Child: Grid borderedImageContent })
        {
            return FindFirstDescendant<Image>(borderedImageContent);
        }

        if (controlElement is Grid grid)
        {
            return (FrameworkElement?)grid.Children.OfType<Image>().FirstOrDefault()
                   ?? FindFirstDescendant<TextBlock>(grid);
        }

        return (FrameworkElement?)FindFirstDescendant<TextBlock>(controlElement)
               ?? FindFirstDescendant<Image>(controlElement)
               ?? (controlElement is Control ? controlElement : null);
    }

    private Rectangle? EnsureOverlay(
        FrontedAnimationTarget controlTarget,
        bool above,
        FrontedAnimationExecutionContext context)
    {
        var canvas = FindAncestorOrSelf<Canvas>(controlTarget.Element);
        if (canvas is null)
        {
            context.Logger?.LogWarning(
                "Fronted animation overlay layer cannot be created because target {Target} is not on a Canvas.",
                controlTarget.Name ?? controlTarget.BehaviorGuid.ToString());
            return null;
        }

        var session = GetSession(context.Root);
        var key = new RuntimeOverlayKey(controlTarget.Element, above);
        if (!session.Overlays.TryGetValue(key, out var overlay))
        {
            overlay = new Rectangle
            {
                Fill = Brushes.Transparent,
                Stroke = Brushes.Transparent,
                StrokeThickness = 0D,
                IsHitTestVisible = false
            };
            FrontedRendererProperties.SetIsAnimationAuxiliaryElement(overlay, true);
            canvas.Children.Add(overlay);
            session.Overlays[key] = overlay;
        }

        SyncOverlay(controlTarget.Element, overlay, above);
        return overlay;
    }

    private static void SyncOverlay(FrameworkElement target, Rectangle overlay, bool above)
    {
        var left = Canvas.GetLeft(target);
        var top = Canvas.GetTop(target);
        Canvas.SetLeft(overlay, double.IsNaN(left) ? 0D : left);
        Canvas.SetTop(overlay, double.IsNaN(top) ? 0D : top);
        overlay.Width = ResolveSize(target.Width, target.ActualWidth);
        overlay.Height = ResolveSize(target.Height, target.ActualHeight);
        overlay.Visibility = target.Visibility;
        Panel.SetZIndex(overlay, Panel.GetZIndex(target) + (above ? 1 : -1));
    }

    private static double ResolveSize(double configured, double actual) =>
        configured > 0D && double.IsFinite(configured)
            ? configured
            : actual > 0D && double.IsFinite(actual) ? actual : 1D;

    private static T? FindFirstDescendant<T>(DependencyObject root)
        where T : FrameworkElement
    {
        var children = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < children; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match && !FrontedRendererProperties.GetIsAnimationAuxiliaryElement(match))
            {
                return match;
            }

            var descendant = FindFirstDescendant<T>(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    private static T? FindAncestorOrSelf<T>(DependencyObject element)
        where T : DependencyObject
    {
        var current = element;
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private void CaptureBaseValue(
        FrontedAnimationTarget target,
        string propertyName,
        IAnimatablePropertyAdapter adapter,
        FrontedAnimationExecutionContext context)
    {
        var session = GetSession(context.Root);
        var key = new RuntimePropertyKey(target.Element, Normalize(propertyName));
        if (session.BaseValues.ContainsKey(key))
        {
            return;
        }

        session.BaseValues[key] = new RuntimeBaseValue(
            target,
            propertyName,
            adapter,
            adapter.CaptureBaseValue(target, propertyName));
    }

    private RuntimeSession GetSession(FrameworkElement root)
    {
        lock (_gate)
        {
            if (!_sessions.TryGetValue(root, out var session))
            {
                session = new RuntimeSession();
                _sessions[root] = session;
            }

            return session;
        }
    }

    private static void CancelConflict(RuntimeSession session, FrameworkElement target, string propertyName)
    {
        var key = new RuntimePropertyKey(target, Normalize(propertyName));
        if (session.Conflicts.TryGetValue(key, out var cts))
        {
            cts.Cancel();
            // Note: cleanup (remove + dispose) is handled by the animation's finally block.
            // This avoids races where a new animation's CTS replaces ours before we clean up.
        }
    }

    private static string Normalize(string propertyName) =>
        propertyName.Trim().ToUpperInvariant();

    private static async Task InvokeOnDispatcherAsync(FrameworkElement root, Func<Task> action)
    {
        if (root.Dispatcher.CheckAccess())
        {
            await action();
            return;
        }

        await await root.Dispatcher.InvokeAsync(action);
    }

    private static void InvokeOnDispatcher(FrameworkElement root, Action action)
    {
        if (root.Dispatcher.CheckAccess())
        {
            action();
            return;
        }

        root.Dispatcher.Invoke(action);
    }

    private sealed class RuntimeSession
    {
        public Dictionary<RuntimePropertyKey, RuntimeBaseValue> BaseValues { get; } = [];
        public Dictionary<RuntimePropertyKey, CancellationTokenSource> Conflicts { get; } = [];
        public Dictionary<RuntimeOverlayKey, Rectangle> Overlays { get; } = [];
    }

    private sealed record RuntimePropertyKey(FrameworkElement Element, string PropertyName);

    private sealed record RuntimeOverlayKey(FrameworkElement Element, bool Above);

    private sealed record RuntimeBaseValue(
        FrontedAnimationTarget Target,
        string PropertyName,
        IAnimatablePropertyAdapter Adapter,
        object? BaseValue);
}
