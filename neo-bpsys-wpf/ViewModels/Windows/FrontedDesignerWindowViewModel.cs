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
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer.V3;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Parts;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.StyleTransfer;
using neo_bpsys_wpf.Core.Models.ScoreSystem;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Geometry;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Parts;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3.StyleTransfer;
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
    private readonly IFrontedImageSafetyService? _imageSafetyService;
    private readonly IFrontedWindowLayoutOptionsService? _windowLayoutOptionsService;
    private readonly IFrontedLayoutPackageManager? _packageManager;
    private readonly IFrontedWindowService? _frontedWindowService;
    private readonly IFrontedBehaviorService _behaviorService;
    private readonly FrontedBehaviorEventCatalog _behaviorEventCatalog;
    private readonly FrontedDesignerLayoutCatalog _layoutCatalog;
    private readonly IFrontedAnimationRuntime? _animationRuntime;
    private readonly FrontedDesignerPreviewAnimationScope? _previewAnimationScope;
    private readonly IFrontedBehaviorClipboard _behaviorClipboard;
    private readonly FrontedBehaviorCopyPasteService _behaviorCopyPasteService;
    private readonly ILogger<FrontedDesignerWindowViewModel> _logger;
    private readonly ISettingsHostService? _settingsHostService;
    private readonly FrontedV3DesignSelectionBuilder _selectionBuilder;
    private readonly FrontedV3StyleTransferService? _styleTransferService;
    private FrontedV3StyleTransferService StyleTransferService
        => _styleTransferService ?? new FrontedV3StyleTransferService();

    private static ILogger<FrontedDesignerWindowViewModel>? StaticLogger =>
        IAppHost.TryGetService<ILogger<FrontedDesignerWindowViewModel>>();

    private readonly Dictionary<string, string> _propertyEditErrors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _propertyEditBuffers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _displayNamesByWindowId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FrontedV3PropertyDefinition> _schemaPropertiesByPath = new(StringComparer.Ordinal);
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
    private bool _isApplyingSelectedTarget;
    private FrontedControlDesignItem? _lastSelectedDesignItem;
    private DesignerLayerNode? _selectedLayerNode;
    private FrontedV3DesignSelection? _selectedTarget;
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
        _behaviorEventCatalog = new FrontedBehaviorEventCatalog();
        _animationRuntime = null;
        _previewAnimationScope = null;
        _behaviorClipboard = new FrontedBehaviorClipboard();
        _behaviorCopyPasteService = new FrontedBehaviorCopyPasteService(
            new FrontedBehaviorControlSemanticResolver(),
            _localizationService);
        _logger = NullLogger<FrontedDesignerWindowViewModel>.Instance;
        _settingsHostService = null;
        // 设计时/测试构造：使用空注册表，避免与生产 DI 容器耦合。
        // 测试需要真实注册表时使用接收 IFrontedV3ControlRegistry 的构造重载。
        _selectionBuilder = new FrontedV3DesignSelectionBuilder(
            new FrontedV3ControlRegistry(Array.Empty<FrontedV3ControlRegistration>()));
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
    /// 使用指定的 v3 控件注册表初始化面向测试的实例。
    /// </summary>
    /// <param name="v3ControlRegistry">测试使用的 v3 控件注册表；用于在缺少生产 DI 容器时为 SelectionBuilder 提供真实注册信息。</param>
    public FrontedDesignerWindowViewModel(IFrontedV3ControlRegistry v3ControlRegistry)
        : this()
    {
        _selectionBuilder = new FrontedV3DesignSelectionBuilder(v3ControlRegistry);
    }

    /// <summary>
    /// 使用指定的默认配置工厂初始化面向测试的实例。
    /// </summary>
    /// <param name="defaultConfigFactory">测试使用的默认配置工厂。</param>
    public FrontedDesignerWindowViewModel(FrontedControlDefaultConfigFactory defaultConfigFactory)
        : this()
    {
        _defaultConfigFactory = defaultConfigFactory;
    }

    /// <summary>
    /// 使用 3.0/3.1 版本的生产构造函数签名初始化 Designer v3 编辑器视图模型。
    /// </summary>
    /// <param name="layoutCatalog">可编辑的 v3 布局窗口目录。</param>
    /// <param name="layoutService">布局加载/保存服务。</param>
    /// <param name="designConverter">运行时布局配置与设计文档之间的转换器。</param>
    /// <param name="validator">布局校验器。</param>
    /// <param name="referenceScanner">用于重命名/删除安全检查的引用扫描器。</param>
    /// <param name="propertyGridBuilder">属性网格行构建器。</param>
    /// <param name="defaultConfigFactory">“添加控件”使用的默认控件配置工厂。</param>
    /// <param name="controlNameGenerator">唯一控件名称生成器。</param>
    /// <param name="localizationService">设计器本地化服务。</param>
    /// <param name="designerPreviewSharedDataService">仅用于预览的共享数据服务。</param>
    /// <param name="localResourceStore">本地包资源存储。</param>
    /// <param name="windowLayoutOptionsService">窗口选项持久化服务。</param>
    /// <param name="packageManager">布局包管理器。</param>
    /// <param name="frontedWindowService">前台窗口服务，用于保存后刷新实时输出。</param>
    /// <param name="behaviorService">行为文档服务。</param>
    /// <param name="behaviorClipboard">共享行为剪贴板。</param>
    /// <param name="behaviorCopyPasteService">行为复制/粘贴服务。</param>
    /// <param name="animationRuntime">预览使用的动画运行时。</param>
    /// <param name="previewAnimationScope">预览动画目标作用域。</param>
    /// <param name="logger">日志记录器。</param>
    /// <param name="settingsHostService">可选的设置宿主服务。</param>
    /// <param name="v3ControlRegistry">可选的 V3 控件注册表。</param>
    /// <param name="styleTransferService">可选的 v3 控件 StyleTransfer 服务。</param>
    /// <param name="imageSafetyService">可选的图片安全服务。</param>
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
        ISettingsHostService? settingsHostService = null,
        IFrontedV3ControlRegistry? v3ControlRegistry = null,
        FrontedV3StyleTransferService? styleTransferService = null,
        IFrontedImageSafetyService? imageSafetyService = null)
        : this(
            layoutCatalog,
            layoutService,
            designConverter,
            validator,
            referenceScanner,
            propertyGridBuilder,
            defaultConfigFactory,
            controlNameGenerator,
            localizationService,
            designerPreviewSharedDataService,
            localResourceStore,
            windowLayoutOptionsService,
            packageManager,
            frontedWindowService,
            behaviorService,
            new FrontedBehaviorEventCatalog(),
            behaviorClipboard,
            behaviorCopyPasteService,
            animationRuntime,
            previewAnimationScope,
            logger,
            settingsHostService,
            v3ControlRegistry,
            styleTransferService,
            imageSafetyService)
    {
    }

    /// <summary>
    /// 初始化生产环境 Designer v3 编辑器视图模型。
    /// </summary>
    /// <param name="layoutCatalog">可编辑的 v3 布局窗口目录。</param>
    /// <param name="layoutService">布局加载/保存服务。</param>
    /// <param name="designConverter">运行时布局配置与设计文档之间的转换器。</param>
    /// <param name="validator">布局校验器。</param>
    /// <param name="referenceScanner">用于重命名/删除安全检查的引用扫描器。</param>
    /// <param name="propertyGridBuilder">属性网格行构建器。</param>
    /// <param name="defaultConfigFactory">“添加控件”使用的默认控件配置工厂。</param>
    /// <param name="controlNameGenerator">唯一控件名称生成器。</param>
    /// <param name="localizationService">设计器本地化服务。</param>
    /// <param name="designerPreviewSharedDataService">仅用于预览的共享数据服务。</param>
    /// <param name="localResourceStore">本地包资源存储。</param>
    /// <param name="windowLayoutOptionsService">窗口选项持久化服务。</param>
    /// <param name="packageManager">布局包管理器。</param>
    /// <param name="frontedWindowService">前台窗口服务，用于保存后刷新实时输出。</param>
    /// <param name="behaviorService">行为文档服务。</param>
    /// <param name="behaviorEventCatalog">包含插件注册的统一行为事件目录。</param>
    /// <param name="behaviorClipboard">共享行为剪贴板。</param>
    /// <param name="behaviorCopyPasteService">行为复制/粘贴服务。</param>
    /// <param name="animationRuntime">预览使用的动画运行时。</param>
    /// <param name="previewAnimationScope">预览动画目标作用域。</param>
    /// <param name="logger">日志记录器。</param>
    /// <param name="settingsHostService">可选的设置宿主服务。</param>
    /// <param name="v3ControlRegistry">可选的 V3 控件注册表。</param>
    /// <param name="styleTransferService">可选的 v3 控件 StyleTransfer 服务；为 <see langword="null"/> 时按需创建默认实例。</param>
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
        FrontedBehaviorEventCatalog behaviorEventCatalog,
        IFrontedBehaviorClipboard behaviorClipboard,
        FrontedBehaviorCopyPasteService behaviorCopyPasteService,
        IFrontedAnimationRuntime animationRuntime,
        FrontedDesignerPreviewAnimationScope previewAnimationScope,
        ILogger<FrontedDesignerWindowViewModel> logger,
        ISettingsHostService? settingsHostService = null,
        IFrontedV3ControlRegistry? v3ControlRegistry = null,
        FrontedV3StyleTransferService? styleTransferService = null,
        IFrontedImageSafetyService? imageSafetyService = null)
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
        _imageSafetyService = imageSafetyService;
        _windowLayoutOptionsService = windowLayoutOptionsService;
        _packageManager = packageManager;
        _frontedWindowService = frontedWindowService;
        _behaviorService = behaviorService;
        _behaviorEventCatalog = behaviorEventCatalog;
        _behaviorClipboard = behaviorClipboard;
        _behaviorCopyPasteService = behaviorCopyPasteService;
        _animationRuntime = animationRuntime;
        _previewAnimationScope = previewAnimationScope;
        _logger = logger;
        _settingsHostService = settingsHostService;
        _layoutCatalog = layoutCatalog;
        _selectionBuilder = new FrontedV3DesignSelectionBuilder(v3ControlRegistry);
        _styleTransferService = styleTransferService;
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
        LoadWindowOptions(_selectedCatalogEntry.CanonicalWindowId);
    }

    /// <summary>
    /// 异步读取当前设计器目录中的布局译名，并刷新窗口选择器。
    /// </summary>
    /// <returns>译名加载和界面刷新完成后结束的任务。</returns>
    public async Task RefreshWindowDisplayNamesAsync()
    {
        foreach (var entry in _layoutCatalog.GetEntries())
        {
            try
            {
                var config = await _layoutService.LoadWindowConfigAsync(entry.CanonicalWindowId);
                if (config.DisplayNames.Count > 0)
                {
                    _displayNamesByWindowId[entry.CanonicalWindowId] =
                        new Dictionary<string, string>(config.DisplayNames, StringComparer.Ordinal);
                }
                else
                {
                    _displayNamesByWindowId.Remove(entry.CanonicalWindowId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(
                    ex,
                    "Failed to preload display names for designer window {WindowTypeName}.",
                    entry.CanonicalWindowId);
                _displayNamesByWindowId.Remove(entry.CanonicalWindowId);
            }
        }

        RefreshWindowDisplayNames();
    }

    /// <summary>
    /// 选中指定的窗口并加载其设计文档。
    /// </summary>
    /// <param name="canonicalWindowId">窗口 Canonical ID。</param>
    public void SelectWindow(string canonicalWindowId)
    {
        if (string.IsNullOrWhiteSpace(canonicalWindowId))
        {
            return;
        }

        var option = WindowOptions.FirstOrDefault(item =>
            string.Equals(item.WindowTypeName, canonicalWindowId, StringComparison.OrdinalIgnoreCase));
        if (option is not null)
        {
            SelectedWindow = option;
        }
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
    [NotifyPropertyChangedFor(nameof(IsPolygonSelected))]
    private FrontedControlDesignItem? _selectedDesignItem;

    public bool HasSelectedDesignItem => SelectedDesignItem is not null;

    /// <summary>
    /// 获取当前统一选中目标。根控件选中时为 Root selection；
    /// 子控件（Part/CollectionItem）选中时为对应子目标；
    /// 无选中或选中无可用 Schema 时为 <see langword="null"/>。
    /// </summary>
    public FrontedV3DesignSelection? SelectedTarget
    {
        get => _selectedTarget;
        private set
        {
            if (ReferenceEquals(_selectedTarget, value))
            {
                return;
            }

            _selectedTarget = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedTarget));
            OnPropertyChanged(nameof(IsSubControlSelected));
            OnPropertyChanged(nameof(HasChildAppearanceProperties));
            OnPropertyChanged(nameof(HasLayoutTemplate));
            OnPropertyChanged(nameof(HasNamedLayoutTemplates));
            RefreshLayoutTemplates();
            ApplyParentStyleToChildrenCommand.NotifyCanExecuteChanged();
            ClearChildStyleOverridesCommand.NotifyCanExecuteChanged();
            ApplyLayoutTemplateCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>
    /// 获取是否具有统一选中目标。
    /// </summary>
    public bool HasSelectedTarget => _selectedTarget is not null;

    /// <summary>
    /// 获取当前是否选中了子控件（Part 或 CollectionItem）。
    /// </summary>
    public bool IsSubControlSelected =>
        _selectedTarget is { Kind: not FrontedV3DesignSelectionKind.Root };

    /// <summary>
    /// 获取当前选中根控件在 <see cref="CurrentDocument"/> 中是否存在相同
    /// <see cref="FrontedControlConfigBase.ControlType"/> 的其他控件（同类型 peer），
    /// 用于驱动"应用到同类型控件"按钮的启用状态。
    /// </summary>
    /// <remarks>
    /// 仅根控件选中时检查；子控件选中时（Part/CollectionItem）始终返回 <see langword="false"/>。
    /// peer 判定使用 <see cref="FrontedControlConfigBase.ControlType"/> 完全相等 +
    /// Config 引用不等，避免在源控件自身上报真。
    /// </remarks>
    public bool HasSameTypePeers => TryGetSameTypePeerDesignItems().Count > 0;

    /// <summary>
    /// 获取当前选中根控件是否声明了 <c>SupportsPeerStyleTransfer</c>，
    /// 用于驱动"应用到同类型控件"按钮的可见性。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 仅当根控件选中且对应 Registration 的
    /// <see cref="FrontedV3ControlRegistration.SupportsPeerStyleTransfer"/>
    /// 为 <see langword="true"/> 时返回 <see langword="true"/>。
    /// 子控件选中时（Part/CollectionItem）始终返回 <see langword="false"/>。
    /// </para>
    /// <para>
    /// 该属性只决定按钮的可见性；按钮的启用状态由
    /// <see cref="CanApplyAppearanceToSameType"/>（同时检查 attribute 与 peer）决定。
    /// </para>
    /// </remarks>
    public bool CanShowPeerStyleTransferButton
    {
        get
        {
            if (_selectedTarget is { Kind: not FrontedV3DesignSelectionKind.Root })
            {
                return false;
            }

            if (SelectedDesignItem?.Config is not { } sourceConfig)
            {
                return false;
            }

            return _selectionBuilder.ResolveRegistration(sourceConfig)?.SupportsPeerStyleTransfer == true;
        }
    }

    /// <summary>
    /// 获取当前选中的根控件是否拥有可派发外观属性的子控件集合，
    /// 用于驱动"应用到所有子控件"与"清除子控件外观覆盖"按钮的可见性。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 仅当根控件选中（<see cref="SelectedTarget"/> 为 <see langword="null"/> 或
    /// <see cref="FrontedV3DesignSelectionKind.Root"/>）且其 Config 在
    /// <see cref="BuiltInPartCollectionDefinitionResolver"/> 中存在至少一个
    /// <see cref="FrontedV3PartCollectionDefinition.ItemPropertiesFactory"/> 非 <see langword="null"/>
    /// 的 PartCollection 定义时返回 <see langword="true"/>。
    /// </para>
    /// <para>
    /// 该属性只决定按钮的可见性；按钮的启用状态由
    /// <see cref="CanApplyParentStyleToChildren"/>/<see cref="CanClearChildStyleOverrides"/> 决定。
    /// </para>
    /// </remarks>
    public bool HasChildAppearanceProperties
    {
        get
        {
            // 子控件选中时（Part/CollectionItem）不显示父到子派发按钮。
            if (_selectedTarget is { Kind: not FrontedV3DesignSelectionKind.Root })
            {
                return false;
            }

            if (SelectedDesignItem?.Config is not { } sourceConfig)
            {
                return false;
            }

            foreach (var collection in _selectionBuilder.GetCollections(sourceConfig))
            {
                if (collection.ItemPropertiesFactory is not null)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// 获取当前选中根控件是否支持"按模板重新分配"布局能力。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 当选中根控件且其 Config 存在 <see cref="FrontedV3PartCollectionDefinition.ApplyTemplate"/>
    /// 非 <see langword="null"/> 的 PartCollection 定义时为 <see langword="true"/>。
    /// </para>
    /// <para>
    /// 该属性驱动 Designer 中"按模板重新分配"通用按钮的可见性；
    /// 按钮的启用状态由 <see cref="CanApplyLayoutTemplate"/> 决定。
    /// 当 <see cref="HasNamedLayoutTemplates"/> 为 <see langword="true"/> 时，
    /// 通用按钮不再显示，改为渲染 <see cref="LayoutTemplates"/> 中的具名模板按钮。
    /// </para>
    /// </remarks>
    public bool HasLayoutTemplate
    {
        get
        {
            if (_selectedTarget is { Kind: not FrontedV3DesignSelectionKind.Root })
            {
                return false;
            }

            if (SelectedDesignItem?.Config is not { } sourceConfig)
            {
                return false;
            }

            foreach (var collection in _selectionBuilder.GetCollections(sourceConfig))
            {
                if (collection.ApplyTemplate is not null)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// 获取当前选中根控件是否声明了具名布局模板列表（<see cref="FrontedV3PartCollectionDefinition.Templates"/>）。
    /// </summary>
    /// <remarks>
    /// 当为 <see langword="true"/> 时，Designer 渲染 <see cref="LayoutTemplates"/> 中的具名模板按钮，
    /// 不再显示单一通用"按模板重新分配"按钮。每个按钮点击后通过
    /// <see cref="ApplyLayoutTemplateByNameCommand"/> 调用 <see cref="ApplyLayoutTemplateByName"/>
    /// 并以模板 Id 作为 <see cref="FrontedV3TemplateContext.TemplateId"/> 传入回调。
    /// </remarks>
    public bool HasNamedLayoutTemplates => LayoutTemplates.Count > 0;

    /// <summary>
    /// 获取当前选中根控件的具名布局模板视图列表，供 Designer 渲染独立模板按钮。
    /// </summary>
    public ObservableCollection<FrontedV3LayoutTemplateViewModel> LayoutTemplates { get; } = [];

    /// <summary>
    /// 刷新 <see cref="LayoutTemplates"/> 列表，根据当前选中根控件的
    /// <see cref="FrontedV3PartCollectionDefinition.Templates"/> 与本地化服务构建按钮视图。
    /// </summary>
    private void RefreshLayoutTemplates()
    {
        LayoutTemplates.Clear();

        if (_selectedTarget is { Kind: not FrontedV3DesignSelectionKind.Root })
        {
            return;
        }

        if (SelectedDesignItem?.Config is not { } sourceConfig)
        {
            return;
        }

        foreach (var collection in _selectionBuilder.GetCollections(sourceConfig))
        {
            if (collection.ApplyTemplate is null)
            {
                continue;
            }

            if (collection.Templates is null || collection.Templates.Count == 0)
            {
                continue;
            }

            foreach (var template in collection.Templates)
            {
                var displayName = _localizationService.GetDesignerText(
                    template.DisplayNameKey,
                    fallback: template.Id);
                var description = string.IsNullOrEmpty(template.DescriptionKey)
                    ? null
                    : _localizationService.GetDesignerText(
                        template.DescriptionKey,
                        fallback: null);
                LayoutTemplates.Add(new FrontedV3LayoutTemplateViewModel
                {
                    Id = template.Id,
                    DisplayName = displayName,
                    ToolTip = description
                });
            }

            break;
        }
    }

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

    public bool CanDeleteSelectedControl =>
        SelectedDesignItem is { IsSelectableInEditor: true, IsEditableInEditor: true };

    public bool CanCopySelectedControl =>
        CanCopyControl(SelectedDesignItem);

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
            .FirstOrDefault(e => e.CanonicalWindowId == value.WindowTypeName);
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
        OnPropertyChanged(nameof(HasSameTypePeers));
        OnPropertyChanged(nameof(CanShowPeerStyleTransferButton));
        OnPropertyChanged(nameof(HasChildAppearanceProperties));
        OnPropertyChanged(nameof(HasLayoutTemplate));
        OnPropertyChanged(nameof(HasNamedLayoutTemplates));
        RefreshLayoutTemplates();
        ApplyAppearanceToSameTypeCommand.NotifyCanExecuteChanged();
        ApplyParentStyleToChildrenCommand.NotifyCanExecuteChanged();
        ClearChildStyleOverridesCommand.NotifyCanExecuteChanged();
        ApplyLayoutTemplateCommand.NotifyCanExecuteChanged();
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
        SelectedPolygonVertexIndex = value?.Config is IPolygonFrontedControlConfig polygon && polygon.Points.Count > 0
            ? 0
            : -1;
        ApplyDesignSelectionFlags();
        SyncSelectedTargetOnDesignItemChanged(value);

        BehaviorPanel.SetSelectedControl(SelectedDesignItems.Count > 1 ? null : value);
        RefreshSelectedControlDisplay();
        RebuildAnimationPartEditorItems();
        RebuildPropertyEditorItems();
        RefreshLayerNodeSelection();
        DeleteSelectedControlCommand.NotifyCanExecuteChanged();
        CopySelectedControlCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(IsPolygonSelected));
        OnPropertyChanged(nameof(HasAnimationPartEditor));
        OnPropertyChanged(nameof(SelectedPolygonVertexDisplay));
        OnPropertyChanged(nameof(CanRemovePolygonVertex));
        RemovePolygonVertexCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(HasSameTypePeers));
        OnPropertyChanged(nameof(CanShowPeerStyleTransferButton));
        OnPropertyChanged(nameof(HasChildAppearanceProperties));
        OnPropertyChanged(nameof(HasLayoutTemplate));
        OnPropertyChanged(nameof(HasNamedLayoutTemplates));
        RefreshLayoutTemplates();
        ApplyAppearanceToSameTypeCommand.NotifyCanExecuteChanged();
        ApplyParentStyleToChildrenCommand.NotifyCanExecuteChanged();
        ClearChildStyleOverridesCommand.NotifyCanExecuteChanged();
        ApplyLayoutTemplateCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// 在 <see cref="SelectedDesignItem"/> 变化时同步 <see cref="SelectedTarget"/>。
    /// 新值为 <see langword="null"/> 时清除 <see cref="SelectedTarget"/>；
    /// 新值非 <see langword="null"/> 且当前 <see cref="SelectedTarget"/> 为 <see langword="null"/>
    /// 或非 Root 选中时，重建 Root 选中。
    /// </summary>
    /// <param name="value">新选中的设计项。</param>
    private void SyncSelectedTargetOnDesignItemChanged(FrontedControlDesignItem? value)
    {
        if (_isApplyingSelectedTarget)
        {
            return;
        }

        if (value is null)
        {
            SelectedTarget = null;
            return;
        }

        if (_selectedTarget is { Kind: FrontedV3DesignSelectionKind.Root } root
            && ReferenceEquals(root.DesignItem, value))
        {
            return;
        }

        SelectedTarget = _selectionBuilder.BuildRootSelection(value);
    }

    partial void OnSelectedPolygonVertexIndexChanged(int value)
    {
        RemovePolygonVertexCommand.NotifyCanExecuteChanged();
    }

}
