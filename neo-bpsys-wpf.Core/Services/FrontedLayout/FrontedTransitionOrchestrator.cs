using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 用前台行为图包装业务提交的默认转换编排器。
/// </summary>
public sealed class FrontedTransitionOrchestrator : IFrontedTransitionOrchestrator
{
    private readonly FrontedBehaviorRuntimeHostManager _hostManager;
    private readonly ILogger<FrontedTransitionOrchestrator> _logger;
    private readonly Dispatcher _uiDispatcher;
    private readonly SemaphoreSlim _transitionGate = new(1, 1);

    /// <summary>
    /// 初始化 <see cref="FrontedTransitionOrchestrator"/> 的新实例。
    /// </summary>
    /// <param name="hostManager">包含已附加前台行为宿主的运行时宿主管理器。</param>
    /// <param name="logger">用于转换诊断的日志记录器。</param>
    public FrontedTransitionOrchestrator(
        FrontedBehaviorRuntimeHostManager hostManager,
        ILogger<FrontedTransitionOrchestrator> logger)
    {
        _hostManager = hostManager;
        _logger = logger;
        _uiDispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
    }

    /// <inheritdoc />
    public Task RunTransitionAsync(
        FrontedTransitionRequest request,
        Func<Task> commitAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(commitAsync);

        return RunMultiTargetTransitionAsync([request], commitAsync, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RunMultiTargetTransitionAsync(
        IReadOnlyList<FrontedTransitionRequest> requests,
        Func<Task> commitAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(commitAsync);

        // 转场共享同一组行为宿主和动画会话。快速连续操作若并发执行，会互相取消 Exit/Enter 图，
        // 使部分控件停留在中间透明度/可见性状态；这里按到达顺序串行化整段转场。
        await _transitionGate.WaitAsync(cancellationToken);
        try
        {
            await RunTransitionSerializedAsync(requests, commitAsync, cancellationToken);
        }
        finally
        {
            _transitionGate.Release();
        }
    }

    private async Task RunTransitionSerializedAsync(
        IReadOnlyList<FrontedTransitionRequest> requests,
        Func<Task> commitAsync,
        CancellationToken cancellationToken)
    {
        var executions = requests
            .SelectMany(request => _hostManager.CreateTransitionExecutions(request, cancellationToken))
            .ToArray();

        try
        {
            if (executions.Length > 0)
            {
                await Task.WhenAll(executions.Select(execution => execution.RunExitGraphAsync(cancellationToken)));
                _logger.LogInformation("Transition ExitGraph completed.");

                if (executions.Any(execution => execution.IsCancellationRequested))
                {
                    _logger.LogInformation(
                        "Transition commit skipped because one or more matching transition executions were interrupted.");
                    return;
                }
            }

            await InvokeCommitOnUiAsync(commitAsync);

            if (executions.Length > 0)
            {
                _logger.LogInformation(
                    "Transition EnterGraph begin: dispatcherAccess={DispatcherAccess}",
                    _uiDispatcher.CheckAccess());
                await Task.WhenAll(executions.Select(execution => execution.RunEnterGraphAsync(cancellationToken)));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Transition commit failed; EnterGraph execution will be skipped.");
            throw;
        }
        finally
        {
            foreach (var execution in executions)
            {
                execution.Complete();
            }
        }
    }

    private async Task InvokeCommitOnUiAsync(Func<Task> commitAsync)
    {
        _logger.LogInformation(
            "Transition commit begin: dispatcherAccess={DispatcherAccess}",
            _uiDispatcher.CheckAccess());

        if (_uiDispatcher.CheckAccess())
        {
            await commitAsync();
        }
        else
        {
            await _uiDispatcher.InvokeAsync(commitAsync).Task.Unwrap();
        }

        _logger.LogInformation("Transition commit completed.");
    }
}
