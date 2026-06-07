using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using System.Collections.ObjectModel;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace neo_bpsys_wpf.ViewModels.FrontedDesigner;

public sealed partial class BehaviorPanelViewModel : ViewModelBase
{
    private readonly IFrontedDesignerLocalizationService _localizationService;
    private readonly Action _markLayoutDirty;
    private readonly Action _markBehaviorsDirty;
    private readonly JsonSerializerOptions _cloneJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        MaxDepth = FrontedLayoutLimits.MaxJsonDepth
    };

    private ControlBehaviorSet? _currentSet;

    public BehaviorPanelViewModel()
        : this(
            new FrontedDesignerLocalizationService(),
            new FrontedBehaviorEventCatalog(),
            static () => { },
            static () => { })
    {
    }

    public BehaviorPanelViewModel(
        IFrontedDesignerLocalizationService localizationService,
        FrontedBehaviorEventCatalog eventCatalog,
        Action markLayoutDirty,
        Action markBehaviorsDirty)
    {
        _localizationService = localizationService;
        _markLayoutDirty = markLayoutDirty;
        _markBehaviorsDirty = markBehaviorsDirty;
        EventOptions = [.. eventCatalog.Events.Select(CreateEventOption)];
        OperatorOptions = CreateEnumOptions<TriggerFilterOperator>("Designer.Behaviors.Operator");
        RightValueKindOptions = CreateEnumOptions<TriggerFilterValueKind>("Designer.Behaviors.RightValueKind");
        StopModeOptions = CreateEnumOptions<FrontedLoopStopMode>("Designer.Behaviors.StopMode");
        ReentryPolicyOptions = CreateEnumOptions<FrontedReentryPolicy>("Designer.Behaviors.ReentryPolicy");
    }

    public ObservableCollection<BehaviorEditorViewModel> Behaviors { get; } = [];

    public IReadOnlyList<BehaviorOptionViewModel> EventOptions { get; }

    public IReadOnlyList<BehaviorOptionViewModel> OperatorOptions { get; }

    public IReadOnlyList<BehaviorOptionViewModel> RightValueKindOptions { get; }

    public IReadOnlyList<BehaviorOptionViewModel> StopModeOptions { get; }

    public IReadOnlyList<BehaviorOptionViewModel> ReentryPolicyOptions { get; }

    public FrontedBehaviorDocument CurrentDocument { get; private set; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedControl))]
    [NotifyPropertyChangedFor(nameof(EmptyText))]
    private FrontedControlDesignItem? _selectedControl;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedBehavior))]
    private BehaviorEditorViewModel? _selectedBehavior;

    public bool HasSelectedControl => SelectedControl is not null;

    public bool HasSelectedBehavior => SelectedBehavior is not null;

    public bool HasBehaviors => Behaviors.Count > 0;

    public string EmptyText => HasSelectedControl
        ? Localize("Designer.Behaviors.Empty", "No behaviors yet.")
        : Localize("NoSelectedControl", "No selected control.");

    public string GraphPlaceholder => Localize(
        "Designer.Behaviors.GraphPlaceholder",
        "Node graph editor will be available in Phase 3");

    public void SetDocument(FrontedBehaviorDocument document)
    {
        CurrentDocument = document;
        RefreshForSelectedControl();
    }

    public void SetSelectedControl(FrontedControlDesignItem? selectedControl)
    {
        SelectedControl = selectedControl;
        RefreshForSelectedControl();
    }

    public bool RemoveBehaviors(Guid behaviorGuid)
    {
        var removed = CurrentDocument.RemoveSet(behaviorGuid);
        if (!removed)
        {
            return false;
        }

        if (SelectedControl?.Config.BehaviorGuid == behaviorGuid)
        {
            RefreshForSelectedControl();
        }

        MarkBehaviorsDirty();
        return true;
    }

    [RelayCommand]
    public void AddOneShotBehavior()
    {
        var set = GetOrCreateSelectedSet();
        if (set is null)
        {
            return;
        }

        var behavior = new FrontedBehavior
        {
            Kind = FrontedBehaviorKind.OneShot,
            Name = Localize("Designer.Behaviors.NewOneShot", "New OneShot Behavior"),
            Enabled = true,
            Trigger = new TriggerDescriptor { EventType = "ManualTrigger" },
            Graph = new FrontedNodeGraph()
        };
        set.Behaviors.Add(behavior);
        RefreshFromSet(set, behavior);
        MarkBehaviorsDirty();
    }

    [RelayCommand]
    public void AddLoopBehavior()
    {
        var set = GetOrCreateSelectedSet();
        if (set is null)
        {
            return;
        }

        var behavior = new FrontedBehavior
        {
            Kind = FrontedBehaviorKind.Loop,
            Name = Localize("Designer.Behaviors.NewLoop", "New Loop Behavior"),
            Enabled = true,
            StartTrigger = new TriggerDescriptor { EventType = "ManualTrigger" },
            EndTrigger = new TriggerDescriptor { EventType = "ManualTrigger" },
            StartGraph = new FrontedNodeGraph(),
            LoopGraph = new FrontedNodeGraph(),
            StopGraph = new FrontedNodeGraph(),
            LoopPolicy = new FrontedLoopPolicy()
        };
        set.Behaviors.Add(behavior);
        RefreshFromSet(set, behavior);
        MarkBehaviorsDirty();
    }

    [RelayCommand]
    public void SelectBehavior(BehaviorEditorViewModel? behavior)
    {
        SelectedBehavior = behavior;
    }

    [RelayCommand]
    public void DeleteBehavior(BehaviorEditorViewModel? behavior)
    {
        if (behavior is null || _currentSet is null)
        {
            return;
        }

        if (!_currentSet.Behaviors.Remove(behavior.Model))
        {
            return;
        }

        if (_currentSet.Behaviors.Count == 0)
        {
            CurrentDocument.RemoveSet(_currentSet.BehaviorGuid);
            _currentSet = null;
            Behaviors.Clear();
            SelectedBehavior = null;
        }
        else
        {
            RefreshFromSet(_currentSet, _currentSet.Behaviors.FirstOrDefault());
        }

        MarkBehaviorsDirty();
    }

    [RelayCommand]
    public void DuplicateBehavior(BehaviorEditorViewModel? behavior)
    {
        if (behavior is null || _currentSet is null)
        {
            return;
        }

        var json = JsonSerializer.Serialize(behavior.Model, _cloneJsonOptions);
        var clone = JsonSerializer.Deserialize<FrontedBehavior>(json, _cloneJsonOptions);
        if (clone is null)
        {
            return;
        }

        clone.BehaviorId = FrontedBehaviorGuidHelper.NewGuid();
        clone.Name = string.Format(
            Localize("Designer.Behaviors.CopyOfFormat", "Copy of {0}"),
            string.IsNullOrWhiteSpace(clone.Name) ? behavior.Name : clone.Name);
        RegenerateGraphIds(clone.Graph);
        RegenerateGraphIds(clone.StartGraph);
        RegenerateGraphIds(clone.LoopGraph);
        RegenerateGraphIds(clone.StopGraph);

        var index = _currentSet.Behaviors.IndexOf(behavior.Model);
        _currentSet.Behaviors.Insert(index + 1, clone);
        RefreshFromSet(_currentSet, clone);
        MarkBehaviorsDirty();
    }

    partial void OnSelectedControlChanged(FrontedControlDesignItem? value)
    {
        OnPropertyChanged(nameof(HasSelectedControl));
        OnPropertyChanged(nameof(EmptyText));
    }

    private ControlBehaviorSet? GetOrCreateSelectedSet()
    {
        if (SelectedControl is null)
        {
            return null;
        }

        if (SelectedControl.Config.BehaviorGuid == Guid.Empty)
        {
            SelectedControl.Config.BehaviorGuid = FrontedBehaviorGuidHelper.NewGuid();
            _markLayoutDirty();
        }

        _currentSet = CurrentDocument.GetOrCreateSet(SelectedControl.Config.BehaviorGuid, SelectedControl.Name);
        _currentSet.DisplayName = SelectedControl.Name;
        return _currentSet;
    }

    private void RefreshForSelectedControl()
    {
        Behaviors.Clear();
        _currentSet = null;
        SelectedBehavior = null;

        if (SelectedControl?.Config.BehaviorGuid is not { } behaviorGuid || behaviorGuid == Guid.Empty)
        {
            OnPropertyChanged(nameof(HasBehaviors));
            OnPropertyChanged(nameof(EmptyText));
            return;
        }

        var set = CurrentDocument.FindSet(behaviorGuid);
        if (set is null)
        {
            OnPropertyChanged(nameof(HasBehaviors));
            OnPropertyChanged(nameof(EmptyText));
            return;
        }

        RefreshFromSet(set, set.Behaviors.FirstOrDefault());
    }

    private void RefreshFromSet(ControlBehaviorSet set, FrontedBehavior? selectedModel)
    {
        _currentSet = set;
        Behaviors.Clear();
        foreach (var behavior in set.Behaviors)
        {
            Behaviors.Add(CreateBehaviorEditor(behavior));
        }

        SelectedBehavior = selectedModel is null
            ? null
            : Behaviors.FirstOrDefault(item => ReferenceEquals(item.Model, selectedModel));
        OnPropertyChanged(nameof(HasBehaviors));
        OnPropertyChanged(nameof(EmptyText));
    }

    private BehaviorEditorViewModel CreateBehaviorEditor(FrontedBehavior behavior)
    {
        return new BehaviorEditorViewModel(
            behavior,
            EventOptions,
            OperatorOptions,
            RightValueKindOptions,
            StopModeOptions,
            ReentryPolicyOptions,
            GraphPlaceholder,
            MarkBehaviorsDirty,
            Localize);
    }

    private BehaviorOptionViewModel CreateEventOption(FrontedBehaviorEventDescriptor descriptor)
    {
        return new BehaviorOptionViewModel(
            descriptor.EventType,
            Localize(descriptor.DisplayNameKey, descriptor.EventType));
    }

    private IReadOnlyList<BehaviorOptionViewModel> CreateEnumOptions<TEnum>(string prefix)
        where TEnum : struct, Enum
    {
        return Enum.GetValues<TEnum>()
            .Select(value =>
            {
                var raw = value.ToString();
                return new BehaviorOptionViewModel(value, Localize($"{prefix}.{raw}", raw));
            })
            .ToArray();
    }

    private void MarkBehaviorsDirty()
    {
        _markBehaviorsDirty();
        OnPropertyChanged(nameof(HasBehaviors));
    }

    private string Localize(string key, string fallback) =>
        _localizationService.GetDesignerText(key, fallback);

    private static void RegenerateGraphIds(FrontedNodeGraph? graph)
    {
        if (graph is null)
        {
            return;
        }

        var nodeIds = new Dictionary<Guid, Guid>();
        foreach (var node in graph.Nodes)
        {
            var oldId = node.NodeId;
            node.NodeId = FrontedBehaviorGuidHelper.NewGuid();
            nodeIds[oldId] = node.NodeId;
        }

        foreach (var connection in graph.Connections)
        {
            connection.ConnectionId = FrontedBehaviorGuidHelper.NewGuid();
            if (nodeIds.TryGetValue(connection.SourceNodeId, out var sourceNodeId))
            {
                connection.SourceNodeId = sourceNodeId;
            }

            if (nodeIds.TryGetValue(connection.TargetNodeId, out var targetNodeId))
            {
                connection.TargetNodeId = targetNodeId;
            }
        }
    }
}

