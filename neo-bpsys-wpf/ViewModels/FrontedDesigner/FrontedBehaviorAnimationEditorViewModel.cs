using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Services.FrontedDesigner;
using neo_bpsys_wpf.ViewModels.FrontedDesigner.GraphEditor;
using System.Collections.ObjectModel;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Windows;

namespace neo_bpsys_wpf.ViewModels.FrontedDesigner;

public sealed partial class FrontedBehaviorAnimationEditorViewModel : ObservableObject
{
    private const int MaxPreviewLogItems = 300;
    private const int MinimumLoopPreviewIntervalMs = 16;
    private readonly FrontedBehavior _behavior;
    private readonly IFrontedNodeGraphRuntime _runtime;
    private readonly IFrontedAnimationRuntime? _animationRuntime;
    private readonly FrontedDesignerPreviewAnimationScope? _previewAnimationScope;
    private readonly Func<string, string, string> _localize;
    private readonly Func<Task<bool>>? _saveAsync;
    private CancellationTokenSource? _loopPreviewCancellation;
    private TaskCompletionSource? _loopPreviewStopped;

    public FrontedBehaviorAnimationEditorViewModel(
        FrontedBehavior behavior,
        Func<string, string, string> localize,
        FrontedNodeCatalog? catalog = null,
        FrontedNodeGraphValidator? validator = null,
        IFrontedNodeGraphRuntime? runtime = null,
        IFrontedAnimationRuntime? animationRuntime = null,
        FrontedDesignerPreviewAnimationScope? previewAnimationScope = null,
        Action? markDirty = null,
        IReadOnlyList<FrontedNodeTargetOptionViewModel>? targetOptions = null,
        Func<Task<bool>>? saveAsync = null,
        FrontedBehaviorEventCatalog? eventCatalog = null,
        Action? captureUndoSnapshot = null)
    {
        _behavior = behavior;
        _runtime = runtime ?? new FrontedNodeGraphRuntime(catalog, validator);
        _animationRuntime = animationRuntime;
        _previewAnimationScope = previewAnimationScope;
        _localize = localize;
        _saveAsync = saveAsync;
        eventCatalog ??= new FrontedBehaviorEventCatalog();
        Title = behavior.Name;
        IsLoop = behavior.Kind == FrontedBehaviorKind.Loop;
        IsTransition = behavior.Kind == FrontedBehaviorKind.Transition;
        Stages = behavior.Kind switch
        {
            FrontedBehaviorKind.Loop =>
            [
                Stage(localize("Designer.Behaviors.StartAnimation", "Start animation"), behavior.StartGraph, catalog, validator, _runtime, animationRuntime, previewAnimationScope, markDirty, localize, targetOptions, BuildEventFields(eventCatalog, behavior.StartTrigger?.EventType, "Event.", localize), captureUndoSnapshot),
                Stage(localize("Designer.Behaviors.LoopAnimation", "Loop animation"), behavior.LoopGraph, catalog, validator, _runtime, animationRuntime, previewAnimationScope, markDirty, localize, targetOptions, BuildEventFields(eventCatalog, behavior.StartTrigger?.EventType, "Event.", localize), captureUndoSnapshot),
                Stage(localize("Designer.Behaviors.StopAnimation", "Stop animation"), behavior.StopGraph, catalog, validator, _runtime, animationRuntime, previewAnimationScope, markDirty, localize, targetOptions, BuildLoopStopFields(eventCatalog, behavior, localize), captureUndoSnapshot)
            ],
            FrontedBehaviorKind.Transition =>
            [
                Stage(localize("Designer.Behaviors.ExitGraph", "Exit animation"), behavior.ExitGraph, catalog, validator, _runtime, animationRuntime, previewAnimationScope, markDirty, localize, targetOptions, BuildEventFields(eventCatalog, behavior.TransitionTrigger?.EventType, "Event.", localize), captureUndoSnapshot),
                Stage(localize("Designer.Behaviors.EnterGraph", "Enter animation"), behavior.EnterGraph, catalog, validator, _runtime, animationRuntime, previewAnimationScope, markDirty, localize, targetOptions, BuildEventFields(eventCatalog, behavior.TransitionTrigger?.EventType, "Event.", localize), captureUndoSnapshot)
            ],
            _ => [Stage(localize("Designer.Behaviors.Animation", "Animation"), behavior.Graph, catalog, validator, _runtime, animationRuntime, previewAnimationScope, markDirty, localize, targetOptions, BuildEventFields(eventCatalog, behavior.Trigger?.EventType, "Event.", localize), captureUndoSnapshot)]
        };

        // 将每个阶段图编辑器的保存动作接到当前动画编辑器的 SaveAllAsync。
        foreach (var stage in Stages)
        {
            var vm = this;
            stage.GraphEditor.SetSaveAction(vm.SaveAllAsync);
        }

        PreviewStartCommand = new AsyncRelayCommand(PreviewStartAsync, () => IsLoop && !IsLoopPreviewRunning);
        PreviewLoopOnceCommand = new AsyncRelayCommand(PreviewLoopOnceAsync, () => IsLoop && !IsLoopPreviewRunning);
        StartLoopPreviewCommand = new AsyncRelayCommand(StartLoopPreviewAsync, () => IsLoop && !IsLoopPreviewRunning);
        StopLoopPreviewCommand = new AsyncRelayCommand(StopLoopPreviewAsync, () => IsLoop);
        PreviewStopCommand = new AsyncRelayCommand(PreviewStopAsync, () => IsLoop);
        ResetCommand = new RelayCommand(Reset);
    }

