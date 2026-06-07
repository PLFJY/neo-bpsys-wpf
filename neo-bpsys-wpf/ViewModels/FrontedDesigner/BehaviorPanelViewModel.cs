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
        OperatorOptions = CreateOperatorOptions();
        StopModeOptions = CreateEnumOptions<FrontedLoopStopMode>("Designer.Behaviors.StopMode");
        ReentryPolicyOptions = CreateEnumOptions<FrontedReentryPolicy>("Designer.Behaviors.ReentryPolicy");
    }

    public ObservableCollection<BehaviorEditorViewModel> Behaviors { get; } = [];

    public IReadOnlyList<BehaviorEventOptionViewModel> EventOptions { get; }

    public IReadOnlyList<BehaviorOptionViewModel> OperatorOptions { get; }

    public IReadOnlyList<BehaviorOptionViewModel> StopModeOptions { get; }

    public IReadOnlyList<BehaviorOptionViewModel> ReentryPolicyOptions { get; }

    public FrontedBehaviorDocument CurrentDocument { get; private set; } = new();

    public event Action<FrontedBehaviorAnimationEditorViewModel>? AnimationEditorRequested;

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
            Trigger = new TriggerDescriptor { EventType = EventOptions.FirstOrDefault()?.EventType ?? string.Empty },
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
            StartTrigger = new TriggerDescriptor { EventType = EventOptions.FirstOrDefault()?.EventType ?? string.Empty },
            EndTrigger = new TriggerDescriptor { EventType = EventOptions.FirstOrDefault()?.EventType ?? string.Empty },
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
            StopModeOptions,
            ReentryPolicyOptions,
            GraphPlaceholder,
            MarkBehaviorsDirty,
            Localize,
            editor => AnimationEditorRequested?.Invoke(editor));
    }

    private BehaviorEventOptionViewModel CreateEventOption(FrontedBehaviorEventDescriptor descriptor)
    {
        var category = Localize(descriptor.CategoryDisplayNameKey, descriptor.Category);
        return new BehaviorEventOptionViewModel(
            descriptor.EventType,
            $"{category} / {Localize(descriptor.DisplayNameKey, descriptor.EventType)}",
            category,
            Localize(descriptor.DescriptionKey, descriptor.EventType),
            descriptor.PayloadFields.Select(field => new BehaviorPayloadFieldOptionViewModel(
                field.Path,
                Localize(field.DisplayNameKey, field.Path),
                Localize(field.DescriptionKey, field.Path),
                field.TypeName,
                false,
                field.IsCommonFilterTarget)).ToArray());
    }

    private IReadOnlyList<BehaviorOptionViewModel> CreateOperatorOptions() =>
    [
        new(TriggerFilterOperator.Equals, "="),
        new(TriggerFilterOperator.NotEquals, "≠"),
        new(TriggerFilterOperator.GreaterThan, ">"),
        new(TriggerFilterOperator.LessThan, "<"),
        new(TriggerFilterOperator.GreaterThanOrEqual, "≥"),
        new(TriggerFilterOperator.LessThanOrEqual, "≤"),
        new(TriggerFilterOperator.Contains, Localize("Designer.Behaviors.Operator.Contains", "contains")),
        new(TriggerFilterOperator.NotContains, Localize("Designer.Behaviors.Operator.NotContains", "does not contain"))
    ];

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

public sealed class BehaviorEventOptionViewModel(
    string eventType,
    string displayName,
    string categoryDisplayName,
    string description,
    IReadOnlyList<BehaviorPayloadFieldOptionViewModel> payloadFields)
{
    public string EventType { get; } = eventType;
    public string DisplayName { get; } = displayName;
    public string CategoryDisplayName { get; } = categoryDisplayName;
    public string Description { get; } = description;
    public IReadOnlyList<BehaviorPayloadFieldOptionViewModel> PayloadFields { get; } = payloadFields;
}

public sealed class BehaviorPayloadFieldOptionViewModel(
    string path,
    string displayName,
    string description,
    string typeName,
    bool isUnknown,
    bool isCommonFilterTarget)
{
    public string Path { get; } = path;
    public string DisplayName { get; } = displayName;
    public string Description { get; } = description;
    public string TypeName { get; } = typeName;
    public bool IsUnknown { get; } = isUnknown;
    public bool IsCommonFilterTarget { get; } = isCommonFilterTarget;
}

