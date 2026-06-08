using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using System.Windows;
using System.Windows.Controls;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Wraps <see cref="IFrontedAnimationRuntime" /> as an <see cref="IFrontedGraphActionExecutor" />
/// for use by <see cref="FrontedNodeGraphRuntime" /> during graph execution.
/// </summary>
public sealed class FrontedAnimationRuntimeActionExecutor : IFrontedGraphActionExecutor
{
    private readonly IFrontedAnimationRuntime _animationRuntime;
    private readonly Canvas _rootCanvas;
    private readonly Guid _selfBehaviorGuid;
    private readonly string? _selfDisplayName;
    private readonly string _windowId;
    private readonly string _canvasName;
    private readonly ILogger? _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="FrontedAnimationRuntimeActionExecutor" />.
    /// </summary>
    public FrontedAnimationRuntimeActionExecutor(
        IFrontedAnimationRuntime animationRuntime,
        Canvas rootCanvas,
        Guid selfBehaviorGuid,
        string? selfDisplayName,
        string windowId,
        string canvasName,
        ILogger? logger = null)
    {
        _animationRuntime = animationRuntime;
        _rootCanvas = rootCanvas;
        _selfBehaviorGuid = selfBehaviorGuid;
        _selfDisplayName = selfDisplayName;
        _windowId = windowId;
        _canvasName = canvasName;
        _logger = logger ?? NullLogger.Instance;
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(FrontedGraphActionRequest request, CancellationToken cancellationToken)
    {
        var context = new FrontedAnimationExecutionContext
        {
            Root = _rootCanvas,
            SelfBehaviorGuid = _selfBehaviorGuid,
            SelfDisplayName = _selfDisplayName,
            WindowId = _windowId,
            CanvasName = _canvasName,
            IsDesignerPreview = false,
            Logger = _logger,
            CancellationToken = cancellationToken
        };

        await _animationRuntime.ExecuteAsync(request, context, cancellationToken);
    }
}
