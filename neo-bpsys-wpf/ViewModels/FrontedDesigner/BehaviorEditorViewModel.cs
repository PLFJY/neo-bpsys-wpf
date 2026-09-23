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

public sealed partial class BehaviorEditorViewModel : ObservableObject
{
    private readonly Action _markDirty;
    private readonly Action _captureUndoSnapshot;
    private readonly Func<string, string, string> _localize;
    private readonly string _graphPlaceholder;
    private readonly IReadOnlyList<BehaviorEventOptionViewModel> _eventBusEventOptions;

    /// <summary>
    /// 使用 3.0/3.1 版本的构造函数签名初始化行为编辑器。
    /// </summary>
    /// <param name="model">行为模型。</param>
    /// <param name="eventOptions">所有触发位置共用的事件选项。</param>
    /// <param name="operatorOptions">过滤运算符选项。</param>
    /// <param name="stopModeOptions">循环停止模式选项。</param>
    /// <param name="reentryPolicyOptions">重入策略选项。</param>
    /// <param name="graphPlaceholder">节点图占位文本。</param>
    /// <param name="markDirty">标记行为文档已修改的回调。</param>
    /// <param name="captureUndoSnapshot">捕获撤销快照的回调。</param>
    /// <param name="localize">本地化函数。</param>
    /// <param name="nodeCatalog">节点目录。</param>
    /// <param name="graphValidator">节点图校验器。</param>
    /// <param name="graphRuntime">节点图运行时。</param>
    /// <param name="animationRuntime">动画运行时。</param>
    /// <param name="previewAnimationScope">预览动画作用域。</param>
    /// <param name="openAnimationEditor">打开动画编辑器的回调。</param>
    /// <param name="createTargetOptions">创建动画目标选项的回调。</param>
    /// <param name="saveBehaviorAsync">保存行为文档的回调。</param>
    public BehaviorEditorViewModel(
        FrontedBehavior model,
        IReadOnlyList<BehaviorEventOptionViewModel> eventOptions,
        IReadOnlyList<BehaviorOptionViewModel> operatorOptions,
        IReadOnlyList<BehaviorOptionViewModel> stopModeOptions,
        IReadOnlyList<BehaviorOptionViewModel> reentryPolicyOptions,
        string graphPlaceholder,
        Action markDirty,
        Action captureUndoSnapshot,
        Func<string, string, string> localize,
        FrontedNodeCatalog nodeCatalog,
        FrontedNodeGraphValidator graphValidator,
        IFrontedNodeGraphRuntime graphRuntime,
        IFrontedAnimationRuntime? animationRuntime,
        FrontedDesignerPreviewAnimationScope? previewAnimationScope,
        Action<FrontedBehaviorAnimationEditorViewModel> openAnimationEditor,
        Func<IReadOnlyList<FrontedNodeTargetOptionViewModel>>? createTargetOptions = null,
        Func<Task<bool>>? saveBehaviorAsync = null)
        : this(
            model,
            eventOptions,
            eventOptions,
            operatorOptions,
            stopModeOptions,
            reentryPolicyOptions,
            graphPlaceholder,
            markDirty,
            captureUndoSnapshot,
            localize,
            nodeCatalog,
            graphValidator,
            graphRuntime,
            animationRuntime,
            previewAnimationScope,
            openAnimationEditor,
            createTargetOptions,
            saveBehaviorAsync,
            eventCatalog: null)
    {
    }

