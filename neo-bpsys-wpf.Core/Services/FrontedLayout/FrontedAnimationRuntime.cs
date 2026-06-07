using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using System.Windows;

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

    private async Task ExecuteOnDispatcherAsync(
        FrontedGraphActionRequest action,
        FrontedAnimationExecutionContext context,
        CancellationToken cancellationToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, cancellationToken);
        var effectiveContext = context.WithCancellationToken(linkedCts.Token);
        var target = targetResolver.Resolve(FrontedAnimationTargetReference.Parse(action.Target), effectiveContext);
        if (target is null)
        {
            effectiveContext.Logger?.LogWarning("Fronted animation action skipped because target {Target} was not resolved.", action.Target);
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
                "Fronted animation property {PropertyName} is unsupported for target {Target}.",
                action.PropertyName,
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

        var conflictCts = CancellationTokenSource.CreateLinkedTokenSource(effectiveContext.CancellationToken);
        session.Conflicts[new RuntimePropertyKey(target.Element, Normalize(action.PropertyName))] = conflictCts;
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
            session.Conflicts.Remove(new RuntimePropertyKey(target.Element, Normalize(action.PropertyName)));
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
        if (!session.Conflicts.Remove(key, out var cts))
        {
            return;
        }

        cts.Cancel();
        cts.Dispose();
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
    }

    private sealed record RuntimePropertyKey(FrameworkElement Element, string PropertyName);

    private sealed record RuntimeBaseValue(
        FrontedAnimationTarget Target,
        string PropertyName,
        IAnimatablePropertyAdapter Adapter,
        object? BaseValue);
}