public sealed class BehaviorOptionViewModel(object value, string displayName)
{
    public object Value { get; } = value;

    public string DisplayName { get; } = displayName;
}

public sealed partial class BehaviorEditorViewModel : ObservableObject
{
    private readonly Action _markDirty;
    private readonly Func<string, string, string> _localize;
    private readonly string _graphPlaceholder;

    public BehaviorEditorViewModel(
        FrontedBehavior model,
        IReadOnlyList<BehaviorOptionViewModel> eventOptions,
        IReadOnlyList<BehaviorOptionViewModel> operatorOptions,
        IReadOnlyList<BehaviorOptionViewModel> rightValueKindOptions,
        IReadOnlyList<BehaviorOptionViewModel> stopModeOptions,
        IReadOnlyList<BehaviorOptionViewModel> reentryPolicyOptions,
        string graphPlaceholder,
        Action markDirty,
        Func<string, string, string> localize)
    {
        Model = model;
        _markDirty = markDirty;
        _localize = localize;
        _graphPlaceholder = graphPlaceholder;

        if (Model.Kind == FrontedBehaviorKind.OneShot)
        {
            Model.Trigger ??= new TriggerDescriptor { EventType = "ManualTrigger" };
        }
        else
        {
            Model.StartTrigger ??= new TriggerDescriptor { EventType = "ManualTrigger" };
            Model.EndTrigger ??= new TriggerDescriptor { EventType = "ManualTrigger" };
            Model.LoopPolicy ??= new FrontedLoopPolicy();
        }

        Trigger = new TriggerDescriptorEditorViewModel(Model.Trigger, eventOptions, operatorOptions, rightValueKindOptions, markDirty);
        StartTrigger = new TriggerDescriptorEditorViewModel(Model.StartTrigger, eventOptions, operatorOptions, rightValueKindOptions, markDirty);
        EndTrigger = new TriggerDescriptorEditorViewModel(Model.EndTrigger, eventOptions, operatorOptions, rightValueKindOptions, markDirty);
        LoopPolicy = new LoopPolicyEditorViewModel(Model.LoopPolicy, stopModeOptions, reentryPolicyOptions, markDirty);
    }