    public BehaviorEditorViewModel(
        FrontedBehavior model,
        IReadOnlyList<BehaviorEventOptionViewModel> eventBusEventOptions,
        IReadOnlyList<BehaviorEventOptionViewModel> transitionEventOptions,
        IReadOnlyList<BehaviorOptionViewModel> operatorOptions,
        IReadOnlyList<BehaviorOptionViewModel> stopModeOptions,
        IReadOnlyList<BehaviorOptionViewModel> reentryPolicyOptions,
        string graphPlaceholder,
        Action markDirty,
        Action captureUndoSnapshot,
        Func<string, string, string> localize,
        FrontedNodeCatalog nodeCatalog,
        FrontedNodeGraphValidator graphValidator,
        IFrontedNodeGraphRuntime graphRuntime,
        IFrontedAnimationRuntime? animationRuntime,
        FrontedDesignerPreviewAnimationScope? previewAnimationScope,
        Action<FrontedBehaviorAnimationEditorViewModel> openAnimationEditor,
        Func<IReadOnlyList<FrontedNodeTargetOptionViewModel>>? createTargetOptions = null,
        Func<Task<bool>>? saveBehaviorAsync = null,
        FrontedBehaviorEventCatalog? eventCatalog = null)
    {
        Model = model;
        _markDirty = markDirty;
        _captureUndoSnapshot = captureUndoSnapshot;
        _localize = localize;
        _graphPlaceholder = graphPlaceholder;
        _eventBusEventOptions = eventBusEventOptions;

        if (Model.Kind == FrontedBehaviorKind.OneShot)
        {
            Model.Trigger ??= new TriggerDescriptor { EventType = eventBusEventOptions.FirstOrDefault()?.EventType ?? string.Empty };
        }
        else if (Model.Kind == FrontedBehaviorKind.Loop)
        {
            Model.StartTrigger ??= new TriggerDescriptor { EventType = eventBusEventOptions.FirstOrDefault()?.EventType ?? string.Empty };
            if (Model.StopTriggers.Count == 0)
            {
                Model.StopTriggers.Add(new TriggerDescriptor { EventType = eventBusEventOptions.FirstOrDefault()?.EventType ?? string.Empty });
            }
            Model.LoopPolicy ??= new FrontedLoopPolicy();
        }
        else if (Model.Kind == FrontedBehaviorKind.Transition)
        {
            Model.TransitionTrigger ??= new TriggerDescriptor
            {
                EventType = transitionEventOptions.FirstOrDefault(option => option.EventType == "Selection.CharacterPick")?.EventType
                            ?? transitionEventOptions.FirstOrDefault()?.EventType
                            ?? string.Empty
            };
        }

        Trigger = new TriggerDescriptorEditorViewModel(Model.Trigger, eventBusEventOptions, operatorOptions, markDirty, localize, captureUndoSnapshot);
        StartTrigger = new TriggerDescriptorEditorViewModel(Model.StartTrigger, eventBusEventOptions, operatorOptions, markDirty, localize, captureUndoSnapshot);
        foreach (var trigger in Model.StopTriggers)
        {
            StopTriggers.Add(new TriggerDescriptorEditorViewModel(trigger, eventBusEventOptions, operatorOptions, markDirty, localize, captureUndoSnapshot));
        }

        TransitionTrigger = new TriggerDescriptorEditorViewModel(Model.TransitionTrigger, transitionEventOptions, operatorOptions, markDirty, localize, captureUndoSnapshot);
        LoopPolicy = new LoopPolicyEditorViewModel(Model.LoopPolicy, stopModeOptions, reentryPolicyOptions, markDirty, captureUndoSnapshot);
        ReentryPolicyOptions = reentryPolicyOptions;
        OpenAnimationEditorCommand = new RelayCommand(() => openAnimationEditor(
            new FrontedBehaviorAnimationEditorViewModel(
                Model,
                localize,
                nodeCatalog,
                graphValidator,
                graphRuntime,
                animationRuntime,
                previewAnimationScope,
                markDirty,
                createTargetOptions?.Invoke(),
                saveAsync: saveBehaviorAsync,
                eventCatalog: eventCatalog,
                captureUndoSnapshot: captureUndoSnapshot)));
    }

    public FrontedBehavior Model { get; }

    public TriggerDescriptorEditorViewModel Trigger { get; }

    public TriggerDescriptorEditorViewModel StartTrigger { get; }

    public ObservableCollection<TriggerDescriptorEditorViewModel> StopTriggers { get; } = [];

    public TriggerDescriptorEditorViewModel TransitionTrigger { get; }

    public LoopPolicyEditorViewModel LoopPolicy { get; }

    public IReadOnlyList<BehaviorOptionViewModel> ReentryPolicyOptions { get; }

    public IRelayCommand OpenAnimationEditorCommand { get; }

    public string Name
    {
        get => Model.Name;
        set
        {
            if (string.Equals(Model.Name, value, StringComparison.Ordinal))
            {
                return;
            }

            _captureUndoSnapshot();
            if (SetProperty(Model.Name, value, Model, static (model, next) => model.Name = next))
            {
                _markDirty();
            }
        }
    }

    public bool Enabled
    {
        get => Model.Enabled;
        set
        {
            if (Model.Enabled == value)
            {
                return;
            }

            _captureUndoSnapshot();
            if (SetProperty(Model.Enabled, value, Model, static (model, next) => model.Enabled = next))
            {
                _markDirty();
            }
        }
    }

    public FrontedBehaviorKind Kind => Model.Kind;

    public bool IsOneShot => Model.Kind == FrontedBehaviorKind.OneShot;

    public bool IsLoop => Model.Kind == FrontedBehaviorKind.Loop;

    public bool IsTransition => Model.Kind == FrontedBehaviorKind.Transition;

    public string KindDisplay => Model.Kind switch
    {
        FrontedBehaviorKind.Loop => _localize("Designer.Behaviors.Loop", "Loop"),
        FrontedBehaviorKind.Transition => _localize("Designer.Behaviors.Transition", "Transition"),
        _ => _localize("Designer.Behaviors.OneShot", "OneShot")
    };

    public string TriggerSummary => Model.Kind switch
    {
        FrontedBehaviorKind.Loop => $"{StartTrigger.EventType} / {string.Join(" OR ", StopTriggers.Select(trigger => trigger.EventType))}",
        FrontedBehaviorKind.Transition => TransitionTrigger.EventType,
        _ => Trigger.EventType
    };