public sealed partial class BehaviorEditorViewModel : ObservableObject
{
    private readonly Action _markDirty;
    private readonly Func<string, string, string> _localize;
    private readonly string _graphPlaceholder;

    public BehaviorEditorViewModel(
        FrontedBehavior model,
        IReadOnlyList<BehaviorEventOptionViewModel> eventOptions,
        IReadOnlyList<BehaviorOptionViewModel> operatorOptions,
        IReadOnlyList<BehaviorOptionViewModel> stopModeOptions,
        IReadOnlyList<BehaviorOptionViewModel> reentryPolicyOptions,
        string graphPlaceholder,
        Action markDirty,
        Func<string, string, string> localize,
        Action<FrontedBehaviorAnimationEditorViewModel> openAnimationEditor)
    {
        Model = model;
        _markDirty = markDirty;
        _localize = localize;
        _graphPlaceholder = graphPlaceholder;

        if (Model.Kind == FrontedBehaviorKind.OneShot)
        {
            Model.Trigger ??= new TriggerDescriptor { EventType = eventOptions.FirstOrDefault()?.EventType ?? string.Empty };
        }
        else
        {
            Model.StartTrigger ??= new TriggerDescriptor { EventType = eventOptions.FirstOrDefault()?.EventType ?? string.Empty };
            Model.EndTrigger ??= new TriggerDescriptor { EventType = eventOptions.FirstOrDefault()?.EventType ?? string.Empty };
            Model.LoopPolicy ??= new FrontedLoopPolicy();
        }

        Trigger = new TriggerDescriptorEditorViewModel(Model.Trigger, eventOptions, operatorOptions, markDirty, localize);
        StartTrigger = new TriggerDescriptorEditorViewModel(Model.StartTrigger, eventOptions, operatorOptions, markDirty, localize);
        EndTrigger = new TriggerDescriptorEditorViewModel(Model.EndTrigger, eventOptions, operatorOptions, markDirty, localize);
        LoopPolicy = new LoopPolicyEditorViewModel(Model.LoopPolicy, stopModeOptions, reentryPolicyOptions, markDirty);
        OpenAnimationEditorCommand = new RelayCommand(() => openAnimationEditor(new FrontedBehaviorAnimationEditorViewModel(Model, localize)));
    }

    public FrontedBehavior Model { get; }

    public TriggerDescriptorEditorViewModel Trigger { get; }

    public TriggerDescriptorEditorViewModel StartTrigger { get; }

    public TriggerDescriptorEditorViewModel EndTrigger { get; }

    public LoopPolicyEditorViewModel LoopPolicy { get; }

    public IRelayCommand OpenAnimationEditorCommand { get; }

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
    private readonly Func<string, string, string> _localize;

    public TriggerDescriptorEditorViewModel(
        TriggerDescriptor? model,
        IReadOnlyList<BehaviorEventOptionViewModel> eventOptions,
        IReadOnlyList<BehaviorOptionViewModel> operatorOptions,
        Action markDirty,
        Func<string, string, string> localize)
    {
        Model = model ?? new TriggerDescriptor { EventType = eventOptions.FirstOrDefault()?.EventType ?? string.Empty };
        EventOptions = eventOptions;
        OperatorOptions = operatorOptions;
        _markDirty = markDirty;
        _localize = localize;
        foreach (var filter in Model.Filters)
        {
            Filters.Add(new TriggerFilterEditorViewModel(filter, operatorOptions, markDirty));
        }
        UpdateSelectedEvent(localize);
    }

    public TriggerDescriptor Model { get; }

    public IReadOnlyList<BehaviorEventOptionViewModel> EventOptions { get; }

    public IReadOnlyList<BehaviorOptionViewModel> OperatorOptions { get; }

    public ObservableCollection<TriggerFilterEditorViewModel> Filters { get; } = [];

    public BehaviorEventOptionViewModel? SelectedEventDescriptor { get; private set; }

    public IReadOnlyList<BehaviorPayloadFieldOptionViewModel> PayloadFieldOptions { get; private set; } = [];

    public bool HasPayloadFields => PayloadFieldOptions.Count > 0;

    public string EventType
    {
        get => Model.EventType;
        set
        {
            if (SetProperty(Model.EventType, value, Model, static (model, next) => model.EventType = next))
            {
                _markDirty();
                UpdateSelectedEvent(_localize);
            }
        }
    }

