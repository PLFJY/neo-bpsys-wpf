using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Services.FrontedDesigner;
using neo_bpsys_wpf.ViewModels.FrontedDesigner.GraphEditor;
using System.Collections.ObjectModel;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Windows;

namespace neo_bpsys_wpf.ViewModels.FrontedDesigner;

public sealed partial class BehaviorPanelViewModel : ViewModelBase
{
    private readonly IFrontedDesignerLocalizationService _localizationService;
    private readonly Action _markLayoutDirty;
    private readonly Action _markBehaviorsDirty;
    private readonly FrontedNodeCatalog _nodeCatalog;
    private readonly FrontedNodeGraphValidator _graphValidator;
    private readonly IFrontedNodeGraphRuntime _graphRuntime;
    private readonly IFrontedAnimationRuntime? _animationRuntime;
    private readonly FrontedDesignerPreviewAnimationScope? _previewAnimationScope;
    private readonly JsonSerializerOptions _cloneJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        MaxDepth = FrontedLayoutLimits.MaxJsonDepth
    };

    private ControlBehaviorSet? _currentSet;

    private readonly Func<Task<bool>>? _saveBehaviorAsync;

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
        Action markBehaviorsDirty,
        FrontedNodeCatalog? nodeCatalog = null,
        FrontedNodeGraphValidator? graphValidator = null,
        IFrontedNodeGraphRuntime? graphRuntime = null,
        IFrontedAnimationRuntime? animationRuntime = null,
        FrontedDesignerPreviewAnimationScope? previewAnimationScope = null,
        Func<Task<bool>>? saveBehaviorAsync = null)
    {
        _localizationService = localizationService;
        _markLayoutDirty = markLayoutDirty;
        _markBehaviorsDirty = markBehaviorsDirty;
        _saveBehaviorAsync = saveBehaviorAsync;
        _nodeCatalog = nodeCatalog ?? new FrontedNodeCatalog();
        _graphValidator = graphValidator ?? new FrontedNodeGraphValidator(_nodeCatalog);
        _graphRuntime = graphRuntime ?? new FrontedNodeGraphRuntime(_nodeCatalog, _graphValidator);
        _animationRuntime = animationRuntime;
        _previewAnimationScope = previewAnimationScope;
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
        "Node graph editor is available from the animation editor.");

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
            _nodeCatalog,
            _graphValidator,
            _graphRuntime,
            _animationRuntime,
            _previewAnimationScope,
            editor => AnimationEditorRequested?.Invoke(editor),
            CreateTargetOptions(),
            saveBehaviorAsync: _saveBehaviorAsync);
    }

    private IReadOnlyList<FrontedNodeTargetOptionViewModel> CreateTargetOptions()
    {
        var targets = new List<FrontedNodeTargetOptionViewModel>
        {
            new("Self", Localize("Designer.Graph.Target.Self", "Self"))
        };

        if (_previewAnimationScope is not null)
        {
            targets.AddRange(_previewAnimationScope.Targets.Select(target =>
                new FrontedNodeTargetOptionViewModel(
                    $"guid:{target.BehaviorGuid}",
                    string.IsNullOrWhiteSpace(target.DisplayName) ? target.BehaviorGuid.ToString() : target.DisplayName)));
        }

        return targets;
    }

    private BehaviorEventOptionViewModel CreateEventOption(FrontedBehaviorEventDescriptor descriptor)
    {
        return new BehaviorEventOptionViewModel(
            descriptor.EventType,
            descriptor.DisplayNameKey,
            descriptor.CategoryDisplayNameKey,
            descriptor.DescriptionKey,
            descriptor.EventType,
            descriptor.Category,
            descriptor.PayloadFields.Select(field => new BehaviorPayloadFieldOptionViewModel(
                field.Path,
                field.DisplayNameKey,
                field.DescriptionKey,
                field.TypeName,
                false,
                field.IsCommonFilterTarget,
                Localize)).ToArray(),
            Localize);
    }

    private IReadOnlyList<BehaviorOptionViewModel> CreateOperatorOptions() =>
    [
        new(TriggerFilterOperator.Equals, "="),
        new(TriggerFilterOperator.NotEquals, "≠"),
        new(TriggerFilterOperator.GreaterThan, ">"),
        new(TriggerFilterOperator.LessThan, "<"),
        new(TriggerFilterOperator.GreaterThanOrEqual, "≥"),
        new(TriggerFilterOperator.LessThanOrEqual, "≤"),
        new(TriggerFilterOperator.Contains, "Designer.Behaviors.Operator.Contains", "contains", Localize),
        new(TriggerFilterOperator.NotContains, "Designer.Behaviors.Operator.NotContains", "does not contain", Localize)
    ];

    private IReadOnlyList<BehaviorOptionViewModel> CreateEnumOptions<TEnum>(string prefix)
        where TEnum : struct, Enum
    {
        return Enum.GetValues<TEnum>()
            .Select(value =>
            {
                var raw = value.ToString();
                return new BehaviorOptionViewModel(value, $"{prefix}.{raw}", raw, Localize);
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

    /// <summary>
    /// Refreshes all localized display strings in the behavior panel to support
    /// hot language switching without requiring restart.
    /// </summary>
    public void RefreshLocalization()
    {
        foreach (var option in EventOptions)
        {
            option.Refresh();
        }

        foreach (var option in OperatorOptions)
        {
            option.Refresh();
        }

        foreach (var option in StopModeOptions)
        {
            option.Refresh();
        }

        foreach (var option in ReentryPolicyOptions)
        {
            option.Refresh();
        }

        foreach (var behavior in Behaviors)
        {
            behavior.RefreshLocalization();
        }

        OnPropertyChanged(nameof(EmptyText));
        OnPropertyChanged(nameof(GraphPlaceholder));
    }

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

public sealed class BehaviorOptionViewModel : ObservableObject
{
    private readonly string _displayNameKey;
    private readonly string _displayNameFallback;
    private readonly Func<string, string, string> _localize;
    private string _displayName;

    public BehaviorOptionViewModel(object value, string displayNameKey, string displayNameFallback, Func<string, string, string> localize)
    {
        Value = value;
        _displayNameKey = displayNameKey;
        _displayNameFallback = displayNameFallback;
        _localize = localize;
        _displayName = localize(displayNameKey, displayNameFallback);
    }

    /// <summary>
    /// For non-localized values (e.g. operators with symbol-only display).
    /// </summary>
    public BehaviorOptionViewModel(object value, string displayName)
    {
        Value = value;
        _displayNameKey = string.Empty;
        _displayNameFallback = displayName;
        _localize = static (_, fallback) => fallback;
        _displayName = displayName;
    }

    public object Value { get; }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    /// <summary>
    /// Re-resolves <see cref="DisplayName"/> from the localization function.
    /// </summary>
    public void Refresh()
    {
        if (!string.IsNullOrEmpty(_displayNameKey))
        {
            DisplayName = _localize(_displayNameKey, _displayNameFallback);
        }
    }
}

public sealed class BehaviorEventOptionViewModel : ObservableObject
{
    private readonly string _displayNameKey;
    private readonly string _categoryDisplayNameKey;
    private readonly string _descriptionKey;
    private readonly string _eventTypeFallback;
    private readonly string _categoryFallback;
    private readonly Func<string, string, string> _localize;
    private string _displayName;
    private string _categoryDisplayName;
    private string _description;

    public BehaviorEventOptionViewModel(
        string eventType,
        string displayNameKey,
        string categoryDisplayNameKey,
        string descriptionKey,
        string eventTypeFallback,
        string categoryFallback,
        IReadOnlyList<BehaviorPayloadFieldOptionViewModel> payloadFields,
        Func<string, string, string> localize)
    {
        EventType = eventType;
        _displayNameKey = displayNameKey;
        _categoryDisplayNameKey = categoryDisplayNameKey;
        _descriptionKey = descriptionKey;
        _eventTypeFallback = eventTypeFallback;
        _categoryFallback = categoryFallback;
        _localize = localize;
        PayloadFields = payloadFields;

        var category = localize(categoryDisplayNameKey, categoryFallback);
        _categoryDisplayName = category;
        _displayName = $"{category} / {localize(displayNameKey, eventTypeFallback)}";
        _description = localize(descriptionKey, eventTypeFallback);
    }

    public string EventType { get; }
    public IReadOnlyList<BehaviorPayloadFieldOptionViewModel> PayloadFields { get; }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public string CategoryDisplayName
    {
        get => _categoryDisplayName;
        set => SetProperty(ref _categoryDisplayName, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    /// <summary>
    /// Re-resolves all localized display strings from the localization function.
    /// </summary>
    public void Refresh()
    {
        CategoryDisplayName = _localize(_categoryDisplayNameKey, _categoryFallback);
        DisplayName = $"{CategoryDisplayName} / {_localize(_displayNameKey, _eventTypeFallback)}";
        Description = _localize(_descriptionKey, _eventTypeFallback);
        foreach (var field in PayloadFields)
        {
            field.Refresh();
        }
    }
}

public sealed class BehaviorPayloadFieldOptionViewModel : ObservableObject
{
    private readonly string _displayNameKey;
    private readonly string _descriptionKey;
    private readonly string _pathOrTypeFallback;
    private readonly Func<string, string, string> _localize;
    private string _displayName;
    private string _description;

    public BehaviorPayloadFieldOptionViewModel(
        string path,
        string displayNameKey,
        string descriptionKey,
        string typeName,
        bool isUnknown,
        bool isCommonFilterTarget,
        Func<string, string, string> localize)
    {
        Path = path;
        _displayNameKey = displayNameKey;
        _descriptionKey = descriptionKey;
        _pathOrTypeFallback = typeName;
        _localize = localize;
        TypeName = typeName;
        IsUnknown = isUnknown;
        IsCommonFilterTarget = isCommonFilterTarget;
        if (isUnknown)
        {
            _displayName = string.Format(
                localize(displayNameKey, "Unknown parameter: {0}"),
                path);
        }
        else
        {
            _displayName = localize(displayNameKey, path);
        }
        _description = localize(descriptionKey, path);
    }

    public string Path { get; }
    public string TypeName { get; }
    public bool IsUnknown { get; }
    public bool IsCommonFilterTarget { get; }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    /// <summary>
    /// Re-resolves all localized display strings from the localization function.
    /// </summary>
    public void Refresh()
    {
        if (IsUnknown)
        {
            DisplayName = string.Format(
                _localize(_displayNameKey, "Unknown parameter: {0}"),
                Path);
        }
        else
        {
            DisplayName = _localize(_displayNameKey, Path);
            Description = _localize(_descriptionKey, Path);
        }
    }
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
        FrontedNodeCatalog nodeCatalog,
        FrontedNodeGraphValidator graphValidator,
        IFrontedNodeGraphRuntime graphRuntime,
        IFrontedAnimationRuntime? animationRuntime,
        FrontedDesignerPreviewAnimationScope? previewAnimationScope,
        Action<FrontedBehaviorAnimationEditorViewModel> openAnimationEditor,
        IReadOnlyList<FrontedNodeTargetOptionViewModel>? targetOptions = null,
        Func<Task<bool>>? saveBehaviorAsync = null)
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
                targetOptions,
                saveAsync: saveBehaviorAsync)));
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

    /// <summary>
    /// Refreshes all localized display strings to support hot language switching.
    /// </summary>
    public void RefreshLocalization()
    {
        Trigger.RefreshLocalization();
        StartTrigger.RefreshLocalization();
        EndTrigger.RefreshLocalization();
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
                ClearFilters();
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
        var filterVm = new TriggerFilterEditorViewModel(filter, OperatorOptions, _markDirty);
        filterVm.SetPayloadFieldOptions(PayloadFieldOptions);
        Filters.Add(filterVm);
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

    private void ClearFilters()
    {
        Model.Filters.Clear();
        Filters.Clear();
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
                    "Designer.Behaviors.UnknownParameterFormat",
                    string.Empty,
                    path,
                    true,
                    false,
                    localize));
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

    /// <summary>
    /// Refreshes all localized display strings to support hot language switching.
    /// </summary>
    public void RefreshLocalization()
    {
        foreach (var option in PayloadFieldOptions)
        {
            option.Refresh();
        }

        foreach (var filter in Filters)
        {
            filter.RefreshLocalization();
        }

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

    /// <summary>
    /// Refreshes payload field option display strings to support hot language switching.
    /// </summary>
    public void RefreshLocalization()
    {
        foreach (var option in PayloadFieldOptions)
        {
            option.Refresh();
        }
        OnPropertyChanged(nameof(IsUnknownParameter));
    }
}

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
        Func<Task<bool>>? saveAsync = null)
    {
        _behavior = behavior;
        _runtime = runtime ?? new FrontedNodeGraphRuntime(catalog, validator);
        _animationRuntime = animationRuntime;
        _previewAnimationScope = previewAnimationScope;
        _localize = localize;
        _saveAsync = saveAsync;
        Title = behavior.Name;
        IsLoop = behavior.Kind == FrontedBehaviorKind.Loop;
        Stages = IsLoop
            ?
            [
                Stage(localize("Designer.Behaviors.StartAnimation", "Start animation"), behavior.StartGraph, catalog, validator, _runtime, animationRuntime, previewAnimationScope, markDirty, localize, targetOptions),
                Stage(localize("Designer.Behaviors.LoopAnimation", "Loop animation"), behavior.LoopGraph, catalog, validator, _runtime, animationRuntime, previewAnimationScope, markDirty, localize, targetOptions),
                Stage(localize("Designer.Behaviors.EndAnimation", "End animation"), behavior.StopGraph, catalog, validator, _runtime, animationRuntime, previewAnimationScope, markDirty, localize, targetOptions)
            ]
            : [Stage(localize("Designer.Behaviors.Animation", "Animation"), behavior.Graph, catalog, validator, _runtime, animationRuntime, previewAnimationScope, markDirty, localize, targetOptions)];

        // Wire each stage's graph editor save action to trigger SaveAllAsync on this animation editor.
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
                // Save failed — keep dirty state intact
                return false;
            }
        }

        // Clear all stages' dirty state after successful save.
        // Must dispatch to the UI thread because setting IsDirty triggers RelayCommand.NotifyCanExecuteChanged.
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
                stage.GraphEditor.IsDirty = false;
            }
        }
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PreviewStartCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviewLoopOnceCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartLoopPreviewCommand))]
    private bool _isLoopPreviewRunning;

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
        IReadOnlyList<FrontedNodeTargetOptionViewModel>? targetOptions)
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
            targetOptions: targetOptions)
        {
            PreviewRoot = previewAnimationScope?.Root
        };
        return new(name, graph, editorVm);
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

        _loopPreviewCancellation = new CancellationTokenSource();
        IsLoopPreviewRunning = true;
        try
        {
            var token = _loopPreviewCancellation.Token;
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
            IsLoopPreviewRunning = false;
            _loopPreviewCancellation?.Dispose();
            _loopPreviewCancellation = null;
        }
    }

    private async Task StopLoopPreviewAsync()
    {
        _loopPreviewCancellation?.Cancel();
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
        var result = await _runtime.ExecuteAsync(graph, graphContext, cancellationToken);
        var stage = Stages.FirstOrDefault(item => ReferenceEquals(item.Graph, graph));
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

public sealed record FrontedBehaviorAnimationStageViewModel(
    string DisplayName,
    FrontedNodeGraph Graph,
    FrontedNodeGraphEditorViewModel GraphEditor);

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