    public FrontedBehavior Model { get; }

    public TriggerDescriptorEditorViewModel Trigger { get; }

    public TriggerDescriptorEditorViewModel StartTrigger { get; }

    public TriggerDescriptorEditorViewModel EndTrigger { get; }

    public LoopPolicyEditorViewModel LoopPolicy { get; }

    public string Name
    {
        get => Model.Name;
        set
        {
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
            if (SetProperty(Model.Enabled, value, Model, static (model, next) => model.Enabled = next))
            {
                _markDirty();
            }
        }
    }

    public FrontedBehaviorKind Kind => Model.Kind;

    public bool IsOneShot => Model.Kind == FrontedBehaviorKind.OneShot;

    public bool IsLoop => Model.Kind == FrontedBehaviorKind.Loop;

    public string KindDisplay => Model.Kind == FrontedBehaviorKind.Loop
        ? _localize("Designer.Behaviors.Loop", "Loop")
        : _localize("Designer.Behaviors.OneShot", "OneShot");

    public string TriggerSummary => IsLoop
        ? $"{StartTrigger.EventType} / {EndTrigger.EventType}"
        : Trigger.EventType;

    public int FilterCount => IsLoop
        ? StartTrigger.Filters.Count + EndTrigger.Filters.Count
        : Trigger.Filters.Count;