    public string Title { get; }
    public bool IsLoop { get; }
    public bool IsTransition { get; }
    public IReadOnlyList<FrontedBehaviorAnimationStageViewModel> Stages { get; }

    /// <summary>是否有任何 stage 包含未保存的更改</summary>
    public bool HasUnsavedChanges => Stages.Any(s => s.GraphEditor.IsDirty);

    public IAsyncRelayCommand PreviewStartCommand { get; }
    public IAsyncRelayCommand PreviewLoopOnceCommand { get; }
    public IAsyncRelayCommand StartLoopPreviewCommand { get; }
    public IAsyncRelayCommand StopLoopPreviewCommand { get; }
    public IAsyncRelayCommand PreviewStopCommand { get; }
    public IRelayCommand ResetCommand { get; }

    /// <summary>异步保存所有 stage 的更改到行为文档。</summary>
    /// <returns>如果保存成功返回 <c>true</c>，否则返回 <c>false</c>。</returns>
    public async Task<bool> SaveAllAsync()
    {
        if (_saveAsync is not null)
        {
            try
            {
                var saved = await _saveAsync().ConfigureAwait(false);
                if (!saved)
                {
                    return false;
                }
            }
            catch (Exception)
            {
                // 保存失败，保留已修改状态。
                return false;
            }
        }

        // 保存成功后清除所有阶段的已修改状态。
        // 设置 IsDirty 会触发 RelayCommand.NotifyCanExecuteChanged，因此必须调度到 UI 线程。
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            ClearStageDirtyStates();
            return true;
        }

        if (dispatcher.CheckAccess())
        {
            ClearStageDirtyStates();
        }
        else
        {
            await dispatcher.InvokeAsync(ClearStageDirtyStates);
        }

