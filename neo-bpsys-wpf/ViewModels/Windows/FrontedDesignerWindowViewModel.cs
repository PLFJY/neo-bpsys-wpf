using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Messages;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Models.ScoreSystem;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Services.FrontedDesigner;
using neo_bpsys_wpf.ViewModels.FrontedDesigner;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace neo_bpsys_wpf.ViewModels.Windows;

/// <summary>
/// Controls how a designer snapshot restore updates validation and preview visuals.
/// </summary>
public enum FrontedDesignerSnapshotRestoreMode
{
    /// <summary>
    /// Prefer an in-place geometry patch, then defer validation; fall back to scheduled atomic preview.
    /// </summary>
    PreferGeometryFastPathThenScheduledAtomicPreview,

    /// <summary>
    /// Render preview immediately, then defer validation to the scheduled designer work queue.
    /// </summary>
    ImmediatePreviewThenScheduledValidation,

    /// <summary>
    /// Defer validation and preview together to the scheduled designer work queue.
    /// </summary>
    ScheduledValidationAndPreview,

    /// <summary>
    /// Run validation and preview immediately in one restore transaction.
    /// </summary>
    ImmediateValidationAndPreview
}

/// <summary>
/// ViewModel for the independent v3 fronted designer editor shell.
/// </summary>
public partial class FrontedDesignerWindowViewModel : ViewModelBase
{
    private static readonly Regex ValidControlNameRegex = new(
        "^[A-Za-z_][A-Za-z0-9_]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IFrontedLayoutService _layoutService;
    private readonly FrontedLayoutDesignConverter _designConverter;
    private readonly FrontedLayoutRuntimeContractCatalog _runtimeContracts;
    private readonly FrontedLayoutValidator _validator;
    private readonly FrontedLayoutReferenceScanner _referenceScanner;
    private readonly FrontedPropertyGridBuilder _propertyGridBuilder;
    private readonly FrontedControlDefaultConfigFactory _defaultConfigFactory;
    private readonly FrontedControlNameGenerator _controlNameGenerator;
    private readonly IFrontedDesignerLocalizationService _localizationService;
    private readonly ISharedDataService _designerPreviewSharedDataService;
    private readonly IFrontedLocalResourceStore? _localResourceStore;
    private readonly IFrontedWindowLayoutOptionsService? _windowLayoutOptionsService;
    private readonly IFrontedWindowService? _frontedWindowService;
    private readonly IFrontedBehaviorService _behaviorService;
    private readonly IFrontedBehaviorRuntime? _behaviorRuntime;
    private readonly IFrontedAnimationRuntime? _animationRuntime;
    private readonly FrontedDesignerPreviewAnimationScope? _previewAnimationScope;
    private readonly ILogger<FrontedDesignerWindowViewModel> _logger;

    private static ILogger<FrontedDesignerWindowViewModel>? StaticLogger =>
        IAppHost.TryGetService<ILogger<FrontedDesignerWindowViewModel>>();

    private readonly Dictionary<string, string> _propertyEditErrors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _propertyEditBuffers = new(StringComparer.Ordinal);
    private readonly Stack<string> _undoStack = new();
    private readonly Stack<string> _redoStack = new();
    private readonly List<PendingImportedResource> _pendingImportedResources = [];
    private FrontedDesignerClipboardPayload? _copiedControl;
    private IReadOnlyList<FrontedLayoutValidationMessage> _lastValidationMessages = [];
    private bool _isChangingZoomPreset;
    private bool _suppressZoomEditTextUpdate;
    private bool _isRebuildingPropertyGrid;
    private bool _isRestoringSnapshot;
    private bool _isLoadingWindowOptions;
    private bool _windowBackgroundColorConfigured;
    private bool _isUpdatingBoModeStateUi;
    private bool _preserveUndoRedoDuringDocumentSwap;
    private bool _scheduledValidationAndPreviewPending;
    private bool _scheduledValidationRequested;
    private bool _scheduledPreviewRequested;
    private bool _clearRestoreVisualsAfterScheduledPreview;
    private FrontedControlDesignItem? _lastSelectedDesignItem;
    private DesignerLayerNode? _selectedLayerNode;
    private CancellationTokenSource? _reloadLayoutCancellation;
    private int _reloadLayoutVersion;
    private double _lastPreviewViewportWidth;
    private double _lastPreviewViewportHeight;

#pragma warning disable CS8618
    public FrontedDesignerWindowViewModel()
#pragma warning restore CS8618
    {
        // Decorative constructor for design-time only.
        _layoutService = null!;
        _designConverter = new FrontedLayoutDesignConverter();
        _runtimeContracts = new FrontedLayoutRuntimeContractCatalog();
        _referenceScanner = new FrontedLayoutReferenceScanner();
        _validator = new FrontedLayoutValidator(
            runtimeContracts: _runtimeContracts,
            referenceScanner: _referenceScanner);
        _propertyGridBuilder = new FrontedPropertyGridBuilder();
        _defaultConfigFactory = new FrontedControlDefaultConfigFactory();
        _controlNameGenerator = new FrontedControlNameGenerator();
        _localizationService = new FrontedDesignerLocalizationService();
        _designerPreviewSharedDataService = new DesignerPreviewSharedDataService();
        _localResourceStore = null;
        _windowLayoutOptionsService = null;
        _frontedWindowService = null;
        _behaviorService = new NoopFrontedBehaviorService();
        _animationRuntime = null;
        _previewAnimationScope = null;
        _logger = NullLogger<FrontedDesignerWindowViewModel>.Instance;
        BehaviorPanel = CreateBehaviorPanel();
        InitializeZoomPresets();
    }

    public FrontedDesignerWindowViewModel(IFrontedBehaviorService behaviorService)
        : this()
    {
        _behaviorService = behaviorService;
    }

    public FrontedDesignerWindowViewModel(
        FrontedDesignerLayoutCatalog layoutCatalog,
        IFrontedLayoutService layoutService,
        FrontedLayoutDesignConverter designConverter,
        FrontedLayoutRuntimeContractCatalog runtimeContracts,
        FrontedLayoutValidator validator,
        FrontedLayoutReferenceScanner referenceScanner,
        FrontedPropertyGridBuilder propertyGridBuilder,
        FrontedControlDefaultConfigFactory defaultConfigFactory,
        FrontedControlNameGenerator controlNameGenerator,
        IFrontedDesignerLocalizationService localizationService,
        DesignerPreviewSharedDataService designerPreviewSharedDataService,
        IFrontedLocalResourceStore localResourceStore,
        IFrontedWindowLayoutOptionsService windowLayoutOptionsService,
        IFrontedWindowService frontedWindowService,
        IFrontedBehaviorService behaviorService,
        IFrontedAnimationRuntime animationRuntime,
        FrontedDesignerPreviewAnimationScope previewAnimationScope,
        IFrontedBehaviorRuntime behaviorRuntime,
        ILogger<FrontedDesignerWindowViewModel> logger)
    {
        _layoutService = layoutService;
        _designConverter = designConverter;
        _runtimeContracts = runtimeContracts;
        _validator = validator;
        _referenceScanner = referenceScanner;
        _propertyGridBuilder = propertyGridBuilder;
        _defaultConfigFactory = defaultConfigFactory;
        _controlNameGenerator = controlNameGenerator;
        _localizationService = localizationService;
        _designerPreviewSharedDataService = designerPreviewSharedDataService;
        _localResourceStore = localResourceStore;
        _windowLayoutOptionsService = windowLayoutOptionsService;
        _frontedWindowService = frontedWindowService;
        _behaviorService = behaviorService;
        _behaviorRuntime = behaviorRuntime;
        _animationRuntime = animationRuntime;
        _previewAnimationScope = previewAnimationScope;
        _logger = logger;
        BehaviorPanel = CreateBehaviorPanel();

        foreach (var group in layoutCatalog.GetEntries()
                     .Where(entry => entry.IsMigrated && entry.IsEditable)
                     .GroupBy(entry => entry.WindowTypeName)
                     .Select(group =>
                     {
                         var firstEntry = group.First();
                         var groupDisplayName = !string.IsNullOrWhiteSpace(firstEntry.DisplayName)
                             ? firstEntry.DisplayName
                             : _localizationService.GetWindowDisplayName(group.Key);
                         return new FrontedDesignerWindowOption(
                             group.Key,
                             groupDisplayName,
                             group
                                 .Select(entry => new FrontedDesignerLayoutCatalogEntry
                                 {
                                     WindowTypeName = entry.WindowTypeName,
                                     DisplayName = !string.IsNullOrWhiteSpace(entry.DisplayName)
                                         ? entry.DisplayName
                                         : _localizationService.GetWindowDisplayName(entry.WindowTypeName),
                                     WindowId = entry.WindowId,
                                     CanvasName = entry.CanvasName,
                                     CanvasDisplayName = !string.IsNullOrWhiteSpace(entry.CanvasDisplayName)
                                         ? entry.CanvasDisplayName
                                         : _localizationService.GetCanvasDisplayName(entry.CanvasName),
                                     CanvasWidth = entry.CanvasWidth,
                                     CanvasHeight = entry.CanvasHeight,
                                     IsMigrated = entry.IsMigrated,
                                     IsEditable = entry.IsEditable
                                 })
                                 .ToArray());
                     }))
        {
            WindowOptions.Add(group);
        }

        InitializeZoomPresets();
        RebuildAddControlCatalog();
        SelectedZoomPreset = ZoomPresets.FirstOrDefault();
        SelectedWindow = WindowOptions.FirstOrDefault();
    }

    /// <summary>
    /// Raised when the view should render or clear the preview canvas.
    /// </summary>
    public event EventHandler<FrontedDesignerPreviewRenderRequestedEventArgs>? PreviewRenderRequested;

    /// <summary>
    /// Raised when an undo/redo restore can be applied by patching existing preview elements.
    /// </summary>
    public event EventHandler<FrontedDesignerGeometryPatchRequestedEventArgs>? DesignerGeometryPatchRequested;

    public ObservableCollection<FrontedDesignerWindowOption> WindowOptions { get; } = [];

    public ObservableCollection<FrontedDesignerLayoutCatalogEntry> CanvasOptions { get; } = [];

    public ObservableCollection<FrontedLayoutValidationMessage> ValidationMessages { get; } = [];

    public ObservableCollection<FrontedDesignerZoomPreset> ZoomPresets { get; } = [];

    /// <summary>
    /// Zoom presets for the ComboBox dropdown, excluding the Fit item.
    /// </summary>
    public IEnumerable<FrontedDesignerZoomPreset> ManualZoomPresets =>
        ZoomPresets.Where(p => !p.IsFit).ToList();

    public ObservableCollection<FrontedControlDesignItem> FilteredDesignItems { get; } = [];

    public ObservableCollection<FrontedLayerGroup> LayerGroups { get; } = [];

    public ObservableCollection<GlobalScoreCellConfig> GlobalScoreCellEditorItems { get; } = [];

    public DesignerLayerNode? SelectedLayerNode
    {
        get => _selectedLayerNode;
        private set => SetProperty(ref _selectedLayerNode, value);
    }

    public ObservableCollection<FrontedPropertyEditorItem> PropertyEditorItems { get; } = [];

    public ObservableCollection<FrontedAddControlCatalogGroup> AddControlCatalogGroups { get; } = [];

    public BehaviorPanelViewModel BehaviorPanel { get; private set; }

    public ObservableCollection<FrontedCanvasBoModeStateOption> BoModeStateOptions { get; } =
    [
        new(FrontedCanvasBoModeState.Bo5, I18nHelper.GetLocalizedString("Designer.Canvas.Bo5State")),
        new(FrontedCanvasBoModeState.Bo3, I18nHelper.GetLocalizedString("Designer.Canvas.Bo3State"))
    ];

    public bool IsRebuildingPropertyGrid => _isRebuildingPropertyGrid;

    [ObservableProperty]
    private FrontedDesignerWindowOption? _selectedWindow;

    [ObservableProperty]
    private FrontedDesignerLayoutCatalogEntry? _selectedCanvas;

    [ObservableProperty]
    private FrontedCanvasDesignDocument? _currentDocument;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSaveLayout))]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private bool _areBehaviorsDirty;

    [ObservableProperty]
    private string _layoutSourcePath = string.Empty;

    [ObservableProperty]
    private string _layoutSourceDisplay = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _currentWindowCanvasDisplay = string.Empty;

    [ObservableProperty]
    private string _dirtyIndicatorText = "○";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectiveSnapEnabled))]
    private bool _snapEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectiveSnapEnabled))]
    private bool _isShiftSnapActive;

    [ObservableProperty]
    private IReadOnlyList<FrontedDesignerSnapGuide> _activeSnapGuides = [];

    [ObservableProperty]
    private double _snapGridSize = FrontedDesignerGeometryHelper.DefaultSnapGridSize;

    public bool EffectiveSnapEnabled => SnapEnabled || IsShiftSnapActive;

    public string SnapStatusText
    {
        get
        {
            if (SnapEnabled)
            {
                return I18nHelper.GetLocalizedString("SnapOn");
            }

            return IsShiftSnapActive
                ? I18nHelper.GetLocalizedString("TemporarySnap")
                : I18nHelper.GetLocalizedString("SnapOff");
        }
    }

    [ObservableProperty]
    private double _zoomScale = 1D;

    [ObservableProperty]
    private string _zoomDisplay = "Fit";

    [ObservableProperty]
    private bool _isFitMode = true;

    [ObservableProperty]
    private string _zoomEditText = "Fit";

    public double ZoomPercent
    {
        get => ZoomScale * 100;
        set
        {
            var clamped = Math.Clamp(value, 25D, 400D);
            ApplyManualZoom(clamped / 100D);
        }
    }

    [ObservableProperty]
    private FrontedDesignerZoomPreset? _selectedZoomPreset;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedDesignItem))]
    [NotifyPropertyChangedFor(nameof(CanDeleteSelectedControl))]
    [NotifyPropertyChangedFor(nameof(IsBorderedImageSelected))]
    [NotifyPropertyChangedFor(nameof(IsMapV2DisplaySelected))]
    [NotifyPropertyChangedFor(nameof(IsPolygonSelected))]
    private FrontedControlDesignItem? _selectedDesignItem;

    public bool HasSelectedDesignItem => SelectedDesignItem is not null;

    public bool IsBorderedImageSelected => SelectedDesignItem?.Config is BorderedImageFrontedControlConfig;

    public bool IsMapV2DisplaySelected => SelectedDesignItem?.Config is MapV2DisplayControlConfig;

    public bool IsPolygonSelected => SelectedDesignItem?.Config is IPolygonFrontedControlConfig;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedPolygonVertexDisplay))]
    [NotifyPropertyChangedFor(nameof(CanRemovePolygonVertex))]
    private int _selectedPolygonVertexIndex = -1;

    public string SelectedPolygonVertexDisplay =>
        SelectedDesignItem?.Config is IPolygonFrontedControlConfig polygon
        && SelectedPolygonVertexIndex >= 0
        && SelectedPolygonVertexIndex < polygon.Points.Count
            ? $"{SelectedPolygonVertexIndex + 1} / {polygon.Points.Count}"
            : $"- / {(SelectedDesignItem?.Config as IPolygonFrontedControlConfig)?.Points.Count ?? 0}";

    public bool CanRemovePolygonVertex =>
        SelectedDesignItem?.Config is IPolygonFrontedControlConfig polygon
        && polygon.Points.Count > 3
        && SelectedPolygonVertexIndex >= 0
        && SelectedPolygonVertexIndex < polygon.Points.Count;

    private string? _selectedGlobalScoreCellParentName;
    private string? _selectedGlobalScoreCellId;

    public string? SelectedGlobalScoreCellParentName
    {
        get => _selectedGlobalScoreCellParentName;
        private set
        {
            if (SetProperty(ref _selectedGlobalScoreCellParentName, value))
            {
                OnGlobalScoreCellSelectionChanged();
            }
        }
    }

    public string? SelectedGlobalScoreCellId
    {
        get => _selectedGlobalScoreCellId;
        private set
        {
            if (SetProperty(ref _selectedGlobalScoreCellId, value))
            {
                OnGlobalScoreCellSelectionChanged();
            }
        }
    }

    public GlobalScoreCellConfig? SelectedGlobalScoreCell
    {
        get => TryGetSelectedGlobalScoreCell(out _, out _, out var cell) ? cell : null;
        set
        {
            if (value is null)
            {
                ClearSelectedGlobalScoreCell();
                return;
            }

            if (SelectedDesignItem is not null)
            {
                SelectGlobalScoreCell(SelectedDesignItem, value);
            }
        }
    }

    public bool HasSelectedGlobalScoreCell => SelectedGlobalScoreCell is not null;

    public bool HasGlobalScoreCellEditor => SelectedDesignItem?.Config is GlobalScoreRowControlConfig;

    private FrontedDesignerResizeTarget _borderedImageResizeTarget = FrontedDesignerResizeTarget.Border;

    public FrontedDesignerResizeTarget BorderedImageResizeTarget
    {
        get => _borderedImageResizeTarget;
        set
        {
            if (SetProperty(ref _borderedImageResizeTarget, value))
            {
                OnPropertyChanged(nameof(IsBorderResizeTargetSelected));
                OnPropertyChanged(nameof(IsImageResizeTargetSelected));
            }
        }
    }

    public bool IsBorderResizeTargetSelected
    {
        get => BorderedImageResizeTarget == FrontedDesignerResizeTarget.Border;
        set
        {
            if (value)
            {
                BorderedImageResizeTarget = FrontedDesignerResizeTarget.Border;
            }
        }
    }

    public bool IsImageResizeTargetSelected
    {
        get => BorderedImageResizeTarget == FrontedDesignerResizeTarget.Image;
        set
        {
            if (value)
            {
                BorderedImageResizeTarget = FrontedDesignerResizeTarget.Image;
            }
        }
    }

    public bool CanDeleteSelectedControl =>
        !HasSelectedGlobalScoreCell
        && SelectedDesignItem is { IsSelectableInEditor: true, IsEditableInEditor: true };

    public bool CanCopySelectedControl => !HasSelectedGlobalScoreCell && CanCopyControl(SelectedDesignItem);

    public bool CanPasteControl => CurrentDocument is not null && _copiedControl is not null;

    public bool HasPendingScheduledDesignerWork => _scheduledValidationAndPreviewPending;

    public int ScheduledDesignerValidationExecutionCount { get; private set; }

    public int ScheduledDesignerPreviewExecutionCount { get; private set; }

    /// <summary>
    /// True while snapshot restore is updating preview visuals and selection state as one transaction.
    /// </summary>
    public bool IsRestoringSnapshotVisuals => _isRestoringSnapshot;

    public bool CanUndo => _undoStack.Count > 0;

    public bool CanRedo => _redoStack.Count > 0;

    public bool CanSaveLayout => CurrentDocument is not null && (CurrentDocument.IsDirty || AreBehaviorsDirty);

    public bool HasUnsavedChanges => CurrentDocument?.IsDirty == true || AreBehaviorsDirty;

    public bool CanResetToBuiltIn => CurrentDocument is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanReorderLayers))]
    private string _controlFilterText = string.Empty;

    public bool CanReorderLayers => CurrentDocument is not null && string.IsNullOrWhiteSpace(ControlFilterText);

    public string LayerReorderHint => CanReorderLayers
        ? string.Empty
        : I18nHelper.GetLocalizedString("Designer.LayerPanel.ClearFilterToReorder");

    [ObservableProperty]
    private string _selectedControlDisplay = string.Empty;

    [ObservableProperty]
    private string _selectedControlTypeDisplay = string.Empty;

    [ObservableProperty]
    private string _selectedControlGeometryDisplay = string.Empty;

    [ObservableProperty]
    private string _selectedControlRuntimeCriticalDisplay = string.Empty;

    [ObservableProperty]
    private int _selectedControlValidationMessageCount;

    [ObservableProperty]
    private int _errorCount;

    [ObservableProperty]
    private int _warningCount;

    [ObservableProperty]
    private int _infoCount;

    [ObservableProperty]
    private string _canvasWidthEditText = string.Empty;

    [ObservableProperty]
    private string _canvasHeightEditText = string.Empty;

    [ObservableProperty]
    private string _backgroundImageEditText = string.Empty;

    [ObservableProperty]
    private bool _enableBoModeStates;

    [ObservableProperty]
    private FrontedCanvasBoModeStateOption? _selectedBoModeStateOption;

    public bool IsBoModeStateSelectorVisible => EnableBoModeStates;

    public bool CanCopyBo5ToBo3 => CurrentDocument is not null && EnableBoModeStates;

    [ObservableProperty]
    private string _canvasPropertiesStatus = string.Empty;

    [ObservableProperty]
    private string _windowOptionsWindowTypeName = string.Empty;

    [ObservableProperty]
    private bool _windowAllowTransparency;

    [ObservableProperty]
    private string _windowBackgroundColorEditText = "#00000000";

    [ObservableProperty]
    private Color _windowBackgroundColorValue = Colors.Transparent;

    [ObservableProperty]
    private bool _windowOptionsRestartRequired;

    [ObservableProperty]
    private string _windowOptionsStatus = string.Empty;

    partial void OnSelectedWindowChanged(FrontedDesignerWindowOption? value)
    {
        ControlFilterText = string.Empty;
        SelectDesignItem(null);
        CanvasOptions.Clear();

        if (value is null)
        {
            SelectedCanvas = null;
            return;
        }

        foreach (var canvas in value.Canvases)
        {
            CanvasOptions.Add(canvas);
        }

        SelectedCanvas = CanvasOptions.FirstOrDefault();
        LoadWindowOptions(value.WindowTypeName);
    }

    partial void OnSelectedCanvasChanged(FrontedDesignerLayoutCatalogEntry? value)
    {
        ControlFilterText = string.Empty;
        SelectDesignItem(null);
    }

    partial void OnCurrentDocumentChanged(FrontedCanvasDesignDocument? value)
    {
        _propertyEditErrors.Clear();
        if (!_isRestoringSnapshot && !_preserveUndoRedoDuringDocumentSwap)
        {
            ClearUndoRedo();
        }
        NormalizeSelectionState();
        RefreshCanvasPropertyBuffers();
        OnPropertyChanged(nameof(IsBoModeStateSelectorVisible));
        OnPropertyChanged(nameof(CanCopyBo5ToBo3));
        RebuildFilteredDesignItems();
        OnPropertyChanged(nameof(CanReorderLayers));
        OnPropertyChanged(nameof(LayerReorderHint));
        RebuildPropertyEditorItems();
        UpdateFitZoomFromCurrentDocument();
        DeleteSelectedControlCommand.NotifyCanExecuteChanged();
        CopySelectedControlCommand.NotifyCanExecuteChanged();
        PasteControlCommand.NotifyCanExecuteChanged();
        NotifyLayoutCommandState();
    }

    partial void OnAreBehaviorsDirtyChanged(bool value)
    {
        RefreshDirtyState();
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    partial void OnSnapEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(EffectiveSnapEnabled));
        OnPropertyChanged(nameof(SnapStatusText));
        if (!EffectiveSnapEnabled)
        {
            ClearActiveSnapGuides();
        }
    }

    partial void OnIsShiftSnapActiveChanged(bool value)
    {
        OnPropertyChanged(nameof(EffectiveSnapEnabled));
        OnPropertyChanged(nameof(SnapStatusText));
        if (!EffectiveSnapEnabled)
        {
            ClearActiveSnapGuides();
        }
    }

    partial void OnZoomScaleChanged(double value)
    {
        OnPropertyChanged(nameof(ZoomPercent));
        if (!_suppressZoomEditTextUpdate)
        {
            UpdateZoomEditTextFromCurrentZoom();
        }
    }

    partial void OnIsFitModeChanged(bool value)
    {
        if (!_suppressZoomEditTextUpdate)
        {
            UpdateZoomEditTextFromCurrentZoom();
        }
    }

    partial void OnSelectedDesignItemChanged(FrontedControlDesignItem? value)
    {
        ClearActiveSnapGuides();
        _propertyEditErrors.Clear();
        _propertyEditBuffers.Clear();
        ClearSelectedGlobalScoreCell();
        SelectedPolygonVertexIndex = value?.Config is IPolygonFrontedControlConfig polygon && polygon.Points.Count > 0
            ? 0
            : -1;
        if (_lastSelectedDesignItem is not null && !ReferenceEquals(_lastSelectedDesignItem, value))
        {
            _lastSelectedDesignItem.IsSelected = false;
        }

        if (value is not null)
        {
            value.IsSelected = true;
        }

        _lastSelectedDesignItem = value;

        BehaviorPanel.SetSelectedControl(value);
        RefreshSelectedControlDisplay();
        RebuildGlobalScoreCellEditorItems();
        RebuildPropertyEditorItems();
        RefreshLayerNodeSelection();
        DeleteSelectedControlCommand.NotifyCanExecuteChanged();
        CopySelectedControlCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(IsBorderedImageSelected));
        OnPropertyChanged(nameof(IsBorderResizeTargetSelected));
        OnPropertyChanged(nameof(IsImageResizeTargetSelected));
        OnPropertyChanged(nameof(HasGlobalScoreCellEditor));
        OnPropertyChanged(nameof(IsMapV2DisplaySelected));
        OnPropertyChanged(nameof(IsPolygonSelected));
        OnPropertyChanged(nameof(SelectedPolygonVertexDisplay));
        OnPropertyChanged(nameof(CanRemovePolygonVertex));
        RemovePolygonVertexCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedPolygonVertexIndexChanged(int value)
    {
        RemovePolygonVertexCommand.NotifyCanExecuteChanged();
    }

    private void OnGlobalScoreCellSelectionChanged()
    {
        if (SelectedGlobalScoreCellParentName is not null
            && SelectedDesignItem is not null
            && !string.Equals(SelectedDesignItem.Name, SelectedGlobalScoreCellParentName, StringComparison.Ordinal))
        {
            var parent = CurrentDocument?.Controls.FirstOrDefault(control =>
                string.Equals(control.Name, SelectedGlobalScoreCellParentName, StringComparison.Ordinal));
            if (parent is not null)
            {
                SelectedDesignItem = parent;
            }
        }

        if (SelectedGlobalScoreCellParentName is not null
            && !TryGetSelectedGlobalScoreCell(out _, out _, out _))
        {
            ClearSelectedGlobalScoreCell(notify: false);
        }

        _propertyEditErrors.Clear();
        _propertyEditBuffers.Clear();
        OnPropertyChanged(nameof(SelectedGlobalScoreCell));
        OnPropertyChanged(nameof(HasSelectedGlobalScoreCell));
        RefreshSelectedControlDisplay();
        RebuildPropertyEditorItems();
        RefreshLayerNodeSelection();
        DeleteSelectedControlCommand.NotifyCanExecuteChanged();
        CopySelectedControlCommand.NotifyCanExecuteChanged();
    }

    partial void OnControlFilterTextChanged(string value)
    {
        var clamped = FrontedTextLimitHelper.Clamp(value, FrontedLayoutLimits.MaxSearchTextLength);
        if (!string.Equals(value, clamped, StringComparison.Ordinal))
        {
            ControlFilterText = clamped;
            StatusMessage = I18nHelper.GetLocalizedString("InputTruncated");
            return;
        }

        RebuildFilteredDesignItems();
        OnPropertyChanged(nameof(CanReorderLayers));
        OnPropertyChanged(nameof(LayerReorderHint));
    }

    partial void OnSelectedZoomPresetChanged(FrontedDesignerZoomPreset? value)
    {
        if (_isChangingZoomPreset || value is null)
        {
            return;
        }

        ApplyZoomPreset(value);
    }

    partial void OnWindowAllowTransparencyChanged(bool value)
    {
        if (_isLoadingWindowOptions || _windowLayoutOptionsService is null || SelectedWindow is null)
        {
            return;
        }

        _ = SaveWindowOptionsAsync(restartRequired: true, applyBackgroundImmediately: false);
    }

    partial void OnWindowBackgroundColorEditTextChanged(string value)
    {
        if (FrontedPropertyColorHelper.TryParseArgbColor(value, out var color))
        {
            WindowBackgroundColorValue = color;
        }
    }

    partial void OnWindowBackgroundColorValueChanged(Color value)
    {
        WindowBackgroundColorEditText = FrontedPropertyColorHelper.ToArgbString(value);
    }

    partial void OnEnableBoModeStatesChanged(bool value)
    {
        OnPropertyChanged(nameof(IsBoModeStateSelectorVisible));
        OnPropertyChanged(nameof(CanCopyBo5ToBo3));
        CopyBo5ToBo3Command.NotifyCanExecuteChanged();

        if (_isUpdatingBoModeStateUi || CurrentDocument is null)
        {
            return;
        }

        CaptureUndoSnapshot();
        var config = _designConverter.ToConfig(CurrentDocument);
        config.EnableBoModeStates = value;
        if (value)
        {
            EnsureBo3State(config);
        }

        var nextState = value ? CurrentDocument.EditingBoModeState : FrontedCanvasBoModeState.Bo5;
        RebuildDocumentFromConfig(config, nextState, preserveDirty: true, selectedControlName: SelectedDesignItem?.Name);
        CanvasPropertiesStatus = value
            ? I18nHelper.GetLocalizedString("Designer.Canvas.BoModeStatesEnabled")
            : I18nHelper.GetLocalizedString("Designer.Canvas.BoModeStatesDisabledConfirm");
    }

    partial void OnSelectedBoModeStateOptionChanged(FrontedCanvasBoModeStateOption? value)
    {
        if (_isUpdatingBoModeStateUi || CurrentDocument is null || value is null)
        {
            return;
        }

        if (CurrentDocument.EditingBoModeState == value.State)
        {
            return;
        }

        var config = _designConverter.ToConfig(CurrentDocument);
        RebuildDocumentFromConfig(
            config,
            value.State,
            preserveDirty: CurrentDocument.IsDirty,
            selectedControlName: SelectedDesignItem?.Name);
    }

    [RelayCommand]
    private async Task ReloadLayoutAsync()
    {
        await ReloadLayoutCoreAsync();
    }

    public async Task ReloadLayoutCoreAsync()
    {
        if (SelectedWindow is null || SelectedCanvas is null)
        {
            ClearLoadedLayout(CreateMessage(
                FrontedLayoutValidationSeverity.Error,
                "LayoutSelectionMissing",
                "Window and Canvas selection are required."));
            return;
        }

        var entry = SelectedCanvas;
        CurrentWindowCanvasDisplay = $"{entry.DisplayName} / {entry.CanvasDisplayName}";
        DirtyIndicatorText = string.Empty;
        var reloadVersion = StartReloadLayoutRequest();
        var cancellationToken = _reloadLayoutCancellation?.Token ?? CancellationToken.None;

        try
        {
            var loadResult = await _layoutService.LoadCanvasConfigWithMetadataAsync(
                entry.WindowTypeName,
                entry.CanvasName,
                cancellationToken);
            if (cancellationToken.IsCancellationRequested || reloadVersion != _reloadLayoutVersion)
            {
                return;
            }

            ApplyLayoutSource(loadResult, entry);

            var config = loadResult.Config;
            if (config is null)
            {
                ClearLoadedLayout(CreateMessage(
                    FrontedLayoutValidationSeverity.Error,
                    "MissingLayout",
                    loadResult.Error ?? $"Layout file was not found for {entry.WindowTypeName}/{entry.CanvasName}."));
                return;
            }

            var document = _designConverter.FromConfig(
                entry.WindowTypeName,
                entry.CanvasName,
                config,
                _runtimeContracts);

            ControlFilterText = string.Empty;
            CurrentDocument = document;
            CurrentDocument.IsDirty = false;
            var behaviorDocument = await _behaviorService.LoadDocumentAsync(
                entry.WindowTypeName,
                entry.CanvasName,
                cancellationToken);
            if (cancellationToken.IsCancellationRequested || reloadVersion != _reloadLayoutVersion)
            {
                return;
            }

            ResetBehaviorDocument(behaviorDocument);
            SelectDesignItem(null);
            var validationMessages = _validator.Validate(document).ToList();
            if (!string.IsNullOrWhiteSpace(loadResult.Error))
            {
                validationMessages.Add(CreateMessage(
                    FrontedLayoutValidationSeverity.Warning,
                    "UserLayoutLoadFailed",
                    loadResult.Error));
            }

            ApplyValidationMessages(validationMessages);
            RequestPreviewRender(config, entry);
            RefreshDirtyState();
        }
        catch (OperationCanceledException)
        {
            // A newer window/canvas selection superseded this load request.
        }
        catch (Exception ex)
        {
            if (reloadVersion != _reloadLayoutVersion)
            {
                return;
            }

            _logger.LogError(
                ex,
                "Failed to load fronted designer layout. Window: {WindowTypeName}, Canvas: {CanvasName}",
                entry.WindowTypeName,
                entry.CanvasName);

            ClearLoadedLayout(CreateMessage(
                FrontedLayoutValidationSeverity.Error,
                "LayoutLoadFailed",
                ex.Message));
        }
    }

    [RelayCommand(CanExecute = nameof(CanSaveLayout))]
    private async Task SaveLayoutAsync()
    {
        await SaveCurrentLayoutAsync();
    }

    public async Task<bool> SaveCurrentLayoutAsync()
    {
        if (CurrentDocument is null)
        {
            return false;
        }

        var shouldSaveLayout = CurrentDocument.IsDirty;
        var shouldSaveBehaviors = AreBehaviorsDirty;
        if (!shouldSaveLayout && !shouldSaveBehaviors)
        {
            var messages = _validator.Validate(CurrentDocument);
            ApplyValidationMessages(messages);
            if (messages.Any(message => message.Severity == FrontedLayoutValidationSeverity.Error))
            {
                StatusMessage = I18nHelper.GetLocalizedString("CannotSaveInvalidLayout");
                return false;
            }

            return true;
        }

        if (shouldSaveLayout)
        {
            var messages = _validator.Validate(CurrentDocument);
            ApplyValidationMessages(messages);
            if (messages.Any(message => message.Severity == FrontedLayoutValidationSeverity.Error))
            {
                StatusMessage = I18nHelper.GetLocalizedString("CannotSaveInvalidLayout");
                return false;
            }
        }

        try
        {
            var wasBuiltInSource = string.Equals(
                LayoutSourceDisplay,
                I18nHelper.GetLocalizedString("LayoutSourceBuiltIn"),
                StringComparison.Ordinal);

            if (shouldSaveLayout)
            {
                var config = _designConverter.ToConfig(CurrentDocument);
                config.Version = 3;
                await _layoutService.SaveCanvasConfigAsync(
                    CurrentDocument.WindowTypeName,
                    CurrentDocument.CanvasName,
                    config);

                CleanupPendingImportedResources(includeCurrentDocument: true);
                CurrentDocument.IsDirty = false;
            }

            if (shouldSaveBehaviors)
            {
                BehaviorPanel.CurrentDocument.WindowType = CurrentDocument.WindowTypeName;
                BehaviorPanel.CurrentDocument.CanvasName = CurrentDocument.CanvasName;
                await _behaviorService.SaveDocumentAsync(BehaviorPanel.CurrentDocument);
                AreBehaviorsDirty = false;
            }

            if (shouldSaveLayout || wasBuiltInSource)
            {
                var savedResult = await _layoutService.LoadCanvasConfigWithMetadataAsync(
                    CurrentDocument.WindowTypeName,
                    CurrentDocument.CanvasName);
                if (SelectedCanvas is not null)
                {
                    ApplyLayoutSource(savedResult, SelectedCanvas);
                }
                else
                {
                    LayoutSourceDisplay = I18nHelper.GetLocalizedString("LayoutSourceUser");
                    LayoutSourcePath = savedResult.Path ?? string.Empty;
                }
            }

            StatusMessage = wasBuiltInSource
                ? I18nHelper.GetLocalizedString("EditableLayoutSchemeCreated")
                : I18nHelper.GetLocalizedString("LayoutSaved");
            if (wasBuiltInSource)
            {
                WeakReferenceMessenger.Default.Send(new FrontedLayoutPackagesChangedMessage(this, null));
            }

            RefreshDirtyState();

            if (shouldSaveLayout && _frontedWindowService is not null)
            {
                await _frontedWindowService.ReloadFrontedLayoutsAsync();
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to save fronted designer user layout. Window: {WindowTypeName}, Canvas: {CanvasName}",
                CurrentDocument.WindowTypeName,
                CurrentDocument.CanvasName);
            StatusMessage = $"{I18nHelper.GetLocalizedString("LayoutSaveFailed")}: {ex.Message}";
            return false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanResetToBuiltIn))]
    private async Task ResetToBuiltInAsync()
    {
        await ResetToBuiltInCoreAsync();
    }

    public async Task<bool> ResetToBuiltInCoreAsync()
    {
        if (CurrentDocument is null)
        {
            return false;
        }

        var windowTypeName = CurrentDocument.WindowTypeName;
        var canvasName = CurrentDocument.CanvasName;
        await _layoutService.DeleteUserLayoutAsync(windowTypeName, canvasName);

        var config = await LoadBuiltInLayoutForResetAsync(windowTypeName, canvasName);
        if (config is null)
        {
            StatusMessage = I18nHelper.GetLocalizedString("MissingLayout");
            return false;
        }

        var document = _designConverter.FromConfig(
            windowTypeName,
            canvasName,
            config,
            _runtimeContracts);
        document.IsDirty = false;

        ControlFilterText = string.Empty;
        CurrentDocument = document;
        ResetBehaviorDocument(new FrontedBehaviorDocument
        {
            Version = 1,
            WindowType = windowTypeName,
            CanvasName = canvasName
        });
        SelectDesignItem(null);
        ApplyValidationMessages(_validator.Validate(document));
        RequestPreviewRender(config, SelectedCanvas);
        LayoutSourceDisplay = I18nHelper.GetLocalizedString("LayoutSourceBuiltIn");
        LayoutSourcePath = _layoutService.GetBuiltInDefaultLayoutPath(windowTypeName, canvasName);
        StatusMessage = I18nHelper.GetLocalizedString("LayoutReset");
        ClearUndoRedo();
        CleanupPendingImportedResources(includeCurrentDocument: false);
        RefreshDirtyState();
        return true;
    }

    public void UpdateShiftSnapActive(bool isActive)
    {
        IsShiftSnapActive = isActive;
    }

    [RelayCommand]
    private void ValidateLayout()
    {
        if (CurrentDocument is null)
        {
            ApplyValidationMessages(_lastValidationMessages);
            return;
        }

        ValidateCurrentDocument();
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        if (CurrentDocument is null || _undoStack.Count == 0)
        {
            StatusMessage = I18nHelper.GetLocalizedString("CannotUndo");
            return;
        }

        var total = StartDesignerPerfTrace();
        LogDesignerPerf("Undo", "start");
        var currentSnapshot = CreateSnapshot();
        LogDesignerPerf("Undo", "create current snapshot", Elapsed(total));
        if (currentSnapshot is not null)
        {
            PushRedoSnapshot(currentSnapshot);
        }

        RestoreSnapshot(
            _undoStack.Pop(),
            FrontedDesignerSnapshotRestoreMode.PreferGeometryFastPathThenScheduledAtomicPreview,
            "Undo");
        StatusMessage = I18nHelper.GetLocalizedString("Undo");
        LogDesignerPerf("Undo", "total", Elapsed(total));
    }

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        if (CurrentDocument is null || _redoStack.Count == 0)
        {
            StatusMessage = I18nHelper.GetLocalizedString("CannotRedo");
            return;
        }

        var total = StartDesignerPerfTrace();
        LogDesignerPerf("Redo", "start");
        var currentSnapshot = CreateSnapshot();
        LogDesignerPerf("Redo", "create current snapshot", Elapsed(total));
        if (currentSnapshot is not null)
        {
            PushUndoSnapshot(currentSnapshot);
        }

        RestoreSnapshot(
            _redoStack.Pop(),
            FrontedDesignerSnapshotRestoreMode.PreferGeometryFastPathThenScheduledAtomicPreview,
            "Redo");
        StatusMessage = I18nHelper.GetLocalizedString("Redo");
        LogDesignerPerf("Redo", "total", Elapsed(total));
    }

    [RelayCommand]
    private void ZoomIn()
    {
        ApplyManualZoom(GetNextManualZoom(ZoomScale));
    }

    [RelayCommand]
    private void ZoomOut()
    {
        ApplyManualZoom(GetPreviousManualZoom(ZoomScale));
    }

    [RelayCommand]
    private void FitToWindow()
    {
        var fitPreset = ZoomPresets.FirstOrDefault(preset => preset.IsFit);
        if (fitPreset is not null)
        {
            ApplyZoomPreset(fitPreset);
        }
    }

    [RelayCommand]
    private void SetZoomPreset(object? parameter)
    {
        var displayName = Convert.ToString(parameter, CultureInfo.InvariantCulture);
        var preset = ZoomPresets.FirstOrDefault(item =>
            string.Equals(item.DisplayName, displayName, StringComparison.OrdinalIgnoreCase));
        if (preset is not null)
        {
            ApplyZoomPreset(preset);
        }
    }

    [RelayCommand]
    private void AddControl(object? parameter)
    {
        if (CurrentDocument is null)
        {
            StatusMessage = I18nHelper.GetLocalizedString("CannotAddControl");
            return;
        }

        var request = parameter as FrontedAddControlRequest;
        var controlType = request?.ControlType ?? Convert.ToString(parameter, CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(controlType) || !_defaultConfigFactory.CanCreate(controlType))
        {
            StatusMessage = I18nHelper.GetLocalizedString("UnsupportedControlType");
            return;
        }

        if (CurrentDocument.Controls.Count >= FrontedLayoutLimits.MaxControlsPerCanvas)
        {
            StatusMessage = I18nHelper.GetLocalizedString("ControlCountLimitReached");
            return;
        }

        CaptureUndoSnapshot();
        var config = _defaultConfigFactory.Create(
            controlType,
            CurrentDocument,
            request?.CenterX,
            request?.CenterY);
        var item = new FrontedControlDesignItem
        {
            Name = _controlNameGenerator.Generate(GetNameSeed(controlType), CurrentDocument),
            Config = config,
            IsSelectableInEditor = true,
            IsEditableInEditor = true
        };

        item.IsRuntimeCritical = _runtimeContracts.IsRuntimeCritical(
            CurrentDocument.WindowTypeName,
            CurrentDocument.CanvasName,
            item.Name);

        CurrentDocument.Controls.Add(item);
        CurrentDocument.IsDirty = true;
        RefreshDirtyState();
        ControlFilterText = string.Empty;
        RebuildFilteredDesignItems();
        SelectDesignItem(item);
        ValidateCurrentDocument();
        RequestPreviewRenderCurrentDocument();
        StatusMessage = $"{I18nHelper.GetLocalizedString("AddedControl")}: {item.Name}";
    }

    [RelayCommand(CanExecute = nameof(CanCopySelectedControl))]
    private void CopySelectedControl()
    {
        var selected = SelectedDesignItem;
        if (selected is null || !CanCopyControl(selected))
        {
            StatusMessage = I18nHelper.GetLocalizedString("CannotCopyControl");
            return;
        }

        _copiedControl = FrontedDesignerClipboardPayload.Create(selected);
        PasteControlCommand.NotifyCanExecuteChanged();
        StatusMessage = I18nHelper.GetLocalizedString("CopyControl");
    }

    [RelayCommand(CanExecute = nameof(CanPasteControl))]
    private void PasteControl()
    {
        if (CurrentDocument is null || _copiedControl is null)
        {
            StatusMessage = I18nHelper.GetLocalizedString("CannotPasteControl");
            return;
        }

        if (CurrentDocument.Controls.Count + 1 > FrontedLayoutLimits.MaxControlsPerCanvas)
        {
            StatusMessage = I18nHelper.GetLocalizedString("ControlCountLimitReached");
            return;
        }

        var copiedControl = _copiedControl;
        if (copiedControl is null)
        {
            StatusMessage = I18nHelper.GetLocalizedString("CannotPasteControl");
            return;
        }

        var total = StartDesignerPerfTrace();
        LogDesignerPerf("Paste", "start");
        var clonedConfig = copiedControl.CreateConfig();
        clonedConfig.BehaviorGuid = FrontedBehaviorGuidHelper.NewGuid();
        LogDesignerPerf("Paste", "clone config", Elapsed(total));
        clonedConfig.Left += 10D;
        clonedConfig.Top += 10D;
        clonedConfig.ZIndex = CurrentDocument.Controls.Count == 0
            ? clonedConfig.ZIndex
            : CurrentDocument.Controls.Max(control => control.Config.ZIndex) + 1;

        var item = new FrontedControlDesignItem
        {
            Name = GeneratePasteName(copiedControl.SourceName, copiedControl.ControlType, CurrentDocument),
            Config = clonedConfig,
            IsSelectableInEditor = true,
            IsEditableInEditor = true
        };
        LogDesignerPerf("Paste", "name/z-index preparation", Elapsed(total));

        CaptureUndoSnapshot();
        LogDesignerPerf("Paste", "undo snapshot capture", Elapsed(total));
        CurrentDocument.Controls.Add(item);
        LogDesignerPerf("Paste", "add control", Elapsed(total));
        CurrentDocument.IsDirty = true;
        RefreshDirtyState();
        AddFilteredDesignItemIfVisible(item);
        LogDesignerPerf("Paste", "filtered list update", Elapsed(total));
        SelectDesignItem(item);
        LogDesignerPerf("Paste", "selection update", Elapsed(total));
        ScheduleValidationAndPreviewRender("Paste");
        LogDesignerPerf("Paste", "validation scheduling", Elapsed(total));
        LogDesignerPerf("Paste", "preview render scheduling", Elapsed(total));
        StatusMessage = $"{I18nHelper.GetLocalizedString("PasteControl")}: {item.Name}";
        LogDesignerPerf("Paste", "total", Elapsed(total));
    }

    [RelayCommand(CanExecute = nameof(CanDeleteSelectedControl))]
    private void DeleteSelectedControl()
    {
        if (CurrentDocument is null || SelectedDesignItem is null)
        {
            return;
        }

        if (SelectedDesignItem.IsRuntimeCritical)
        {
            StatusMessage = I18nHelper.GetLocalizedString("CannotDeleteRuntimeCriticalControl");
            return;
        }

        if (!SelectedDesignItem.IsEditableInEditor || !SelectedDesignItem.IsSelectableInEditor)
        {
            StatusMessage = I18nHelper.GetLocalizedString("CannotDeleteRuntimeCriticalControl");
            return;
        }

        _referenceScanner.SetControls(CurrentDocument.Controls);
        if (_referenceScanner.GetIncomingReferences(SelectedDesignItem.Name).Count > 0)
        {
            StatusMessage = I18nHelper.GetLocalizedString("CannotDeleteReferencedControl");
            return;
        }

        var total = StartDesignerPerfTrace();
        LogDesignerPerf("Delete", "start");
        CaptureUndoSnapshot();
        LogDesignerPerf("Delete", "undo snapshot capture", Elapsed(total));
        var deletedName = SelectedDesignItem.Name;
        var deletedItem = SelectedDesignItem;
        var deletedBehaviorGuid = deletedItem.Config.BehaviorGuid;
        CurrentDocument.Controls.Remove(SelectedDesignItem);
        if (deletedBehaviorGuid != Guid.Empty)
        {
            BehaviorPanel.RemoveBehaviors(deletedBehaviorGuid);
            _behaviorService.RemoveBehaviors(deletedBehaviorGuid);
        }

        LogDesignerPerf("Delete", "remove control", Elapsed(total));
        CurrentDocument.IsDirty = true;
        RefreshDirtyState();
        SelectDesignItem(null);
        RemoveFilteredDesignItem(deletedItem);
        LogDesignerPerf("Delete", "filtered list update", Elapsed(total));
        RebuildPropertyEditorItems();
        LogDesignerPerf("Delete", "selection/property update", Elapsed(total));
        ScheduleValidationAndPreviewRender("Delete");
        LogDesignerPerf("Delete", "validation scheduling", Elapsed(total));
        LogDesignerPerf("Delete", "preview render scheduling", Elapsed(total));
        StatusMessage = $"{I18nHelper.GetLocalizedString("DeleteSelectedControl")}: {deletedName}";
        LogDesignerPerf("Delete", "total", Elapsed(total));
    }

    [RelayCommand]
    private void FillMissingGlobalScoreCells()
    {
        if (CurrentDocument is null || SelectedDesignItem?.Config is not GlobalScoreRowControlConfig row)
        {
            return;
        }

        CaptureUndoSnapshot();
        GlobalScoreRowCellLayoutHelper.EnsureCompleteCells(row, CurrentDocument.EditingBoModeState == FrontedCanvasBoModeState.Bo3);
        FinishGlobalScoreRowAction();
    }

    [RelayCommand]
    private void AutoArrangeGlobalScoreCellsBySpacing()
    {
        if (CurrentDocument is null || SelectedDesignItem?.Config is not GlobalScoreRowControlConfig row)
        {
            return;
        }

        CaptureUndoSnapshot();
        GlobalScoreRowCellLayoutHelper.AutoArrangeBySpacing(
            row,
            CurrentDocument.EditingBoModeState == FrontedCanvasBoModeState.Bo3);
        FinishGlobalScoreRowAction();
    }

    [RelayCommand]
    private void ApplyBo3GlobalScoreVisibilityTemplate()
    {
        if (CurrentDocument is null || SelectedDesignItem?.Config is not GlobalScoreRowControlConfig row)
        {
            return;
        }

        CaptureUndoSnapshot();
        GlobalScoreRowCellLayoutHelper.ApplyBo3VisibilityTemplate(row);
        FinishGlobalScoreRowAction();
    }

    [RelayCommand]
    private void ApplyBo5GlobalScoreVisibilityTemplate()
    {
        if (CurrentDocument is null || SelectedDesignItem?.Config is not GlobalScoreRowControlConfig row)
        {
            return;
        }

        CaptureUndoSnapshot();
        GlobalScoreRowCellLayoutHelper.ApplyBo5VisibilityTemplate(row);
        FinishGlobalScoreRowAction();
    }

    [RelayCommand]
    private void ApplyParentStyleToGlobalScoreCells()
    {
        if (CurrentDocument is null || SelectedDesignItem?.Config is not GlobalScoreRowControlConfig row)
        {
            return;
        }

        CaptureUndoSnapshot();
        GlobalScoreRowCellLayoutHelper.EnsureCompleteCells(row, CurrentDocument.EditingBoModeState == FrontedCanvasBoModeState.Bo3);
        foreach (var cell in row.Cells)
        {
            cell.FontFamily = row.FontFamily;
            cell.FontWeight = row.FontWeight;
            cell.Color = row.Color;
            cell.FontSize = row.FontSize;
            cell.ShowCampIcon = row.ShowCampIcon;
        }

        FinishGlobalScoreRowAction();
    }

    [RelayCommand]
    private void ClearGlobalScoreCellStyleOverrides()
    {
        if (CurrentDocument is null || SelectedDesignItem?.Config is not GlobalScoreRowControlConfig row)
        {
            return;
        }

        CaptureUndoSnapshot();
        foreach (var cell in row.Cells)
        {
            cell.FontFamily = null;
            cell.FontWeight = null;
            cell.Color = null;
            cell.FontSize = null;
            cell.ShowCampIcon = null;
        }

        FinishGlobalScoreRowAction();
    }

    private void FinishGlobalScoreRowAction()
    {
        if (CurrentDocument is null)
        {
            return;
        }

        CurrentDocument.IsDirty = true;
        RebuildGlobalScoreCellEditorItems();
        RefreshGlobalScoreCellSelection();
        RebuildPropertyEditorItems();
        RefreshDirtyState();
        RequestPreviewRenderCurrentDocument();
    }

    [RelayCommand]
    private void ApplyMapV2DisplayStyleToAll()
    {
        if (CurrentDocument is null || SelectedDesignItem?.Config is not MapV2DisplayControlConfig source)
        {
            return;
        }

        var targets = CurrentDocument.Controls
            .Where(item => !ReferenceEquals(item, SelectedDesignItem))
            .Select(item => item.Config)
            .OfType<MapV2DisplayControlConfig>()
            .ToArray();
        if (targets.Length == 0)
        {
            return;
        }

        CaptureUndoSnapshot();
        foreach (var target in targets)
        {
            CopyMapV2DisplayStyle(source, target);
        }

        CurrentDocument.IsDirty = true;
        RebuildPropertyEditorItems();
        RefreshDirtyState();
        ValidateCurrentDocument();
        RequestPreviewRenderCurrentDocument();
        StatusMessage = I18nHelper.GetLocalizedString("Designer.MapV2Display.StyleAppliedToAll");
    }

    private static void CopyMapV2DisplayStyle(MapV2DisplayControlConfig source, MapV2DisplayControlConfig target)
    {
        target.Width = source.Width;
        target.Height = source.Height;
        target.MapNameFontFamily = source.MapNameFontFamily;
        target.MapNameFontWeight = source.MapNameFontWeight;
        target.MapNameColor = source.MapNameColor;
        target.MapNameFontSize = source.MapNameFontSize;
        target.TeamNameFontFamily = source.TeamNameFontFamily;
        target.TeamNameFontWeight = source.TeamNameFontWeight;
        target.TeamNameColor = source.TeamNameColor;
        target.TeamNameFontSize = source.TeamNameFontSize;
        target.CampNameFontFamily = source.CampNameFontFamily;
        target.CampNameFontWeight = source.CampNameFontWeight;
        target.CampNameColor = source.CampNameColor;
        target.CampNameFontSize = source.CampNameFontSize;
        target.MapBorderNormalColor = source.MapBorderNormalColor;
        target.MapBorderBannedColor = source.MapBorderBannedColor;
        target.PickingBorderImagePath = source.PickingBorderImagePath;
        target.PickingBorderFillColor = source.PickingBorderFillColor;
    }

    [RelayCommand]
    private void ApplyCanvasSize()
    {
        ApplyCanvasSizeEdit(CanvasWidthEditText, CanvasHeightEditText);
    }

    public bool ApplyCanvasSizeEdit(string widthText, string heightText)
    {
        if (CurrentDocument is null)
        {
            return false;
        }

        if (!TryParsePositiveDouble(widthText, out var width)
            || !TryParsePositiveDouble(heightText, out var height))
        {
            CanvasPropertiesStatus = I18nHelper.GetLocalizedString("CanvasSizeMustBePositive");
            return false;
        }

        if (Math.Abs(CurrentDocument.CanvasConfig.CanvasWidth - width) < 0.0001D
            && Math.Abs(CurrentDocument.CanvasConfig.CanvasHeight - height) < 0.0001D)
        {
            RefreshCanvasPropertyBuffers();
            return true;
        }

        CaptureUndoSnapshot();
        CurrentDocument.CanvasConfig.CanvasWidth = width;
        CurrentDocument.CanvasConfig.CanvasHeight = height;
        CurrentDocument.IsDirty = true;
        RefreshCanvasPropertyBuffers();
        FinishCanvasConfigEdit(I18nHelper.GetLocalizedString("CanvasPropertiesApplied"));
        return true;
    }

    [RelayCommand]
    private void ApplyBackgroundImage()
    {
        ApplyCanvasBackgroundEdit(BackgroundImageEditText);
    }

    public bool ApplyCanvasBackgroundEdit(string? backgroundImage)
    {
        if (CurrentDocument is null)
        {
            return false;
        }

        if (IsAbsoluteFilePath(backgroundImage))
        {
            return StoreLocalBackgroundImage(backgroundImage!);
        }

        var rawValue = string.IsNullOrWhiteSpace(backgroundImage) ? null : backgroundImage.Trim();
        var normalizedValue = rawValue is null
            ? null
            : FrontedTextLimitHelper.Clamp(rawValue, FrontedLayoutLimits.MaxResourcePathLength);
        if (!string.Equals(rawValue, normalizedValue, StringComparison.Ordinal))
        {
            CanvasPropertiesStatus = I18nHelper.GetLocalizedString("InputTruncated");
        }

        if (string.Equals(GetEditingStateBackground(CurrentDocument), normalizedValue, StringComparison.Ordinal))
        {
            BackgroundImageEditText = normalizedValue ?? string.Empty;
            return true;
        }

        CaptureUndoSnapshot();
        SetEditingStateBackground(CurrentDocument, normalizedValue);
        CurrentDocument.IsDirty = true;
        BackgroundImageEditText = normalizedValue ?? string.Empty;
        FinishCanvasConfigEdit(I18nHelper.GetLocalizedString("CanvasPropertiesApplied"));
        return true;
    }

    public bool ApplyCanvasBackgroundResourceSelection(string selectedResourcePath)
    {
        return IsAbsoluteFilePath(selectedResourcePath)
            ? StoreLocalBackgroundImage(selectedResourcePath)
            : ApplyCanvasBackgroundEdit(selectedResourcePath);
    }

    [RelayCommand]
    private void ClearBackgroundImage()
    {
        ClearCanvasBackground();
    }

    public bool ClearCanvasBackground()
    {
        return ApplyCanvasBackgroundEdit(null);
    }

    public bool StoreLocalBackgroundImage(string sourcePath)
    {
        if (_localResourceStore is null)
        {
            return false;
        }

        try
        {
            var result = _localResourceStore.StoreImageWithResult(sourcePath);
            var applied = ApplyCanvasBackgroundEdit(result.ResourceUri);
            RecordPendingImportedResource(result, "Canvas BackgroundImage", applied);
            return applied;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to store local fronted canvas background image.");
            CanvasPropertiesStatus = $"{I18nHelper.GetLocalizedString("FailedToApplyPicture")}: {ex.Message}";
            return false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCopyBo5ToBo3))]
    private void CopyBo5ToBo3()
    {
        if (CurrentDocument is null)
        {
            return;
        }

        CaptureUndoSnapshot();
        var config = _designConverter.ToConfig(CurrentDocument);
        EnsureBo3State(config);
        config.EnableBoModeStates = true;
        config.BoModeStates[FrontedCanvasRuntimeStateResolver.Bo3StateKey] = new FrontedCanvasStateConfig
        {
            BackgroundImage = config.BackgroundImage,
            RequiredPlugins = DeepClone(config.RequiredPlugins),
            Controls = CloneControls(config.Controls)
        };

        RebuildDocumentFromConfig(
            config,
            FrontedCanvasBoModeState.Bo3,
            preserveDirty: true,
            selectedControlName: SelectedDesignItem?.Name);
        CanvasPropertiesStatus = I18nHelper.GetLocalizedString("Designer.Canvas.Bo3LayoutCopied");
    }

    public bool ApplyPropertyResourceSelection(FrontedPropertyEditorItem item, string selectedResourcePath)
    {
        if (IsAbsoluteFilePath(selectedResourcePath))
        {
            if (_localResourceStore is null)
            {
                return false;
            }

            try
            {
                var result = _localResourceStore.StoreImageWithResult(selectedResourcePath);
                var applied = ApplyPropertyEdit(item, result.ResourceUri);
                RecordPendingImportedResource(result, item.PropertyName, applied);
                return applied;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to store local fronted control resource for property {PropertyName}.",
                    item.PropertyName);
                SetPropertyEditError(
                    item,
                    $"{I18nHelper.GetLocalizedString("FailedToApplyPicture")}: {ex.Message}",
                    selectedResourcePath);
                return false;
            }
        }

        return ApplyPropertyEdit(item, selectedResourcePath);
    }

    [RelayCommand]
    private void ResetWindowOptions()
    {
        if (_windowLayoutOptionsService is null || SelectedWindow is null)
        {
            return;
        }

        _ = ResetWindowOptionsAsync();
    }

    /// <summary>
    /// Adds a non-fatal render error to the validation/status panel.
    /// </summary>
    public void ReportRenderFailure(Exception exception)
    {
        _logger.LogError(exception, "Failed to render fronted designer preview.");

        var messages = _lastValidationMessages
            .Concat(
            [
                CreateMessage(
                    FrontedLayoutValidationSeverity.Error,
                    "RenderFailed",
                    exception.Message)
            ])
            .ToArray();

        ApplyValidationMessages(messages);
        StatusMessage = exception.Message;
    }

    public void SelectDesignItem(FrontedControlDesignItem? item)
    {
        if (item?.IsSelectableInEditor == false)
        {
            item = null;
        }

        SelectedDesignItem = item;
        ClearSelectedGlobalScoreCell();
    }

    public void SelectGlobalScoreCell(FrontedControlDesignItem parent, GlobalScoreCellConfig? cell)
    {
        if (parent.Config is not GlobalScoreRowControlConfig row || cell is not null && !row.Cells.Contains(cell))
        {
            return;
        }

        if (!ReferenceEquals(SelectedDesignItem, parent))
        {
            SelectedDesignItem = parent;
        }

        if (cell is null)
        {
            ClearSelectedGlobalScoreCell();
            return;
        }

        RestoreGlobalScoreCellSelectionKeys(parent.Name, cell.Id);
        OnGlobalScoreCellSelectionChanged();
    }

    public void SelectLayerNode(DesignerLayerNode? node)
    {
        if (node is null || !node.CanSelect)
        {
            ClearSelection();
            return;
        }

        SelectDesignItem(node.ControlItem);
    }

    public void ToggleLayerNodeExpansion(DesignerLayerNode node)
    {
    }

    public void ClearSelection()
    {
        ClearSelectedGlobalScoreCell();
        SelectDesignItem(null);
    }

    public bool TryGetSelectedGlobalScoreCell(
        out FrontedControlDesignItem parentItem,
        out GlobalScoreRowControlConfig row,
        out GlobalScoreCellConfig cell)
    {
        parentItem = null!;
        row = null!;
        cell = null!;

        if (CurrentDocument is null
            || string.IsNullOrWhiteSpace(SelectedGlobalScoreCellParentName)
            || string.IsNullOrWhiteSpace(SelectedGlobalScoreCellId))
        {
            return false;
        }

        parentItem = CurrentDocument.Controls.FirstOrDefault(item =>
            string.Equals(item.Name, SelectedGlobalScoreCellParentName, StringComparison.Ordinal))!;
        if (parentItem?.Config is not GlobalScoreRowControlConfig resolvedRow)
        {
            return false;
        }

        var resolvedCell = resolvedRow.Cells.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, SelectedGlobalScoreCellId, StringComparison.Ordinal));
        if (resolvedCell is null)
        {
            return false;
        }

        row = resolvedRow;
        cell = resolvedCell;
        return true;
    }

    public void ClearSelectedGlobalScoreCell(bool notify = true)
    {
        var hadSelection = SelectedGlobalScoreCellParentName is not null || SelectedGlobalScoreCellId is not null;
        _selectedGlobalScoreCellParentName = null;
        _selectedGlobalScoreCellId = null;
        if (!notify || !hadSelection)
        {
            if (notify)
            {
                OnPropertyChanged(nameof(SelectedGlobalScoreCell));
                OnPropertyChanged(nameof(HasSelectedGlobalScoreCell));
            }

            return;
        }

        OnPropertyChanged(nameof(SelectedGlobalScoreCellParentName));
        OnPropertyChanged(nameof(SelectedGlobalScoreCellId));
        OnGlobalScoreCellSelectionChanged();
    }

    private void RebuildGlobalScoreCellEditorItems()
    {
        GlobalScoreCellEditorItems.Clear();
        if (SelectedDesignItem?.Config is not GlobalScoreRowControlConfig row)
        {
            return;
        }

        GlobalScoreRowCellLayoutHelper.EnsureCompleteCells(
            row,
            CurrentDocument?.EditingBoModeState == FrontedCanvasBoModeState.Bo3);
        foreach (var cell in row.Cells)
        {
            GlobalScoreCellEditorItems.Add(cell);
        }
    }

    private void RefreshGlobalScoreCellSelection()
    {
        if (SelectedGlobalScoreCellParentName is null && SelectedGlobalScoreCellId is null)
        {
            return;
        }

        if (!TryGetSelectedGlobalScoreCell(out var parent, out _, out _))
        {
            ClearSelectedGlobalScoreCell();
            return;
        }

        if (!ReferenceEquals(SelectedDesignItem, parent))
        {
            SelectedDesignItem = parent;
        }
        else
        {
            OnPropertyChanged(nameof(SelectedGlobalScoreCell));
            OnPropertyChanged(nameof(HasSelectedGlobalScoreCell));
        }
    }

    private void RestoreGlobalScoreCellSelectionKeys(string? parentName, string? cellId)
    {
        _selectedGlobalScoreCellParentName = parentName;
        _selectedGlobalScoreCellId = cellId;
        OnPropertyChanged(nameof(SelectedGlobalScoreCellParentName));
        OnPropertyChanged(nameof(SelectedGlobalScoreCellId));
        OnPropertyChanged(nameof(SelectedGlobalScoreCell));
        OnPropertyChanged(nameof(HasSelectedGlobalScoreCell));
    }

    public void MoveSelectedDesignItem(
        double originalLeft,
        double originalTop,
        double deltaX,
        double deltaY,
        bool renderPreview)
    {
        if (CurrentDocument is null || SelectedDesignItem is null || IsRebuildingPropertyGrid)
        {
            return;
        }

        if (HasSelectedGlobalScoreCell)
        {
            MoveSelectedGlobalScoreCell(originalLeft, originalTop, deltaX, deltaY, renderPreview);
            return;
        }

        var bounds = FrontedDesignerBoundsResolver.Resolve(SelectedDesignItem.Config);
        var result = FrontedDesignerSmartSnapHelper.Move(
            SelectedDesignItem,
            CurrentDocument,
            originalLeft,
            originalTop,
            bounds.Width,
            bounds.Height,
            deltaX,
            deltaY,
            EffectiveSnapEnabled,
            SnapGridSize,
            FrontedDesignerSmartSnapHelper.CalculateLogicalTolerance(ZoomScale));

        SelectedDesignItem.Config.Left = result.Left;
        SelectedDesignItem.Config.Top = result.Top;
        CurrentDocument.IsDirty = true;
        ActiveSnapGuides = EffectiveSnapEnabled ? result.Guides : [];
        SyncLinkedOverlays(SelectedDesignItem);
        OnDesignItemGeometryChanged(renderPreview);
    }

    public void MoveSelectedDesignItemBy(double deltaX, double deltaY)
    {
        if (CurrentDocument is null || SelectedDesignItem is null)
        {
            return;
        }

        CaptureUndoSnapshot();
        ClearActiveSnapGuides();
        if (TryGetSelectedGlobalScoreCell(out _, out _, out var selectedCell))
        {
            MoveSelectedGlobalScoreCell(
                selectedCell.X,
                selectedCell.Y,
                deltaX,
                deltaY,
                renderPreview: true);
            return;
        }

        FrontedDesignerGeometryHelper.MoveBy(
            SelectedDesignItem,
            deltaX,
            deltaY,
            CurrentDocument,
            EffectiveSnapEnabled,
            SnapGridSize);
        var changedItems = new List<FrontedControlDesignItem> { SelectedDesignItem };
        foreach (var linkedOverlay in SyncLinkedOverlays(SelectedDesignItem))
        {
            if (!changedItems.Contains(linkedOverlay))
            {
                changedItems.Add(linkedOverlay);
            }
        }

        OnDesignItemGeometryChanged(renderPreview: false);
        RequestDesignerGeometryPatch(changedItems, updateSelection: true);
    }

    public void ResizeSelectedDesignItem(
        FrontedDesignerResizeHandleKind handle,
        double originalLeft,
        double originalTop,
        double originalWidth,
        double originalHeight,
        double deltaX,
        double deltaY,
        bool renderPreview)
    {
        if (CurrentDocument is null || SelectedDesignItem is null)
        {
            return;
        }

        if (HasSelectedGlobalScoreCell)
        {
            ResizeSelectedGlobalScoreCell(
                handle,
                originalLeft,
                originalTop,
                originalWidth,
                originalHeight,
                deltaX,
                deltaY,
                renderPreview);
            return;
        }

        if (SelectedDesignItem.Config is BorderedImageFrontedControlConfig imageConfig
            && BorderedImageResizeTarget == FrontedDesignerResizeTarget.Image)
        {
            ClearActiveSnapGuides();
            ResizeSelectedBorderedImageInnerImage(
                imageConfig,
                handle,
                originalWidth,
                originalHeight,
                deltaX,
                deltaY);
            CurrentDocument.IsDirty = true;
            OnDesignItemGeometryChanged(renderPreview);
            return;
        }

        var result = FrontedDesignerSmartSnapHelper.Resize(
            SelectedDesignItem,
            CurrentDocument,
            handle,
            originalLeft,
            originalTop,
            originalWidth,
            originalHeight,
            deltaX,
            deltaY,
            EffectiveSnapEnabled,
            SnapGridSize,
            FrontedDesignerSmartSnapHelper.CalculateLogicalTolerance(ZoomScale));

        SelectedDesignItem.Config.Left = result.Left;
        SelectedDesignItem.Config.Top = result.Top;
        SelectedDesignItem.Config.Width = result.Width;
        SelectedDesignItem.Config.Height = result.Height;
        CurrentDocument.IsDirty = true;
        ActiveSnapGuides = EffectiveSnapEnabled ? result.Guides : [];
        SyncLinkedOverlays(SelectedDesignItem);
        OnDesignItemGeometryChanged(renderPreview);
    }

    public void MoveSelectedGlobalScoreCell(
        double originalX,
        double originalY,
        double deltaX,
        double deltaY,
        bool renderPreview)
    {
        if (CurrentDocument is null
            || !TryGetSelectedGlobalScoreCell(out var parentItem, out var row, out var cell))
        {
            return;
        }

        var x = FrontedDesignerGeometryHelper.Snap(originalX + deltaX);
        var y = FrontedDesignerGeometryHelper.Snap(originalY + deltaY);
        var maxX = Math.Max(0D, (row.Width ?? double.PositiveInfinity) - Math.Max(cell.Width, 0D));
        var maxY = Math.Max(0D, (row.Height ?? double.PositiveInfinity) - Math.Max(cell.Height, 0D));
        cell.X = double.IsInfinity(maxX) ? x : Math.Clamp(x, 0D, maxX);
        cell.Y = double.IsInfinity(maxY) ? y : Math.Clamp(y, 0D, maxY);
        CurrentDocument.IsDirty = true;
        SelectedDesignItem = parentItem;
        OnDesignItemGeometryChanged(renderPreview);
    }

    public void ResizeSelectedGlobalScoreCell(
        FrontedDesignerResizeHandleKind handle,
        double originalX,
        double originalY,
        double originalWidth,
        double originalHeight,
        double deltaX,
        double deltaY,
        bool renderPreview)
    {
        if (CurrentDocument is null
            || !TryGetSelectedGlobalScoreCell(out var parentItem, out var row, out var cell))
        {
            return;
        }

        var left = originalX;
        var top = originalY;
        var width = originalWidth;
        var height = originalHeight;

        if (handle is FrontedDesignerResizeHandleKind.Left
            or FrontedDesignerResizeHandleKind.TopLeft
            or FrontedDesignerResizeHandleKind.BottomLeft)
        {
            left = originalX + deltaX;
            width = originalWidth - deltaX;
        }
        else if (handle is FrontedDesignerResizeHandleKind.Right
                 or FrontedDesignerResizeHandleKind.TopRight
                 or FrontedDesignerResizeHandleKind.BottomRight)
        {
            width = originalWidth + deltaX;
        }

        if (handle is FrontedDesignerResizeHandleKind.Top
            or FrontedDesignerResizeHandleKind.TopLeft
            or FrontedDesignerResizeHandleKind.TopRight)
        {
            top = originalY + deltaY;
            height = originalHeight - deltaY;
        }
        else if (handle is FrontedDesignerResizeHandleKind.Bottom
                 or FrontedDesignerResizeHandleKind.BottomLeft
                 or FrontedDesignerResizeHandleKind.BottomRight)
        {
            height = originalHeight + deltaY;
        }

        width = Math.Max(FrontedDesignerGeometryHelper.MinResizeWidth, FrontedDesignerGeometryHelper.Snap(width));
        height = Math.Max(FrontedDesignerGeometryHelper.MinResizeHeight, FrontedDesignerGeometryHelper.Snap(height));
        left = FrontedDesignerGeometryHelper.Snap(left);
        top = FrontedDesignerGeometryHelper.Snap(top);

        var maxX = Math.Max(0D, row.Width ?? left + width);
        var maxY = Math.Max(0D, row.Height ?? top + height);
        cell.X = Math.Clamp(left, 0D, Math.Max(0D, maxX - width));
        cell.Y = Math.Clamp(top, 0D, Math.Max(0D, maxY - height));
        cell.Width = Math.Min(width, maxX);
        cell.Height = Math.Min(height, maxY);
        CurrentDocument.IsDirty = true;
        SelectedDesignItem = parentItem;
        OnDesignItemGeometryChanged(renderPreview);
    }

    private void ClampSelectedGlobalScoreCell()
    {
        if (!TryGetSelectedGlobalScoreCell(out _, out var row, out var cell))
        {
            return;
        }

        cell.Width = Math.Max(FrontedDesignerGeometryHelper.MinResizeWidth, FrontedDesignerGeometryHelper.Snap(cell.Width));
        cell.Height = Math.Max(FrontedDesignerGeometryHelper.MinResizeHeight, FrontedDesignerGeometryHelper.Snap(cell.Height));
        cell.X = FrontedDesignerGeometryHelper.Snap(cell.X);
        cell.Y = FrontedDesignerGeometryHelper.Snap(cell.Y);

        if (row.Width.HasValue)
        {
            cell.Width = Math.Min(cell.Width, row.Width.Value);
            cell.X = Math.Clamp(cell.X, 0D, Math.Max(0D, row.Width.Value - cell.Width));
        }

        if (row.Height.HasValue)
        {
            cell.Height = Math.Min(cell.Height, row.Height.Value);
            cell.Y = Math.Clamp(cell.Y, 0D, Math.Max(0D, row.Height.Value - cell.Height));
        }
    }

    public void ClearActiveSnapGuides()
    {
        if (ActiveSnapGuides.Count > 0)
        {
            ActiveSnapGuides = [];
        }
    }

    private void ResizeSelectedBorderedImageInnerImage(
        BorderedImageFrontedControlConfig config,
        FrontedDesignerResizeHandleKind handle,
        double originalWidth,
        double originalHeight,
        double deltaX,
        double deltaY)
    {
        var widthDelta = handle is FrontedDesignerResizeHandleKind.Left
            or FrontedDesignerResizeHandleKind.TopLeft
            or FrontedDesignerResizeHandleKind.BottomLeft
            ? -deltaX
            : handle is FrontedDesignerResizeHandleKind.Right
                or FrontedDesignerResizeHandleKind.TopRight
                or FrontedDesignerResizeHandleKind.BottomRight
                ? deltaX
                : 0D;
        var heightDelta = handle is FrontedDesignerResizeHandleKind.Top
            or FrontedDesignerResizeHandleKind.TopLeft
            or FrontedDesignerResizeHandleKind.TopRight
            ? -deltaY
            : handle is FrontedDesignerResizeHandleKind.Bottom
                or FrontedDesignerResizeHandleKind.BottomLeft
                or FrontedDesignerResizeHandleKind.BottomRight
                ? deltaY
                : 0D;

        var width = originalWidth + widthDelta;
        var height = originalHeight + heightDelta;

        if (EffectiveSnapEnabled)
        {
            width = FrontedDesignerGeometryHelper.NormalizeCoordinate(width, effectiveSnapEnabled: true, SnapGridSize);
            height = FrontedDesignerGeometryHelper.NormalizeCoordinate(height, effectiveSnapEnabled: true, SnapGridSize);
        }
        else
        {
            width = FrontedDesignerGeometryHelper.Snap(width);
            height = FrontedDesignerGeometryHelper.Snap(height);
        }

        config.ImageWidth = Math.Max(FrontedDesignerGeometryHelper.MinResizeWidth, width);
        config.ImageHeight = Math.Max(FrontedDesignerGeometryHelper.MinResizeHeight, height);
    }

    public IReadOnlyList<FrontedControlDesignItem> SyncLinkedOverlays(
        FrontedControlDesignItem changedTarget,
        FrontedDesignerResolvedBounds? targetBounds = null)
    {
        return [];
    }

    public bool IsLayerReorderable(FrontedControlDesignItem? item)
    {
        return item is
        {
            IsSelectableInEditor: true,
            IsEditableInEditor: true,
            IsLinkedOverlay: false
        };
    }

    public bool CommitLayerDrop(
        FrontedControlDesignItem source,
        int? targetZIndex,
        FrontedControlDesignItem? targetItem,
        bool insertAfter,
        bool moveToNewTopLayer = false,
        bool moveToNewBottomLayer = false)
    {
        if (CurrentDocument is null || !CanReorderLayers || !IsLayerReorderable(source))
        {
            StatusMessage = I18nHelper.GetLocalizedString("Designer.LayerPanel.ReorderBlocked");
            return false;
        }

        if (targetItem is not null && !IsLayerReorderable(targetItem))
        {
            StatusMessage = I18nHelper.GetLocalizedString("Designer.LayerPanel.ReorderBlocked");
            return false;
        }

        var reorderableItems = CurrentDocument.Controls
            .Where(IsLayerReorderable)
            .ToList();
        if (!reorderableItems.Contains(source))
        {
            StatusMessage = I18nHelper.GetLocalizedString("Designer.LayerPanel.ReorderBlocked");
            return false;
        }

        var oldSnapshot = CreateSnapshot();
        var targetLayer = ResolveDropTargetZIndex(targetZIndex, moveToNewTopLayer, moveToNewBottomLayer);
        var desiredGroups = reorderableItems
            .Where(item => !ReferenceEquals(item, source))
            .GroupBy(item => item.Config.ZIndex)
            .ToDictionary(
                group => group.Key,
                group => group.ToList());

        if (!desiredGroups.TryGetValue(targetLayer, out var targetGroupItems))
        {
            targetGroupItems = [];
            desiredGroups[targetLayer] = targetGroupItems;
        }

        source.Config.ZIndex = targetLayer;
        var insertIndex = targetGroupItems.Count;
        if (targetItem is not null)
        {
            var targetIndex = targetGroupItems.IndexOf(targetItem);
            if (targetIndex >= 0)
            {
                insertIndex = targetIndex + (insertAfter ? 1 : 0);
            }
        }

        targetGroupItems.Insert(Math.Clamp(insertIndex, 0, targetGroupItems.Count), source);

        var desiredReorderable = desiredGroups
            .OrderByDescending(group => group.Key)
            .SelectMany(group => group.Value)
            .ToList();
        RebuildDocumentControlOrder(desiredReorderable);

        var newSnapshot = CreateSnapshot();
        if (oldSnapshot == newSnapshot)
        {
            RebuildFilteredDesignItems();
            SelectDesignItem(source);
            StatusMessage = I18nHelper.GetLocalizedString("Designer.LayerPanel.ReorderBlocked");
            return false;
        }

        if (oldSnapshot is not null)
        {
            if (!_undoStack.TryPeek(out var previous) || previous != oldSnapshot)
            {
                PushUndoSnapshot(oldSnapshot);
            }

            _redoStack.Clear();
            NotifyUndoRedoCommands();
        }

        CurrentDocument.IsDirty = true;
        RefreshDirtyState();
        RebuildFilteredDesignItems();
        SelectDesignItem(source);
        ScheduleValidationAndPreviewRender("LayerReorder");
        StatusMessage = I18nHelper.GetLocalizedString("Designer.LayerPanel.Reordered");
        return true;
    }

    public bool CommitLayerNodeDrop(
        DesignerLayerNode sourceNode,
        int? targetZIndex,
        DesignerLayerNode? targetNode,
        bool insertAfter,
        bool moveToNewTopLayer = false,
        bool moveToNewBottomLayer = false)
    {
        if (sourceNode.Kind != DesignerLayerNodeKind.Control
            || !sourceNode.CanReorder
            || sourceNode.ControlItem is null
            || targetNode is not null && (targetNode.Kind != DesignerLayerNodeKind.Control || targetNode.ControlItem is null))
        {
            StatusMessage = I18nHelper.GetLocalizedString("Designer.LayerPanel.ReorderBlocked");
            return false;
        }

        return CommitLayerDrop(
            sourceNode.ControlItem,
            targetZIndex,
            targetNode?.ControlItem,
            insertAfter,
            moveToNewTopLayer,
            moveToNewBottomLayer);
    }

    public void CaptureUndoSnapshot()
    {
        var snapshot = CreateSnapshot();
        if (snapshot is null)
        {
            return;
        }

        if (_undoStack.TryPeek(out var previous) && previous == snapshot)
        {
            return;
        }

        PushUndoSnapshot(snapshot);
        _redoStack.Clear();
        NotifyUndoRedoCommands();
    }

    private static void PushLimited(Stack<string> stack, string snapshot)
    {
        stack.Push(snapshot);
        if (stack.Count <= FrontedLayoutLimits.MaxDesignerUndoSnapshots)
        {
            return;
        }

        var retained = stack
            .Take(FrontedLayoutLimits.MaxDesignerUndoSnapshots)
            .Reverse()
            .ToList();

        stack.Clear();
        foreach (var item in retained)
        {
            stack.Push(item);
        }
    }

    private void PushUndoSnapshot(string snapshot)
    {
        PushLimited(_undoStack, snapshot);
    }

    private void PushRedoSnapshot(string snapshot)
    {
        PushLimited(_redoStack, snapshot);
    }

    public void ClearPropertyEditErrorForBufferUpdate(string propertyName)
    {
        ClearPropertyEditError(propertyName);
    }

    public void CommitDesignItemGeometryEdit()
    {
        if (CurrentDocument is null)
        {
            return;
        }

        ValidateCurrentDocument();
        RequestPreviewRenderCurrentDocument();
    }

    public void SelectPolygonVertex(int index)
    {
        if (SelectedDesignItem?.Config is not IPolygonFrontedControlConfig polygon || polygon.Points.Count == 0)
        {
            SelectedPolygonVertexIndex = -1;
            return;
        }

        SelectedPolygonVertexIndex = Math.Clamp(index, 0, polygon.Points.Count - 1);
    }

    public void MoveSelectedPolygonVertex(Point canvasPoint, bool renderPreview)
    {
        if (CurrentDocument is null
            || SelectedDesignItem?.Config is not IPolygonFrontedControlConfig polygon
            || SelectedPolygonVertexIndex < 0
            || SelectedPolygonVertexIndex >= polygon.Points.Count)
        {
            return;
        }

        var normalized = PolygonVertexGeometryHelper.ToNormalizedPoint(SelectedDesignItem.Config, canvasPoint);
        polygon.Points[SelectedPolygonVertexIndex].X = normalized.X;
        polygon.Points[SelectedPolygonVertexIndex].Y = normalized.Y;
        CurrentDocument.IsDirty = true;
        OnDesignItemGeometryChanged(renderPreview);
    }

    [RelayCommand]
    private void AddPolygonVertex()
    {
        if (CurrentDocument is null || SelectedDesignItem?.Config is not IPolygonFrontedControlConfig polygon)
        {
            return;
        }

        CaptureUndoSnapshot();
        var afterIndex = SelectedPolygonVertexIndex >= 0 && SelectedPolygonVertexIndex < polygon.Points.Count
            ? SelectedPolygonVertexIndex
            : polygon.Points.Count - 1;
        var nextIndex = polygon.Points.Count == 0 ? 0 : (afterIndex + 1) % polygon.Points.Count;
        var first = polygon.Points.Count == 0 ? new PolygonVertexConfig(0, 0) : polygon.Points[afterIndex];
        var second = polygon.Points.Count == 0 ? new PolygonVertexConfig(1, 1) : polygon.Points[nextIndex];
        polygon.Points.Insert(afterIndex + 1, new PolygonVertexConfig(
            (first.X + second.X) / 2D,
            (first.Y + second.Y) / 2D));
        SelectedPolygonVertexIndex = afterIndex + 1;
        CurrentDocument.IsDirty = true;
        FinishPolygonVertexEdit();
    }

    [RelayCommand(CanExecute = nameof(CanRemovePolygonVertex))]
    private void RemovePolygonVertex()
    {
        if (CurrentDocument is null
            || SelectedDesignItem?.Config is not IPolygonFrontedControlConfig polygon
            || !CanRemovePolygonVertex)
        {
            return;
        }

        CaptureUndoSnapshot();
        polygon.Points.RemoveAt(SelectedPolygonVertexIndex);
        SelectedPolygonVertexIndex = Math.Min(SelectedPolygonVertexIndex, polygon.Points.Count - 1);
        CurrentDocument.IsDirty = true;
        FinishPolygonVertexEdit();
    }

    private void FinishPolygonVertexEdit()
    {
        OnPropertyChanged(nameof(SelectedPolygonVertexDisplay));
        OnPropertyChanged(nameof(CanRemovePolygonVertex));
        RemovePolygonVertexCommand.NotifyCanExecuteChanged();
        RefreshDirtyState();
        ValidateCurrentDocument();
        RequestPreviewRenderCurrentDocument();
    }

    public bool ApplyPropertyEdit(FrontedPropertyEditorItem item, object? newValue)
    {
        if (CurrentDocument is null || SelectedDesignItem is null)
        {
            return false;
        }

        if (item.IsReadOnly || item.EditorKind == FrontedPropertyEditorKind.ReadOnly)
        {
            return false;
        }

        if (item.CanBrowseResource
            && newValue is string text
            && IsAbsoluteFilePath(text))
        {
            return ApplyPropertyResourceSelection(item, text);
        }

        _propertyEditErrors.Remove(item.PropertyName);
        _propertyEditBuffers.Remove(item.PropertyName);

        if (item.PropertyName == nameof(FrontedControlDesignItem.Name))
        {
            return ApplyNameEdit(item, newValue);
        }

        if (HasSelectedGlobalScoreCell)
        {
            return ApplyGlobalScoreCellPropertyEdit(item, newValue);
        }

        var property = SelectedDesignItem.Config.GetType().GetProperty(
            item.PropertyName,
            BindingFlags.Instance | BindingFlags.Public);
        if (property is null || !property.CanWrite)
        {
            return false;
        }

        var commitValue = ClampEditorPropertyValue(item.PropertyName, SelectedDesignItem.Config.ControlType, newValue, out var wasClamped);
        if (!TryConvertPropertyValue(property, commitValue, out var convertedValue, out var errorMessage))
        {
            SetPropertyEditError(item, errorMessage, newValue);
            return false;
        }

        var oldValue = property.GetValue(SelectedDesignItem.Config);
        if (ValuesEqual(oldValue, convertedValue))
        {
            ClearPropertyEditError(item.PropertyName);
            item.Value = convertedValue;
            item.EditText = Convert.ToString(convertedValue, CultureInfo.InvariantCulture) ?? string.Empty;
            return true;
        }

        CaptureUndoSnapshot();
        property.SetValue(SelectedDesignItem.Config, convertedValue);
        item.Value = convertedValue;
        item.EditText = Convert.ToString(convertedValue, CultureInfo.InvariantCulture) ?? string.Empty;
        CurrentDocument.IsDirty = true;

        if (IsGeometryProperty(item.PropertyName))
        {
            SyncLinkedOverlays(SelectedDesignItem);
        }

        FinishPropertyEdit(item.PropertyName);
        if (wasClamped)
        {
            StatusMessage = I18nHelper.GetLocalizedString("InputTruncated");
        }

        return true;
    }

    private bool ApplyGlobalScoreCellPropertyEdit(FrontedPropertyEditorItem item, object? newValue)
    {
        if (CurrentDocument is null || !TryGetSelectedGlobalScoreCell(out _, out _, out var cell))
        {
            return false;
        }

        var property = typeof(GlobalScoreCellConfig).GetProperty(
            item.PropertyName,
            BindingFlags.Instance | BindingFlags.Public);
        if (property is null || !property.CanWrite)
        {
            return false;
        }

        if (!TryConvertPropertyValue(property, newValue, out var convertedValue, out var errorMessage))
        {
            SetPropertyEditError(item, errorMessage, newValue);
            return false;
        }

        var oldValue = property.GetValue(cell);
        if (ValuesEqual(oldValue, convertedValue))
        {
            item.Value = convertedValue;
            item.EditText = Convert.ToString(convertedValue, CultureInfo.InvariantCulture) ?? string.Empty;
            return true;
        }

        CaptureUndoSnapshot();
        property.SetValue(cell, convertedValue);
        if (item.PropertyName == nameof(GlobalScoreCellConfig.Id))
        {
            RestoreGlobalScoreCellSelectionKeys(
                SelectedDesignItem?.Name,
                Convert.ToString(convertedValue, CultureInfo.InvariantCulture));
            RebuildGlobalScoreCellEditorItems();
        }
        ClampSelectedGlobalScoreCell();
        item.Value = convertedValue;
        item.EditText = Convert.ToString(convertedValue, CultureInfo.InvariantCulture) ?? string.Empty;
        CurrentDocument.IsDirty = true;
        FinishPropertyEdit(item.PropertyName);
        return true;
    }

    private bool ApplyNameEdit(FrontedPropertyEditorItem item, object? newValue)
    {
        if (CurrentDocument is null || SelectedDesignItem is null)
        {
            return false;
        }

        if (SelectedDesignItem.IsRuntimeCritical
            || !SelectedDesignItem.IsSelectableInEditor
            || !SelectedDesignItem.IsEditableInEditor)
        {
            SetPropertyEditError(
                item,
                I18nHelper.GetLocalizedString("RuntimeCriticalControl"),
                newValue);
            return false;
        }

        var oldName = SelectedDesignItem.Name;
        var rawName = Convert.ToString(newValue, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
        var newName = FrontedTextLimitHelper.Clamp(rawName, FrontedLayoutLimits.MaxControlNameLength);
        var wasClamped = !string.Equals(rawName, newName, StringComparison.Ordinal);
        if (oldName == newName)
        {
            ClearPropertyEditError(item.PropertyName);
            item.Value = oldName;
            item.EditText = oldName;
            return true;
        }

        if (string.IsNullOrWhiteSpace(newName) || !ValidControlNameRegex.IsMatch(newName))
        {
            SetPropertyEditError(
                item,
                I18nHelper.GetLocalizedString("InvalidControlName"),
                newValue);
            return false;
        }

        if (CurrentDocument.Controls.Any(control =>
                !ReferenceEquals(control, SelectedDesignItem)
                && string.Equals(control.Name, newName, StringComparison.Ordinal)))
        {
            SetPropertyEditError(
                item,
                I18nHelper.GetLocalizedString("DuplicateControlName"),
                newValue);
            return false;
        }

        _referenceScanner.SetControls(CurrentDocument.Controls);
        if (_referenceScanner.GetIncomingReferences(oldName).Count > 0)
        {
            SetPropertyEditError(
                item,
                I18nHelper.GetLocalizedString("ReferencedControlRenameBlocked"),
                newValue);
            return false;
        }

        CaptureUndoSnapshot();
        SelectedDesignItem.Name = newName;
        item.Value = newName;
        item.EditText = newName;
        CurrentDocument.IsDirty = true;
        FinishPropertyEdit(item.PropertyName);
        if (wasClamped)
        {
            StatusMessage = I18nHelper.GetLocalizedString("InputTruncated");
        }

        return true;
    }

    private void ApplyLayoutSource(
        FrontedLayoutLoadResult loadResult,
        FrontedDesignerLayoutCatalogEntry entry)
    {
        LayoutSourceDisplay = loadResult.Source switch
        {
            FrontedLayoutSource.User => I18nHelper.GetLocalizedString("LayoutSourceUser"),
            FrontedLayoutSource.BuiltIn => I18nHelper.GetLocalizedString("LayoutSourceBuiltIn"),
            FrontedLayoutSource.PluginDefault => I18nHelper.GetLocalizedString("LayoutSourceBuiltIn"),
            _ => I18nHelper.GetLocalizedString("LayoutSourceError")
        };
        LayoutSourcePath = loadResult.Path
            ?? _layoutService.GetBuiltInDefaultLayoutPath(entry.WindowTypeName, entry.CanvasName);

        if (!string.IsNullOrWhiteSpace(loadResult.Error))
        {
            StatusMessage = loadResult.Error;
        }
    }

    private async Task<FrontedCanvasConfig?> LoadBuiltInLayoutForResetAsync(
        string windowTypeName,
        string canvasName)
    {
        // 直接读取内置布局，不走 fallback 链（避免被活动包/用户布局拦截）
        var config = await _layoutService.LoadBuiltInDefaultLayoutAsync(windowTypeName, canvasName);
        if (config is not null)
        {
            return config;
        }

        // 内置布局不存在时回退到插件默认布局
        var builtInPath = _layoutService.GetBuiltInDefaultLayoutPath(windowTypeName, canvasName);
        _logger.LogWarning(
            "Built-in layout not found for reset. Window: {WindowTypeName}, Canvas: {CanvasName}, Path: {Path}",
            windowTypeName,
            canvasName,
            builtInPath);
        return null;
    }

    private void ClearLoadedLayout(FrontedLayoutValidationMessage message)
    {
        ControlFilterText = string.Empty;
        CurrentDocument = null;
        SelectDesignItem(null);
        ResetBehaviorDocument();
        ApplyValidationMessages([message]);
        RequestPreviewRender(null, SelectedCanvas);
    }

    private void ApplyValidationMessages(
        IReadOnlyList<FrontedLayoutValidationMessage> messages,
        bool refreshPropertyGrid = true)
    {
        _lastValidationMessages = messages;
        ValidationMessages.Clear();
        foreach (var message in messages.Take(FrontedLayoutLimits.MaxValidationMessagesShown))
        {
            ValidationMessages.Add(new FrontedLayoutValidationMessage
            {
                Severity = message.Severity,
                Code = message.Code,
                ControlName = message.ControlName,
                PropertyName = message.PropertyName,
                Message = FrontedTextLimitHelper.Clamp(
                    message.Message,
                    FrontedLayoutLimits.MaxValidationMessageLength)
            });
        }

        if (messages.Count > FrontedLayoutLimits.MaxValidationMessagesShown)
        {
            ValidationMessages.Add(CreateMessage(
                FrontedLayoutValidationSeverity.Info,
                "ValidationMessagesTruncated",
                string.Format(
                    CultureInfo.InvariantCulture,
                    I18nHelper.GetLocalizedString("ValidationMessagesTruncated"),
                    messages.Count - FrontedLayoutLimits.MaxValidationMessagesShown)));
        }

        ErrorCount = messages.Count(message => message.Severity == FrontedLayoutValidationSeverity.Error);
        WarningCount = messages.Count(message => message.Severity == FrontedLayoutValidationSeverity.Warning);
        InfoCount = messages.Count(message => message.Severity == FrontedLayoutValidationSeverity.Info);
        StatusMessage =
            $"{I18nHelper.GetLocalizedString("Errors")}: {ErrorCount}  "
            + $"{I18nHelper.GetLocalizedString("Warnings")}: {WarningCount}  "
            + $"{I18nHelper.GetLocalizedString("Infos")}: {InfoCount}";
        RefreshSelectedControlDisplay();
        if (refreshPropertyGrid)
        {
            RebuildPropertyEditorItems();
        }
    }

    private int StartReloadLayoutRequest()
    {
        _reloadLayoutCancellation?.Cancel();
        _reloadLayoutCancellation?.Dispose();
        _reloadLayoutCancellation = new CancellationTokenSource();
        return ++_reloadLayoutVersion;
    }

    private void ValidateCurrentDocument()
    {
        if (CurrentDocument is null || _validator is null)
        {
            return;
        }

        ApplyValidationMessages(_validator.Validate(CurrentDocument));
    }

    private void ScheduleValidationAndPreviewRender(string reason)
    {
        ScheduleDesignerWork(reason, validate: true, preview: true);
    }

    private void ScheduleValidationOnly(string reason)
    {
        ScheduleDesignerWork(reason, validate: true, preview: false);
    }

    private void ScheduleDesignerWork(string reason, bool validate, bool preview)
    {
        _scheduledValidationRequested |= validate;
        _scheduledPreviewRequested |= preview;
        if (_scheduledValidationAndPreviewPending)
        {
            LogDesignerPerf(reason, "designer work already scheduled");
            return;
        }

        _scheduledValidationAndPreviewPending = true;
        var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        var priority = preview ? DispatcherPriority.Render : DispatcherPriority.Background;
        dispatcher.BeginInvoke(
            new Action(ExecuteScheduledValidationAndPreviewRender),
            priority);
    }

    public void ExecuteScheduledDesignerWorkForTests()
    {
        if (!_scheduledValidationAndPreviewPending)
        {
            return;
        }

        ExecuteScheduledValidationAndPreviewRender();
    }

    private void ExecuteScheduledValidationAndPreviewRender()
    {
        if (!_scheduledValidationAndPreviewPending)
        {
            return;
        }

        _scheduledValidationAndPreviewPending = false;
        var shouldValidate = _scheduledValidationRequested;
        var shouldPreview = _scheduledPreviewRequested;
        var shouldClearRestoreVisuals = _clearRestoreVisualsAfterScheduledPreview;
        _scheduledValidationRequested = false;
        _scheduledPreviewRequested = false;
        _clearRestoreVisualsAfterScheduledPreview = false;

        if (CurrentDocument is null)
        {
            if (shouldClearRestoreVisuals)
            {
                SetIsRestoringSnapshotVisuals(false);
            }

            return;
        }

        try
        {
            var total = StartDesignerPerfTrace();
            if (shouldPreview)
            {
                RequestPreviewRenderCurrentDocument();
                ScheduledDesignerPreviewExecutionCount++;
                LogDesignerPerf("ScheduledDesignerWork", "preview render execution", Elapsed(total));
            }

            if (shouldValidate)
            {
                ValidateCurrentDocument();
                ScheduledDesignerValidationExecutionCount++;
                LogDesignerPerf("ScheduledDesignerWork", "validation execution", Elapsed(total));
            }
        }
        finally
        {
            if (shouldClearRestoreVisuals)
            {
                SetIsRestoringSnapshotVisuals(false);
                NotifyUndoRedoCommands();
            }
        }
    }

    private void RebuildPropertyEditorItems()
    {
        _isRebuildingPropertyGrid = true;
        OnPropertyChanged(nameof(IsRebuildingPropertyGrid));
        try
        {
            PropertyEditorItems.Clear();

            if (CurrentDocument is null || SelectedDesignItem is null)
            {
                return;
            }

            var rows = TryGetSelectedGlobalScoreCell(out _, out _, out var selectedCell)
                ? BuildGlobalScoreCellPropertyRows(selectedCell)
                : _propertyGridBuilder.Build(
                    CurrentDocument,
                    SelectedDesignItem,
                    _validator,
                    _referenceScanner,
                    _runtimeContracts);

            foreach (var row in rows)
            {
                if (_propertyEditErrors.TryGetValue(row.PropertyName, out var editError))
                {
                    if (_propertyEditBuffers.TryGetValue(row.PropertyName, out var editBuffer))
                    {
                        row.EditText = editBuffer;
                    }

                    row.SetEditError(editError);
                    row.ValidationErrors = row.ValidationErrors
                        .Concat([editError])
                        .Distinct(StringComparer.Ordinal)
                        .ToArray();
                    row.ValidationMessages = row.ValidationMessages
                        .Concat([CreatePropertyEditValidationMessage(editError, row.PropertyName)])
                        .ToArray();
                }

                PropertyEditorItems.Add(row);
            }
        }
        finally
        {
            _isRebuildingPropertyGrid = false;
            OnPropertyChanged(nameof(IsRebuildingPropertyGrid));
        }
    }

    private void SetPropertyEditError(FrontedPropertyEditorItem item, string message, object? attemptedValue)
    {
        var attemptedText = Convert.ToString(attemptedValue, CultureInfo.InvariantCulture) ?? string.Empty;
        _propertyEditErrors[item.PropertyName] = message;
        _propertyEditBuffers[item.PropertyName] = attemptedText;
        item.EditText = attemptedText;
        item.SetEditError(message);
        item.ValidationErrors = item.ValidationErrors
            .Concat([message])
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        item.ValidationMessages = item.ValidationMessages
            .Concat([CreatePropertyEditValidationMessage(message, item.PropertyName)])
            .ToArray();
        StatusMessage = message;
    }

    private FrontedLayoutValidationMessage CreatePropertyEditValidationMessage(string message, string propertyName) =>
        new()
        {
            Severity = FrontedLayoutValidationSeverity.Error,
            Code = "PropertyEditError",
            Message = message,
            ControlName = SelectedDesignItem?.Name,
            PropertyName = propertyName
        };

    private void ClearPropertyEditError(string propertyName)
    {
        _propertyEditErrors.Remove(propertyName);
        _propertyEditBuffers.Remove(propertyName);
    }

    private void FinishPropertyEdit(string propertyName)
    {
        ClearPropertyEditError(propertyName);
        RefreshDirtyState();
        RebuildFilteredDesignItems();
        ValidateCurrentDocument();
        RequestPreviewRenderCurrentDocument();
        DeleteSelectedControlCommand.NotifyCanExecuteChanged();
    }

    private void FinishCanvasConfigEdit(string statusMessage)
    {
        CanvasPropertiesStatus = statusMessage;
        RefreshDirtyState();
        ValidateCurrentDocument();
        RequestPreviewRenderCurrentDocument();
        UpdateFitZoomFromCurrentDocument();
    }

    private void RefreshCanvasPropertyBuffers()
    {
        if (CurrentDocument is null)
        {
            CanvasWidthEditText = string.Empty;
            CanvasHeightEditText = string.Empty;
            BackgroundImageEditText = string.Empty;
            SetBoModeStateUi(enableBoModeStates: false, FrontedCanvasBoModeState.Bo5);
            return;
        }

        CanvasWidthEditText = CurrentDocument.CanvasConfig.CanvasWidth.ToString("0.##", CultureInfo.InvariantCulture);
        CanvasHeightEditText = CurrentDocument.CanvasConfig.CanvasHeight.ToString("0.##", CultureInfo.InvariantCulture);
        BackgroundImageEditText = GetEditingStateBackground(CurrentDocument) ?? string.Empty;
        _designerPreviewSharedDataService.IsBo3Mode = CurrentDocument.EditingBoModeState == FrontedCanvasBoModeState.Bo3;
        SetBoModeStateUi(CurrentDocument.CanvasConfig.EnableBoModeStates, CurrentDocument.EditingBoModeState);
    }

    private void SetBoModeStateUi(bool enableBoModeStates, FrontedCanvasBoModeState editingState)
    {
        _isUpdatingBoModeStateUi = true;
        try
        {
            EnableBoModeStates = enableBoModeStates;
            SelectedBoModeStateOption = BoModeStateOptions.FirstOrDefault(option => option.State == editingState)
                                        ?? BoModeStateOptions.FirstOrDefault();
        }
        finally
        {
            _isUpdatingBoModeStateUi = false;
        }

        OnPropertyChanged(nameof(IsBoModeStateSelectorVisible));
        OnPropertyChanged(nameof(CanCopyBo5ToBo3));
        CopyBo5ToBo3Command.NotifyCanExecuteChanged();
    }

    private static string? GetEditingStateBackground(FrontedCanvasDesignDocument document)
    {
        if (document.EditingBoModeState == FrontedCanvasBoModeState.Bo3
            && document.CanvasConfig.BoModeStates.TryGetValue(
                FrontedCanvasRuntimeStateResolver.Bo3StateKey,
                out var bo3State))
        {
            return bo3State.BackgroundImage;
        }

        return document.CanvasConfig.BackgroundImage;
    }

    private static void SetEditingStateBackground(FrontedCanvasDesignDocument document, string? value)
    {
        if (document.EditingBoModeState == FrontedCanvasBoModeState.Bo3)
        {
            EnsureBo3State(document.CanvasConfig).BackgroundImage = value;
            return;
        }

        document.CanvasConfig.BackgroundImage = value;
    }

    private static FrontedCanvasStateConfig EnsureBo3State(FrontedCanvasConfig config)
    {
        if (!config.BoModeStates.TryGetValue(FrontedCanvasRuntimeStateResolver.Bo3StateKey, out var state))
        {
            state = new FrontedCanvasStateConfig();
            config.BoModeStates[FrontedCanvasRuntimeStateResolver.Bo3StateKey] = state;
        }

        return state;
    }

    private void RebuildDocumentFromConfig(
        FrontedCanvasConfig config,
        FrontedCanvasBoModeState editingState,
        bool preserveDirty,
        string? selectedControlName)
    {
        if (CurrentDocument is null)
        {
            return;
        }

        var document = _designConverter.FromConfig(
            CurrentDocument.WindowTypeName,
            CurrentDocument.CanvasName,
            config,
            _runtimeContracts,
            editingState);
        document.IsDirty = preserveDirty || CurrentDocument.IsDirty;

        _preserveUndoRedoDuringDocumentSwap = true;
        try
        {
            CurrentDocument = document;
        }
        finally
        {
            _preserveUndoRedoDuringDocumentSwap = false;
        }

        SelectDesignItem(document.Controls.FirstOrDefault(control =>
            string.Equals(control.Name, selectedControlName, StringComparison.Ordinal)));
        RefreshCanvasPropertyBuffers();
        RefreshDirtyState();
        ValidateCurrentDocument();
        RequestPreviewRenderCurrentDocument();
    }

    private static T DeepClone<T>(T value)
    {
        var json = JsonSerializer.Serialize(value);
        var result = JsonSerializer.Deserialize<T>(json);
        if (result is null)
        {
            StaticLogger?.LogError("Failed to clone fronted layout state.");
            throw new InvalidOperationException("Failed to clone fronted layout state.");
        }

        return result;
    }

    private static Dictionary<string, FrontedControlConfigBase> CloneControls(
        IReadOnlyDictionary<string, FrontedControlConfigBase> controls)
    {
        var cloned = new Dictionary<string, FrontedControlConfigBase>(StringComparer.Ordinal);
        foreach (var (name, control) in controls)
        {
            var json = JsonSerializer.Serialize(control, control.GetType());
            var deserialized = (FrontedControlConfigBase?)JsonSerializer.Deserialize(json, control.GetType());
            if (deserialized is null)
            {
                StaticLogger?.LogError("Failed to clone fronted control config.");
                throw new InvalidOperationException("Failed to clone fronted control config.");
            }

            cloned[name] = deserialized;
        }

        return cloned;
    }

    public void DiscardPendingResourceImports()
    {
        CleanupPendingImportedResources(includeCurrentDocument: false);
    }

    private void RecordPendingImportedResource(
        FrontedLocalResourceStoreResult result,
        string sourceContext,
        bool wasApplied)
    {
        if (!wasApplied || !result.WasNewlyCreated)
        {
            return;
        }

        if (_pendingImportedResources.Any(resource =>
                string.Equals(resource.ResourceUri, result.ResourceUri, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _pendingImportedResources.Add(new PendingImportedResource(
            result.ResourceUri,
            result.PhysicalPath,
            DateTimeOffset.UtcNow,
            sourceContext));
    }

    private void CleanupPendingImportedResources(bool includeCurrentDocument)
    {
        if (_pendingImportedResources.Count == 0 || _localResourceStore is null)
        {
            return;
        }

        var referencedResources = CollectSavedLocalResourceReferences();
        if (includeCurrentDocument && CurrentDocument is not null)
        {
            foreach (var reference in EnumerateLocalResourceReferences(_designConverter.ToConfig(CurrentDocument)))
            {
                referencedResources.Add(reference);
            }
        }

        foreach (var pending in _pendingImportedResources.ToArray())
        {
            if (referencedResources.Contains(pending.ResourceUri))
            {
                _pendingImportedResources.Remove(pending);
                continue;
            }

            try
            {
                if (File.Exists(pending.PhysicalPath))
                {
                    File.Delete(pending.PhysicalPath);
                }

                _pendingImportedResources.Remove(pending);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to cleanup pending fronted designer resource {ResourceUri} from {SourceContext}.",
                    pending.ResourceUri,
                    pending.SourceContext);
            }
        }
    }

    private HashSet<string> CollectSavedLocalResourceReferences()
    {
        var references = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var root = _layoutService.GetUserLayoutRootFolder();
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            return references;
        }

        foreach (var file in Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                var json = File.ReadAllText(file);
                if (JsonNode.Parse(json) is not { } node)
                {
                    continue;
                }

                foreach (var reference in EnumerateLocalResourceReferences(node))
                {
                    references.Add(reference);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to scan fronted layout resource references: {Path}", file);
            }
        }

        return references;
    }

    private static IEnumerable<string> EnumerateLocalResourceReferences(FrontedCanvasConfig config)
    {
        var node = JsonSerializer.SerializeToNode(config);
        return node is null ? [] : EnumerateLocalResourceReferences(node);
    }

    private static IEnumerable<string> EnumerateLocalResourceReferences(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach (var child in obj)
            {
                if (child.Value is not null)
                {
                    foreach (var reference in EnumerateLocalResourceReferences(child.Value))
                    {
                        yield return reference;
                    }
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array)
            {
                if (child is not null)
                {
                    foreach (var reference in EnumerateLocalResourceReferences(child))
                    {
                        yield return reference;
                    }
                }
            }
        }
        else if (node is JsonValue value
                 && value.TryGetValue<string>(out var text)
                 && text.StartsWith("bpui://local/", StringComparison.OrdinalIgnoreCase))
        {
            yield return text;
        }
    }

    private void LoadWindowOptions(string windowTypeName)
    {
        WindowOptionsWindowTypeName = $"{_localizationService.GetWindowDisplayName(windowTypeName)} ({windowTypeName})";
        WindowOptionsRestartRequired = false;

        if (_windowLayoutOptionsService is null)
        {
            WindowAllowTransparency = false;
            WindowBackgroundColorEditText = "#00000000";
            WindowBackgroundColorValue = Colors.Transparent;
            _windowBackgroundColorConfigured = false;
            return;
        }

        _isLoadingWindowOptions = true;
        try
        {
            var options = _windowLayoutOptionsService.LoadOptions(windowTypeName);
            WindowAllowTransparency = options.AllowTransparency;
            var configuredBackgroundColor = options.BackgroundColor;
            _windowBackgroundColorConfigured = !string.IsNullOrWhiteSpace(configuredBackgroundColor);
            var backgroundColor = configuredBackgroundColor ?? "#00000000";
            if (!FrontedPropertyColorHelper.TryParseArgbColor(configuredBackgroundColor, out var color))
            {
                backgroundColor = "#00000000";
                color = Colors.Transparent;
                _windowBackgroundColorConfigured = false;
            }
            else
            {
                backgroundColor = FrontedPropertyColorHelper.ToArgbString(color);
            }

            WindowBackgroundColorEditText = backgroundColor;
            WindowBackgroundColorValue = color;
            WindowOptionsStatus = string.Empty;
        }
        finally
        {
            _isLoadingWindowOptions = false;
        }
    }

    public bool ApplyWindowBackgroundColorEdit()
    {
        if (!FrontedPropertyColorHelper.TryParseArgbColor(WindowBackgroundColorEditText, out var color))
        {
            WindowOptionsStatus = I18nHelper.GetLocalizedString("Designer.Validation.InvalidArgbColor");
            return false;
        }

        WindowBackgroundColorEditText = FrontedPropertyColorHelper.ToArgbString(color);
        WindowBackgroundColorValue = color;
        _windowBackgroundColorConfigured = true;
        _ = SaveWindowOptionsAsync(restartRequired: false, applyBackgroundImmediately: true);
        return true;
    }

    public bool ApplyTextBindingEdit(
        FrontedPropertyEditorItem item,
        Core.Models.FrontedLayout.Binding.FrontedTextBindingExpression expression)
    {
        if (CurrentDocument is null || SelectedDesignItem is null || item.IsReadOnly)
        {
            return false;
        }

        var property = SelectedDesignItem.Config.GetType().GetProperty(item.PropertyName);
        if (property?.PropertyType != typeof(Core.Models.FrontedLayout.Binding.FrontedTextBindingExpression)
            || !property.CanWrite)
        {
            return false;
        }

        var oldJson = JsonSerializer.Serialize(property.GetValue(SelectedDesignItem.Config));
        var newJson = JsonSerializer.Serialize(expression);
        if (string.Equals(oldJson, newJson, StringComparison.Ordinal))
        {
            return true;
        }

        CaptureUndoSnapshot();
        property.SetValue(SelectedDesignItem.Config, expression);
        item.Value = expression;
        item.DisplayValue = expression.GetActiveSources().Count == 0
            ? I18nHelper.GetLocalizedString("Designer.TextBinding.None")
            : string.Format(
                CultureInfo.CurrentCulture,
                I18nHelper.GetLocalizedString("Designer.TextBinding.SourceSummary"),
                expression.GetActiveSources().Count,
                string.Join(", ", expression.GetActiveSources().Select(source => source.Path)));
        CurrentDocument.IsDirty = true;
        FinishPropertyEdit(item.PropertyName);
        return true;
    }

    private async Task SaveWindowOptionsAsync(bool restartRequired, bool applyBackgroundImmediately)
    {
        if (_windowLayoutOptionsService is null || SelectedWindow is null)
        {
            return;
        }

        try
        {
            await _windowLayoutOptionsService.SaveOptionsAsync(
                SelectedWindow.WindowTypeName,
                new FrontedWindowLayoutOptions
                {
                    AllowTransparency = WindowAllowTransparency,
                    BackgroundColor = _windowBackgroundColorConfigured
                        ? WindowBackgroundColorEditText
                        : null
                });
            if (applyBackgroundImmediately)
            {
                _frontedWindowService?.ApplyWindowBackgroundColor(SelectedWindow.WindowTypeName);
            }

            WindowOptionsRestartRequired = restartRequired;
            WindowOptionsStatus = restartRequired
                ? I18nHelper.GetLocalizedString("RestartRequired")
                : I18nHelper.GetLocalizedString("WindowOptionsApplied");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save fronted window layout options.");
            WindowOptionsStatus = ex.Message;
        }
    }

    private async Task ResetWindowOptionsAsync()
    {
        if (_windowLayoutOptionsService is null || SelectedWindow is null)
        {
            return;
        }

        try
        {
            await _windowLayoutOptionsService.ResetOptionsAsync(SelectedWindow.WindowTypeName);
            LoadWindowOptions(SelectedWindow.WindowTypeName);
            WindowOptionsRestartRequired = true;
            WindowOptionsStatus = I18nHelper.GetLocalizedString("RestartRequired");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to reset fronted window layout options.");
            WindowOptionsStatus = ex.Message;
        }
    }

    private void OnDesignItemGeometryChanged(bool renderPreview)
    {
        RefreshDirtyState();
        RefreshSelectedControlDisplay();

        if (renderPreview)
        {
            ValidateCurrentDocument();
            RequestPreviewRenderCurrentDocument();
        }
    }

    private void RequestDesignerGeometryPatch(
        IReadOnlyList<FrontedControlDesignItem> changedItems,
        bool updateSelection)
    {
        if (changedItems.Count == 0)
        {
            return;
        }

        var args = new FrontedDesignerGeometryPatchRequestedEventArgs(
            changedItems,
            rebuildLayerPanel: false,
            rebuildInteractionLayer: false,
            updateSelection,
            zIndexChanged: false);
        DesignerGeometryPatchRequested?.Invoke(this, args);

        if (!args.Applied)
        {
            ValidateCurrentDocument();
            RequestPreviewRenderCurrentDocument();
        }
    }

    private void RefreshDirtyState()
    {
        DirtyIndicatorText = CurrentDocument?.IsDirty == true || AreBehaviorsDirty
            ? $"* {(CurrentDocument?.IsDirty == true ? I18nHelper.GetLocalizedString("Unsaved") : I18nHelper.GetLocalizedString("Designer.Behaviors.UnsavedBehaviorChanges"))}"
            : string.Empty;
        OnPropertyChanged(nameof(HasUnsavedChanges));
        NotifyLayoutCommandState();
    }

    private BehaviorPanelViewModel CreateBehaviorPanel()
    {
        return new BehaviorPanelViewModel(
            _localizationService,
            new FrontedBehaviorEventCatalog(),
            MarkLayoutDirtyFromBehaviorPanel,
            MarkBehaviorsDirty,
            animationRuntime: _animationRuntime,
            previewAnimationScope: _previewAnimationScope,
            behaviorRuntime: _behaviorRuntime);
    }

    private void MarkLayoutDirtyFromBehaviorPanel()
    {
        if (CurrentDocument is null)
        {
            return;
        }

        CurrentDocument.IsDirty = true;
        RefreshDirtyState();
    }

    private void MarkBehaviorsDirty()
    {
        AreBehaviorsDirty = true;
        RefreshDirtyState();
    }

    private void ResetBehaviorDocument(FrontedBehaviorDocument? document = null)
    {
        BehaviorPanel.SetDocument(document ?? new FrontedBehaviorDocument
        {
            Version = 1,
            WindowType = CurrentDocument?.WindowTypeName,
            CanvasName = CurrentDocument?.CanvasName
        });
        AreBehaviorsDirty = false;
        BehaviorPanel.SetSelectedControl(SelectedDesignItem);
    }

    public void UpdateBehaviorPreviewAnimationScope(FrameworkElement previewRoot)
    {
        _previewAnimationScope?.Update(
            previewRoot,
            SelectedDesignItem,
            SelectedCanvas?.WindowId,
            CurrentDocument?.CanvasName ?? SelectedCanvas?.CanvasName,
            CurrentDocument?.Controls ?? []);
    }

    public void ClearBehaviorPreviewAnimationScope()
    {
        _previewAnimationScope?.Clear();
    }

    private void NotifyLayoutCommandState()
    {
        OnPropertyChanged(nameof(CanSaveLayout));
        OnPropertyChanged(nameof(CanResetToBuiltIn));
        SaveLayoutCommand.NotifyCanExecuteChanged();
        ResetToBuiltInCommand.NotifyCanExecuteChanged();
    }

    private void RequestPreviewRenderCurrentDocument()
    {
        if (CurrentDocument is null)
        {
            RequestPreviewRender(null, SelectedCanvas);
            return;
        }

        RequestPreviewRender(_designConverter.ToConfig(CurrentDocument), SelectedCanvas);
    }

    private int ResolveDropTargetZIndex(
        int? targetZIndex,
        bool moveToNewTopLayer,
        bool moveToNewBottomLayer)
    {
        if (CurrentDocument is null || CurrentDocument.Controls.Count == 0)
        {
            return targetZIndex ?? 0;
        }

        if (moveToNewTopLayer)
        {
            return CurrentDocument.Controls.Max(control => control.Config.ZIndex) + 1;
        }

        if (moveToNewBottomLayer)
        {
            return CurrentDocument.Controls.Min(control => control.Config.ZIndex) - 1;
        }

        return targetZIndex ?? 0;
    }

    private void RebuildDocumentControlOrder(IReadOnlyList<FrontedControlDesignItem> desiredReorderable)
    {
        if (CurrentDocument is null)
        {
            return;
        }

        var originalControls = CurrentDocument.Controls.ToList();
        var added = new HashSet<FrontedControlDesignItem>();
        var rebuilt = new List<FrontedControlDesignItem>(originalControls.Count);

        foreach (var item in desiredReorderable)
        {
            AddControlAndLinkedOverlays(item, originalControls, rebuilt, added);
        }

        foreach (var item in originalControls)
        {
            if (added.Contains(item))
            {
                continue;
            }

            rebuilt.Add(item);
            added.Add(item);
        }

        CurrentDocument.Controls.Clear();
        foreach (var item in rebuilt)
        {
            CurrentDocument.Controls.Add(item);
        }
    }

    private static void AddControlAndLinkedOverlays(
        FrontedControlDesignItem item,
        IReadOnlyList<FrontedControlDesignItem> originalControls,
        ICollection<FrontedControlDesignItem> rebuilt,
        ISet<FrontedControlDesignItem> added)
    {
        if (!added.Add(item))
        {
            return;
        }

        rebuilt.Add(item);
    }

    private void RebuildFilteredDesignItems()
    {
        FilteredDesignItems.Clear();
        LayerGroups.Clear();

        if (CurrentDocument is null)
        {
            return;
        }

        var filter = ControlFilterText?.Trim();
        var controls = CurrentDocument.Controls
            .Select((item, index) => new { Item = item, Index = index })
            .Where(entry => entry.Item.IsSelectableInEditor && MatchesControlFilter(entry.Item, filter))
            .OrderByDescending(entry => entry.Item.Config.ZIndex)
            .ThenBy(entry => entry.Index)
            .Select(entry => entry.Item);

        foreach (var item in controls)
        {
            FilteredDesignItems.Add(item);
        }

        RebuildLayerGroups();
    }

    private void AddFilteredDesignItemIfVisible(FrontedControlDesignItem item)
    {
        var filter = ControlFilterText?.Trim();
        if (!item.IsSelectableInEditor || !MatchesControlFilter(item, filter))
        {
            return;
        }

        var insertIndex = GetFilteredInsertIndex(item);
        FilteredDesignItems.Insert(insertIndex, item);
        RebuildLayerGroups();
    }

    private void RemoveFilteredDesignItem(FrontedControlDesignItem item)
    {
        var index = FilteredDesignItems.IndexOf(item);
        if (index >= 0)
        {
            FilteredDesignItems.RemoveAt(index);
            RebuildLayerGroups();
        }
    }

    private void RefreshFilteredDesignItemPosition(FrontedControlDesignItem item)
    {
        RemoveFilteredDesignItem(item);
        AddFilteredDesignItemIfVisible(item);
    }

    private void RebuildLayerGroups()
    {
        LayerGroups.Clear();
        foreach (var group in FilteredDesignItems
                     .GroupBy(item => item.Config.ZIndex)
                     .OrderByDescending(group => group.Key))
        {
            var layerGroup = new FrontedLayerGroup
            {
                ZIndex = group.Key,
                DisplayName = $"{I18nHelper.GetLocalizedString("Designer.LayerPanel.Layer")} {group.Key}"
            };

            foreach (var item in group.OrderBy(item => CurrentDocument?.Controls.IndexOf(item) ?? 0))
            {
                layerGroup.Items.Add(CreateControlLayerNode(item));
            }

            LayerGroups.Add(layerGroup);
        }

        RefreshLayerNodeSelection();
    }

    private DesignerLayerNode CreateControlLayerNode(FrontedControlDesignItem item)
    {
        return new DesignerLayerNode
        {
            Kind = DesignerLayerNodeKind.Control,
            ControlItem = item,
            CanSelect = item.IsSelectableInEditor,
            CanReorder = IsLayerReorderable(item),
            DisplayName = item.Name,
            Metadata = _localizationService.GetControlTypeDisplayName(item.Config.ControlType),
            ZIndex = item.Config.ZIndex
        };
    }

    private void RefreshLayerNodeSelection()
    {
        DesignerLayerNode? selectedNode = null;
        foreach (var node in LayerGroups.SelectMany(group => group.Items))
        {
            var isSelected = IsSelectedLayerNode(node);
            node.IsSelected = isSelected;
            if (isSelected)
            {
                selectedNode = node;
            }
        }

        SelectedLayerNode = selectedNode;
    }

    private bool IsSelectedLayerNode(DesignerLayerNode node)
    {
        return node.Kind switch
        {
            DesignerLayerNodeKind.Control => ReferenceEquals(node.ControlItem, SelectedDesignItem),
            _ => false
        };
    }

    private static string GetLayerNodeExpansionKey(FrontedControlDesignItem item) => item.Name;

    private int GetFilteredInsertIndex(FrontedControlDesignItem item)
    {
        if (CurrentDocument is null)
        {
            return FilteredDesignItems.Count;
        }

        for (var index = 0; index < FilteredDesignItems.Count; index++)
        {
            if (CompareFilteredOrder(item, FilteredDesignItems[index]) < 0)
            {
                return index;
            }
        }

        return FilteredDesignItems.Count;
    }

    private int CompareFilteredOrder(FrontedControlDesignItem left, FrontedControlDesignItem right)
    {
        var zIndexCompare = right.Config.ZIndex.CompareTo(left.Config.ZIndex);
        if (zIndexCompare != 0)
        {
            return zIndexCompare;
        }

        if (CurrentDocument is null)
        {
            return 0;
        }

        return CurrentDocument.Controls.IndexOf(left).CompareTo(CurrentDocument.Controls.IndexOf(right));
    }

    private void NormalizeSelectionState()
    {
        _lastSelectedDesignItem = null;
        if (CurrentDocument is null)
        {
            SelectedDesignItem = null;
            return;
        }

        if (SelectedDesignItem is not null && !CurrentDocument.Controls.Contains(SelectedDesignItem))
        {
            SelectedDesignItem = null;
        }

        foreach (var control in CurrentDocument.Controls)
        {
            control.IsSelected = ReferenceEquals(control, SelectedDesignItem);
            if (control.IsSelected)
            {
                _lastSelectedDesignItem = control;
            }
        }

        RebuildGlobalScoreCellEditorItems();
        RefreshGlobalScoreCellSelection();
    }

    private ObservableCollection<FrontedPropertyEditorItem> BuildGlobalScoreCellPropertyRows(GlobalScoreCellConfig cell)
    {
        var rows = new List<FrontedPropertyEditorItem>
        {
            CreateCellPropertyRow(nameof(GlobalScoreCellConfig.Id), typeof(string), FrontedPropertyEditorKind.Text, cell.Id, "Identity"),
            CreateCellPropertyRow(nameof(GlobalScoreCellConfig.GameNumber), typeof(int), FrontedPropertyEditorKind.Number, cell.GameNumber, "ControlSpecific"),
            CreateCellPropertyRow(nameof(GlobalScoreCellConfig.GameKind), typeof(ScoreGameKind), FrontedPropertyEditorKind.Enum, cell.GameKind, "ControlSpecific"),
            CreateCellPropertyRow(nameof(GlobalScoreCellConfig.HalfKind), typeof(ScoreHalfKind), FrontedPropertyEditorKind.Enum, cell.HalfKind, "ControlSpecific"),
            CreateCellPropertyRow(nameof(GlobalScoreCellConfig.X), typeof(double), FrontedPropertyEditorKind.Number, cell.X, "Layout"),
            CreateCellPropertyRow(nameof(GlobalScoreCellConfig.Y), typeof(double), FrontedPropertyEditorKind.Number, cell.Y, "Layout"),
            CreateCellPropertyRow(nameof(GlobalScoreCellConfig.Width), typeof(double), FrontedPropertyEditorKind.Number, cell.Width, "Layout"),
            CreateCellPropertyRow(nameof(GlobalScoreCellConfig.Height), typeof(double), FrontedPropertyEditorKind.Number, cell.Height, "Layout"),
            CreateCellPropertyRow(nameof(GlobalScoreCellConfig.Visibility), typeof(FrontedControlVisibility), FrontedPropertyEditorKind.Enum, cell.Visibility, "Layout"),
            CreateCellPropertyRow(nameof(GlobalScoreCellConfig.FontFamily), typeof(string), FrontedPropertyEditorKind.FontFamily, cell.FontFamily, "Appearance"),
            CreateCellPropertyRow(nameof(GlobalScoreCellConfig.FontWeight), typeof(string), FrontedPropertyEditorKind.Enum, cell.FontWeight, "Appearance", new object[] { "Normal", "Bold", "SemiBold", "Light", "Medium", "ExtraBold" }),
            CreateCellPropertyRow(nameof(GlobalScoreCellConfig.Color), typeof(string), FrontedPropertyEditorKind.Color, cell.Color, "Appearance"),
            CreateCellPropertyRow(nameof(GlobalScoreCellConfig.FontSize), typeof(double?), FrontedPropertyEditorKind.Number, cell.FontSize, "Appearance"),
            CreateCellPropertyRow(nameof(GlobalScoreCellConfig.ShowCampIcon), typeof(bool?), FrontedPropertyEditorKind.Boolean, cell.ShowCampIcon, "Appearance")
        };

        string? currentGroup = null;
        foreach (var row in rows)
        {
            row.Description = row.PropertyName is nameof(GlobalScoreCellConfig.FontFamily)
                or nameof(GlobalScoreCellConfig.FontWeight)
                or nameof(GlobalScoreCellConfig.Color)
                or nameof(GlobalScoreCellConfig.FontSize)
                or nameof(GlobalScoreCellConfig.ShowCampIcon)
                ? I18nHelper.GetLocalizedString("Designer.GlobalScoreRow.CellInheritsFromParent")
                : row.Description;
            row.IsGroupHeaderVisible = row.GroupName != currentGroup;
            row.GroupDisplayName = _localizationService.GetGroupDisplayName(row.GroupName ?? string.Empty);
            currentGroup = row.GroupName;
        }

        return new ObservableCollection<FrontedPropertyEditorItem>(rows);
    }

    private FrontedPropertyEditorItem CreateCellPropertyRow(
        string propertyName,
        Type propertyType,
        FrontedPropertyEditorKind editorKind,
        object? value,
        string groupName,
        IReadOnlyList<object>? stringOptions = null)
    {
        var row = new FrontedPropertyEditorItem
        {
            DisplayName = _localizationService.GetPropertyDisplayName(GetGlobalScoreCellPropertyDisplayKey(propertyName)),
            PropertyName = propertyName,
            PropertyType = propertyType,
            EditorKind = editorKind,
            Value = value,
            DisplayValue = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
            EditText = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
            GroupName = groupName,
            IsRequired = propertyName is nameof(GlobalScoreCellConfig.Id)
                or nameof(GlobalScoreCellConfig.GameNumber)
                or nameof(GlobalScoreCellConfig.X)
                or nameof(GlobalScoreCellConfig.Y)
                or nameof(GlobalScoreCellConfig.Width)
                or nameof(GlobalScoreCellConfig.Height)
        };

        if (editorKind == FrontedPropertyEditorKind.Enum)
        {
            row.Options = stringOptions is not null
                ? stringOptions.Select(value => new FrontedPropertyEditorOption
                {
                    Value = value,
                    DisplayName = _localizationService.GetOptionDisplayName(propertyName, value)
                }).Cast<object>().ToArray()
                : Enum.GetValues(Nullable.GetUnderlyingType(propertyType) ?? propertyType)
                    .Cast<object>()
                    .Select(value => new FrontedPropertyEditorOption
                    {
                        Value = value,
                        DisplayName = _localizationService.GetOptionDisplayName(propertyName, value)
                    })
                    .Cast<object>()
                    .ToArray();
        }

        return row;
    }

    private static string GetGlobalScoreCellPropertyDisplayKey(string propertyName) =>
        propertyName switch
        {
            nameof(GlobalScoreCellConfig.Id) => "CellId",
            nameof(GlobalScoreCellConfig.X) => "CellX",
            nameof(GlobalScoreCellConfig.Y) => "CellY",
            nameof(GlobalScoreCellConfig.ShowCampIcon) => "ShowCampIconOverride",
            _ => propertyName
        };

    private Stopwatch? StartDesignerPerfTrace()
    {
        return _logger.IsEnabled(LogLevel.Debug) ? Stopwatch.StartNew() : null;
    }

    private static TimeSpan Elapsed(Stopwatch? stopwatch)
    {
        return stopwatch?.Elapsed ?? TimeSpan.Zero;
    }

    [Conditional("DEBUG")]
    private void LogDesignerPerf(string operation, string stage)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("FrontedDesigner perf {Operation}: {Stage}", operation, stage);
        }
    }

    [Conditional("DEBUG")]
    private void LogDesignerPerf(string operation, string stage, TimeSpan elapsed)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "FrontedDesigner perf {Operation}: {Stage} at {ElapsedMilliseconds:F2} ms",
                operation,
                stage,
                elapsed.TotalMilliseconds);
        }
    }

    public static bool MatchesControlFilter(FrontedControlDesignItem item, string? filter)
    {
        if (!item.IsSelectableInEditor)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        return item.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
               || item.Config.ControlType.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshSelectedControlDisplay()
    {
        if (SelectedDesignItem is null)
        {
            SelectedControlDisplay = I18nHelper.GetLocalizedString("NoControlSelected");
            SelectedControlTypeDisplay = string.Empty;
            SelectedControlGeometryDisplay = string.Empty;
            SelectedControlRuntimeCriticalDisplay = string.Empty;
            SelectedControlValidationMessageCount = 0;
            return;
        }

        var config = SelectedDesignItem.Config;
        if (TryGetSelectedGlobalScoreCell(out _, out _, out var cell))
        {
            SelectedControlDisplay = $"{SelectedDesignItem.Name} / {cell.Id}";
            SelectedControlTypeDisplay = I18nHelper.GetLocalizedString("Designer.GlobalScoreRow.SelectedCell");
            SelectedControlGeometryDisplay =
                $"X {cell.X:0.##}  Y {cell.Y:0.##}  "
                + $"W {cell.Width:0.##}  H {cell.Height:0.##}";
            SelectedControlRuntimeCriticalDisplay = string.Empty;
            SelectedControlValidationMessageCount = SelectedDesignItem.ValidationMessages.Count;
            return;
        }

        SelectedControlDisplay = SelectedDesignItem.Name;
        SelectedControlTypeDisplay = _localizationService.GetControlTypeDisplayName(config.ControlType);
        SelectedControlGeometryDisplay =
            $"L {config.Left:0.##}  T {config.Top:0.##}  "
            + $"W {(config.Width?.ToString("0.##") ?? "-")}  "
            + $"H {(config.Height?.ToString("0.##") ?? "-")}";
        SelectedControlRuntimeCriticalDisplay = SelectedDesignItem.IsRuntimeCritical
            ? I18nHelper.GetLocalizedString("RuntimeCriticalControl")
            : string.Empty;
        SelectedControlValidationMessageCount = SelectedDesignItem.ValidationMessages.Count;
    }

    private void RequestPreviewRender(FrontedCanvasConfig? config, FrontedDesignerLayoutCatalogEntry? entry)
    {
        PreviewRenderRequested?.Invoke(
            this,
            new FrontedDesignerPreviewRenderRequestedEventArgs(
                config,
                entry is null
                    ? null
                    : new FrontedRenderContext
                    {
                        WindowId = entry.WindowId,
                        WindowTypeName = entry.WindowTypeName,
                        CanvasName = entry.CanvasName,
                        SharedDataServiceOverride = _designerPreviewSharedDataService,
                        RenderMissingPluginPlaceholders = true,
                        IsDesignerPreview = true
                    }));
    }

    private void RebuildAddControlCatalog()
    {
        AddControlCatalogGroups.Clear();
        foreach (var group in _defaultConfigFactory.GetCatalog())
        {
            AddControlCatalogGroups.Add(group);
        }
    }

    private string? CreateSnapshot()
    {
        if (CurrentDocument is null)
        {
            return null;
        }

        return JsonSerializer.Serialize(_designConverter.ToConfig(CurrentDocument));
    }

    private static bool CanCopyControl(FrontedControlDesignItem? item)
    {
        return item is
        {
            IsSelectableInEditor: true,
            IsEditableInEditor: true,
            IsRuntimeCritical: false
        };
    }

    private static string GeneratePasteName(string sourceName, string controlType, FrontedCanvasDesignDocument document)
    {
        var match = Regex.Match(sourceName, "^(.*?)(\\d+)$", RegexOptions.CultureInvariant);
        var baseName = match.Success ? match.Groups[1].Value : GetNameSeed(controlType);
        var index = match.Success && int.TryParse(match.Groups[2].Value, out var parsed) ? parsed + 1 : 1;
        var existingNames = document.Controls.Select(control => control.Name).ToHashSet(StringComparer.Ordinal);

        while (true)
        {
            var candidate = FrontedTextLimitHelper.Clamp($"{baseName}{index}", FrontedLayoutLimits.MaxControlNameLength);
            if (!existingNames.Contains(candidate) && ValidControlNameRegex.IsMatch(candidate))
            {
                return candidate;
            }

            index++;
        }
    }

    private static string GetNameSeed(string controlType)
    {
        return FrontedPluginControlType.TryParse(controlType, out var parsed)
            ? parsed.ControlTypeName
            : controlType;
    }

    private static object? ClampEditorPropertyValue(
        string propertyName,
        string? controlType,
        object? newValue,
        out bool wasClamped)
    {
        wasClamped = false;
        if (newValue is not string text)
        {
            return newValue;
        }

        var maxLength = FrontedTextLimitHelper.GetMaxLengthForProperty(propertyName, controlType);
        if (maxLength == int.MaxValue)
        {
            return newValue;
        }

        var clamped = FrontedTextLimitHelper.Clamp(text, maxLength);
        wasClamped = !string.Equals(text, clamped, StringComparison.Ordinal);
        return clamped;
    }

    private void RestoreSnapshot(
        string snapshot,
        FrontedDesignerSnapshotRestoreMode mode,
        string traceOperation = "RestoreSnapshot")
    {
        if (CurrentDocument is null)
        {
            return;
        }

        var total = StartDesignerPerfTrace();
        var selectedName = SelectedDesignItem?.Name;
        var selectedCellParentName = SelectedGlobalScoreCellParentName;
        var selectedCellId = SelectedGlobalScoreCellId;
        var windowTypeName = CurrentDocument.WindowTypeName;
        var canvasName = CurrentDocument.CanvasName;
        var config = JsonSerializer.Deserialize<FrontedCanvasConfig>(snapshot);
        LogDesignerPerf(traceOperation, "restore snapshot deserialize", Elapsed(total));
        if (config is null)
        {
            return;
        }

        if (mode == FrontedDesignerSnapshotRestoreMode.PreferGeometryFastPathThenScheduledAtomicPreview
            && TryRestoreGeometryOnlySnapshot(config, traceOperation, total))
        {
            LogDesignerPerf(traceOperation, "total", Elapsed(total));
            return;
        }

        var shouldNotifyUndoRedoInFinally = true;
        SetIsRestoringSnapshotVisuals(true);
        try
        {
            var document = _designConverter.FromConfig(
                windowTypeName,
                canvasName,
                config,
                _runtimeContracts);
            LogDesignerPerf(traceOperation, "design document rebuild", Elapsed(total));
            document.IsDirty = true;
            CurrentDocument = document;
            SelectDesignItem(document.Controls.FirstOrDefault(control =>
                string.Equals(control.Name, selectedName, StringComparison.Ordinal)));
            RestoreGlobalScoreCellSelectionKeys(selectedCellParentName, selectedCellId);
            NormalizeSelectionState();

            switch (mode)
            {
                case FrontedDesignerSnapshotRestoreMode.PreferGeometryFastPathThenScheduledAtomicPreview:
                    _clearRestoreVisualsAfterScheduledPreview = true;
                    shouldNotifyUndoRedoInFinally = false;
                    ScheduleValidationAndPreviewRender(traceOperation);
                    LogDesignerPerf(traceOperation, "scheduled full restore", Elapsed(total));
                    break;

                case FrontedDesignerSnapshotRestoreMode.ImmediatePreviewThenScheduledValidation:
                    RequestPreviewRender(config, SelectedCanvas);
                    LogDesignerPerf(traceOperation, "preview render execution", Elapsed(total));
                    SetIsRestoringSnapshotVisuals(false);
                    ScheduleValidationOnly(traceOperation);
                    LogDesignerPerf(traceOperation, "validation scheduling", Elapsed(total));
                    break;

                case FrontedDesignerSnapshotRestoreMode.ScheduledValidationAndPreview:
                    _clearRestoreVisualsAfterScheduledPreview = true;
                    shouldNotifyUndoRedoInFinally = false;
                    ScheduleValidationAndPreviewRender(traceOperation);
                    LogDesignerPerf(traceOperation, "validation scheduling", Elapsed(total));
                    LogDesignerPerf(traceOperation, "preview render scheduling", Elapsed(total));
                    break;

                case FrontedDesignerSnapshotRestoreMode.ImmediateValidationAndPreview:
                    ApplyValidationMessages(_validator.Validate(document));
                    LogDesignerPerf(traceOperation, "validation execution", Elapsed(total));
                    RequestPreviewRender(config, SelectedCanvas);
                    LogDesignerPerf(traceOperation, "preview render execution", Elapsed(total));
                    SetIsRestoringSnapshotVisuals(false);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }

            RefreshDirtyState();
            LogDesignerPerf(traceOperation, "total", Elapsed(total));
        }
        finally
        {
            if (shouldNotifyUndoRedoInFinally)
            {
                SetIsRestoringSnapshotVisuals(false);
                NotifyUndoRedoCommands();
            }
        }
    }

    private bool TryRestoreGeometryOnlySnapshot(
        FrontedCanvasConfig targetConfig,
        string traceOperation,
        Stopwatch? total)
    {
        if (CurrentDocument is null)
        {
            return false;
        }

        var plan = FrontedDesignerSnapshotRestorePlanner.CreatePlan(
            _designConverter.ToConfig(CurrentDocument),
            targetConfig);
        LogDesignerPerf(traceOperation, $"diff plan: {plan.Reason}", Elapsed(total));
        if (!plan.CanRestoreGeometryOnly)
        {
            return false;
        }

        var currentItemsByName = CurrentDocument.Controls.ToDictionary(item => item.Name, StringComparer.Ordinal);
        var selectedName = SelectedDesignItem?.Name;
        var selectedCellParentName = SelectedGlobalScoreCellParentName;
        var selectedCellId = SelectedGlobalScoreCellId;
        var changedItems = new List<FrontedControlDesignItem>();
        var restoreAppliedToPreview = false;
        var shouldKeepRestoreSuppression = false;

        SetIsRestoringSnapshotVisuals(true);
        try
        {
            foreach (var (name, targetControl) in targetConfig.Controls)
            {
                var item = currentItemsByName[name];
                if (ApplyGeometryPatch(item.Config, targetControl))
                {
                    changedItems.Add(item);
                }
            }

            if (plan.OrderChanged)
            {
                ReorderCurrentDocumentControls(targetConfig.Controls.Keys, currentItemsByName);
                changedItems = CurrentDocument.Controls.ToList();
            }

            CurrentDocument.CanvasConfig.RequiredPlugins = targetConfig.RequiredPlugins;
            CurrentDocument.IsDirty = true;
            RefreshDirtyState();
            if (plan.OrderChanged || plan.ZIndexChanged)
            {
                RebuildFilteredDesignItems();
            }
            else
            {
                RebuildLayerGroups();
            }

            var selectedItem = selectedName is null
                ? null
                : CurrentDocument.Controls.FirstOrDefault(control =>
                    string.Equals(control.Name, selectedName, StringComparison.Ordinal));
            if (!ReferenceEquals(SelectedDesignItem, selectedItem))
            {
                SelectDesignItem(selectedItem);
                RestoreGlobalScoreCellSelectionKeys(selectedCellParentName, selectedCellId);
            }
            else
            {
                NormalizeSelectionState();
                RefreshSelectedControlDisplay();
            }

            OnPropertyChanged(nameof(CanReorderLayers));
            OnPropertyChanged(nameof(LayerReorderHint));
            DeleteSelectedControlCommand.NotifyCanExecuteChanged();
            CopySelectedControlCommand.NotifyCanExecuteChanged();

            var args = new FrontedDesignerGeometryPatchRequestedEventArgs(
                changedItems,
                rebuildLayerPanel: plan.OrderChanged || plan.ZIndexChanged,
                rebuildInteractionLayer: plan.OrderChanged || plan.ZIndexChanged,
                updateSelection: changedItems.Any(item => ReferenceEquals(item, SelectedDesignItem))
                                 || plan.OrderChanged
                                 || plan.ZIndexChanged,
                zIndexChanged: plan.ZIndexChanged);
            DesignerGeometryPatchRequested?.Invoke(this, args);

            if (!args.Applied)
            {
                shouldKeepRestoreSuppression = true;
                _clearRestoreVisualsAfterScheduledPreview = true;
                ScheduleValidationAndPreviewRender(traceOperation);
                LogDesignerPerf(
                    traceOperation,
                    $"scheduled full restore after geometry patch failed: {args.FailureReason}",
                    Elapsed(total));
                return true;
            }

            restoreAppliedToPreview = true;
            ScheduleValidationOnly(traceOperation);
            LogDesignerPerf(traceOperation, $"geometry fast restore: {changedItems.Count} item(s)", Elapsed(total));
            return true;
        }
        finally
        {
            if (!shouldKeepRestoreSuppression)
            {
                SetIsRestoringSnapshotVisuals(false);
                NotifyUndoRedoCommands();
            }
            else if (!restoreAppliedToPreview)
            {
                NotifyUndoRedoCommands();
            }
        }
    }

    private static bool ApplyGeometryPatch(
        FrontedControlConfigBase current,
        FrontedControlConfigBase target)
    {
        var changed = false;
        if (!DoubleEquals(current.Left, target.Left))
        {
            current.Left = target.Left;
            changed = true;
        }

        if (!DoubleEquals(current.Top, target.Top))
        {
            current.Top = target.Top;
            changed = true;
        }

        if (!NullableDoubleEquals(current.Width, target.Width))
        {
            current.Width = target.Width;
            changed = true;
        }

        if (!NullableDoubleEquals(current.Height, target.Height))
        {
            current.Height = target.Height;
            changed = true;
        }

        if (current.ZIndex != target.ZIndex)
        {
            current.ZIndex = target.ZIndex;
            changed = true;
        }

        if (current is BorderedImageFrontedControlConfig currentImage
            && target is BorderedImageFrontedControlConfig targetImage)
        {
            if (!NullableDoubleEquals(currentImage.ImageWidth, targetImage.ImageWidth))
            {
                currentImage.ImageWidth = targetImage.ImageWidth;
                changed = true;
            }

            if (!NullableDoubleEquals(currentImage.ImageHeight, targetImage.ImageHeight))
            {
                currentImage.ImageHeight = targetImage.ImageHeight;
                changed = true;
            }
        }

        return changed;
    }

    private static bool DoubleEquals(double left, double right)
    {
        return Math.Abs(left - right) < 0.0001D;
    }

    private static bool NullableDoubleEquals(double? left, double? right)
    {
        if (!left.HasValue || !right.HasValue)
        {
            return left.HasValue == right.HasValue;
        }

        return Math.Abs(left.Value - right.Value) < 0.0001D;
    }

    private void ReorderCurrentDocumentControls(
        IEnumerable<string> targetOrder,
        IReadOnlyDictionary<string, FrontedControlDesignItem> currentItemsByName)
    {
        if (CurrentDocument is null)
        {
            return;
        }

        var reordered = targetOrder.Select(name => currentItemsByName[name]).ToList();
        CurrentDocument.Controls.Clear();
        foreach (var item in reordered)
        {
            CurrentDocument.Controls.Add(item);
        }
    }

    private void SetIsRestoringSnapshotVisuals(bool value)
    {
        if (_isRestoringSnapshot == value)
        {
            return;
        }

        _isRestoringSnapshot = value;
        OnPropertyChanged(nameof(IsRestoringSnapshotVisuals));
    }

    private void ClearUndoRedo()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        NotifyUndoRedoCommands();
    }

    private void NotifyUndoRedoCommands()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private bool HasIncomingReferences(string controlName)
    {
        if (CurrentDocument is null)
        {
            return false;
        }

        _referenceScanner.SetControls(CurrentDocument.Controls);
        return _referenceScanner.GetIncomingReferences(controlName).Count > 0;
    }

    private void ApplyZoomPreset(FrontedDesignerZoomPreset preset)
    {
        if (preset.IsFit)
        {
            IsFitMode = true;
            UpdateFitZoomFromCurrentDocument();
        }
        else
        {
            ApplyManualZoom(preset.Scale);
        }

        SetSelectedZoomPreset(preset);
    }

    public void ApplyManualZoom(double scale)
    {
        var normalizedScale = Math.Clamp(scale, 0.25D, 2D);
        IsFitMode = false;
        ZoomScale = normalizedScale;
        ZoomDisplay = $"{normalizedScale:P0}";

        var matchingPreset = ZoomPresets.FirstOrDefault(
            preset => !preset.IsFit && Math.Abs(preset.Scale - normalizedScale) < 0.001D);
        SetSelectedZoomPreset(matchingPreset);
    }

    public void ZoomByWheelDelta(int delta)
    {
        if (delta == 0)
        {
            return;
        }

        var multiplier = delta > 0 ? 1.1D : 1D / 1.1D;
        ApplyManualZoom(ZoomScale * multiplier);
    }

    public void UpdateFitZoom(double viewportWidth, double viewportHeight)
    {
        _lastPreviewViewportWidth = viewportWidth;
        _lastPreviewViewportHeight = viewportHeight;
        UpdateFitZoomFromCurrentDocument();
    }

    public void UpdateFitZoom(
        double viewportWidth,
        double viewportHeight,
        double canvasWidth,
        double canvasHeight)
    {
        _lastPreviewViewportWidth = viewportWidth;
        _lastPreviewViewportHeight = viewportHeight;

        if (!IsFitMode)
        {
            return;
        }

        ApplyFitZoom(viewportWidth, viewportHeight, canvasWidth, canvasHeight);
    }

    public static double CalculateFitZoom(
        double viewportWidth,
        double viewportHeight,
        double canvasWidth,
        double canvasHeight,
        double padding = 0D)
    {
        if (canvasWidth <= 0D || canvasHeight <= 0D)
        {
            return 1D;
        }

        var availableWidth = Math.Max(1D, viewportWidth - padding);
        var availableHeight = Math.Max(1D, viewportHeight - padding);
        var scale = Math.Min(availableWidth / canvasWidth, availableHeight / canvasHeight);
        return Math.Clamp(scale, 0.05D, 4D);
    }

    private double GetNextManualZoom(double currentScale)
    {
        return ZoomPresets
            .Where(preset => !preset.IsFit && preset.Scale > currentScale + 0.001D)
            .OrderBy(preset => preset.Scale)
            .FirstOrDefault()?.Scale ?? 4D;
    }

    private double GetPreviousManualZoom(double currentScale)
    {
        return ZoomPresets
            .Where(preset => !preset.IsFit && preset.Scale < currentScale - 0.001D)
            .OrderByDescending(preset => preset.Scale)
            .FirstOrDefault()?.Scale ?? 0.25D;
    }

    private void InitializeZoomPresets()
    {
        if (ZoomPresets.Count > 0)
        {
            return;
        }

        ZoomPresets.Add(new FrontedDesignerZoomPreset("Fit", 0D, isFit: true));
        ZoomPresets.Add(new FrontedDesignerZoomPreset("25%", 0.25D));
        ZoomPresets.Add(new FrontedDesignerZoomPreset("50%", 0.5D));
        ZoomPresets.Add(new FrontedDesignerZoomPreset("75%", 0.75D));
        ZoomPresets.Add(new FrontedDesignerZoomPreset("100%", 1D));
        ZoomPresets.Add(new FrontedDesignerZoomPreset("125%", 1.25D));
        ZoomPresets.Add(new FrontedDesignerZoomPreset("150%", 1.5D));
        ZoomPresets.Add(new FrontedDesignerZoomPreset("200%", 2D));
        ZoomPresets.Add(new FrontedDesignerZoomPreset("300%", 3D));
        ZoomPresets.Add(new FrontedDesignerZoomPreset("400%", 4D));
    }

    private void UpdateFitZoomFromCurrentDocument()
    {
        if (!IsFitMode)
        {
            return;
        }

        var canvas = CurrentDocument?.CanvasConfig;
        if (canvas is null)
        {
            ZoomScale = 1D;
            ZoomDisplay = I18nHelper.GetLocalizedString("Fit");
            return;
        }

        ApplyFitZoom(
            _lastPreviewViewportWidth,
            _lastPreviewViewportHeight,
            canvas.CanvasWidth,
            canvas.CanvasHeight);
    }

    private void ApplyFitZoom(
        double viewportWidth,
        double viewportHeight,
        double canvasWidth,
        double canvasHeight)
    {
        ZoomScale = CalculateFitZoom(viewportWidth, viewportHeight, canvasWidth, canvasHeight);
        ZoomDisplay = $"{I18nHelper.GetLocalizedString("Fit")} ({ZoomScale:P0})";
    }

    private void SetSelectedZoomPreset(FrontedDesignerZoomPreset? preset)
    {
        _isChangingZoomPreset = true;
        SelectedZoomPreset = preset;
        _isChangingZoomPreset = false;
    }

    public bool TryApplyZoomText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            UpdateZoomEditTextFromCurrentZoom();
            return false;
        }

        text = text.Trim();

        var fitKey = I18nHelper.GetLocalizedString("Fit");
        if (string.Equals(text, fitKey, StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, "Fit", StringComparison.OrdinalIgnoreCase))
        {
            FitToWindow();
            return true;
        }

        double scale;
        if (text.EndsWith("x", StringComparison.OrdinalIgnoreCase))
        {
            var numericPart = text.AsSpan(0, text.Length - 1).Trim();
            if (double.TryParse(numericPart, NumberStyles.Float, CultureInfo.InvariantCulture, out var multiplier) && multiplier > 0D)
            {
                scale = multiplier;
            }
            else
            {
                StatusMessage = I18nHelper.GetLocalizedString("Designer.ZoomInvalid");
                UpdateZoomEditTextFromCurrentZoom();
                return false;
            }
        }
        else if (text.EndsWith("%", StringComparison.Ordinal))
        {
            var numericPart = text.AsSpan(0, text.Length - 1).Trim();
            if (double.TryParse(numericPart, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent) && percent > 0D)
            {
                scale = percent / 100D;
            }
            else
            {
                StatusMessage = I18nHelper.GetLocalizedString("Designer.ZoomInvalid");
                UpdateZoomEditTextFromCurrentZoom();
                return false;
            }
        }
        else if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var plainNumber) && plainNumber > 0D)
        {
            scale = plainNumber / 100D;
        }
        else
        {
            StatusMessage = I18nHelper.GetLocalizedString("Designer.ZoomInvalid");
            UpdateZoomEditTextFromCurrentZoom();
            return false;
        }

        ApplyManualZoom(scale);
        return true;
    }

    public void ApplyZoomPercent(double percent)
    {
        ZoomPercent = percent;
    }

    private void UpdateZoomEditTextFromCurrentZoom()
    {
        if (IsFitMode)
        {
            ZoomEditText = I18nHelper.GetLocalizedString("Fit");
        }
        else
        {
            ZoomEditText = $"{ZoomScale:P0}";
        }
    }

    private static FrontedLayoutValidationMessage CreateMessage(
        FrontedLayoutValidationSeverity severity,
        string code,
        string message)
    {
        return new FrontedLayoutValidationMessage
        {
            Severity = severity,
            Code = code,
            Message = message
        };
    }

    private static bool TryConvertPropertyValue(
        PropertyInfo property,
        object? value,
        out object? convertedValue,
        out string errorMessage)
    {
        convertedValue = null;
        errorMessage = string.Empty;

        var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        var text = Convert.ToString(value, CultureInfo.InvariantCulture);

        if (Nullable.GetUnderlyingType(property.PropertyType) is not null
            && string.IsNullOrWhiteSpace(text))
        {
            convertedValue = null;
            return true;
        }

        if (property.DeclaringType == typeof(GlobalScoreCellConfig)
            && property.PropertyType == typeof(string)
            && property.Name is nameof(GlobalScoreCellConfig.FontFamily)
                or nameof(GlobalScoreCellConfig.FontWeight)
                or nameof(GlobalScoreCellConfig.Color)
            && string.IsNullOrWhiteSpace(text))
        {
            convertedValue = null;
            return true;
        }

        try
        {
            if (targetType == typeof(string))
            {
                if (IsColorProperty(property.Name))
                {
                    if (!FrontedPropertyColorHelper.TryParseArgbColor(text, out var color))
                    {
                        errorMessage = I18nHelper.GetLocalizedString("PropertyValidationErrors");
                        return false;
                    }

                    convertedValue = FrontedPropertyColorHelper.ToArgbString(color);
                }
                else
                {
                    convertedValue = text;
                }
            }
            else if (targetType == typeof(bool))
            {
                convertedValue = value is bool boolValue
                    ? boolValue
                    : bool.Parse(text ?? string.Empty);
            }
            else if (targetType.IsEnum)
            {
                convertedValue = value?.GetType() == targetType
                    ? value
                    : Enum.Parse(targetType, text ?? string.Empty, ignoreCase: true);
            }
            else if (targetType == typeof(int))
            {
                convertedValue = Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            else if (targetType == typeof(long))
            {
                convertedValue = Convert.ToInt64(value, CultureInfo.InvariantCulture);
            }
            else if (targetType == typeof(float))
            {
                convertedValue = Convert.ToSingle(value, CultureInfo.InvariantCulture);
            }
            else if (targetType == typeof(decimal))
            {
                convertedValue = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            }
            else if (targetType == typeof(double))
            {
                var doubleValue = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                if (double.IsNaN(doubleValue) || double.IsInfinity(doubleValue))
                {
                    errorMessage = I18nHelper.GetLocalizedString("PropertyValidationErrors");
                    return false;
                }

                convertedValue = NormalizeDoubleProperty(property.Name, doubleValue);
            }
            else
            {
                errorMessage = I18nHelper.GetLocalizedString("PropertyValidationErrors");
                return false;
            }

            return true;
        }
        catch (Exception)
        {
            errorMessage = I18nHelper.GetLocalizedString("PropertyValidationErrors");
            return false;
        }
    }

    private static double NormalizeDoubleProperty(string propertyName, double value)
    {
        if (propertyName is nameof(FrontedControlConfigBase.Left)
            or nameof(FrontedControlConfigBase.Top))
        {
            return FrontedDesignerGeometryHelper.Snap(value);
        }

        if (propertyName is nameof(FrontedControlConfigBase.Width)
            or nameof(FrontedControlConfigBase.Height)
            or nameof(BorderedImageFrontedControlConfig.ImageWidth)
            or nameof(BorderedImageFrontedControlConfig.ImageHeight))
        {
            return Math.Max(
                FrontedDesignerGeometryHelper.MinResizeWidth,
                FrontedDesignerGeometryHelper.Snap(value));
        }

        return value;
    }

    private static bool TryParsePositiveDouble(string text, out double value)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
               && double.IsFinite(value)
               && value > 0D;
    }

    private static bool ValuesEqual(object? left, object? right)
    {
        if (left is double leftDouble && right is double rightDouble)
        {
            return Math.Abs(leftDouble - rightDouble) < 0.0001D;
        }

        return Equals(left, right);
    }

    private static bool IsGeometryProperty(string propertyName)
    {
        return propertyName is nameof(FrontedControlConfigBase.Left)
            or nameof(FrontedControlConfigBase.Top)
            or nameof(FrontedControlConfigBase.Width)
            or nameof(FrontedControlConfigBase.Height);
    }

    private static bool IsColorProperty(string propertyName)
    {
        return propertyName.EndsWith("Color", StringComparison.OrdinalIgnoreCase)
               || propertyName.Equals("Foreground", StringComparison.OrdinalIgnoreCase)
               || propertyName.Equals("Background", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAbsoluteFilePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.StartsWith("bpui://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("pack://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("Resources/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Path.IsPathRooted(Environment.ExpandEnvironmentVariables(value));
    }
}

public sealed class FrontedDesignerWindowOption(
    string windowTypeName,
    string displayName,
    IReadOnlyList<FrontedDesignerLayoutCatalogEntry> canvases)
{
    public string WindowTypeName { get; } = windowTypeName;

    public string DisplayName { get; } = displayName;

    public IReadOnlyList<FrontedDesignerLayoutCatalogEntry> Canvases { get; } = canvases;
}

public sealed class FrontedDesignerZoomPreset(string displayName, double scale, bool isFit = false)
{
    public string DisplayName { get; } = displayName;

    public double Scale { get; } = scale;

    public bool IsFit { get; } = isFit;
}

internal sealed record PendingImportedResource(
    string ResourceUri,
    string PhysicalPath,
    DateTimeOffset ImportedAt,
    string SourceContext);

public sealed class FrontedDesignerClipboardPayload(
    string sourceName,
    string controlType,
    string configJson,
    Type configType)
{
    public string SourceName { get; } = sourceName;

    public string ControlType { get; } = controlType;

    public string ConfigJson { get; } = configJson;

    public Type ConfigType { get; } = configType;

    public static FrontedDesignerClipboardPayload Create(FrontedControlDesignItem item)
    {
        var configType = item.Config.GetType();
        return new FrontedDesignerClipboardPayload(
            item.Name,
            item.Config.ControlType,
            JsonSerializer.Serialize(item.Config, configType),
            configType);
    }

    public FrontedControlConfigBase CreateConfig()
    {
        return (FrontedControlConfigBase?)JsonSerializer.Deserialize(ConfigJson, ConfigType)
               ?? throw new InvalidOperationException("Failed to deserialize copied control config.");
    }
}

public sealed class FrontedCanvasBoModeStateOption(
    FrontedCanvasBoModeState state,
    string displayName)
{
    public FrontedCanvasBoModeState State { get; } = state;

    public string DisplayName { get; } = displayName;
}

public sealed class FrontedDesignerPreviewRenderRequestedEventArgs(
    FrontedCanvasConfig? config,
    FrontedRenderContext? context) : EventArgs
{
    public FrontedCanvasConfig? Config { get; } = config;

    public FrontedRenderContext? Context { get; } = context;
}

public sealed class FrontedDesignerGeometryPatchRequestedEventArgs(
    IReadOnlyList<FrontedControlDesignItem> changedItems,
    bool rebuildLayerPanel,
    bool rebuildInteractionLayer,
    bool updateSelection,
    bool zIndexChanged) : EventArgs
{
    public IReadOnlyList<FrontedControlDesignItem> ChangedItems { get; } = changedItems;

    public bool RebuildLayerPanel { get; } = rebuildLayerPanel;

    public bool RebuildInteractionLayer { get; } = rebuildInteractionLayer;

    public bool UpdateSelection { get; } = updateSelection;

    public bool ZIndexChanged { get; } = zIndexChanged;

    public bool Applied { get; private set; } = true;

    public string? FailureReason { get; private set; }

    public void RequestFullRenderFallback(string reason)
    {
        Applied = false;
        FailureReason = reason;
    }
}

internal sealed class FrontedDesignerSnapshotDiff(
    bool CanRestoreGeometryOnly,
    bool OrderChanged,
    bool ZIndexChanged,
    string Reason)
{
    public bool CanRestoreGeometryOnly { get; } = CanRestoreGeometryOnly;

    public bool OrderChanged { get; } = OrderChanged;

    public bool ZIndexChanged { get; } = ZIndexChanged;

    public string Reason { get; } = Reason;
}

internal static class FrontedDesignerSnapshotRestorePlanner
{
    private static readonly string[] GeometryProperties =
    [
        nameof(FrontedControlConfigBase.Left),
        nameof(FrontedControlConfigBase.Top),
        nameof(FrontedControlConfigBase.Width),
        nameof(FrontedControlConfigBase.Height),
        nameof(FrontedControlConfigBase.ZIndex),
        nameof(BorderedImageFrontedControlConfig.ImageWidth),
        nameof(BorderedImageFrontedControlConfig.ImageHeight)
    ];

    public static FrontedDesignerSnapshotDiff CreatePlan(
        FrontedCanvasConfig current,
        FrontedCanvasConfig target)
    {
        if (current.Version != target.Version
            || Math.Abs(current.CanvasWidth - target.CanvasWidth) >= 0.0001D
            || Math.Abs(current.CanvasHeight - target.CanvasHeight) >= 0.0001D
            || !string.Equals(current.BackgroundImage, target.BackgroundImage, StringComparison.Ordinal)
            || current.EnableBoModeStates != target.EnableBoModeStates
            || !JsonEquivalent(current.BoModeStates, target.BoModeStates)
            || !JsonEquivalent(current.RequiredPlugins, target.RequiredPlugins))
        {
            return Fail("canvas/window config changed");
        }

        if (current.Controls.Count != target.Controls.Count)
        {
            return Fail("control count changed");
        }

        var currentNames = current.Controls.Keys.ToArray();
        var targetNames = target.Controls.Keys.ToArray();
        var orderChanged = !currentNames.SequenceEqual(targetNames, StringComparer.Ordinal);
        if (!currentNames.OrderBy(name => name, StringComparer.Ordinal)
                .SequenceEqual(targetNames.OrderBy(name => name, StringComparer.Ordinal), StringComparer.Ordinal))
        {
            return Fail("control names changed");
        }

        var zIndexChanged = false;
        foreach (var (name, targetControl) in target.Controls)
        {
            var currentControl = current.Controls[name];
            if (!string.Equals(currentControl.ControlType, targetControl.ControlType, StringComparison.Ordinal)
                || currentControl.GetType() != targetControl.GetType())
            {
                return Fail($"control identity changed: {name}");
            }

            if (!JsonEquivalentWithoutGeometry(currentControl, targetControl))
            {
                return Fail($"non-geometry property changed: {name}");
            }

            zIndexChanged |= currentControl.ZIndex != targetControl.ZIndex;
        }

        return new FrontedDesignerSnapshotDiff(
            CanRestoreGeometryOnly: true,
            OrderChanged: orderChanged,
            ZIndexChanged: zIndexChanged,
            Reason: "geometry-only");
    }

    private static FrontedDesignerSnapshotDiff Fail(string reason)
    {
        return new FrontedDesignerSnapshotDiff(false, false, false, reason);
    }

    private static bool JsonEquivalentWithoutGeometry(
        FrontedControlConfigBase current,
        FrontedControlConfigBase target)
    {
        return string.Equals(
            CanonicalNonGeometryJson(current),
            CanonicalNonGeometryJson(target),
            StringComparison.Ordinal);
    }

    private static string CanonicalNonGeometryJson(FrontedControlConfigBase config)
    {
        var json = JsonSerializer.Serialize(config, config.GetType());
        var node = JsonNode.Parse(json)?.AsObject();
        if (node is null)
        {
            IAppHost.TryGetService<ILogger<FrontedDesignerWindowViewModel>>()
                ?.LogError("Failed to parse fronted control config JSON.");
            throw new InvalidOperationException("Failed to parse fronted control config JSON.");
        }
        foreach (var property in GeometryProperties)
        {
            node.Remove(property);
        }

        return node.ToJsonString();
    }

    private static bool JsonEquivalent<T>(T current, T target)
    {
        return string.Equals(
            JsonSerializer.Serialize(current),
            JsonSerializer.Serialize(target),
            StringComparison.Ordinal);
    }
}