    public string GraphSummary => IsLoop
        ? $"{_localize("Designer.Behaviors.StartGraph", "StartGraph")}: {GraphStats(Model.StartGraph)}; "
          + $"{_localize("Designer.Behaviors.LoopGraph", "LoopGraph")}: {GraphStats(Model.LoopGraph)}; "
          + $"{_localize("Designer.Behaviors.StopGraph", "StopGraph")}: {GraphStats(Model.StopGraph)}"
        : GraphStats(Model.Graph);

    public string GraphPlaceholder => _graphPlaceholder;

    private static string GraphStats(FrontedNodeGraph? graph)
    {
        return graph is null
            ? "0 nodes / 0 links"
            : $"{graph.Nodes.Count} nodes / {graph.Connections.Count} links";
    }
}

public sealed partial class TriggerDescriptorEditorViewModel : ObservableObject
{
    private readonly Action _markDirty;

    public TriggerDescriptorEditorViewModel(
        TriggerDescriptor? model,
        IReadOnlyList<BehaviorOptionViewModel> eventOptions,
        IReadOnlyList<BehaviorOptionViewModel> operatorOptions,
        IReadOnlyList<BehaviorOptionViewModel> rightValueKindOptions,
        Action markDirty)
    {
        Model = model ?? new TriggerDescriptor { EventType = "ManualTrigger" };
        EventOptions = eventOptions;
        OperatorOptions = operatorOptions;
        RightValueKindOptions = rightValueKindOptions;
        _markDirty = markDirty;
        foreach (var filter in Model.Filters)
        {
            Filters.Add(new TriggerFilterEditorViewModel(filter, operatorOptions, rightValueKindOptions, markDirty));
        }
    }

    public TriggerDescriptor Model { get; }

    public IReadOnlyList<BehaviorOptionViewModel> EventOptions { get; }

    public IReadOnlyList<BehaviorOptionViewModel> OperatorOptions { get; }

    public IReadOnlyList<BehaviorOptionViewModel> RightValueKindOptions { get; }

    public ObservableCollection<TriggerFilterEditorViewModel> Filters { get; } = [];

    public string EventType
    {
        get => Model.EventType;
        set
        {
            if (SetProperty(Model.EventType, value, Model, static (model, next) => model.EventType = next))
            {
                _markDirty();
            }
        }
    }

    public string? Source
    {
        get => Model.Source;
        set
        {
            if (SetProperty(Model.Source, value, Model, static (model, next) => model.Source = next))
            {
                _markDirty();
            }
        }
    }

    [RelayCommand]
    public void AddFilter()
    {
        var filter = new TriggerFilter
        {
            Left = "Event.",
            Operator = TriggerFilterOperator.Equals,
            RightValueKind = TriggerFilterValueKind.Literal
        };
        Model.Filters.Add(filter);
        Filters.Add(new TriggerFilterEditorViewModel(filter, OperatorOptions, RightValueKindOptions, _markDirty));
        _markDirty();
    }