    public int FilterCount => Model.Kind switch
    {
        FrontedBehaviorKind.Loop => StartTrigger.Filters.Count + StopTriggers.Sum(trigger => trigger.Filters.Count),
        FrontedBehaviorKind.Transition => TransitionTrigger.Filters.Count,
        _ => Trigger.Filters.Count
    };

    public string GraphSummary => Model.Kind switch
    {
        FrontedBehaviorKind.Loop =>
            $"{_localize("Designer.Behaviors.StartGraph", "StartGraph")}: {GraphStats(Model.StartGraph)}; "
            + $"{_localize("Designer.Behaviors.LoopGraph", "LoopGraph")}: {GraphStats(Model.LoopGraph)}; "
            + $"{_localize("Designer.Behaviors.StopGraph", "StopGraph")}: {GraphStats(Model.StopGraph)}",
        FrontedBehaviorKind.Transition =>
            $"{_localize("Designer.Behaviors.ExitGraph", "ExitGraph")}: {GraphStats(Model.ExitGraph)}; "
            + $"{_localize("Designer.Behaviors.EnterGraph", "EnterGraph")}: {GraphStats(Model.EnterGraph)}",
        _ => GraphStats(Model.Graph)
    };

    public FrontedReentryPolicy ReentryPolicy
    {
        get => Model.ReentryPolicy;
        set
        {
            if (Model.ReentryPolicy == value)
            {
                return;
            }

            _captureUndoSnapshot();
            if (SetProperty(Model.ReentryPolicy, value, Model, static (model, next) => model.ReentryPolicy = next))
            {
                _markDirty();
            }
        }
    }

    public string GraphPlaceholder => _graphPlaceholder;

    /// <summary>
    /// 刷新所有本地化显示字符串以支持热切换语言。
    /// </summary>
    public void RefreshLocalization()
    {
        Trigger.RefreshLocalization();
        StartTrigger.RefreshLocalization();
        foreach (var trigger in StopTriggers)
        {
            trigger.RefreshLocalization();
        }

        TransitionTrigger.RefreshLocalization();
        OnPropertyChanged(nameof(KindDisplay));
        OnPropertyChanged(nameof(GraphSummary));
        OnPropertyChanged(nameof(GraphPlaceholder));
    }

    private static string GraphStats(FrontedNodeGraph? graph)
    {
        return graph is null
            ? "0 nodes / 0 links"
            : $"{graph.Nodes.Count} nodes / {graph.Connections.Count} links";
    }

    [RelayCommand]
    private void AddStopTrigger()
    {
        _captureUndoSnapshot();
        var trigger = new TriggerDescriptor { EventType = _eventBusEventOptions.FirstOrDefault()?.EventType ?? string.Empty };
        Model.StopTriggers.Add(trigger);
        StopTriggers.Add(new TriggerDescriptorEditorViewModel(
            trigger,
            _eventBusEventOptions,
            StartTrigger.OperatorOptions,
            _markDirty,
            _localize,
            _captureUndoSnapshot));
        _markDirty();
        OnPropertyChanged(nameof(TriggerSummary));
    }

    [RelayCommand]
    private void RemoveStopTrigger(TriggerDescriptorEditorViewModel? trigger)
    {
        if (trigger is null || StopTriggers.Count <= 1)
        {
            return;
        }

        var index = StopTriggers.IndexOf(trigger);
        if (index < 0)
        {
            return;
        }

        _captureUndoSnapshot();
        StopTriggers.RemoveAt(index);
        Model.StopTriggers.RemoveAt(index);
        _markDirty();
        OnPropertyChanged(nameof(TriggerSummary));
        OnPropertyChanged(nameof(FilterCount));
    }

    [RelayCommand]
    private void DuplicateStopTrigger(TriggerDescriptorEditorViewModel? trigger)
    {
        if (trigger is null)
        {
            return;
        }

        var index = StopTriggers.IndexOf(trigger);
        if (index < 0)
        {
            return;
        }

        _captureUndoSnapshot();
        var clone = new TriggerDescriptor
        {
            EventType = trigger.Model.EventType,
            Filters = trigger.Model.Filters
                .Select(filter => new TriggerFilter
                {
                    Left = filter.Left,
                    Operator = filter.Operator,
                    Right = filter.Right,
                    RightValueKind = filter.RightValueKind
                })
                .ToList()
        };
        Model.StopTriggers.Insert(index + 1, clone);
        StopTriggers.Insert(index + 1, new TriggerDescriptorEditorViewModel(
            clone,
            _eventBusEventOptions,
            StartTrigger.OperatorOptions,
            _markDirty,
            _localize,
            _captureUndoSnapshot));
        _markDirty();
        OnPropertyChanged(nameof(TriggerSummary));
        OnPropertyChanged(nameof(FilterCount));
    }
}