    [RelayCommand]
    public void AddFilter()
    {
        var filter = new TriggerFilter
        {
            Left = PayloadFieldOptions.FirstOrDefault(field => field.IsCommonFilterTarget)?.Path
                   ?? PayloadFieldOptions.FirstOrDefault()?.Path
                   ?? string.Empty,
            Operator = TriggerFilterOperator.Equals
        };
        Model.Filters.Add(filter);
        Filters.Add(new TriggerFilterEditorViewModel(filter, OperatorOptions, _markDirty));
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

    private void UpdateSelectedEvent(Func<string, string, string> localize)
    {
        SelectedEventDescriptor = EventOptions.FirstOrDefault(option =>
            string.Equals(option.EventType, Model.EventType, StringComparison.Ordinal));
        var options = SelectedEventDescriptor?.PayloadFields.ToList() ?? [];
        foreach (var path in Model.Filters.Select(filter => filter.Left).Where(path => !string.IsNullOrWhiteSpace(path)).Distinct())
        {
            if (options.All(option => !string.Equals(option.Path, path, StringComparison.Ordinal)))
            {
                options.Add(new BehaviorPayloadFieldOptionViewModel(
                    path,
                    string.Format(localize("Designer.Behaviors.UnknownParameterFormat", "Unknown parameter: {0}"), path),
                    path,
                    "string",
                    true,
                    false));
            }
        }

        PayloadFieldOptions = options;
        foreach (var filter in Filters)
        {
            filter.SetPayloadFieldOptions(options);
        }
        OnPropertyChanged(nameof(SelectedEventDescriptor));
        OnPropertyChanged(nameof(PayloadFieldOptions));
        OnPropertyChanged(nameof(HasPayloadFields));
    }
}

public sealed partial class TriggerFilterEditorViewModel : ObservableObject
{
    private readonly Action _markDirty;

    public TriggerFilterEditorViewModel(
        TriggerFilter model,
        IReadOnlyList<BehaviorOptionViewModel> operatorOptions,
        Action markDirty)
    {
        Model = model;
        OperatorOptions = operatorOptions;
        _markDirty = markDirty;
    }

    public TriggerFilter Model { get; }

    public IReadOnlyList<BehaviorOptionViewModel> OperatorOptions { get; }

    public IReadOnlyList<BehaviorPayloadFieldOptionViewModel> PayloadFieldOptions { get; private set; } = [];

    public bool IsUnknownParameter => PayloadFieldOptions.FirstOrDefault(option =>
        string.Equals(option.Path, Left, StringComparison.Ordinal))?.IsUnknown == true;

    public string Left
    {
        get => Model.Left;
        set
        {
            if (SetProperty(Model.Left, value, Model, static (model, next) => model.Left = next))
            {
                _markDirty();
                OnPropertyChanged(nameof(IsUnknownParameter));
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

    public void SetPayloadFieldOptions(IReadOnlyList<BehaviorPayloadFieldOptionViewModel> options)
    {
        PayloadFieldOptions = options;
        OnPropertyChanged(nameof(PayloadFieldOptions));
        OnPropertyChanged(nameof(IsUnknownParameter));
    }
}

public sealed class FrontedBehaviorAnimationEditorViewModel
{
    public FrontedBehaviorAnimationEditorViewModel(FrontedBehavior behavior, Func<string, string, string> localize)
    {
        Title = behavior.Name;
        IsLoop = behavior.Kind == FrontedBehaviorKind.Loop;
        Stages = IsLoop
            ?
            [
                Stage(localize("Designer.Behaviors.StartAnimation", "Start animation"), behavior.StartGraph),
                Stage(localize("Designer.Behaviors.LoopAnimation", "Loop animation"), behavior.LoopGraph),
                Stage(localize("Designer.Behaviors.EndAnimation", "End animation"), behavior.StopGraph)
            ]
            : [Stage(localize("Designer.Behaviors.Animation", "Animation"), behavior.Graph)];
        Placeholder = localize("Designer.Behaviors.AnimationEditorPlaceholder", "Node graph animation editor will be implemented in Phase 3.");
    }

    public string Title { get; }
    public bool IsLoop { get; }
    public string Placeholder { get; }
    public IReadOnlyList<FrontedBehaviorAnimationStageViewModel> Stages { get; }

    private static FrontedBehaviorAnimationStageViewModel Stage(string name, FrontedNodeGraph graph) =>
        new(name, graph.Nodes.Count, graph.Connections.Count);
}

public sealed record FrontedBehaviorAnimationStageViewModel(string DisplayName, int NodeCount, int LinkCount);

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
