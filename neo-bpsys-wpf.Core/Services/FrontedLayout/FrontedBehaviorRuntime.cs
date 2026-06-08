using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Facade over <see cref="FrontedBehaviorRuntimeHostManager" /> that implements
/// <see cref="IFrontedBehaviorRuntime" /> for DI consumers.
/// </summary>
public sealed class FrontedBehaviorRuntime : IFrontedBehaviorRuntime
{
    private readonly FrontedBehaviorRuntimeHostManager _hostManager;

    /// <summary>
    /// Initializes a new instance of <see cref="FrontedBehaviorRuntime" />.
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
    public Task DetachAsync(string windowId, string canvasName, CancellationToken cancellationToken = default)
    {
        _hostManager.DetachHost(windowId, canvasName);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void PublishManualTrigger(string triggerName, string? windowId = null, string? canvasName = null)
    {
        _hostManager.PublishManualTrigger(triggerName, windowId, canvasName);
    }
}
