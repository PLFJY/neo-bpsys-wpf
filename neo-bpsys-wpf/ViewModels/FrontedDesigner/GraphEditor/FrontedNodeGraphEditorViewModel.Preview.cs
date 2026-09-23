using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Tutorial;

namespace neo_bpsys_wpf.ViewModels.FrontedDesigner.GraphEditor;

/// <summary>
/// 节点图编辑器的Preview逻辑。
/// </summary>
public sealed partial class FrontedNodeGraphEditorViewModel
{
    /// <summary>
    /// 重新校验当前图，并替换校验消息集合。
    /// </summary>
    [RelayCommand]
    public void ValidateGraph()
    {
        ValidationMessages.Clear();
        foreach (var message in _validator.Validate(Graph))
        {
            ValidationMessages.Add(message);
        }
    }

    /// <summary>
    /// 以预览模式执行图，并将运行时日志输出写入 <see cref="ExecutionLog"/>。
    /// </summary>
    /// <returns>预览执行停止后结束的任务。</returns>
    [RelayCommand]
    public async Task RunGraphPreviewAsync()
    {
        if (IsPreviewRunning)
        {
            return;
        }

        ValidateGraph();
        if (ValidationMessages.Any(message => message.Code is "MissingStart" or "MultipleStarts"
                                              || message.Severity == FrontedNodeGraphValidationSeverity.Error))
        {
            ExecutionLog.Add(new FrontedGraphExecutionLogItem { Level = FrontedGraphExecutionLogLevel.Warning, Message = _localize("Designer.Graph.Preview.ValidationBlocked", "Preview blocked by graph validation.") });
            return;
        }

        _previewCancellation = new CancellationTokenSource();
        IsPreviewRunning = true;
        try
        {
            var animationContext = _createAnimationContext?.Invoke();
            if (_animationRuntime is not null && animationContext is null)
            {
                ExecutionLog.Add(new FrontedGraphExecutionLogItem
                {
                    Level = FrontedGraphExecutionLogLevel.Warning,
                    Message = _localize("Designer.Graph.Preview.NoTargetScope", "No preview target scope available.")
                });
            }

            var graphContext = new FrontedGraphExecutionContext
            {
                BehaviorGuid = animationContext?.SelfBehaviorGuid ?? Guid.Empty,
                CurrentControlDisplayName = animationContext?.SelfDisplayName ?? string.Empty,
                ActionExecutor = _animationRuntime is null || animationContext is null
                    ? null
                    : new AnimationRuntimeGraphActionExecutor(_animationRuntime, animationContext)
            };
            AddMissingEventContextWarning(Graph, graphContext);
            var result = await _runtime.ExecuteAsync(Graph, graphContext, _previewCancellation.Token);
            foreach (var item in result.LogItems)
            {
                ExecutionLog.Add(item);
            }
        }
        finally
        {
            IsPreviewRunning = false;
            _previewCancellation.Dispose();
            _previewCancellation = null;
        }
    }

    /// <summary>
    /// 取消当前正在运行的图预览（如果存在）。
    /// </summary>
    [RelayCommand]
    public void StopPreview() => _previewCancellation?.Cancel();

    /// <summary>
    /// 重置当前预览目标的动画值。
    /// </summary>
    [RelayCommand]
    public void ResetCurrentTarget()
    {
        var context = _createAnimationContext?.Invoke();
        if (_animationRuntime is null || context is null || context.SelfBehaviorGuid == Guid.Empty)
        {
            ExecutionLog.Add(new FrontedGraphExecutionLogItem
            {
                Level = FrontedGraphExecutionLogLevel.Warning,
                Message = _localize("Designer.Graph.Preview.NoTargetScope", "No preview target scope available.")
            });
            return;
        }

        _animationRuntime.ResetTarget(context.SelfBehaviorGuid, context);
        ExecutionLog.Add(new FrontedGraphExecutionLogItem
        {
            Level = FrontedGraphExecutionLogLevel.Information,
            Message = _localize("Designer.Graph.Preview.ResetCurrent", "Reset current preview target.")
        });
    }

    /// <summary>
    /// 重置当前预览作用域中的所有动画值。
    /// </summary>
    [RelayCommand]
    public void ResetAllPreview()
    {
        var context = _createAnimationContext?.Invoke();
        if (_animationRuntime is null || context is null)
        {
            ExecutionLog.Add(new FrontedGraphExecutionLogItem
            {
                Level = FrontedGraphExecutionLogLevel.Warning,
                Message = _localize("Designer.Graph.Preview.NoTargetScope", "No preview target scope available.")
            });
            return;
        }

        _animationRuntime.ResetAll(context);
        ExecutionLog.Add(new FrontedGraphExecutionLogItem
        {
            Level = FrontedGraphExecutionLogLevel.Information,
            Message = _localize("Designer.Graph.Preview.ResetAll", "Reset all preview animation values.")
        });
    }

    /// <summary>
    /// 清除预览执行日志行。
    /// </summary>
    [RelayCommand]
    public void ClearExecutionLog() => ExecutionLog.Clear();

    /// <summary>
    /// 当预览没有事件上下文导致事件 payload 条件无法解析时添加预览警告。
    /// </summary>
    /// <param name="graph">正在预览的图。</param>
    /// <param name="context">运行时执行上下文。</param>
    private void AddMissingEventContextWarning(FrontedNodeGraph graph, FrontedGraphExecutionContext context)
    {
        var missingPath = graph.Nodes
            .Where(node => node.NodeType == "flow.if")
            .Select(node => node.Properties.TryGetValue("Left", out var value)
                ? value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString()
                : null)
            .FirstOrDefault(path => path?.StartsWith("Event.", StringComparison.Ordinal) == true);
        if (missingPath is null || context.EventPayload.Count > 0)
        {
            return;
        }

        ExecutionLog.Add(new FrontedGraphExecutionLogItem
        {
            Level = FrontedGraphExecutionLogLevel.Warning,
            Message = string.Format(
                _localize(
                    "Designer.Graph.Preview.MissingEventContext",
                    "The current preview has no event context, so {0} cannot be resolved."),
                missingPath)
        });
    }

}
