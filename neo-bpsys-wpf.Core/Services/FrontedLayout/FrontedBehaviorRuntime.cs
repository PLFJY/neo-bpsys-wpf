using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// <see cref="FrontedBehaviorRuntimeHostManager" /> 的外观，为 DI 使用者实现
/// <see cref="IFrontedBehaviorRuntime" />。
/// </summary>
public sealed class FrontedBehaviorRuntime : IFrontedBehaviorRuntime
{
    private readonly FrontedBehaviorRuntimeHostManager _hostManager;

    /// <summary>
    /// 初始化 <see cref="FrontedBehaviorRuntime" /> 的新实例。
    /// </summary>
    public FrontedBehaviorRuntime(FrontedBehaviorRuntimeHostManager hostManager)
    {
        _hostManager = hostManager;
    }

    /// <inheritdoc />
    public Task AttachAsync(FrontedBehaviorRuntimeContext context, CancellationToken cancellationToken = default)
    {
        return _hostManager.AttachHostAsync(context, cancellationToken);
    }

    /// <inheritdoc />
    public Task DetachAsync(string windowId, CancellationToken cancellationToken = default)
    {
        _hostManager.DetachHost(windowId);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void PublishManualTrigger(string triggerName, string? windowId = null)
    {
        _hostManager.PublishManualTrigger(triggerName, windowId);
    }

    /// <inheritdoc />
    public Task<int> StopAllLoopBehaviorsAsync(
        FrontedBehaviorStopReason reason = FrontedBehaviorStopReason.ManualClear,
        CancellationToken cancellationToken = default)
    {
        return _hostManager.StopAllLoopBehaviorsAsync(reason, cancellationToken);
    }

    /// <inheritdoc />
    public Task<int> StopLoopBehaviorsAsync(
        string windowId,
        FrontedBehaviorStopReason reason,
        CancellationToken cancellationToken = default)
    {
        return _hostManager.StopLoopBehaviorsAsync(windowId, reason, cancellationToken);
    }
}
