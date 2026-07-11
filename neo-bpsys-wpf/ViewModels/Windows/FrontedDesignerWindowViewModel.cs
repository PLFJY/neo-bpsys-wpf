using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Messages;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Models.ScoreSystem;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Services.FrontedDesigner;
using neo_bpsys_wpf.ViewModels.FrontedDesigner;
using System.ComponentModel;
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
/// 控制设计器快照恢复时如何更新校验和预览视觉状态。
/// </summary>
public enum FrontedDesignerSnapshotRestoreMode
{
    /// <summary>
    /// 优先就地修补几何信息，然后延迟校验；失败时回退到已调度的原子预览。
    /// </summary>
    PreferGeometryFastPathThenScheduledAtomicPreview,

    /// <summary>
    /// 立即渲染预览，然后将校验延迟到已调度的设计器工作队列。
    /// </summary>
    ImmediatePreviewThenScheduledValidation,

    /// <summary>
    /// 将校验和预览一起延迟到已调度的设计器工作队列。
    /// </summary>
    ScheduledValidationAndPreview,

    /// <summary>
    /// 在一次恢复事务中立即运行校验和预览。
    /// </summary>
    ImmediateValidationAndPreview
}

/// <summary>
/// 独立 v3 前台窗口设计器编辑器外壳的视图模型。
/// </summary>
public partial class FrontedDesignerWindowViewModel : ViewModelBase
{
    private static readonly Regex ValidControlNameRegex = new(
        "^[A-Za-z_][A-Za-z0-9_]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly JsonSerializerOptions BehaviorCloneJsonOptions = new()
    {
        MaxDepth = FrontedLayoutLimits.MaxJsonDepth
    };

    private readonly IFrontedLayoutService _layoutService;
    private readonly FrontedLayoutDesignConverter _designConverter;
    private readonly FrontedLayoutValidator _validator;
    private readonly FrontedLayoutReferenceScanner _referenceScanner;
    private readonly FrontedPropertyGridBuilder _propertyGridBuilder;
    private readonly FrontedControlDefaultConfigFactory _defaultConfigFactory;
    private readonly FrontedControlNameGenerator _controlNameGenerator;
    private readonly IFrontedDesignerLocalizationService _localizationService;
    private readonly ISharedDataService _designerPreviewSharedDataService;
    private readonly IFrontedLocalResourceStore? _localResourceStore;
    private readonly IFrontedWindowLayoutOptionsService? _windowLayoutOptionsService;
    private readonly IFrontedLayoutPackageManager? _packageManager;
    private readonly IFrontedWindowService? _frontedWindowService;
    private readonly IFrontedBehaviorService _behaviorService;
    private readonly FrontedDesignerLayoutCatalog _layoutCatalog;
    private readonly IFrontedAnimationRuntime? _animationRuntime;
    private readonly FrontedDesignerPreviewAnimationScope? _previewAnimationScope;
    private readonly IFrontedBehaviorClipboard _behaviorClipboard;
    private readonly FrontedBehaviorCopyPasteService _behaviorCopyPasteService;
    private readonly ILogger<FrontedDesignerWindowViewModel> _logger;
    private readonly ISettingsHostService? _settingsHostService;

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
    private bool _isApplyingDesignSelection;
    private bool _isRefreshingWindowOptions;
    private FrontedControlDesignItem? _lastSelectedDesignItem;
    private DesignerLayerNode? _selectedLayerNode;
    private CancellationTokenSource? _reloadLayoutCancellation;
    private int _reloadLayoutVersion;
    private double _lastPreviewViewportWidth;
    private double _lastPreviewViewportHeight;
    private FrontedWindowSettings _currentWindowSettings = new();
    private FrontedDesignerLayoutCatalogEntry? _selectedCatalogEntry;

#pragma warning disable CS8618
    /// <summary>
    /// 初始化设计器视图模型的设计时实例。
    /// </summary>
    public FrontedDesignerWindowViewModel()
#pragma warning restore CS8618
    {
        // 仅供设计器构造预览使用。
        _layoutService = null!;
        _designConverter = new FrontedLayoutDesignConverter();
        _referenceScanner = new FrontedLayoutReferenceScanner();
        _validator = new FrontedLayoutValidator(
            referenceScanner: _referenceScanner);
        _propertyGridBuilder = new FrontedPropertyGridBuilder();
        _defaultConfigFactory = new FrontedControlDefaultConfigFactory();
        _controlNameGenerator = new FrontedControlNameGenerator();
        _localizationService = new FrontedDesignerLocalizationService();
        _designerPreviewSharedDataService = new DesignerPreviewSharedDataService();
        _localResourceStore = null;
        _windowLayoutOptionsService = null;
        _packageManager = null;
        _frontedWindowService = null;
        _behaviorService = new NoopFrontedBehaviorService();
        _animationRuntime = null;
        _previewAnimationScope = null;
        _behaviorClipboard = new FrontedBehaviorClipboard();
        _behaviorCopyPasteService = new FrontedBehaviorCopyPasteService(
            new FrontedBehaviorControlSemanticResolver(),
            _localizationService);
        _logger = NullLogger<FrontedDesignerWindowViewModel>.Instance;
        _settingsHostService = null;
        BehaviorPanel = CreateBehaviorPanel();
        InitializeZoomPresets();
    }

    /// <summary>
    /// 使用自定义行为服务初始化面向测试的实例。
    /// </summary>
    /// <param name="behaviorService">测试使用的行为服务。</param>
    public FrontedDesignerWindowViewModel(IFrontedBehaviorService behaviorService)
        : this()
    {
        _behaviorService = behaviorService;
    }

    /// <summary>
    /// 初始化生产环境 Designer v3 编辑器视图模型。
    /// </summary>
    /// <param name="layoutCatalog">Catalog of editable v3 layout windows.</param>
    /// <param name="layoutService">布局加载/保存服务。</param>
    /// <param name="designConverter">Converter between runtime layout config and design document.</param>
    /// <param name="validator">布局校验器。</param>
    /// <param name="referenceScanner">Reference scanner used for rename/delete safety.</param>
    /// <param name="propertyGridBuilder">属性网格行构建器。</param>
    /// <param name="defaultConfigFactory">“添加控件”使用的默认控件配置工厂。</param>
    /// <param name="controlNameGenerator">Unique control name generator.</param>
    /// <param name="localizationService">Designer 本地化服务。</param>
    /// <param name="designerPreviewSharedDataService">仅用于预览的共享数据服务。</param>
    /// <param name="localResourceStore">Local package resource store.</param>
    /// <param name="windowLayoutOptionsService">窗口选项持久化服务。</param>
    /// <param name="packageManager">布局包管理器。</param>
    /// <param name="frontedWindowService">Fronted window service used to refresh live output after saves.</param>
    /// <param name="behaviorService">行为文档服务。</param>
    /// <param name="behaviorClipboard">Shared behavior clipboard.</param>
    /// <param name="behaviorCopyPasteService">行为复制/粘贴服务。</param>
    /// <param name="animationRuntime">预览使用的动画运行时。</param>
    /// <param name="previewAnimationScope">预览动画目标作用域。</param>
    /// <param name="logger">Logger.</param>
    /// <param name="settingsHostService">Optional settings host service.</param>
    public FrontedDesignerWindowViewModel(
        FrontedDesignerLayoutCatalog layoutCatalog,
        IFrontedLayoutService layoutService,
        FrontedLayoutDesignConverter designConverter,
        FrontedLayoutValidator validator,
        FrontedLayoutReferenceScanner referenceScanner,
        FrontedPropertyGridBuilder propertyGridBuilder,
        FrontedControlDefaultConfigFactory defaultConfigFactory,
        FrontedControlNameGenerator controlNameGenerator,
        IFrontedDesignerLocalizationService localizationService,
        DesignerPreviewSharedDataService designerPreviewSharedDataService,
        IFrontedLocalResourceStore localResourceStore,
        IFrontedWindowLayoutOptionsService windowLayoutOptionsService,
        IFrontedLayoutPackageManager packageManager,
        IFrontedWindowService frontedWindowService,
        IFrontedBehaviorService behaviorService,
        IFrontedBehaviorClipboard behaviorClipboard,
        FrontedBehaviorCopyPasteService behaviorCopyPasteService,
        IFrontedAnimationRuntime animationRuntime,
        FrontedDesignerPreviewAnimationScope previewAnimationScope,
        ILogger<FrontedDesignerWindowViewModel> logger,
        ISettingsHostService? settingsHostService = null)
    {
        _layoutService = layoutService;
        _designConverter = designConverter;
        _validator = validator;
        _referenceScanner = referenceScanner;
        _propertyGridBuilder = propertyGridBuilder;
        _defaultConfigFactory = defaultConfigFactory;
        _controlNameGenerator = controlNameGenerator;
        _localizationService = localizationService;
        _designerPreviewSharedDataService = designerPreviewSharedDataService;
        _localResourceStore = localResourceStore;
        _windowLayoutOptionsService = windowLayoutOptionsService;
        _packageManager = packageManager;
        _frontedWindowService = frontedWindowService;
        _behaviorService = behaviorService;
        _behaviorClipboard = behaviorClipboard;
        _behaviorCopyPasteService = behaviorCopyPasteService;
        _animationRuntime = animationRuntime;
        _previewAnimationScope = previewAnimationScope;
        _logger = logger;
        _settingsHostService = settingsHostService;
        _layoutCatalog = layoutCatalog;
        BehaviorPanel = CreateBehaviorPanel();

        RebuildWindowOptions(preserveSelectedWindowTypeName: null);

        InitializeZoomPresets();
        RebuildAddControlCatalog();
        SelectedZoomPreset = ZoomPresets.FirstOrDefault();
    }

    /// <summary>
    /// 需要视图渲染或清除预览画布时触发。
    /// </summary>
    public event EventHandler<FrontedDesignerPreviewRenderRequestedEventArgs>? PreviewRenderRequested;

    /// <summary>
    /// 撤销/重做恢复可通过修补现有预览元素完成时触发。
    /// </summary>
    public event EventHandler<FrontedDesignerGeometryPatchRequestedEventArgs>? DesignerGeometryPatchRequested;

    /// <summary>
    /// 获取窗口选择器中显示的可编辑窗口选项。
    /// </summary>
    public ObservableCollection<FrontedDesignerWindowOption> WindowOptions { get; } = [];

    /// <summary>
    /// 在不重新加载当前布局的情况下刷新本地化窗口名称。
    /// </summary>
    public void RefreshWindowDisplayNames()
    {
        RebuildWindowOptions(SelectedWindow?.WindowTypeName);
        if (_selectedCatalogEntry is null)
        {
            return;
        }

        CurrentWindowCanvasDisplay = ResolveEntryDisplayName(_selectedCatalogEntry);
        LoadWindowOptions(_selectedCatalogEntry.WindowTypeName);
    }

    /// <summary>
    /// 获取当前设计文档的校验消息。
    /// </summary>
    public ObservableCollection<FrontedLayoutValidationMessage> ValidationMessages { get; } = [];

    /// <summary>
    /// 获取 Designer 工具栏中显示的缩放预设项。
    /// </summary>
    public ObservableCollection<FrontedDesignerZoomPreset> ZoomPresets { get; } = [];

    /// <summary>
    /// ComboBox 下拉框使用的缩放预设，不包含自适应项。
    /// </summary>
    public IEnumerable<FrontedDesignerZoomPreset> ManualZoomPresets =>
        ZoomPresets.Where(p => !p.IsFit).ToList();

    /// <summary>
    /// 获取应用图层/搜索过滤后的设计项。
    /// </summary>
    public ObservableCollection<FrontedControlDesignItem> FilteredDesignItems { get; } = [];

    /// <summary>
    /// 获取分组后的图层树节点。
    /// </summary>
    public ObservableCollection<FrontedLayerGroup> LayerGroups { get; } = [];

    public ObservableCollection<GlobalScoreCellConfig> GlobalScoreCellEditorItems { get; } = [];

    /// <summary>
    /// 获取选中 MapV2Display 复合控件暴露的固定内部样式部件。
    /// </summary>
    public ObservableCollection<MapV2InternalStylePartOption> MapV2InternalStylePartOptions { get; } =
    [
        new(MapV2InternalStylePart.TeamName, I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.MapV2Display.Part.TeamName")),
        new(MapV2InternalStylePart.MapCard, I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.MapV2Display.Part.MapCard")),
        new(MapV2InternalStylePart.MapName, I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.MapV2Display.Part.MapName")),
        new(MapV2InternalStylePart.CampName, I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.MapV2Display.Part.CampName")),
        new(MapV2InternalStylePart.PickingBorder, I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.MapV2Display.Part.PickingBorder"))
    ];

    public DesignerLayerNode? SelectedLayerNode
    {
        get => _selectedLayerNode;
        private set => SetProperty(ref _selectedLayerNode, value);
    }

    public ObservableCollection<FrontedPropertyEditorItem> PropertyEditorItems { get; } = [];

    /// <summary>
    /// 获取当前选中的设计控件；主选中项仍保留在 <see cref="SelectedDesignItem"/>。
    /// </summary>
    public ObservableCollection<FrontedControlDesignItem> SelectedDesignItems { get; } = [];

    /// <summary>
    /// 获取选中控件上配置的动画部件。
    /// </summary>
    public ObservableCollection<FrontedAnimationPartConfig> AnimationPartEditorItems { get; } = [];

    public ObservableCollection<FrontedAddControlCatalogGroup> AddControlCatalogGroups { get; } = [];

    public BehaviorPanelViewModel BehaviorPanel { get; private set; }

    public ObservableCollection<FrontedCanvasBoModeStateOption> BoModeStateOptions { get; } =
    [
        new(FrontedCanvasBoModeState.Bo5, I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.Canvas.Bo5State")),
        new(FrontedCanvasBoModeState.Bo3, I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.Canvas.Bo3State"))
    ];

    public bool IsRebuildingPropertyGrid => _isRebuildingPropertyGrid;

    [ObservableProperty]
    public partial FrontedDesignerWindowOption? SelectedWindow { get; set; }

    [ObservableProperty]
    public partial FrontedCanvasDesignDocument? CurrentDocument { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSaveLayout))]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    public partial bool AreBehaviorsDirty { get; set; }

    [ObservableProperty]
    public partial string LayoutSourcePath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LayoutSourceDisplay { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CurrentWindowCanvasDisplay { get; set; } = string.Empty;

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
                return I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "SnapOn");
            }

            return IsShiftSnapActive
                ? I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "TemporarySnap")
                : I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "SnapOff");
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

    /// <summary>
    /// 获取是否已选中 MapV2Display 内部样式部件。
    /// </summary>
    public bool HasSelectedMapV2InternalStylePart => SelectedMapV2InternalStylePart is not null;

    /// <summary>
    /// 获取选中 MapV2Display 内部部件的已持久化布局。
    /// </summary>
    public MapV2InternalPartLayoutConfig? SelectedMapV2InternalPartLayout
    {
        get
        {
            if (SelectedDesignItem?.Config is not MapV2DisplayControlConfig config
                || SelectedMapV2InternalStylePart is not { } option)
            {
                return null;
            }

            MapV2InternalPartLayoutHelper.EnsureParts(config);
            return config.InternalParts.First(part => part.Part == option.Part);
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedMapV2InternalStylePart))]
    private MapV2InternalStylePartOption? _selectedMapV2InternalStylePart;

    [ObservableProperty]
    private bool _isMapV2InternalStyleEditorVisible;

    public bool IsPolygonSelected => SelectedDesignItem?.Config is IPolygonFrontedControlConfig;

    /// <summary>
    /// 获取是否已选择用于动画部件编辑的控件。
    /// </summary>
    public bool HasAnimationPartEditor => SelectedDesignItem is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedAnimationPart))]
    private FrontedAnimationPartConfig? _selectedAnimationPart;

    [ObservableProperty]
    private FrontedAnimationPartEditorViewModel? _AnimationPartEditBuffer;

    /// <summary>
    /// 获取是否已选中动画部件。
    /// </summary>
    public bool HasSelectedAnimationPart => SelectedAnimationPart is not null;

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
        && !HasSelectedMapV2InternalStylePart
        && SelectedDesignItem is { IsSelectableInEditor: true, IsEditableInEditor: true };

    public bool CanCopySelectedControl =>
        !HasSelectedGlobalScoreCell
        && !HasSelectedMapV2InternalStylePart
        && CanCopyControl(SelectedDesignItem);

    public bool CanPasteControl => CurrentDocument is not null && _copiedControl is not null;

    public bool HasPendingScheduledDesignerWork => _scheduledValidationAndPreviewPending;

    public int ScheduledDesignerValidationExecutionCount { get; private set; }

    public int ScheduledDesignerPreviewExecutionCount { get; private set; }

    /// <summary>
    /// 快照恢复正以一个事务更新预览视觉和选择状态时为 true。
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
        : I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.LayerPanel.ClearFilterToReorder");

    [ObservableProperty]
    private string _selectedControlDisplay = string.Empty;

    [ObservableProperty]
    private string _selectedControlTypeDisplay = string.Empty;

    [ObservableProperty]
    private string _selectedControlGeometryDisplay = string.Empty;

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
    private string _windowWidthEditText = string.Empty;

    [ObservableProperty]
    private string _windowHeightEditText = string.Empty;

    [ObservableProperty]
    private string _windowOptionsStatus = string.Empty;

    partial void OnSelectedWindowChanged(FrontedDesignerWindowOption? value)
    {
        if (_isRefreshingWindowOptions)
        {
            return;
        }

        ControlFilterText = string.Empty;
        SelectDesignItem(null);

        if (value is null)
        {
            _selectedCatalogEntry = null;
            _currentWindowSettings = new FrontedWindowSettings();
            return;
        }

        _selectedCatalogEntry = _layoutCatalog?.GetEntries()
            .FirstOrDefault(e => e.WindowTypeName == value.WindowTypeName);
        _currentWindowSettings = new FrontedWindowSettings();
        LoadWindowOptions(value.WindowTypeName);
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
        if (!_isApplyingDesignSelection)
        {
            SetSelectedDesignItems(value is null ? [] : [value], value);
            return;
        }

        ClearActiveSnapGuides();
        _propertyEditErrors.Clear();
        _propertyEditBuffers.Clear();
        ClearSelectedGlobalScoreCell();
        SelectedMapV2InternalStylePart = null;
        IsMapV2InternalStyleEditorVisible = false;
        SelectedPolygonVertexIndex = value?.Config is IPolygonFrontedControlConfig polygon && polygon.Points.Count > 0
            ? 0
            : -1;
        ApplyDesignSelectionFlags();

        BehaviorPanel.SetSelectedControl(SelectedDesignItems.Count > 1 ? null : value);
        RefreshSelectedControlDisplay();
        RebuildGlobalScoreCellEditorItems();
        RebuildAnimationPartEditorItems();
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
        OnPropertyChanged(nameof(HasAnimationPartEditor));
        OnPropertyChanged(nameof(SelectedPolygonVertexDisplay));
        OnPropertyChanged(nameof(CanRemovePolygonVertex));
        RemovePolygonVertexCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedPolygonVertexIndexChanged(int value)
    {
        RemovePolygonVertexCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedMapV2InternalStylePartChanged(MapV2InternalStylePartOption? value)
    {
        if (value is not null && SelectedDesignItem?.Config is MapV2DisplayControlConfig config)
        {
            MapV2InternalPartLayoutHelper.EnsureParts(config);
        }

        _propertyEditErrors.Clear();
        _propertyEditBuffers.Clear();
        OnPropertyChanged(nameof(SelectedMapV2InternalPartLayout));
        OnPropertyChanged(nameof(CanDeleteSelectedControl));
        OnPropertyChanged(nameof(CanCopySelectedControl));
        DeleteSelectedControlCommand.NotifyCanExecuteChanged();
        CopySelectedControlCommand.NotifyCanExecuteChanged();
        RebuildPropertyEditorItems();
        RefreshSelectedControlDisplay();
    }

    partial void OnIsMapV2InternalStyleEditorVisibleChanged(bool value)
    {
        SelectedMapV2InternalStylePart = value
            ? SelectedMapV2InternalStylePart ?? MapV2InternalStylePartOptions[0]
            : null;
    }

    partial void OnSelectedAnimationPartChanged(FrontedAnimationPartConfig? value)
    {
        if (AnimationPartEditBuffer is not null)
        {
            AnimationPartEditBuffer.ErrorsChanged -= AnimationPartEditBuffer_OnErrorsChanged;
        }

        AnimationPartEditBuffer = value is null
            ? null
            : new FrontedAnimationPartEditorViewModel(value, candidate =>
                GetSelectedBehaviorSet(create: false)?.AnimationParts.All(item =>
                    ReferenceEquals(item, value)
                    || !string.Equals(item.Name, candidate, StringComparison.OrdinalIgnoreCase)) == true);
        if (AnimationPartEditBuffer is not null)
        {
            AnimationPartEditBuffer.ErrorsChanged += AnimationPartEditBuffer_OnErrorsChanged;
        }

        RemoveAnimationPartCommand.NotifyCanExecuteChanged();
        ApplyAnimationPartEditCommand.NotifyCanExecuteChanged();
    }

    private void AnimationPartEditBuffer_OnErrorsChanged(object? sender, DataErrorsChangedEventArgs e)
    {
        ApplyAnimationPartEditCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void AddAnimationPart()
    {
        if (CurrentDocument is null || SelectedDesignItem is null)
        {
            return;
        }

        var set = GetSelectedBehaviorSet(create: true);
        if (set is null)
        {
            return;
        }

        CaptureUndoSnapshot();
        var names = set.AnimationParts
            .Select(item => item.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var index = 1;
        var defaultName = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.AnimationParts.DefaultName");
        var name = defaultName;
        while (names.Contains(name))
        {
            name = $"{defaultName}{index++}";
        }

        var created = new FrontedAnimationPartConfig
        {
            Name = name,
            Kind = FrontedAnimationPartKind.Rectangle,
            Layer = FrontedAnimationPartLayer.AboveContent,
            Width = 4,
            HeightText = "100%",
            Fill = "#FFFFFFFF",
            Opacity = 1D,
            Visibility = "Hidden",
            ZIndex = 10,
            IsHitTestVisible = false
        };
        set.AnimationParts.Add(created);
        MarkBehaviorsDirty();
        RebuildAnimationPartEditorItems(created);
        FinishBehaviorPartEdit();
    }

    private bool CanRemoveAnimationPart() => SelectedAnimationPart is not null;

    [RelayCommand(CanExecute = nameof(CanRemoveAnimationPart))]
    private void RemoveAnimationPart()
    {
        if (CurrentDocument is null || SelectedDesignItem is null || SelectedAnimationPart is null)
        {
            return;
        }

        var set = GetSelectedBehaviorSet(create: false);
        if (set is null)
        {
            return;
        }

        CaptureUndoSnapshot();
        set.AnimationParts.Remove(SelectedAnimationPart);
        MarkBehaviorsDirty();
        RebuildAnimationPartEditorItems();
        FinishBehaviorPartEdit();
    }

    private bool CanApplyAnimationPartEdit() =>
        SelectedAnimationPart is not null
        && AnimationPartEditBuffer is { HasErrors: false };

    [RelayCommand(CanExecute = nameof(CanApplyAnimationPartEdit))]
    private void ApplyAnimationPartEdit()
    {
        if (CurrentDocument is null
            || SelectedDesignItem is null
            || SelectedAnimationPart is null
            || AnimationPartEditBuffer is null)
        {
            return;
        }

        AnimationPartEditBuffer.ValidateAll();
        if (AnimationPartEditBuffer.HasErrors)
        {
            StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.AnimationParts.Validation.FixErrors");
            return;
        }

        CaptureUndoSnapshot();
        AnimationPartEditBuffer.ApplyTo(SelectedAnimationPart);
        MarkBehaviorsDirty();
        RebuildAnimationPartEditorItems(SelectedAnimationPart);
        FinishBehaviorPartEdit();
    }

    private void RebuildAnimationPartEditorItems(FrontedAnimationPartConfig? selected = null)
    {
        AnimationPartEditorItems.Clear();
        foreach (var item in GetSelectedBehaviorSet(create: false)?.AnimationParts ?? [])
        {
            AnimationPartEditorItems.Add(item);
        }

        SelectedAnimationPart = selected ?? AnimationPartEditorItems.FirstOrDefault();
    }

    private ControlBehaviorSet? GetSelectedBehaviorSet(bool create)
    {
        if (SelectedDesignItem is null)
        {
            return null;
        }

        if (SelectedDesignItem.Config.BehaviorGuid == Guid.Empty)
        {
            if (!create)
            {
                return null;
            }

            SelectedDesignItem.Config.BehaviorGuid = FrontedBehaviorGuidHelper.NewGuid();
            CurrentDocument!.IsDirty = true;
        }

        return create
            ? BehaviorPanel.CurrentDocument.GetOrCreateSet(SelectedDesignItem.Config.BehaviorGuid, SelectedDesignItem.Name)
            : BehaviorPanel.CurrentDocument.FindSet(SelectedDesignItem.Config.BehaviorGuid);
    }

    private void FinishBehaviorPartEdit()
    {
        RefreshDirtyState();
        RequestPreviewRenderCurrentDocument();
        _previewAnimationScope?.RefreshTargets();
    }

    /// <summary>
    /// 根据资源浏览器选择更新选中动画部件的图片编辑缓冲。
    /// </summary>
    /// <param name="selectedResourcePath">选中的内置、包内或绝对图片路径。</param>
    /// <returns><see langword="true"/> when the edit buffer was updated.</returns>
    public bool ApplyAnimationPartImageResourceSelection(string selectedResourcePath)
    {
        if (AnimationPartEditBuffer is not { IsImage: true } editor)
        {
            return false;
        }

        if (!IsAbsoluteFilePath(selectedResourcePath))
        {
            editor.ImagePath = selectedResourcePath;
            return true;
        }

        return StoreLocalAnimationPartImage(selectedResourcePath);
    }

    /// <summary>
    /// 导入本地图片，并更新选中图片动画部件的编辑缓冲。
    /// </summary>
    /// <param name="sourcePath">Absolute local image path.</param>
    /// <returns><see langword="true"/> when the image was imported and selected.</returns>
    public bool StoreLocalAnimationPartImage(string sourcePath)
    {
        if (_localResourceStore is null || AnimationPartEditBuffer is not { IsImage: true } editor)
        {
            return false;
        }

        try
        {
            var result = _localResourceStore.StoreImageWithResult(sourcePath);
            editor.ImagePath = result.ResourceUri;
            RecordPendingImportedResource(result, "AnimationPart ImagePath", wasApplied: true);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to store local animation part image.");
            StatusMessage = $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "FailedToApplyPicture")}: {ex.Message}";
            return false;
        }
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
            StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "InputTruncated");
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
        if (_isLoadingWindowOptions || SelectedWindow is null)
        {
            return;
        }

        _ = SaveWindowOptionsAsync(restartWindowForTransparencyChange: true, applyBackgroundImmediately: false);
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
            ? I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.Canvas.BoModeStatesEnabled")
            : I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.Canvas.BoModeStatesDisabledConfirm");
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

    /// <summary>
    /// 通过命令包装器重新加载当前布局。
    /// </summary>
    /// <returns>重新加载完成后结束的任务。</returns>
    [RelayCommand]
    private async Task ReloadLayoutAsync()
    {
        await ReloadLayoutCoreAsync();
    }

    /// <summary>
    /// 重新加载选中布局、行为文档、预览状态、校验消息和编辑器选择。
    /// </summary>
    /// <returns>布局完成加载或清除后结束的任务。</returns>
    public async Task ReloadLayoutCoreAsync()
    {
        if (SelectedWindow is null || _selectedCatalogEntry is null)
        {
            ClearLoadedLayout(CreateMessage(
                FrontedLayoutValidationSeverity.Error,
                "LayoutSelectionMissing",
                "Window selection is required."));
            return;
        }

        var entry = _selectedCatalogEntry;
        CurrentWindowCanvasDisplay = ResolveEntryDisplayName(entry);
        DirtyIndicatorText = string.Empty;
        var reloadVersion = StartReloadLayoutRequest();
        var cancellationToken = _reloadLayoutCancellation?.Token ?? CancellationToken.None;

        try
        {
            var loadResult = await _layoutService.LoadWindowConfigWithMetadataAsync(
                entry.WindowTypeName,
                cancellationToken);
            if (cancellationToken.IsCancellationRequested || reloadVersion != _reloadLayoutVersion)
            {
                return;
            }

            ApplyLayoutSource(loadResult, entry);

            var windowConfig = loadResult.Config;
            if (windowConfig is null)
            {
                ClearLoadedLayout(CreateMessage(
                    FrontedLayoutValidationSeverity.Error,
                    "MissingLayout",
                    loadResult.Error ?? $"Layout file was not found for {entry.WindowTypeName}."));
                return;
            }

            var document = _designConverter.FromConfig(
                entry.WindowTypeName,
                FrontedLayoutConstants.BaseCanvasName,
                FrontedWindowConfigCanvasAdapter.ToCanvasConfig(windowConfig));

            _currentWindowSettings = CloneWindowSettings(windowConfig.WindowSettings);
            ControlFilterText = string.Empty;
            CurrentDocument = document;
            CurrentDocument.IsDirty = false;
            LoadWindowOptions(entry.WindowTypeName);
            var behaviorDocument = await _behaviorService.LoadDocumentAsync(
                entry.WindowTypeName,
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
            RequestPreviewRender(FrontedWindowConfigCanvasAdapter.ToCanvasConfig(windowConfig), entry);
            RefreshDirtyState();
        }
        catch (OperationCanceledException)
        {
            // 更新的窗口/画布选择已经取代本次加载请求。
        }
        catch (Exception ex)
        {
            if (reloadVersion != _reloadLayoutVersion)
            {
                return;
            }

            _logger.LogError(
                ex,
                "Failed to load fronted designer layout. Window: {WindowTypeName}",
                entry.WindowTypeName);

            ClearLoadedLayout(CreateMessage(
                FrontedLayoutValidationSeverity.Error,
                "LayoutLoadFailed",
                ex.Message));
        }
    }

    /// <summary>
    /// 通过命令包装器保存当前布局，并保留可见的校验失败消息。
    /// </summary>
    /// <returns>保存成功或失败后结束的任务。</returns>
    [RelayCommand(CanExecute = nameof(CanSaveLayout))]
    private async Task SaveLayoutAsync()
    {
        await SaveCurrentLayoutAsync();
    }

    /// <summary>
    /// 校验当前布局和行为文档，并保存到活动布局包。
    /// </summary>
    /// <returns><see langword="true"/> when save completed successfully.</returns>
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
                StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "CannotSaveInvalidLayout");
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
                StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "CannotSaveInvalidLayout");
                return false;
            }
        }

        try
        {
            var wasBuiltInSource = string.Equals(
                LayoutSourceDisplay,
                I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "LayoutSourceBuiltIn"),
                StringComparison.Ordinal);

            if (shouldSaveLayout)
            {
                var config = _designConverter.ToConfig(CurrentDocument);
                config.Version = 3;
                var windowConfig = FrontedWindowConfigCanvasAdapter.FromCanvasConfig(config);
                windowConfig.WindowSettings = CloneWindowSettings(_currentWindowSettings);
                await _layoutService.SaveWindowConfigAsync(
                    CurrentDocument.WindowTypeName,
                    windowConfig);

                CleanupPendingImportedResources(includeCurrentDocument: true);
                CurrentDocument.IsDirty = false;
            }

            if (shouldSaveBehaviors)
            {
                BehaviorPanel.CurrentDocument.WindowType = CurrentDocument.WindowTypeName;
                BehaviorPanel.CurrentDocument.CanvasName = FrontedLayoutConstants.BaseCanvasName;
                await _behaviorService.SaveDocumentAsync(BehaviorPanel.CurrentDocument);
                AreBehaviorsDirty = false;
            }

            if (shouldSaveLayout || wasBuiltInSource)
            {
                var savedResult = await _layoutService.LoadWindowConfigWithMetadataAsync(
                    CurrentDocument.WindowTypeName);
                if (_selectedCatalogEntry is not null)
                {
                    ApplyLayoutSource(savedResult, _selectedCatalogEntry);
                }
                else
                {
                    LayoutSourceDisplay = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "LayoutSourceUser");
                    LayoutSourcePath = savedResult.Path ?? string.Empty;
                }
            }

            StatusMessage = wasBuiltInSource
                ? I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "EditableLayoutSchemeCreated")
                : I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "LayoutSaved");
            if (wasBuiltInSource)
            {
                WeakReferenceMessenger.Default.Send(new FrontedLayoutPackagesChangedMessage(this, null));
            }

            RefreshDirtyState();

            if ((shouldSaveLayout || shouldSaveBehaviors) && _frontedWindowService is not null)
            {
                _frontedWindowService.MarkWindowLayoutDirty(CurrentDocument.WindowTypeName);
                await _frontedWindowService.ReloadFrontedLayoutsAsync();
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to save fronted designer user layout. Window: {WindowTypeName}",
                CurrentDocument.WindowTypeName);
            StatusMessage = $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "LayoutSaveFailed")}: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// 通过命令包装器重置当前布局。
    /// </summary>
    /// <returns>重置处理完成后结束的任务。</returns>
    [RelayCommand(CanExecute = nameof(CanResetToBuiltIn))]
    private async Task ResetToBuiltInAsync()
    {
        await ResetToBuiltInCoreAsync();
    }

    /// <summary>
    /// 使用内置包版本替换当前可编辑布局。
    /// </summary>
    /// <returns><see langword="true"/> when reset succeeded.</returns>
    public async Task<bool> ResetToBuiltInCoreAsync()
    {
        if (CurrentDocument is null)
        {
            return false;
        }

        var windowTypeName = CurrentDocument.WindowTypeName;
        var canvasName = FrontedLayoutConstants.BaseCanvasName;
        var config = await LoadBuiltInLayoutForResetAsync(windowTypeName, canvasName);
        if (config is null)
        {
            StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "MissingLayout");
            return false;
        }

        var document = _designConverter.FromConfig(
            windowTypeName,
            canvasName,
            config);
        document.IsDirty = false;

        ControlFilterText = string.Empty;
        CurrentDocument = document;
        ResetBehaviorDocument(new FrontedBehaviorDocument
        {
            Version = 1,
            WindowType = windowTypeName,
            CanvasName = FrontedLayoutConstants.BaseCanvasName
        });
        SelectDesignItem(null);
        ApplyValidationMessages(_validator.Validate(document));
        RequestPreviewRender(config, _selectedCatalogEntry);
        LayoutSourceDisplay = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "LayoutSourceBuiltIn");
        LayoutSourcePath = GetBuiltInPackageLayoutPath(windowTypeName);
        StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "LayoutReset");
        ClearUndoRedo();
        CleanupPendingImportedResources(includeCurrentDocument: false);
        RefreshDirtyState();
        return true;
    }

    /// <summary>
    /// 更新临时 Shift 键吸附状态，而不改变已持久化的吸附开关。
    /// </summary>
    /// <param name="isActive">当前 Shift 吸附是否处于活动状态。</param>
    public void UpdateShiftSnapActive(bool isActive)
    {
        IsShiftSnapActive = isActive;
    }

    /// <summary>
    /// 运行完整布局校验并更新校验面板。
    /// </summary>
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

    /// <summary>
    /// 恢复上一个 Designer 撤销快照。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        if (CurrentDocument is null || _undoStack.Count == 0)
        {
            StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "CannotUndo");
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
        StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Undo");
        LogDesignerPerf("Undo", "total", Elapsed(total));
    }

    /// <summary>
    /// 恢复下一个 Designer 重做快照。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        if (CurrentDocument is null || _redoStack.Count == 0)
        {
            StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "CannotRedo");
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
        StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Redo");
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

    /// <summary>
    /// 添加由“添加控件”目录项描述的新控件。
    /// </summary>
    /// <param name="parameter">Expected to be a <see cref="FrontedAddControlCatalogItem"/>.</param>
    [RelayCommand]
    private void AddControl(object? parameter)
    {
        if (CurrentDocument is null)
        {
            StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "CannotAddControl");
            return;
        }

        var request = parameter as FrontedAddControlRequest;
        var controlType = request?.ControlType ?? Convert.ToString(parameter, CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(controlType) || !_defaultConfigFactory.CanCreate(controlType))
        {
            StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "UnsupportedControlType");
            return;
        }

        if (CurrentDocument.Controls.Count >= FrontedLayoutLimits.MaxControlsPerCanvas)
        {
            StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "ControlCountLimitReached");
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

        CurrentDocument.Controls.Add(item);
        CurrentDocument.IsDirty = true;
        RefreshDirtyState();
        ControlFilterText = string.Empty;
        RebuildFilteredDesignItems();
        SelectDesignItem(item);
        ValidateCurrentDocument();
        RequestPreviewRenderCurrentDocument();
        StatusMessage = $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "AddedControl")}: {item.Name}";
    }

    /// <summary>
    /// 将选中的可编辑控件复制到 Designer 控件剪贴板。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCopySelectedControl))]
    private void CopySelectedControl()
    {
        var selected = SelectedDesignItem;
        if (selected is null || !CanCopyControl(selected))
        {
            StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "CannotCopyControl");
            return;
        }

        _copiedControl = FrontedDesignerClipboardPayload.Create(selected);
        PasteControlCommand.NotifyCanExecuteChanged();
        StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "CopyControl");
    }

    /// <summary>
    /// 将复制的控件粘贴到当前文档，并分配不冲突的名称。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanPasteControl))]
    private void PasteControl()
    {
        if (CurrentDocument is null || _copiedControl is null)
        {
            StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "CannotPasteControl");
            return;
        }

        if (CurrentDocument.Controls.Count + 1 > FrontedLayoutLimits.MaxControlsPerCanvas)
        {
            StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "ControlCountLimitReached");
            return;
        }

        var copiedControl = _copiedControl;
        if (copiedControl is null)
        {
            StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "CannotPasteControl");
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
        StatusMessage = $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "PasteControl")}: {item.Name}";
        LogDesignerPerf("Paste", "total", Elapsed(total));
    }

    /// <summary>
    /// 在完成引用和运行时关键检查后删除选中的可编辑控件。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDeleteSelectedControl))]
    private void DeleteSelectedControl()
    {
        if (CurrentDocument is null || SelectedDesignItem is null)
        {
            return;
        }

        if (!SelectedDesignItem.IsEditableInEditor || !SelectedDesignItem.IsSelectableInEditor)
        {
            StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "CannotDeleteReferencedControl");
            return;
        }

        _referenceScanner.SetControls(CurrentDocument.Controls);
        if (_referenceScanner.GetIncomingReferences(SelectedDesignItem.Name).Count > 0)
        {
            StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "CannotDeleteReferencedControl");
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
        StatusMessage = $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "DeleteSelectedControl")}: {deletedName}";
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
    private void ToggleMapV2InternalStyleEditor()
    {
        if (SelectedDesignItem?.Config is not MapV2DisplayControlConfig)
        {
            return;
        }

        IsMapV2InternalStyleEditorVisible = !IsMapV2InternalStyleEditorVisible;
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
            .Where(item => item.Config is MapV2DisplayControlConfig)
            .ToArray();
        if (targets.Length == 0)
        {
            return;
        }

        CaptureUndoSnapshot();
        foreach (var target in targets.Select(item => (MapV2DisplayControlConfig)item.Config))
        {
            CopyMapV2DisplayStyle(source, target);
        }

        ApplyMapV2DisplayBehaviorSetToTargets(SelectedDesignItem, source, targets);
        CurrentDocument.IsDirty = true;
        RebuildPropertyEditorItems();
        RefreshDirtyState();
        ValidateCurrentDocument();
        RequestPreviewRenderCurrentDocument();
        StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.MapV2Display.StyleAppliedToAll");
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
        MapV2InternalPartLayoutHelper.EnsureParts(source);
        target.InternalParts = source.InternalParts
            .Select(part => new MapV2InternalPartLayoutConfig
            {
                Part = part.Part,
                X = part.X,
                Y = part.Y,
                Width = part.Width,
                Height = part.Height
            })
            .ToList();
    }

    private void ApplyMapV2DisplayBehaviorSetToTargets(
        FrontedControlDesignItem sourceItem,
        MapV2DisplayControlConfig source,
        IReadOnlyList<FrontedControlDesignItem> targetItems)
    {
        var sourceSet = source.BehaviorGuid == Guid.Empty
            ? null
            : BehaviorPanel.CurrentDocument.FindSet(source.BehaviorGuid);
        foreach (var targetItem in targetItems)
        {
            if (targetItem.Config is not MapV2DisplayControlConfig target)
            {
                continue;
            }

            if (target.BehaviorGuid != Guid.Empty)
            {
                BehaviorPanel.CurrentDocument.RemoveSet(target.BehaviorGuid);
            }

            if (sourceSet is null)
            {
                continue;
            }

            if (target.BehaviorGuid == Guid.Empty)
            {
                target.BehaviorGuid = FrontedBehaviorGuidHelper.NewGuid();
            }

            var clonedSet = CloneBehaviorSet(sourceSet);
            clonedSet.BehaviorGuid = target.BehaviorGuid;
            clonedSet.DisplayName = targetItem.Name;
            foreach (var behavior in clonedSet.Behaviors)
            {
                behavior.BehaviorId = FrontedBehaviorGuidHelper.NewGuid();
                RegenerateMapV2BehaviorGraphIds(behavior);
                RewriteMapV2BehaviorTargetsAndFilters(
                    behavior,
                    source.BehaviorGuid,
                    target.BehaviorGuid,
                    source.MapKey,
                    target.MapKey);
            }

            BehaviorPanel.CurrentDocument.ControlBehaviorSets.Add(clonedSet);
        }

        MarkBehaviorsDirty();
        BehaviorPanel.SetCopyContext(CurrentDocument?.WindowTypeName, CurrentDocument?.Controls);
        BehaviorPanel.SetSelectedControl(sourceItem);
    }

    private static ControlBehaviorSet CloneBehaviorSet(ControlBehaviorSet source)
    {
        var json = JsonSerializer.Serialize(source, BehaviorCloneJsonOptions);
        return JsonSerializer.Deserialize<ControlBehaviorSet>(json, BehaviorCloneJsonOptions)
               ?? throw new InvalidOperationException("Unable to clone fronted behavior set.");
    }

    private static void RewriteMapV2BehaviorTargetsAndFilters(
        FrontedBehavior behavior,
        Guid sourceGuid,
        Guid targetGuid,
        string sourceMapKey,
        string targetMapKey)
    {
        foreach (var node in EnumerateBehaviorGraphs(behavior).SelectMany(graph => graph.Nodes))
        {
            if (node.Properties.TryGetValue("Target", out var targetValue)
                && targetValue.ValueKind == JsonValueKind.String)
            {
                var parsed = FrontedAnimationTargetReference.Parse(targetValue.GetString());
                if (parsed.BehaviorGuid == sourceGuid)
                {
                    node.Properties["Target"] = JsonSerializer.SerializeToElement(
                        parsed.Kind == FrontedAnimationTargetReferenceKind.GeneratedPart
                            ? $"part:{targetGuid}:{parsed.PartName}"
                            : $"guid:{targetGuid}");
                }
            }

            if (string.Equals(node.NodeType, "flow.if", StringComparison.Ordinal)
                && TryGetStringNodeProperty(node, "Left", out var left)
                && IsMapV2MapKeyFilterLeft(left)
                && TryGetStringNodeProperty(node, "Right", out var right)
                && string.Equals(right, sourceMapKey, StringComparison.Ordinal))
            {
                node.Properties["Right"] = JsonSerializer.SerializeToElement(targetMapKey);
            }
        }

        foreach (var filter in EnumerateBehaviorTriggerFilters(behavior))
        {
            if (IsMapV2MapKeyFilterLeft(filter.Left)
                && string.Equals(filter.Right, sourceMapKey, StringComparison.Ordinal))
            {
                filter.Right = targetMapKey;
            }
        }
    }

    private static bool IsMapV2MapKeyFilterLeft(string left) =>
        string.Equals(left, "Event.MapKey", StringComparison.Ordinal)
        || string.Equals(left, "StartEvent.MapKey", StringComparison.Ordinal)
        || string.Equals(left, "StopEvent.MapKey", StringComparison.Ordinal);

    private static IEnumerable<FrontedNodeGraph> EnumerateBehaviorGraphs(FrontedBehavior behavior)
    {
        yield return behavior.Graph;
        yield return behavior.StartGraph;
        yield return behavior.LoopGraph;
        yield return behavior.StopGraph;
        yield return behavior.ExitGraph;
        yield return behavior.EnterGraph;
    }

    private static IEnumerable<TriggerFilter> EnumerateBehaviorTriggerFilters(FrontedBehavior behavior) =>
        new[] { behavior.Trigger, behavior.StartTrigger, behavior.TransitionTrigger }
            .Concat(behavior.StopTriggers)
            .Where(trigger => trigger is not null)
            .SelectMany(trigger => trigger!.Filters);

    private static bool TryGetStringNodeProperty(FrontedNode node, string propertyName, out string value)
    {
        value = string.Empty;
        if (!node.Properties.TryGetValue(propertyName, out var element)
            || element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = element.GetString() ?? string.Empty;
        return true;
    }

    private static void RegenerateMapV2BehaviorGraphIds(FrontedBehavior behavior)
    {
        foreach (var graph in EnumerateBehaviorGraphs(behavior))
        {
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

    [RelayCommand]
    private void ApplyCanvasSize()
    {
        ApplyCanvasSizeEdit(CanvasWidthEditText, CanvasHeightEditText);
    }

    /// <summary>
    /// 应用画布设置编辑器中的 Canvas 宽高文本。
    /// </summary>
    /// <param name="widthText">Canvas 宽度文本。</param>
    /// <param name="heightText">Canvas 高度文本。</param>
    /// <returns><see langword="true"/> when both values were valid and applied.</returns>
    public bool ApplyCanvasSizeEdit(string widthText, string heightText)
    {
        if (CurrentDocument is null)
        {
            return false;
        }

        if (!TryParsePositiveDouble(widthText, out var width)
            || !TryParsePositiveDouble(heightText, out var height))
        {
            CanvasPropertiesStatus = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "CanvasSizeMustBePositive");
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
        FinishCanvasConfigEdit(I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "CanvasPropertiesApplied"));
        return true;
    }

    [RelayCommand]
    private void ApplyWindowSize()
    {
        ApplyWindowSizeEdit(WindowWidthEditText, WindowHeightEditText);
    }

    /// <summary>
    /// 应用窗口设置编辑器中的前台窗口宽高文本。
    /// </summary>
    /// <param name="widthText">窗口宽度文本。</param>
    /// <param name="heightText">窗口高度文本。</param>
    /// <returns><see langword="true"/> when both values were valid and applied.</returns>
    public bool ApplyWindowSizeEdit(string widthText, string heightText)
    {
        if (SelectedWindow is null)
        {
            return false;
        }

        if (!TryParseOptionalPositiveDouble(widthText).HasValue
            && !TryParseOptionalPositiveDouble(heightText).HasValue)
        {
            // 允许同时清空两个字段，并保存为 null。
        }
        else if (!TryParseOptionalPositiveDouble(widthText).HasValue
                 || !TryParseOptionalPositiveDouble(heightText).HasValue)
        {
            WindowOptionsStatus = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "WindowSizeMustBePositive");
            return false;
        }

        _ = SaveWindowOptionsAsync(
            restartWindowForTransparencyChange: false,
            applyBackgroundImmediately: false,
            applyWindowSizeImmediately: true);
        return true;
    }

    /// <summary>
    /// 将字符串解析为正数 double；字符串为空或空白时返回 <c>null</c>。
    /// </summary>
    /// <param name="text">输入文本，或 <c>null</c>。</param>
    /// <returns>正数 double；输入为空或空白时返回 <c>null</c>。</returns>
    /// <summary>
    /// 从编辑器文本解析可选正数。
    /// </summary>
    /// <param name="text">Text to parse.</param>
    /// <returns>解析后的值；文本为空或无效时返回 <see langword="null"/>。</returns>
    private static double? TryParseOptionalPositiveDouble(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return TryParsePositiveDouble(text!, out var value) ? value : null;
    }

    [RelayCommand]
    private void ApplyBackgroundImage()
    {
        ApplyCanvasBackgroundEdit(BackgroundImageEditText);
    }

    /// <summary>
    /// 根据文本输入应用 Canvas 背景图片 URI。
    /// </summary>
    /// <param name="backgroundImage">Background image URI or path.</param>
    /// <returns><see langword="true"/> when the edit was accepted.</returns>
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
            CanvasPropertiesStatus = I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "InputTruncated");
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
        FinishCanvasConfigEdit(I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "CanvasPropertiesApplied"));
        return true;
    }

    /// <summary>
    /// 应用资源浏览器选中的 Canvas 背景图片。
    /// </summary>
    /// <param name="selectedResourcePath">选中的资源 URI 或文件路径。</param>
    /// <returns><see langword="true"/> when the resource was accepted.</returns>
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

    /// <summary>
    /// 清除活动 Canvas 状态的背景图片。
    /// </summary>
    /// <returns><see langword="true"/> when a document was available and updated.</returns>
    public bool ClearCanvasBackground()
    {
        return ApplyCanvasBackgroundEdit(null);
    }

    /// <summary>
    /// 将本地背景图片复制到可编辑包资源存储，并应用其 BPUI URI。
    /// </summary>
    /// <param name="sourcePath">Local image file selected by the user.</param>
    /// <returns><see langword="true"/> when the file was imported and applied.</returns>
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
            CanvasPropertiesStatus = $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "FailedToApplyPicture")}: {ex.Message}";
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
        CanvasPropertiesStatus = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.Canvas.Bo3LayoutCopied");
    }

    /// <summary>
    /// 将资源浏览器结果应用到图片/资源属性行。
    /// </summary>
    /// <param name="item">接收资源值的属性行。</param>
    /// <param name="selectedResourcePath">选中的资源 URI 或文件路径。</param>
    /// <returns><see langword="true"/> when the property was updated.</returns>
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
                    $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "FailedToApplyPicture")}: {ex.Message}",
                    selectedResourcePath);
                return false;
            }
        }

        return ApplyPropertyEdit(item, selectedResourcePath);
    }

    /// <summary>
    /// 将字体文件导入活动布局包，并应用第一个发现的字体族。
    /// </summary>
    /// <param name="item">Font family property row.</param>
    /// <param name="sourcePath">源字体文件路径。</param>
    /// <returns>是否已导入并应用字体。</returns>
    /// <summary>
    /// 将字体导入当前布局包，并把其 BPUI 字体 URI 应用到属性行。
    /// </summary>
    /// <param name="item">Font-family property row receiving the imported font URI.</param>
    /// <param name="sourcePath">Local font file path.</param>
    /// <returns><see langword="true"/> when import and property application succeeded.</returns>
    public async Task<bool> ImportAndApplyPackageFontAsync(FrontedPropertyEditorItem item, string sourcePath)
    {
        if (_localResourceStore is null || _packageManager is null)
        {
            return false;
        }

        if (item.EditorKind != FrontedPropertyEditorKind.FontFamily)
        {
            return false;
        }

        try
        {
            var package = await _packageManager.EnsureWritableActivePackageAsync();
            var packageRoot = Path.Combine(_packageManager.GetPackageRootFolder(), package.PackageId);
            var results = _localResourceStore.StorePackageFontWithResult(sourcePath, package.PackageId, packageRoot);
            var first = results.FirstOrDefault();
            if (first is null)
            {
                SetPropertyEditError(item, "UnsupportedFontFormat", sourcePath);
                return false;
            }

            _propertyGridBuilder.ClearFontFamilyOptionCache();
            item.Options = _propertyGridBuilder.GetFontFamilyOptions();
            item.Value = first.ResourceUri;
            item.EditText = first.FontFamilyName;
            return ApplyPropertyEdit(item, first.ResourceUri);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to import package font.");
            SetPropertyEditError(item, $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.Editor.ImportFontFailed")}: {ex.Message}", sourcePath);
            return false;
        }
    }

    /// <summary>
    /// 刷新当前可见字体族编辑行的字体选项。
    /// </summary>
    /// <summary>
    /// 包字体变化后重建字体族编辑器选项。
    /// </summary>
    public void RefreshFontFamilyEditorOptions()
    {
        _propertyGridBuilder.ClearFontFamilyOptionCache();
        var options = _propertyGridBuilder.GetFontFamilyOptions();
        foreach (var item in PropertyEditorItems.Where(item => item.EditorKind == FrontedPropertyEditorKind.FontFamily))
        {
            item.Options = options;
        }
    }

    [RelayCommand]
    private void ResetWindowOptions()
    {
        if (SelectedWindow is null)
        {
            return;
        }

        _ = ResetWindowOptionsAsync();
    }

    /// <summary>
    /// 将非致命渲染错误添加到校验/状态面板。
    /// </summary>
    /// <summary>
    /// 将预览渲染异常转换为编辑器校验消息。
    /// </summary>
    /// <param name="exception">Render exception.</param>
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

    /// <summary>
    /// 选择一个设计项，并刷新属性、行为和图层选择状态。
    /// </summary>
    /// <param name="item">Item to select, or <see langword="null"/> to clear selection.</param>
    public void SelectDesignItem(FrontedControlDesignItem? item)
    {
        if (item?.IsSelectableInEditor == false)
        {
            item = null;
        }

        SetSelectedDesignItems(item is null ? [] : [item], item);
        ClearSelectedGlobalScoreCell();
    }

    /// <summary>
    /// 选择多个设计控件，并将其中一个设为属性网格的主目标。
    /// </summary>
    /// <param name="items">要选中的控件。</param>
    /// <param name="primaryItem">主选中控件；省略时使用第一个选中控件。</param>
    /// <summary>
    /// 在一次选择事务中选择多个设计项。
    /// </summary>
    /// <param name="items">Items to select.</param>
    /// <param name="primaryItem">Primary item used for property editing.</param>
    public void SelectDesignItems(
        IEnumerable<FrontedControlDesignItem> items,
        FrontedControlDesignItem? primaryItem = null)
    {
        if (CurrentDocument is null)
        {
            SelectDesignItem(null);
            return;
        }

        var selected = items
            .Where(item => item.IsSelectableInEditor && CurrentDocument.Controls.Contains(item))
            .Distinct()
            .ToList();
        var primary = primaryItem is not null && selected.Contains(primaryItem)
            ? primaryItem
            : selected.FirstOrDefault();

        SetSelectedDesignItems(selected, primary);
        ClearSelectedGlobalScoreCell();
    }

    /// <summary>
    /// 从当前多选中添加或移除一个控件。
    /// </summary>
    /// <param name="item">要切换选中状态的控件。</param>
    /// <summary>
    /// 切换多选集合中的一个项目。
    /// </summary>
    /// <param name="item">Item to toggle.</param>
    public void ToggleDesignItemSelection(FrontedControlDesignItem item)
    {
        if (CurrentDocument is null || !item.IsSelectableInEditor || !CurrentDocument.Controls.Contains(item))
        {
            return;
        }

        var selected = SelectedDesignItems.ToList();
        if (selected.Contains(item))
        {
            selected.Remove(item);
            SetSelectedDesignItems(selected, ReferenceEquals(SelectedDesignItem, item) ? selected.LastOrDefault() : SelectedDesignItem);
        }
        else
        {
            selected.Add(item);
            SetSelectedDesignItems(selected, item);
        }

        ClearSelectedGlobalScoreCell();
    }

    /// <summary>
    /// 选择 <see cref="GlobalScoreRowControlConfig"/> 设计项内部的子单元格。
    /// </summary>
    /// <param name="parent">Parent global score row item.</param>
    /// <param name="cell">Cell to select, or <see langword="null"/> to clear child selection.</param>
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

    /// <summary>
    /// 选择图层树节点，并在可能时将该选择同步到设计画布。
    /// </summary>
    /// <param name="node">Layer node to select.</param>
    public void SelectLayerNode(DesignerLayerNode? node)
    {
        if (node is null || !node.CanSelect)
        {
            ClearSelection();
            return;
        }

        SelectDesignItem(node.ControlItem);
    }

    /// <summary>
    /// 切换图层树节点的展开状态。
    /// </summary>
    /// <param name="node">Layer node to expand or collapse.</param>
    public void ToggleLayerNodeExpansion(DesignerLayerNode node)
    {
    }

    /// <summary>
    /// 清除设计画布、图层树、属性和行为选择状态。
    /// </summary>
    public void ClearSelection()
    {
        ClearSelectedGlobalScoreCell();
        SelectDesignItem(null);
    }

    /// <summary>
    /// 应用完整选择集合，同时保留用于属性编辑的主项目。
    /// </summary>
    /// <param name="items">Items that should become selected.</param>
    /// <param name="primaryItem">Primary selected item.</param>
    private void SetSelectedDesignItems(
        IReadOnlyCollection<FrontedControlDesignItem> items,
        FrontedControlDesignItem? primaryItem)
    {
        var selected = items
            .Where(item => item.IsSelectableInEditor)
            .Distinct()
            .ToList();
        var primary = primaryItem is not null && selected.Contains(primaryItem)
            ? primaryItem
            : selected.FirstOrDefault();

        _isApplyingDesignSelection = true;
        try
        {
            SelectedDesignItems.Clear();
            foreach (var item in selected)
            {
                SelectedDesignItems.Add(item);
            }

            SelectedDesignItem = primary;
            if (ReferenceEquals(SelectedDesignItem, primary))
            {
                ApplyDesignSelectionFlags();
            }

            OnPropertyChanged(nameof(SelectedDesignItems));
        }
        finally
        {
            _isApplyingDesignSelection = false;
        }

        RefreshLayerNodeSelection();
    }

    /// <summary>
    /// 获取可参与移动或缩放操作的选中设计项。
    /// </summary>
    /// <returns>Movable selected items.</returns>
    private IReadOnlyList<FrontedControlDesignItem> GetMovableSelectedDesignItems()
    {
        if (SelectedDesignItems.Count == 0)
        {
            return SelectedDesignItem is { IsSelectableInEditor: true, IsEditableInEditor: true } item
                ? [item]
                : [];
        }

        return SelectedDesignItems
            .Where(item => item.IsSelectableInEditor && item.IsEditableInEditor)
            .ToList();
    }

    /// <summary>
    /// 当前选择指向全局比分行单元格时获取该单元格。
    /// </summary>
    /// <param name="parentItem">Parent design item.</param>
    /// <param name="row">Parent global score row config.</param>
    /// <param name="cell">选中的单元格。</param>
    /// <returns><see langword="true"/> when a cell is selected.</returns>
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

    /// <summary>
    /// 清除全局比分子单元格选择。
    /// </summary>
    /// <param name="notify">是否触发依赖属性通知。</param>
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

    /// <summary>
    /// 按逻辑增量从拖拽起点移动主选中设计项。
    /// </summary>
    /// <param name="originalLeft">Drag-start left coordinate.</param>
    /// <param name="originalTop">Drag-start top coordinate.</param>
    /// <param name="deltaX">Horizontal pointer delta.</param>
    /// <param name="deltaY">Vertical pointer delta.</param>
    /// <param name="renderPreview">是否请求更新预览。</param>
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

        if (HasSelectedMapV2InternalStylePart)
        {
            MoveSelectedMapV2InternalPart(originalLeft, originalTop, deltaX, deltaY, renderPreview);
            return;
        }

        var selectedItems = GetMovableSelectedDesignItems();
        if (selectedItems.Count > 1)
        {
            var changedItems = new List<FrontedControlDesignItem>();
            foreach (var selectedItem in selectedItems)
            {
                FrontedDesignerGeometryHelper.Move(
                    selectedItem,
                    selectedItem.Config.Left,
                    selectedItem.Config.Top,
                    deltaX,
                    deltaY,
                    CurrentDocument,
                    EffectiveSnapEnabled,
                    SnapGridSize);
                changedItems.Add(selectedItem);
                foreach (var linkedOverlay in SyncLinkedOverlays(selectedItem))
                {
                    if (!changedItems.Contains(linkedOverlay))
                    {
                        changedItems.Add(linkedOverlay);
                    }
                }
            }

            CurrentDocument.IsDirty = true;
            ClearActiveSnapGuides();
            OnDesignItemGeometryChanged(renderPreview);
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

    /// <summary>
    /// 按逻辑增量从原始拖拽起点边界移动选中控件。
    /// </summary>
    /// <param name="originalBounds">Original bounds captured when the drag started.</param>
    /// <param name="deltaX">Horizontal pointer delta.</param>
    /// <param name="deltaY">Vertical pointer delta.</param>
    /// <param name="renderPreview">是否立即渲染完整预览。</param>
    public void MoveSelectedDesignItems(
        IReadOnlyDictionary<FrontedControlDesignItem, FrontedDesignerResolvedBounds> originalBounds,
        double deltaX,
        double deltaY,
        bool renderPreview)
    {
        if (CurrentDocument is null || originalBounds.Count == 0)
        {
            return;
        }

        var selectedItems = GetMovableSelectedDesignItems();
        if (selectedItems.Count <= 1)
        {
            var bounds = originalBounds.Values.FirstOrDefault();
            MoveSelectedDesignItem(
                originalBounds.Count > 0 ? bounds.Left : SelectedDesignItem?.Config.Left ?? 0D,
                originalBounds.Count > 0 ? bounds.Top : SelectedDesignItem?.Config.Top ?? 0D,
                deltaX,
                deltaY,
                renderPreview);
            return;
        }

        foreach (var selectedItem in selectedItems)
        {
            if (!originalBounds.TryGetValue(selectedItem, out var bounds))
            {
                continue;
            }

            FrontedDesignerGeometryHelper.Move(
                selectedItem,
                bounds.Left,
                bounds.Top,
                deltaX,
                deltaY,
                CurrentDocument,
                EffectiveSnapEnabled,
                SnapGridSize);
            SyncLinkedOverlays(selectedItem);
        }

        CurrentDocument.IsDirty = true;
        ClearActiveSnapGuides();
        OnDesignItemGeometryChanged(renderPreview);
    }

    /// <summary>
    /// 按增量移动当前选择，通常用于键盘微调。
    /// </summary>
    /// <param name="deltaX">Horizontal delta.</param>
    /// <param name="deltaY">Vertical delta.</param>
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

        if (SelectedMapV2InternalPartLayout is { } internalPart)
        {
            MoveSelectedMapV2InternalPart(
                internalPart.X,
                internalPart.Y,
                deltaX,
                deltaY,
                renderPreview: true);
            return;
        }

        var selectedItems = GetMovableSelectedDesignItems();
        if (selectedItems.Count > 1)
        {
            var batchChangedItems = new List<FrontedControlDesignItem>();
            foreach (var selectedItem in selectedItems)
            {
                FrontedDesignerGeometryHelper.MoveBy(
                    selectedItem,
                    deltaX,
                    deltaY,
                    CurrentDocument,
                    EffectiveSnapEnabled,
                    SnapGridSize);
                batchChangedItems.Add(selectedItem);
                foreach (var linkedOverlay in SyncLinkedOverlays(selectedItem))
                {
                    if (!batchChangedItems.Contains(linkedOverlay))
                    {
                        batchChangedItems.Add(linkedOverlay);
                    }
                }
            }

            OnDesignItemGeometryChanged(renderPreview: false);
            RequestDesignerGeometryPatch(batchChangedItems, updateSelection: true);
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

    /// <summary>
    /// 通过拖拽手柄缩放主选中设计项。
    /// </summary>
    /// <param name="handle">Resize handle being dragged.</param>
    /// <param name="originalLeft">Drag-start left coordinate.</param>
    /// <param name="originalTop">Drag-start top coordinate.</param>
    /// <param name="originalWidth">Drag-start width.</param>
    /// <param name="originalHeight">Drag-start height.</param>
    /// <param name="deltaX">Horizontal drag delta.</param>
    /// <param name="deltaY">Vertical drag delta.</param>
    /// <param name="renderPreview">是否请求更新预览。</param>
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

        if (HasSelectedMapV2InternalStylePart)
        {
            ResizeSelectedMapV2InternalPart(
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

        var selectedItems = GetMovableSelectedDesignItems();
        if (selectedItems.Count > 1)
        {
            var changedItems = new List<FrontedControlDesignItem>();
            foreach (var selectedItem in selectedItems)
            {
                var bounds = FrontedDesignerBoundsResolver.Resolve(selectedItem.Config);
                FrontedDesignerGeometryHelper.Resize(
                    selectedItem,
                    handle,
                    selectedItem.Config.Left,
                    selectedItem.Config.Top,
                    bounds.Width,
                    bounds.Height,
                    deltaX,
                    deltaY,
                    CurrentDocument,
                    EffectiveSnapEnabled,
                    SnapGridSize);
                changedItems.Add(selectedItem);
                foreach (var linkedOverlay in SyncLinkedOverlays(selectedItem))
                {
                    if (!changedItems.Contains(linkedOverlay))
                    {
                        changedItems.Add(linkedOverlay);
                    }
                }
            }

            CurrentDocument.IsDirty = true;
            ClearActiveSnapGuides();
            OnDesignItemGeometryChanged(renderPreview);
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

    /// <summary>
    /// 按逻辑增量从原始拖拽起点边界缩放选中控件。
    /// </summary>
    /// <param name="handle">正在拖拽的缩放手柄。</param>
    /// <param name="originalBounds">Original bounds captured when the resize started.</param>
    /// <param name="deltaX">Horizontal pointer delta.</param>
    /// <param name="deltaY">Vertical pointer delta.</param>
    /// <param name="renderPreview">是否立即渲染完整预览。</param>
    public void ResizeSelectedDesignItems(
        FrontedDesignerResizeHandleKind handle,
        IReadOnlyDictionary<FrontedControlDesignItem, FrontedDesignerResolvedBounds> originalBounds,
        double deltaX,
        double deltaY,
        bool renderPreview)
    {
        if (CurrentDocument is null || originalBounds.Count == 0)
        {
            return;
        }

        var selectedItems = GetMovableSelectedDesignItems();
        if (selectedItems.Count <= 1)
        {
            var bounds = originalBounds.Values.FirstOrDefault();
            ResizeSelectedDesignItem(
                handle,
                originalBounds.Count > 0 ? bounds.Left : SelectedDesignItem?.Config.Left ?? 0D,
                originalBounds.Count > 0 ? bounds.Top : SelectedDesignItem?.Config.Top ?? 0D,
                originalBounds.Count > 0 ? bounds.Width : SelectedDesignItem?.Config.Width ?? FrontedDesignerGeometryHelper.MinHitWidth,
                originalBounds.Count > 0 ? bounds.Height : SelectedDesignItem?.Config.Height ?? FrontedDesignerGeometryHelper.MinHitHeight,
                deltaX,
                deltaY,
                renderPreview);
            return;
        }

        foreach (var selectedItem in selectedItems)
        {
            if (!originalBounds.TryGetValue(selectedItem, out var bounds))
            {
                continue;
            }

            FrontedDesignerGeometryHelper.Resize(
                selectedItem,
                handle,
                bounds.Left,
                bounds.Top,
                bounds.Width,
                bounds.Height,
                deltaX,
                deltaY,
                CurrentDocument,
                EffectiveSnapEnabled,
                SnapGridSize);
            SyncLinkedOverlays(selectedItem);
        }

        CurrentDocument.IsDirty = true;
        ClearActiveSnapGuides();
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

    /// <summary>
    /// 在父级边界内移动选中的 MapV2Display 内部部件。
    /// </summary>
    /// <param name="originalX">Original relative X coordinate.</param>
    /// <param name="originalY">Original relative Y coordinate.</param>
    /// <param name="deltaX">Horizontal pointer delta.</param>
    /// <param name="deltaY">Vertical pointer delta.</param>
    /// <param name="renderPreview">是否立即渲染预览。</param>
    public void MoveSelectedMapV2InternalPart(
        double originalX,
        double originalY,
        double deltaX,
        double deltaY,
        bool renderPreview)
    {
        if (CurrentDocument is null
            || SelectedDesignItem?.Config is not MapV2DisplayControlConfig parent
            || SelectedMapV2InternalPartLayout is not { } part)
        {
            return;
        }

        var parentWidth = Math.Max(1D, parent.Width ?? part.X + part.Width);
        var parentHeight = Math.Max(1D, parent.Height ?? part.Y + part.Height);
        part.X = Math.Clamp(FrontedDesignerGeometryHelper.Snap(originalX + deltaX), 0D, Math.Max(0D, parentWidth - part.Width));
        part.Y = Math.Clamp(FrontedDesignerGeometryHelper.Snap(originalY + deltaY), 0D, Math.Max(0D, parentHeight - part.Height));
        CurrentDocument.IsDirty = true;
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

    /// <summary>
    /// 在父级边界内缩放选中的 MapV2Display 内部部件。
    /// </summary>
    /// <param name="handle">Active resize handle.</param>
    /// <param name="originalX">Original relative X coordinate.</param>
    /// <param name="originalY">Original relative Y coordinate.</param>
    /// <param name="originalWidth">Original width.</param>
    /// <param name="originalHeight">Original height.</param>
    /// <param name="deltaX">Horizontal pointer delta.</param>
    /// <param name="deltaY">Vertical pointer delta.</param>
    /// <param name="renderPreview">是否立即渲染预览。</param>
    public void ResizeSelectedMapV2InternalPart(
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
            || SelectedDesignItem?.Config is not MapV2DisplayControlConfig
            || SelectedMapV2InternalPartLayout is not { } part)
        {
            return;
        }

        var left = originalX;
        var top = originalY;
        var width = originalWidth;
        var height = originalHeight;
        if (handle is FrontedDesignerResizeHandleKind.Left or FrontedDesignerResizeHandleKind.TopLeft or FrontedDesignerResizeHandleKind.BottomLeft)
        {
            left += deltaX;
            width -= deltaX;
        }
        else if (handle is FrontedDesignerResizeHandleKind.Right or FrontedDesignerResizeHandleKind.TopRight or FrontedDesignerResizeHandleKind.BottomRight)
        {
            width += deltaX;
        }

        if (handle is FrontedDesignerResizeHandleKind.Top or FrontedDesignerResizeHandleKind.TopLeft or FrontedDesignerResizeHandleKind.TopRight)
        {
            top += deltaY;
            height -= deltaY;
        }
        else if (handle is FrontedDesignerResizeHandleKind.Bottom or FrontedDesignerResizeHandleKind.BottomLeft or FrontedDesignerResizeHandleKind.BottomRight)
        {
            height += deltaY;
        }

        part.X = FrontedDesignerGeometryHelper.Snap(left);
        part.Y = FrontedDesignerGeometryHelper.Snap(top);
        part.Width = Math.Max(FrontedDesignerGeometryHelper.MinResizeWidth, FrontedDesignerGeometryHelper.Snap(width));
        part.Height = Math.Max(FrontedDesignerGeometryHelper.MinResizeHeight, FrontedDesignerGeometryHelper.Snap(height));
        ClampSelectedMapV2InternalPart();
        CurrentDocument.IsDirty = true;
        OnDesignItemGeometryChanged(renderPreview);
    }

    private void ClampSelectedMapV2InternalPart()
    {
        if (SelectedDesignItem?.Config is not MapV2DisplayControlConfig parent
            || SelectedMapV2InternalPartLayout is not { } part)
        {
            return;
        }

        var parentWidth = Math.Max(1D, parent.Width ?? part.X + part.Width);
        var parentHeight = Math.Max(1D, parent.Height ?? part.Y + part.Height);
        var minimumWidth = Math.Min(FrontedDesignerGeometryHelper.MinResizeWidth, parentWidth);
        var minimumHeight = Math.Min(FrontedDesignerGeometryHelper.MinResizeHeight, parentHeight);
        part.Width = Math.Clamp(part.Width, minimumWidth, parentWidth);
        part.Height = Math.Clamp(part.Height, minimumHeight, parentHeight);
        part.X = Math.Clamp(part.X, 0D, Math.Max(0D, parentWidth - part.Width));
        part.Y = Math.Clamp(part.Y, 0D, Math.Max(0D, parentHeight - part.Height));
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
            StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.LayerPanel.ReorderBlocked");
            return false;
        }

        if (targetItem is not null && !IsLayerReorderable(targetItem))
        {
            StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.LayerPanel.ReorderBlocked");
            return false;
        }

        var reorderableItems = CurrentDocument.Controls
            .Where(IsLayerReorderable)
            .ToList();
        if (!reorderableItems.Contains(source))
        {
            StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.LayerPanel.ReorderBlocked");
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
            StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.LayerPanel.ReorderBlocked");
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
        StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.LayerPanel.Reordered");
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
            StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.LayerPanel.ReorderBlocked");
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

        if (!item.IsMultiSelectionBatchEditable)
        {
            return false;
        }

        if (item.IsMultiSelectionMixedValue && IsEmptyMultiSelectionPlaceholderValue(newValue))
        {
            return true;
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

        if (SelectedMapV2InternalPartLayout is { } internalPart
            && typeof(MapV2InternalPartLayoutConfig).GetProperty(item.PropertyName) is { CanWrite: true } internalProperty)
        {
            return ApplyMapV2InternalPartLayoutPropertyEdit(item, newValue, internalPart, internalProperty);
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

        var editTargets = GetPropertyEditTargets(item.PropertyName);
        var changedTargets = editTargets
            .Where(target =>
            {
                var targetProperty = target.Config.GetType().GetProperty(
                    item.PropertyName,
                    BindingFlags.Instance | BindingFlags.Public);
                return targetProperty is not null
                       && targetProperty.CanWrite
                       && !ValuesEqual(targetProperty.GetValue(target.Config), convertedValue);
            })
            .ToList();

        if (changedTargets.Count == 0)
        {
            ClearPropertyEditError(item.PropertyName);
            item.Value = convertedValue;
            item.EditText = GetCommittedEditText(item, convertedValue);
            return true;
        }

        CaptureUndoSnapshot();
        foreach (var target in changedTargets)
        {
            var targetProperty = target.Config.GetType().GetProperty(
                item.PropertyName,
                BindingFlags.Instance | BindingFlags.Public);
            targetProperty?.SetValue(target.Config, convertedValue);
        }

        item.Value = convertedValue;
        item.EditText = GetCommittedEditText(item, convertedValue);
        CurrentDocument.IsDirty = true;

        if (IsGeometryProperty(item.PropertyName))
        {
            foreach (var target in changedTargets)
            {
                SyncLinkedOverlays(target);
            }
        }

        FinishPropertyEdit(item.PropertyName);
        if (wasClamped)
        {
            StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "InputTruncated");
        }

        return true;
    }

    private IReadOnlyList<FrontedControlDesignItem> GetPropertyEditTargets(string propertyName)
    {
        if (propertyName == nameof(FrontedControlDesignItem.Name)
            || SelectedDesignItem is null
            || SelectedDesignItems.Count <= 1)
        {
            return SelectedDesignItem is null ? [] : [SelectedDesignItem];
        }

        var selectedType = SelectedDesignItem.Config.ControlType;
        var sameTypeTargets = SelectedDesignItems
            .Where(target => target.IsEditableInEditor
                             && target.IsSelectableInEditor
                             && string.Equals(target.Config.ControlType, selectedType, StringComparison.Ordinal))
            .ToList();

        return sameTypeTargets.Count == SelectedDesignItems.Count
            ? sameTypeTargets
            : [SelectedDesignItem];
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
            item.EditText = GetCommittedEditText(item, convertedValue);
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
        item.EditText = GetCommittedEditText(item, convertedValue);
        CurrentDocument.IsDirty = true;
        FinishPropertyEdit(item.PropertyName);
        return true;
    }

    private bool ApplyMapV2InternalPartLayoutPropertyEdit(
        FrontedPropertyEditorItem item,
        object? newValue,
        MapV2InternalPartLayoutConfig part,
        PropertyInfo property)
    {
        if (CurrentDocument is null)
        {
            return false;
        }

        if (!TryConvertPropertyValue(property, newValue, out var convertedValue, out var errorMessage))
        {
            SetPropertyEditError(item, errorMessage, newValue);
            return false;
        }

        CaptureUndoSnapshot();
        property.SetValue(part, convertedValue);
        ClampSelectedMapV2InternalPart();
        item.Value = property.GetValue(part);
        item.EditText = GetCommittedEditText(item, item.Value);
        CurrentDocument.IsDirty = true;
        RefreshDirtyState();
        RequestPreviewRenderCurrentDocument();
        RefreshSelectedControlDisplay();
        return true;
    }

    private bool ApplyNameEdit(FrontedPropertyEditorItem item, object? newValue)
    {
        if (CurrentDocument is null || SelectedDesignItem is null)
        {
            return false;
        }

        if (!SelectedDesignItem.IsSelectableInEditor
            || !SelectedDesignItem.IsEditableInEditor)
        {
            SetPropertyEditError(
                item,
                I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "InvalidControlName"),
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
                I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "InvalidControlName"),
                newValue);
            return false;
        }

        var existingNames = CurrentDocument.Controls
            .Where(control => !ReferenceEquals(control, SelectedDesignItem))
            .Select(control => control.Name)
            .ToHashSet(StringComparer.Ordinal);

        if (existingNames.Contains(newName))
        {
            SetPropertyEditError(
                item,
                I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "DuplicateControlName"),
                newValue);
            return false;
        }

        _referenceScanner.SetControls(CurrentDocument.Controls);
        if (_referenceScanner.GetIncomingReferences(oldName).Count > 0)
        {
            SetPropertyEditError(
                item,
                I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "ReferencedControlRenameBlocked"),
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
            StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "InputTruncated");
        }

        return true;
    }

    private void ApplyLayoutSource(
        FrontedLayoutLoadResult loadResult,
        FrontedDesignerLayoutCatalogEntry entry)
    {
        LayoutSourceDisplay = loadResult.Source switch
        {
            FrontedLayoutSource.User => I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "LayoutSourceUser"),
            FrontedLayoutSource.BuiltIn => I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "LayoutSourceBuiltIn"),
            FrontedLayoutSource.PluginDefault => I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "LayoutSourceBuiltIn"),
            _ => I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "LayoutSourceError")
        };
        LayoutSourcePath = loadResult.Path
            ?? GetBuiltInPackageLayoutPath(entry.WindowTypeName);

        if (!string.IsNullOrWhiteSpace(loadResult.Error))
        {
            StatusMessage = loadResult.Error;
        }
    }

    private async Task<FrontedCanvasConfig?> LoadBuiltInLayoutForResetAsync(
        string windowTypeName,
        string canvasName)
    {
        var builtInPath = GetBuiltInPackageLayoutPath(windowTypeName);
        if (File.Exists(builtInPath))
        {
            var json = await File.ReadAllTextAsync(builtInPath);
            var config = JsonSerializer.Deserialize<FrontedWindowConfig>(json);
            if (config is not null)
            {
                return FrontedWindowConfigCanvasAdapter.ToCanvasConfig(config);
            }
        }

        _logger.LogWarning(
            "Built-in layout not found for reset. Window: {WindowTypeName}, Canvas: {CanvasName}, Path: {Path}",
            windowTypeName,
            canvasName,
            builtInPath);
        return null;
    }

    private string GetBuiltInPackageLayoutPath(string windowTypeName)
    {
        if (_packageManager is null)
        {
            return string.Empty;
        }

        return _packageManager.GetPackageLayoutPath(
            FrontedLayoutPackageManager.BuiltInPackageId,
            windowTypeName);
    }

    private void ClearLoadedLayout(FrontedLayoutValidationMessage message)
    {
        ControlFilterText = string.Empty;
        CurrentDocument = null;
        SelectDesignItem(null);
        ResetBehaviorDocument();
        ApplyValidationMessages([message]);
        RequestPreviewRender(null, _selectedCatalogEntry);
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
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "ValidationMessagesTruncated"),
                    messages.Count - FrontedLayoutLimits.MaxValidationMessagesShown)));
        }

        ErrorCount = messages.Count(message => message.Severity == FrontedLayoutValidationSeverity.Error);
        WarningCount = messages.Count(message => message.Severity == FrontedLayoutValidationSeverity.Warning);
        InfoCount = messages.Count(message => message.Severity == FrontedLayoutValidationSeverity.Info);
        StatusMessage =
            $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Errors")}: {ErrorCount}  "
            + $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Warnings")}: {WarningCount}  "
            + $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Infos")}: {InfoCount}";
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

            IEnumerable<FrontedPropertyEditorItem> rows = TryGetSelectedGlobalScoreCell(out _, out _, out var selectedCell)
                ? BuildGlobalScoreCellPropertyRows(selectedCell)
                : _propertyGridBuilder.Build(
                    CurrentDocument,
                    SelectedDesignItem,
                    _validator,
                    _referenceScanner);

            if (SelectedDesignItem.Config is MapV2DisplayControlConfig
                && SelectedMapV2InternalStylePart is { } selectedPart)
            {
                var styleRows = rows
                    .Where(row => IsMapV2InternalStyleProperty(selectedPart.Part, row.PropertyName));
                rows = BuildMapV2InternalPartLayoutPropertyRows(SelectedMapV2InternalPartLayout)
                    .Concat(styleRows)
                    .ToArray();
            }

            foreach (var row in rows)
            {
                ApplyMultiSelectionPropertyRowState(row);
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

    private static bool IsMapV2InternalStyleProperty(MapV2InternalStylePart part, string propertyName) =>
        part switch
        {
            MapV2InternalStylePart.TeamName => propertyName is
                nameof(MapV2DisplayControlConfig.TeamNameFontFamily)
                or nameof(MapV2DisplayControlConfig.TeamNameFontWeight)
                or nameof(MapV2DisplayControlConfig.TeamNameColor)
                or nameof(MapV2DisplayControlConfig.TeamNameFontSize),
            MapV2InternalStylePart.MapCard => propertyName is
                nameof(MapV2DisplayControlConfig.MapBorderNormalColor)
                or nameof(MapV2DisplayControlConfig.MapBorderBannedColor),
            MapV2InternalStylePart.MapName => propertyName is
                nameof(MapV2DisplayControlConfig.MapNameFontFamily)
                or nameof(MapV2DisplayControlConfig.MapNameFontWeight)
                or nameof(MapV2DisplayControlConfig.MapNameColor)
                or nameof(MapV2DisplayControlConfig.MapNameFontSize),
            MapV2InternalStylePart.CampName => propertyName is
                nameof(MapV2DisplayControlConfig.CampNameFontFamily)
                or nameof(MapV2DisplayControlConfig.CampNameFontWeight)
                or nameof(MapV2DisplayControlConfig.CampNameColor)
                or nameof(MapV2DisplayControlConfig.CampNameFontSize),
            MapV2InternalStylePart.PickingBorder => propertyName is
                nameof(MapV2DisplayControlConfig.PickingBorderImagePath)
                or nameof(MapV2DisplayControlConfig.PickingBorderFillColor),
            _ => false
        };

    private ObservableCollection<FrontedPropertyEditorItem> BuildMapV2InternalPartLayoutPropertyRows(
        MapV2InternalPartLayoutConfig? part)
    {
        if (part is null)
        {
            return [];
        }

        return
        [
            CreateCellPropertyRow(nameof(MapV2InternalPartLayoutConfig.X), typeof(double), FrontedPropertyEditorKind.Number, part.X, "Layout"),
            CreateCellPropertyRow(nameof(MapV2InternalPartLayoutConfig.Y), typeof(double), FrontedPropertyEditorKind.Number, part.Y, "Layout"),
            CreateCellPropertyRow(nameof(MapV2InternalPartLayoutConfig.Width), typeof(double), FrontedPropertyEditorKind.Number, part.Width, "Layout"),
            CreateCellPropertyRow(nameof(MapV2InternalPartLayoutConfig.Height), typeof(double), FrontedPropertyEditorKind.Number, part.Height, "Layout")
        ];
    }

    private void ApplyMultiSelectionPropertyRowState(FrontedPropertyEditorItem row)
    {
        if (CurrentDocument is null
            || SelectedDesignItem is null
            || SelectedDesignItems.Count <= 1
            || HasSelectedGlobalScoreCell)
        {
            return;
        }

        if (!CanBatchEditSelectedProperty(row))
        {
            row.IsMultiSelectionBatchEditable = false;
            ClearMultiSelectionPropertyRow(row, makeReadOnly: true);
            return;
        }

        row.IsMultiSelectionBatchEditable = true;
        if (!TryGetCommonSelectedPropertyValue(row.PropertyName, out var commonValue))
        {
            row.IsMultiSelectionMixedValue = true;
            ClearMultiSelectionPropertyRow(row, makeReadOnly: false);
            return;
        }

        row.IsMultiSelectionMixedValue = false;
        row.Value = commonValue;
        row.DisplayValue = GetPropertyEditorDisplayValue(commonValue);
        row.EditText = GetCommittedEditText(row, commonValue);
    }

    private bool CanBatchEditSelectedProperty(FrontedPropertyEditorItem row)
    {
        if (SelectedDesignItem is null || SelectedDesignItems.Count <= 1)
        {
            return true;
        }

        if (row.IsReadOnly
            || row.EditorKind == FrontedPropertyEditorKind.ReadOnly
            || row.PropertyName == nameof(FrontedControlDesignItem.Name)
            || row.CanBrowseBinding
            || row.CanBrowseResource
            || row.EditorKind == FrontedPropertyEditorKind.TextBinding
            || IsMultiSelectionIsolatedProperty(row.PropertyName))
        {
            return false;
        }

        var controlType = SelectedDesignItem.Config.ControlType;
        return SelectedDesignItems.All(item =>
        {
            if (!item.IsEditableInEditor
                || !item.IsSelectableInEditor
                || !string.Equals(item.Config.ControlType, controlType, StringComparison.Ordinal))
            {
                return false;
            }

            var property = item.Config.GetType().GetProperty(
                row.PropertyName,
                BindingFlags.Instance | BindingFlags.Public);
            return property is not null
                   && property.CanRead
                   && property.CanWrite
                   && IsBatchEditablePropertyType(property.PropertyType);
        });
    }

    private bool TryGetCommonSelectedPropertyValue(string propertyName, out object? commonValue)
    {
        commonValue = null;
        var hasValue = false;
        foreach (var item in SelectedDesignItems)
        {
            var property = item.Config.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            if (property is null || !property.CanRead)
            {
                return false;
            }

            var value = property.GetValue(item.Config);
            if (!hasValue)
            {
                commonValue = value;
                hasValue = true;
                continue;
            }

            if (!ValuesEqual(commonValue, value))
            {
                commonValue = null;
                return false;
            }
        }

        return hasValue;
    }

    private static void ClearMultiSelectionPropertyRow(FrontedPropertyEditorItem row, bool makeReadOnly)
    {
        row.Value = null;
        row.DisplayValue = string.Empty;
        row.EditText = string.Empty;
        if (makeReadOnly)
        {
            row.EditorKind = FrontedPropertyEditorKind.ReadOnly;
            row.IsReadOnly = true;
            row.CanBrowseBinding = false;
            row.CanBrowseResource = false;
        }
    }

    private static bool IsMultiSelectionIsolatedProperty(string propertyName)
    {
        return propertyName.Contains("Binding", StringComparison.OrdinalIgnoreCase)
               || propertyName.Contains("Behavior", StringComparison.OrdinalIgnoreCase)
               || propertyName.Contains("Trigger", StringComparison.OrdinalIgnoreCase)
               || propertyName.Contains("Filter", StringComparison.OrdinalIgnoreCase)
               || propertyName.Contains("Guid", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBatchEditablePropertyType(Type propertyType)
    {
        var type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        return type.IsPrimitive
               || type.IsEnum
               || type == typeof(string)
               || type == typeof(decimal)
               || type == typeof(DateTime)
               || type == typeof(TimeSpan);
    }

    private static bool IsEmptyMultiSelectionPlaceholderValue(object? value)
    {
        if (value is null)
        {
            return true;
        }

        return value is string text && string.IsNullOrEmpty(text);
    }

    private static string GetPropertyEditorDisplayValue(object? value) =>
        Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

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

    private FrontedWindowConfig CreateConfigFromCurrentDocument()
    {
        if (CurrentDocument is null)
        {
            return new FrontedWindowConfig
            {
                WindowSettings = CloneWindowSettings(_currentWindowSettings)
            };
        }

        var canvasConfig = _designConverter.ToConfig(CurrentDocument);
        canvasConfig.Version = 3;
        var windowConfig = FrontedWindowConfigCanvasAdapter.FromCanvasConfig(canvasConfig);
        windowConfig.WindowSettings = CloneWindowSettings(_currentWindowSettings);
        return windowConfig;
    }

    private static FrontedWindowSettings CloneWindowSettings(FrontedWindowSettings settings)
    {
        return new FrontedWindowSettings
        {
            WindowWidth = settings.WindowWidth,
            WindowHeight = settings.WindowHeight,
            WindowLeft = settings.WindowLeft,
            WindowTop = settings.WindowTop,
            AllowsTransparency = settings.AllowsTransparency,
            BackgroundColor = settings.BackgroundColor,
            Topmost = settings.Topmost,
            ViewboxStretch = settings.ViewboxStretch
        };
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
        var root = _packageManager?.GetPackageRootFolder() ?? AppConstants.FrontedLayoutPackagesPath;
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
        WindowOptionsWindowTypeName = $"{ResolveWindowOptionDisplayName(windowTypeName)} ({windowTypeName})";
        _isLoadingWindowOptions = true;
        try
        {
            var settings = _currentWindowSettings;
            WindowAllowTransparency = settings.AllowsTransparency;
            WindowWidthEditText = settings.WindowWidth.ToString("0.##", CultureInfo.InvariantCulture);
            WindowHeightEditText = settings.WindowHeight.ToString("0.##", CultureInfo.InvariantCulture);

            var configuredBackgroundColor = settings.BackgroundColor;
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

    public async Task<bool> ApplyWindowBackgroundColorEditAsync()
    {
        if (!FrontedPropertyColorHelper.TryParseArgbColor(WindowBackgroundColorEditText, out var color))
        {
            WindowOptionsStatus = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.Validation.InvalidArgbColor");
            return false;
        }

        WindowBackgroundColorEditText = FrontedPropertyColorHelper.ToArgbString(color);
        WindowBackgroundColorValue = color;
        _windowBackgroundColorConfigured = true;
        await SaveWindowOptionsAsync(
            restartWindowForTransparencyChange: false,
            applyBackgroundImmediately: true);
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

        if (!item.IsMultiSelectionBatchEditable || item.IsMultiSelectionMixedValue)
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
            ? I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.TextBinding.None")
            : string.Format(
                CultureInfo.CurrentCulture,
                I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.TextBinding.SourceSummary"),
                expression.GetActiveSources().Count,
                string.Join(", ", expression.GetActiveSources().Select(source => source.Path)));
        CurrentDocument.IsDirty = true;
        FinishPropertyEdit(item.PropertyName);
        return true;
    }

    private async Task SaveWindowOptionsAsync(
        bool restartWindowForTransparencyChange,
        bool applyBackgroundImmediately,
        bool applyWindowSizeImmediately = false)
    {
        if (SelectedWindow is null)
        {
            return;
        }

        try
        {
            var windowWidth = TryParseOptionalPositiveDouble(WindowWidthEditText);
            var windowHeight = TryParseOptionalPositiveDouble(WindowHeightEditText);
            var settings = CloneWindowSettings(_currentWindowSettings);
            settings.WindowWidth = windowWidth ?? settings.WindowWidth;
            settings.WindowHeight = windowHeight ?? settings.WindowHeight;
            settings.AllowsTransparency = WindowAllowTransparency;
            settings.BackgroundColor = _windowBackgroundColorConfigured
                ? WindowBackgroundColorEditText
                : null;

            var config = await _layoutService.LoadWindowConfigAsync(SelectedWindow.WindowTypeName)
                         ?? CreateConfigFromCurrentDocument();
            config.WindowSettings = CloneWindowSettings(settings);

            await _layoutService.SaveWindowConfigAsync(
                SelectedWindow.WindowTypeName,
                config);
            _currentWindowSettings = CloneWindowSettings(settings);

            if (applyBackgroundImmediately)
            {
                await (_frontedWindowService?.ApplyWindowBackgroundColorAsync(SelectedWindow.WindowTypeName) ?? Task.FromResult(false));
            }

            if (applyWindowSizeImmediately)
            {
                await (_frontedWindowService?.ApplyWindowSizeAsync(SelectedWindow.WindowTypeName) ?? Task.FromResult(false));
            }

            if (applyWindowSizeImmediately)
            {
                await (_frontedWindowService?.ReloadFrontedLayoutsAsync() ?? Task.CompletedTask);
            }

            if (restartWindowForTransparencyChange)
            {
                await (_frontedWindowService?.RestartWindowForTransparencyChangeAsync(SelectedWindow.WindowTypeName)
                       ?? Task.FromResult(false));
            }

            WindowOptionsStatus = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "WindowOptionsApplied");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save fronted window layout options.");
            WindowOptionsStatus = ex.Message;
        }
    }

    private async Task ResetWindowOptionsAsync()
    {
        if (SelectedWindow is null)
        {
            return;
        }

        try
        {
            FrontedWindowConfig? builtInConfig = null;
            var builtInPath = GetBuiltInPackageLayoutPath(SelectedWindow.WindowTypeName);
            if (File.Exists(builtInPath))
            {
                builtInConfig = JsonSerializer.Deserialize<FrontedWindowConfig>(
                    await File.ReadAllTextAsync(builtInPath));
            }
            _currentWindowSettings = CloneWindowSettings(builtInConfig?.WindowSettings ?? new FrontedWindowSettings());
            var config = await _layoutService.LoadWindowConfigAsync(SelectedWindow.WindowTypeName)
                         ?? CreateConfigFromCurrentDocument();
            config.WindowSettings = CloneWindowSettings(_currentWindowSettings);
            await _layoutService.SaveWindowConfigAsync(SelectedWindow.WindowTypeName, config);
            LoadWindowOptions(SelectedWindow.WindowTypeName);
            await (_frontedWindowService?.RestartWindowForTransparencyChangeAsync(SelectedWindow.WindowTypeName)
                   ?? Task.FromResult(false));
            WindowOptionsStatus = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "WindowOptionsApplied");
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
            ? $"* {(CurrentDocument?.IsDirty == true ? I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Unsaved") : I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.Behaviors.UnsavedBehaviorChanges"))}"
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
            saveBehaviorAsync: SaveBehaviorDocumentAsync,
            behaviorClipboard: _behaviorClipboard,
            copyPasteService: _behaviorCopyPasteService,
            captureUndoSnapshot: CaptureUndoSnapshot);
    }

    /// <summary>
    /// 持久化当前行为文档，并清除外层已修改标记。
    /// 由动画编辑器保存流程调用。
    /// </summary>
    /// <returns><c>true</c> if the save succeeded; otherwise <c>false</c>.</returns>
    private async Task<bool> SaveBehaviorDocumentAsync()
    {
        try
        {
            BehaviorPanel.CurrentDocument.WindowType = CurrentDocument?.WindowTypeName;
            BehaviorPanel.CurrentDocument.CanvasName = CurrentDocument?.CanvasName;
            await _behaviorService.SaveDocumentAsync(BehaviorPanel.CurrentDocument).ConfigureAwait(false);
            AreBehaviorsDirty = false;
            RefreshDirtyState();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save behavior document from animation editor.");
            return false;
        }
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
        BehaviorPanel.SetCopyContext(CurrentDocument?.WindowTypeName, CurrentDocument?.Controls);
        BehaviorPanel.SetSelectedControl(SelectedDesignItem);
    }

    public void UpdateBehaviorPreviewAnimationScope(FrameworkElement previewRoot)
    {
        _previewAnimationScope?.Update(
            previewRoot,
            SelectedDesignItem,
            _selectedCatalogEntry?.WindowId,
            FrontedLayoutConstants.BaseCanvasName,
            CurrentDocument?.Controls ?? [],
            BehaviorPanel.CurrentDocument);
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
            RequestPreviewRender(null, _selectedCatalogEntry);
            return;
        }

        RequestPreviewRender(_designConverter.ToConfig(CurrentDocument), _selectedCatalogEntry);
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
                DisplayName = $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.LayerPanel.Layer")} {group.Key}"
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
            DesignerLayerNodeKind.Control => node.ControlItem is not null && SelectedDesignItems.Contains(node.ControlItem),
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
            SelectedDesignItems.Clear();
            SelectedDesignItem = null;
            return;
        }

        if (SelectedDesignItem is not null && !CurrentDocument.Controls.Contains(SelectedDesignItem))
        {
            SelectedDesignItem = null;
        }

        var retainedSelection = SelectedDesignItems
            .Where(item => CurrentDocument.Controls.Contains(item))
            .Distinct()
            .ToList();
        if (SelectedDesignItem is not null && !retainedSelection.Contains(SelectedDesignItem))
        {
            retainedSelection.Add(SelectedDesignItem);
        }

        SelectedDesignItems.Clear();
        foreach (var item in retainedSelection)
        {
            SelectedDesignItems.Add(item);
        }

        foreach (var control in CurrentDocument.Controls)
        {
            control.IsSelected = SelectedDesignItems.Contains(control);
            if (control.IsSelected)
            {
                _lastSelectedDesignItem = control;
            }
        }

        RebuildGlobalScoreCellEditorItems();
        RefreshGlobalScoreCellSelection();
    }

    private void ApplyDesignSelectionFlags()
    {
        var selected = SelectedDesignItems.ToHashSet();
        if (CurrentDocument is not null)
        {
            foreach (var control in CurrentDocument.Controls)
            {
                control.IsSelected = selected.Contains(control);
            }
        }

        _lastSelectedDesignItem = SelectedDesignItem;
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
            CreateCellPropertyRow(nameof(GlobalScoreCellConfig.ShowCampIcon), typeof(bool?), FrontedPropertyEditorKind.Boolean, cell.ShowCampIcon, "Appearance"),
            CreateCellPropertyRow(nameof(GlobalScoreCellConfig.CampIconColor), typeof(GlobalScoreCampIconColor?), FrontedPropertyEditorKind.Enum, cell.CampIconColor, "Appearance")
        };

        string? currentGroup = null;
        foreach (var row in rows)
        {
            row.Description = row.PropertyName is nameof(GlobalScoreCellConfig.FontFamily)
                or nameof(GlobalScoreCellConfig.FontWeight)
                or nameof(GlobalScoreCellConfig.Color)
                or nameof(GlobalScoreCellConfig.FontSize)
                or nameof(GlobalScoreCellConfig.ShowCampIcon)
                or nameof(GlobalScoreCellConfig.CampIconColor)
                ? I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.GlobalScoreRow.CellInheritsFromParent")
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
            EditText = GetCommittedEditText(editorKind, value, null),
            GroupName = groupName,
            IsRequired = propertyName is nameof(GlobalScoreCellConfig.Id)
                or nameof(GlobalScoreCellConfig.GameNumber)
                or nameof(GlobalScoreCellConfig.X)
                or nameof(GlobalScoreCellConfig.Y)
                or nameof(GlobalScoreCellConfig.Width)
                or nameof(GlobalScoreCellConfig.Height),
            RequiresExplicitCommit = propertyName == nameof(GlobalScoreCellConfig.Id)
                                     || editorKind is FrontedPropertyEditorKind.FontFamily
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
        else if (editorKind == FrontedPropertyEditorKind.FontFamily)
        {
            row.Options = _propertyGridBuilder.GetFontFamilyOptions();
            row.EditText = GetCommittedEditText(row, value);
        }

        return row;
    }

    private static string GetCommittedEditText(FrontedPropertyEditorItem item, object? value) =>
        GetCommittedEditText(item.EditorKind, value, item.Options);

    private static string GetCommittedEditText(
        FrontedPropertyEditorKind editorKind,
        object? value,
        IReadOnlyList<object>? options)
    {
        if (editorKind == FrontedPropertyEditorKind.FontFamily)
        {
            var storedValue = Convert.ToString(value, CultureInfo.InvariantCulture);
            return options?.OfType<FrontedFontFamilyOption>()
                       .FirstOrDefault(option => string.Equals(option.Value, storedValue, StringComparison.Ordinal))?.DisplayName
                   ?? FrontedFontResourceHelper.ExtractFontName(storedValue);
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static string GetGlobalScoreCellPropertyDisplayKey(string propertyName) =>
        propertyName switch
        {
            nameof(GlobalScoreCellConfig.Id) => "CellId",
            nameof(GlobalScoreCellConfig.X) => "CellX",
            nameof(GlobalScoreCellConfig.Y) => "CellY",
            nameof(GlobalScoreCellConfig.ShowCampIcon) => "ShowCampIconOverride",
            nameof(GlobalScoreCellConfig.CampIconColor) => "CampIconColorOverride",
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
            SelectedControlDisplay = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "NoControlSelected");
            SelectedControlTypeDisplay = string.Empty;
            SelectedControlGeometryDisplay = string.Empty;
            SelectedControlValidationMessageCount = 0;
            return;
        }

        var config = SelectedDesignItem.Config;
        if (TryGetSelectedGlobalScoreCell(out _, out _, out var cell))
        {
            SelectedControlDisplay = $"{SelectedDesignItem.Name} / {cell.Id}";
            SelectedControlTypeDisplay = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.GlobalScoreRow.SelectedCell");
            SelectedControlGeometryDisplay =
                $"X {cell.X:0.##}  Y {cell.Y:0.##}  "
                + $"W {cell.Width:0.##}  H {cell.Height:0.##}";
            SelectedControlValidationMessageCount = SelectedDesignItem.ValidationMessages.Count;
            return;
        }

        if (SelectedMapV2InternalStylePart is { } selectedPart)
        {
            SelectedControlDisplay = $"{SelectedDesignItem.Name} / {selectedPart.DisplayName}";
            SelectedControlTypeDisplay = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.MapV2Display.SelectedInternalPart");
            var part = SelectedMapV2InternalPartLayout;
            SelectedControlGeometryDisplay = part is null
                ? string.Empty
                : $"X {part.X:0.##}  Y {part.Y:0.##}  W {part.Width:0.##}  H {part.Height:0.##}";
            SelectedControlValidationMessageCount = SelectedDesignItem.ValidationMessages.Count;
            return;
        }

        SelectedControlDisplay = SelectedDesignItem.Name;
        SelectedControlTypeDisplay = _localizationService.GetControlTypeDisplayName(config.ControlType);
        SelectedControlGeometryDisplay =
            $"L {config.Left:0.##}  T {config.Top:0.##}  "
            + $"W {(config.Width?.ToString("0.##") ?? "-")}  "
            + $"H {(config.Height?.ToString("0.##") ?? "-")}";
        SelectedControlValidationMessageCount = SelectedDesignItem.ValidationMessages.Count;
    }

    private void RequestPreviewRender(FrontedCanvasConfig? config, FrontedDesignerLayoutCatalogEntry? entry)
    {
        PreviewRenderRequested?.Invoke(
            this,
            new FrontedDesignerPreviewRenderRequestedEventArgs(
                config,
                BehaviorPanel.CurrentDocument,
                entry is null
                    ? null
                    : new FrontedRenderContext
                    {
                        WindowId = entry.WindowId,
                        WindowTypeName = entry.WindowTypeName,
                        CanvasName = FrontedLayoutConstants.BaseCanvasName,
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

        return JsonSerializer.Serialize(new FrontedDesignerUndoSnapshot
        {
            CanvasConfig = _designConverter.ToConfig(CurrentDocument),
            BehaviorDocument = BehaviorPanel.CurrentDocument
        });
    }

    private static bool CanCopyControl(FrontedControlDesignItem? item)
    {
        return item is
        {
            IsSelectableInEditor: true,
            IsEditableInEditor: true,
        };
    }

    private static string GeneratePasteName(string sourceName, string controlType, FrontedCanvasDesignDocument document)
    {
        var existingNames = document.Controls.Select(control => control.Name).ToHashSet(StringComparer.Ordinal);
        if (!existingNames.Contains(sourceName) && ValidControlNameRegex.IsMatch(sourceName))
        {
            return sourceName;
        }

        var match = Regex.Match(sourceName, "^(.*?)(\\d+)$", RegexOptions.CultureInvariant);
        var baseName = match.Success ? match.Groups[1].Value : sourceName;
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = GetNameSeed(controlType);
        }

        var index = match.Success && int.TryParse(match.Groups[2].Value, out var parsed) ? parsed + 1 : 1;
        var separator = match.Success ? string.Empty : "_";

        while (true)
        {
            var suffix = $"{separator}{index}";
            var truncatedBaseName = FrontedTextLimitHelper.Clamp(
                baseName,
                Math.Max(1, FrontedLayoutLimits.MaxControlNameLength - suffix.Length));
            var candidate = $"{truncatedBaseName}{suffix}";
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
        var restoreSnapshot = DeserializeUndoSnapshot(snapshot);
        var config = restoreSnapshot?.CanvasConfig;
        var behaviorDocument = restoreSnapshot?.BehaviorDocument;
        LogDesignerPerf(traceOperation, "restore snapshot deserialize", Elapsed(total));
        if (config is null)
        {
            return;
        }

        var behaviorDocumentChanged = behaviorDocument is not null
            && !BehaviorDocumentsEqual(BehaviorPanel.CurrentDocument, behaviorDocument);
        if (mode == FrontedDesignerSnapshotRestoreMode.PreferGeometryFastPathThenScheduledAtomicPreview
            && !behaviorDocumentChanged
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
                config);
            LogDesignerPerf(traceOperation, "design document rebuild", Elapsed(total));
            document.IsDirty = true;
            CurrentDocument = document;
            SelectDesignItem(document.Controls.FirstOrDefault(control =>
                string.Equals(control.Name, selectedName, StringComparison.Ordinal)));
            RestoreGlobalScoreCellSelectionKeys(selectedCellParentName, selectedCellId);
            NormalizeSelectionState();
            RestoreBehaviorDocumentSnapshot(behaviorDocument, windowTypeName, canvasName);

            switch (mode)
            {
                case FrontedDesignerSnapshotRestoreMode.PreferGeometryFastPathThenScheduledAtomicPreview:
                    _clearRestoreVisualsAfterScheduledPreview = true;
                    shouldNotifyUndoRedoInFinally = false;
                    ScheduleValidationAndPreviewRender(traceOperation);
                    LogDesignerPerf(traceOperation, "scheduled full restore", Elapsed(total));
                    break;

                case FrontedDesignerSnapshotRestoreMode.ImmediatePreviewThenScheduledValidation:
                    RequestPreviewRender(config, _selectedCatalogEntry);
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
                    RequestPreviewRender(config, _selectedCatalogEntry);
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

    private static FrontedDesignerUndoSnapshot? DeserializeUndoSnapshot(string snapshot)
    {
        try
        {
            var undoSnapshot = JsonSerializer.Deserialize<FrontedDesignerUndoSnapshot>(snapshot);
            if (undoSnapshot?.CanvasConfig is not null)
            {
                return undoSnapshot;
            }
        }
        catch (JsonException)
        {
            // 较旧的内存快照只存储了画布配置 JSON。
        }

        var config = JsonSerializer.Deserialize<FrontedCanvasConfig>(snapshot);
        return config is null
            ? null
            : new FrontedDesignerUndoSnapshot { CanvasConfig = config };
    }

    private static bool BehaviorDocumentsEqual(
        FrontedBehaviorDocument current,
        FrontedBehaviorDocument snapshot) =>
        string.Equals(
            JsonSerializer.Serialize(current),
            JsonSerializer.Serialize(snapshot),
            StringComparison.Ordinal);

    private void RestoreBehaviorDocumentSnapshot(
        FrontedBehaviorDocument? behaviorDocument,
        string windowTypeName,
        string canvasName)
    {
        if (behaviorDocument is null)
        {
            return;
        }

        behaviorDocument.WindowType = windowTypeName;
        behaviorDocument.CanvasName = canvasName;
        BehaviorPanel.SetDocument(behaviorDocument);
        BehaviorPanel.SetCopyContext(windowTypeName, CurrentDocument?.Controls);
        BehaviorPanel.SetSelectedControl(SelectedDesignItem);
        RebuildAnimationPartEditorItems();
        AreBehaviorsDirty = true;
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
            ZoomDisplay = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Fit");
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
        ZoomDisplay = $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Fit")} ({ZoomScale:P0})";
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

        var fitKey = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Fit");
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
                StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.ZoomInvalid");
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
                StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.ZoomInvalid");
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
            StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.ZoomInvalid");
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
            ZoomEditText = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Fit");
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
                        errorMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "PropertyValidationErrors");
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
                    errorMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "PropertyValidationErrors");
                    return false;
                }

                convertedValue = NormalizeDoubleProperty(property.Name, doubleValue);
            }
            else
            {
                errorMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "PropertyValidationErrors");
                return false;
            }

            return true;
        }
        catch (Exception)
        {
            errorMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "PropertyValidationErrors");
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

    private void RebuildWindowOptions(string? preserveSelectedWindowTypeName)
    {
        _isRefreshingWindowOptions = !string.IsNullOrWhiteSpace(preserveSelectedWindowTypeName);
        try
        {
            WindowOptions.Clear();
            foreach (var entry in _layoutCatalog.GetEntries()
                         .Where(entry => entry.IsMigrated && entry.IsEditable))
            {
                WindowOptions.Add(new FrontedDesignerWindowOption(
                    entry.WindowTypeName,
                    ResolveEntryDisplayName(entry)));
            }

            var nextSelection = string.IsNullOrWhiteSpace(preserveSelectedWindowTypeName)
                ? WindowOptions.FirstOrDefault()
                : WindowOptions.FirstOrDefault(option => string.Equals(
                      option.WindowTypeName,
                      preserveSelectedWindowTypeName,
                      StringComparison.Ordinal))
                  ?? WindowOptions.FirstOrDefault();

            SelectedWindow = nextSelection;
        }
        finally
        {
            _isRefreshingWindowOptions = false;
        }
    }

    private string ResolveWindowOptionDisplayName(string windowTypeName)
    {
        var entry = _layoutCatalog.GetEntries()
            .FirstOrDefault(item => string.Equals(item.WindowTypeName, windowTypeName, StringComparison.Ordinal));
        return entry is null
            ? _localizationService.GetWindowDisplayName(windowTypeName)
            : ResolveEntryDisplayName(entry);
    }

    private string ResolveEntryDisplayName(FrontedDesignerLayoutCatalogEntry entry)
    {
        var settings = _settingsHostService?.Settings;
        return FrontedWindowDisplayNameResolver.ResolveDisplayName(
            new FrontedDesignerLayoutCatalogEntryWindowDescriptor(entry),
            settings?.Language ?? LanguageKey.System,
            settings?.CultureInfo);
    }

    private sealed class FrontedDesignerLayoutCatalogEntryWindowDescriptor(
        FrontedDesignerLayoutCatalogEntry entry) : IFrontedWindowDescriptor
    {
        /// <inheritdoc />
        public string WindowId => entry.WindowId;

        /// <inheritdoc />
        public string WindowTypeName => entry.WindowTypeName;

        /// <inheritdoc />
        public string FullWindowType => entry.WindowTypeName;

        /// <inheritdoc />
        public string DisplayName => entry.DisplayName;

        /// <inheritdoc />
        public IReadOnlyDictionary<LanguageKey, string>? I18nDisplayNames => entry.I18nDisplayNames;

        /// <inheritdoc />
        public string? DisplayNameKey => null;

        /// <inheritdoc />
        public string? Description => null;

        /// <inheritdoc />
        public string? DescriptionKey => null;

        /// <inheritdoc />
        public string? GroupKey => null;

        /// <inheritdoc />
        public int? DisplayOrder => null;

        /// <inheritdoc />
        public bool IsVisibleInFrontManage => true;

        /// <inheritdoc />
        public bool IsV3LayoutWindow => true;

        /// <inheritdoc />
        public bool Customizable => true;

        /// <inheritdoc />
        public FrontedWindowKind Kind => FrontedWindowKind.BuiltIn;

        /// <inheritdoc />
        public bool IsPlugin => false;

        /// <inheritdoc />
        public string? PackageId => null;
    }
}

public sealed class FrontedDesignerWindowOption(
    string windowTypeName,
    string displayName)
{
    public string WindowTypeName { get; } = windowTypeName;

    public string DisplayName { get; } = displayName;
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
    FrontedBehaviorDocument? behaviorDocument,
    FrontedRenderContext? context) : EventArgs
{
    public FrontedCanvasConfig? Config { get; } = config;

    public FrontedBehaviorDocument? BehaviorDocument { get; } = behaviorDocument;

    public FrontedRenderContext? Context { get; } = context;
}

internal sealed class FrontedDesignerUndoSnapshot
{
    /// <summary>
    /// 获取或设置为撤销/重做捕获的画布布局配置。
    /// </summary>
    public FrontedCanvasConfig? CanvasConfig { get; set; }

    /// <summary>
    /// 获取或设置随布局配置一起捕获的行为文档。
    /// </summary>
    public FrontedBehaviorDocument? BehaviorDocument { get; set; }
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