        return true;
    }

    private void ClearStageDirtyStates()
    {
        foreach (var stage in Stages)
        {
            if (stage.GraphEditor.IsDirty)
            {
                stage.GraphEditor.DiscardLocalDirtyState();
            }
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>
    /// 停止预览活动、重置预览动画状态，并丢弃编辑器本地的已修改状态。
    /// </summary>
    public void DiscardAll()
    {
        StopPreviewWithoutRunningStopGraph();
        ResetPreviewIfSafe();

        foreach (var stage in Stages)
        {
            stage.GraphEditor.DiscardLocalDirtyState();
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PreviewStartCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviewLoopOnceCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartLoopPreviewCommand))]
    public partial bool IsLoopPreviewRunning { get; set; }

    private static FrontedBehaviorAnimationStageViewModel Stage(
        string name,
        FrontedNodeGraph graph,
        FrontedNodeCatalog? catalog,
        FrontedNodeGraphValidator? validator,
        IFrontedNodeGraphRuntime? runtime,
        IFrontedAnimationRuntime? animationRuntime,
        FrontedDesignerPreviewAnimationScope? previewAnimationScope,
        Action? markDirty,
        Func<string, string, string> localize,
        IReadOnlyList<FrontedNodeTargetOptionViewModel>? targetOptions,
        IReadOnlyList<FrontedGraphConditionFieldOptionViewModel> conditionFieldOptions,
        Action? captureUndoSnapshot)
    {
        var editorVm = new FrontedNodeGraphEditorViewModel(
            graph,
            catalog,
            validator,
            runtime,
            animationRuntime,
            previewAnimationScope is null ? null : () => previewAnimationScope.CreateContext(),
            markDirty,
            localize,
            captureUndoSnapshot: captureUndoSnapshot,
            targetOptions: targetOptions,
            conditionFieldOptions: conditionFieldOptions)
        {
            PreviewRoot = previewAnimationScope?.Root
        };
        return new(name, graph, editorVm);
    }

    private static IReadOnlyList<FrontedGraphConditionFieldOptionViewModel> BuildEventFields(
        FrontedBehaviorEventCatalog eventCatalog,
        string? eventType,
        string prefix,
        Func<string, string, string> localize)
    {
        var descriptor = string.IsNullOrWhiteSpace(eventType) ? null : eventCatalog.Find(eventType);
        return descriptor?.PayloadFields
            .Select(field => CreateConditionField(field, prefix, eventType, localize))
            .ToArray() ?? [];
    }

    private static IReadOnlyList<FrontedGraphConditionFieldOptionViewModel> BuildLoopStopFields(
        FrontedBehaviorEventCatalog eventCatalog,
        FrontedBehavior behavior,
        Func<string, string, string> localize)
    {
        var fields = behavior.StopTriggers
            .SelectMany(trigger => BuildEventFields(eventCatalog, trigger.EventType, "Event.", localize)
                .Select(field => field with { DisplayText = $"{field.DisplayText} [{trigger.EventType}]", EventType = trigger.EventType }))
            .GroupBy(field => field.ValuePath, StringComparer.Ordinal)
            .Select(group =>
            {
                var first = group.First();
                var eventTypes = group.Select(field => field.EventType).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal);
                return first with { DisplayText = $"{first.LocalizedDisplayName} ({first.ValuePath}) [{string.Join(", ", eventTypes)}]" };
            })
            .ToList();
        fields.AddRange(BuildEventFields(eventCatalog, behavior.StartTrigger?.EventType, "StartEvent.", localize));
        return fields;
    }

    private static FrontedGraphConditionFieldOptionViewModel CreateConditionField(
        FrontedBehaviorEventPayloadField field,
        string prefix,
        string? eventType,
        Func<string, string, string> localize)
    {
        var suffix = field.Path.StartsWith("Event.", StringComparison.Ordinal)
            ? field.Path["Event.".Length..]
            : field.Path;
        var path = prefix + suffix;
        var localizedDisplayName = localize(field.DisplayNameKey, suffix);
        return new FrontedGraphConditionFieldOptionViewModel(
            path,
            $"{localizedDisplayName} ({path})",
            localize(field.DescriptionKey, path),
            field.TypeName,
            field.EnumValues,
            eventType,
            localizedDisplayName);
    }

    private Task PreviewStartAsync() =>
        ExecuteGraphAsync(_behavior.StartGraph, TestContextCancellationToken());

    private Task PreviewLoopOnceAsync() =>
        ExecuteGraphAsync(_behavior.LoopGraph, TestContextCancellationToken());

    private async Task StartLoopPreviewAsync()
    {
        if (IsLoopPreviewRunning)
        {
            if (_behavior.LoopPolicy?.ReentryPolicy == FrontedReentryPolicy.InterruptPrevious)
            {
                _loopPreviewCancellation?.Cancel();
            }
            else
            {
                return;
            }
        }

        var loopPreviewCancellation = new CancellationTokenSource();
        var loopPreviewStopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _loopPreviewCancellation = loopPreviewCancellation;
        _loopPreviewStopped = loopPreviewStopped;
        IsLoopPreviewRunning = true;
        try
        {
            var token = loopPreviewCancellation.Token;
            await ExecuteGraphAsync(_behavior.StartGraph, token);
            var policy = _behavior.LoopPolicy ?? new FrontedLoopPolicy();
            if (policy.AutoReverse)
            {
                AddExecutionLog(Stages.FirstOrDefault()?.GraphEditor, new FrontedGraphExecutionLogItem
                {
                    Level = FrontedGraphExecutionLogLevel.Warning,
                    Message = _localize("Designer.Graph.Preview.AutoReverseStoredOnly", "AutoReverse is saved, but reverse graph playback is not simulated during preview.")
                });
            }

            var repeatCount = policy.RepeatCount;
            var iteration = 0;
            while (!token.IsCancellationRequested && (repeatCount < 0 || iteration < repeatCount))
            {
                await ExecuteGraphAsync(_behavior.LoopGraph, token);
                iteration++;
                if (!token.IsCancellationRequested)
                {
                    await Task.Delay(Math.Max(policy.IntervalMs, MinimumLoopPreviewIntervalMs), token);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (ReferenceEquals(_loopPreviewCancellation, loopPreviewCancellation))
            {
                IsLoopPreviewRunning = false;
                _loopPreviewCancellation = null;
            }

            loopPreviewCancellation.Dispose();
            loopPreviewStopped.TrySetResult();
            if (ReferenceEquals(_loopPreviewStopped, loopPreviewStopped))
            {
                _loopPreviewStopped = null;
            }
        }
    }

    private async Task StopLoopPreviewAsync()
    {
        _loopPreviewCancellation?.Cancel();
        if (_loopPreviewStopped is not null)
        {
            await _loopPreviewStopped.Task;
        }
        var policy = _behavior.LoopPolicy ?? new FrontedLoopPolicy();
        var suppressReset = policy.StopMode == FrontedLoopStopMode.HoldCurrentState;
        if (policy.StopMode == FrontedLoopStopMode.RunStopGraph)
        {
            var stopResult = await ExecuteGraphAsync(_behavior.StopGraph, TestContextCancellationToken());
            suppressReset = stopResult.Status == FrontedGraphExecutionStatus.Success;
        }

        if (policy.ResetOnStop && !suppressReset)
        {
            Reset();
        }
    }

    private Task PreviewStopAsync() =>
        ExecuteGraphAsync(_behavior.StopGraph, TestContextCancellationToken());

    private void Reset()
    {
        var context = _previewAnimationScope?.CreateContext();
        if (_animationRuntime is not null && context is not null)
        {
            _animationRuntime.ResetAll(context);
        }
    }

    private void StopPreviewWithoutRunningStopGraph()
    {
        var cancellation = _loopPreviewCancellation;
        _loopPreviewCancellation = null;
        cancellation?.Cancel();
        cancellation?.Dispose();
        IsLoopPreviewRunning = false;
    }

    private void ResetPreviewIfSafe()
    {
        try
        {
            Reset();
        }
        catch (InvalidOperationException)
        {
            // 编辑器关闭时预览可视树可能已经正在卸载。
        }
    }

    private async Task<FrontedGraphExecutionResult> ExecuteGraphAsync(FrontedNodeGraph graph, CancellationToken cancellationToken)
    {
        var animationContext = _previewAnimationScope?.CreateContext();
        var graphContext = new FrontedGraphExecutionContext
        {
            BehaviorGuid = animationContext?.SelfBehaviorGuid ?? Guid.Empty,
            CurrentControlDisplayName = animationContext?.SelfDisplayName ?? string.Empty,
            ActionExecutor = _animationRuntime is null || animationContext is null
                ? null
                : new AnimationRuntimeGraphActionExecutor(_animationRuntime, animationContext)
        };
        var missingPath = graph.Nodes
            .Where(node => node.NodeType == "flow.if")
            .Select(node => node.Properties.TryGetValue("Left", out var value)
                ? value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString()
                : null)
            .FirstOrDefault(path => path?.StartsWith("Event.", StringComparison.Ordinal) == true);
        var stage = Stages.FirstOrDefault(item => ReferenceEquals(item.Graph, graph));
        if (missingPath is not null && graphContext.EventPayload.Count == 0)
        {
            AddExecutionLog(stage?.GraphEditor, new FrontedGraphExecutionLogItem
            {
                Level = FrontedGraphExecutionLogLevel.Warning,
                Message = string.Format(
                    _localize(
                        "Designer.Graph.Preview.MissingEventContext",
                        "The current preview has no event context, so {0} cannot be resolved."),
                    missingPath)
            });
        }
        var result = await _runtime.ExecuteAsync(graph, graphContext, cancellationToken);
        if (stage is null)
        {
            return result;
        }

        if (_animationRuntime is not null && animationContext is null)
        {
            AddExecutionLog(stage.GraphEditor, new FrontedGraphExecutionLogItem
            {
                Level = FrontedGraphExecutionLogLevel.Warning,
                Message = _localize("Designer.Graph.Preview.NoTargetScope", "No preview target scope available.")
            });
        }

        foreach (var item in result.LogItems)
        {
            AddExecutionLog(stage.GraphEditor, item);
        }

        return result;
    }

    private static void AddExecutionLog(FrontedNodeGraphEditorViewModel? graphEditor, FrontedGraphExecutionLogItem item)
    {
        if (graphEditor is null)
        {
            return;
        }

        graphEditor.ExecutionLog.Add(item);
        while (graphEditor.ExecutionLog.Count > MaxPreviewLogItems)
        {
            graphEditor.ExecutionLog.RemoveAt(0);
        }
    }

    private static CancellationToken TestContextCancellationToken() =>
        CancellationToken.None;

    private sealed class AnimationRuntimeGraphActionExecutor(
        IFrontedAnimationRuntime animationRuntime,
        FrontedAnimationExecutionContext animationContext) : IFrontedGraphActionExecutor
    {
        public Task ExecuteAsync(FrontedGraphActionRequest request, CancellationToken cancellationToken) =>
            animationRuntime.ExecuteAsync(request, animationContext, cancellationToken);
    }
}