    [RelayCommand]
    public void RemoveFilter(TriggerFilterEditorViewModel? filter)
    {
        if (filter is null)
        {
            return;
        }

        if (!Model.Filters.Remove(filter.Model))
        {
            return;
        }

        Filters.Remove(filter);
        _markDirty();
    }
}

public sealed partial class TriggerFilterEditorViewModel : ObservableObject
{
    private readonly Action _markDirty;

    public TriggerFilterEditorViewModel(
        TriggerFilter model,
        IReadOnlyList<BehaviorOptionViewModel> operatorOptions,
        IReadOnlyList<BehaviorOptionViewModel> rightValueKindOptions,
        Action markDirty)
    {
        Model = model;
        OperatorOptions = operatorOptions;
        RightValueKindOptions = rightValueKindOptions;
        _markDirty = markDirty;
    }

    public TriggerFilter Model { get; }

    public IReadOnlyList<BehaviorOptionViewModel> OperatorOptions { get; }

    public IReadOnlyList<BehaviorOptionViewModel> RightValueKindOptions { get; }

    public string Left
    {
        get => Model.Left;
        set
        {
            if (SetProperty(Model.Left, value, Model, static (model, next) => model.Left = next))
            {
                _markDirty();
            }
        }
    }

    public TriggerFilterOperator Operator
    {
        get => Model.Operator;
        set
        {
            if (SetProperty(Model.Operator, value, Model, static (model, next) => model.Operator = next))
            {
                _markDirty();
            }
        }
    }

    public string? Right
    {
        get => Model.Right;
        set
        {
            if (SetProperty(Model.Right, value, Model, static (model, next) => model.Right = next))
            {
                _markDirty();
            }
        }
    }

    public TriggerFilterValueKind RightValueKind
    {
        get => Model.RightValueKind;
        set
        {
            if (SetProperty(Model.RightValueKind, value, Model, static (model, next) => model.RightValueKind = next))
            {
                _markDirty();
            }
        }
    }
}

public sealed partial class LoopPolicyEditorViewModel : ObservableObject
{
    private readonly Action _markDirty;

    public LoopPolicyEditorViewModel(
        FrontedLoopPolicy model,
        IReadOnlyList<BehaviorOptionViewModel> stopModeOptions,
        IReadOnlyList<BehaviorOptionViewModel> reentryPolicyOptions,
        Action markDirty)
    {
        Model = model;
        StopModeOptions = stopModeOptions;
        ReentryPolicyOptions = reentryPolicyOptions;
        _markDirty = markDirty;
    }

    public FrontedLoopPolicy Model { get; }

    public IReadOnlyList<BehaviorOptionViewModel> StopModeOptions { get; }

    public IReadOnlyList<BehaviorOptionViewModel> ReentryPolicyOptions { get; }

    public int RepeatCount
    {
        get => Model.RepeatCount;
        set
        {
            if (SetProperty(Model.RepeatCount, value, Model, static (model, next) => model.RepeatCount = next))
            {
                _markDirty();
            }
        }
    }

    public bool AutoReverse
    {
        get => Model.AutoReverse;
        set
        {
            if (SetProperty(Model.AutoReverse, value, Model, static (model, next) => model.AutoReverse = next))
            {
                _markDirty();
            }
        }
    }

    public int IntervalMs
    {
        get => Model.IntervalMs;
        set
        {
            if (SetProperty(Model.IntervalMs, value, Model, static (model, next) => model.IntervalMs = next))
            {
                _markDirty();
            }
        }
    }

    public FrontedLoopStopMode StopMode
    {
        get => Model.StopMode;
        set
        {
            if (SetProperty(Model.StopMode, value, Model, static (model, next) => model.StopMode = next))
            {
                _markDirty();
            }
        }
    }

    public bool ResetOnStop
    {
        get => Model.ResetOnStop;
        set
        {
            if (SetProperty(Model.ResetOnStop, value, Model, static (model, next) => model.ResetOnStop = next))
            {
                _markDirty();
            }
        }
    }

    public FrontedReentryPolicy ReentryPolicy
    {
        get => Model.ReentryPolicy;
        set
        {
            if (SetProperty(Model.ReentryPolicy, value, Model, static (model, next) => model.ReentryPolicy = next))
            {
                _markDirty();
            }
        }
    }
}
