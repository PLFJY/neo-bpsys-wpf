using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using System.Windows;
using System.Windows.Controls;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 将 <see cref="IFrontedAnimationRuntime" /> 包装为 <see cref="IFrontedGraphActionExecutor" />，
/// 供 <see cref="FrontedNodeGraphRuntime" /> 在图执行期间使用。
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
    /// 初始化 <see cref="FrontedAnimationRuntimeActionExecutor" /> 的新实例。
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
    public Task ExecuteAsync(FrontedGraphActionRequest request, CancellationToken cancellationToken)
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

        if (request.WaitForCompletion)
        {
            return _animationRuntime.ExecuteAsync(request, context, cancellationToken);
        }

        // Fire-and-forget: start the animation but don't wait for completion.
        // The animation remains managed by AnimationRuntime and can be cancelled
        // by Reset/Release/subsequent same-property animations.
        FireAndForgetAsync(request, context, cancellationToken);
        return Task.CompletedTask;
    }

    private async void FireAndForgetAsync(FrontedGraphActionRequest request, FrontedAnimationExecutionContext context, CancellationToken cancellationToken)
    {
        try
        {
            await _animationRuntime.ExecuteAsync(request, context, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Expected when a subsequent animation or Release/Reset cancels this one.
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Fire-and-forget animation failed for {Target}.{Property}", request.Target, request.PropertyName);
        }
    }
}
