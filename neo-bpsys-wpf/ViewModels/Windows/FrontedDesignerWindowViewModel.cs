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
    /// <returns>编辑缓冲已更新时返回 <see langword="true"/>。</returns>
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
    /// 根据资源浏览器选择更新选中动画部件的图片编辑缓冲，并在本地超限图片时提供压缩选项。
    /// </summary>
    /// <param name="selectedResourcePath">选中的内置、包内或绝对图片路径。</param>
    /// <returns>编辑缓冲已更新时返回 <see langword="true"/>。</returns>
    public async Task<bool> ApplyAnimationPartImageResourceSelectionAsync(string selectedResourcePath)
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

        return await StoreLocalAnimationPartImageAsync(selectedResourcePath);
    }

    /// <summary>
    /// 在导入本地图片到资源存储前进行预校验，校验失败时返回本地化的错误消息。
    /// 与 <see cref="FrontedLocalResourceStore.StoreImageWithResult"/> 内部使用的
    /// <see cref="FrontedImagePurpose.Background"/> 限制保持一致。
    /// </summary>
    /// <param name="sourcePath">本地图片的绝对路径。</param>
    /// <returns>校验失败时返回错误消息；校验通过或未注入校验服务时返回 <see langword="null"/>。</returns>
    private string? ValidateLocalImageForStorage(string sourcePath)
    {
        if (_imageSafetyService is null)
        {
            return null;
        }

        var validation = _imageSafetyService.ValidateFile(sourcePath, FrontedImagePurpose.Background);
        if (validation.IsValid)
        {
            return null;
        }

        return BuildImageValidationFailureMessage(validation);
    }

    private async Task<(FrontedLocalResourceStoreResult? Result, string? ErrorMessage)> StoreLocalImageWithOptionalCompressionAsync(
        string sourcePath)
    {
        if (_localResourceStore is null)
        {
            return (null, "Local resource store is unavailable.");
        }

        var validation = _imageSafetyService?.ValidateFile(sourcePath, FrontedImagePurpose.Background);
        var compressOversizedImage = false;
        if (validation is { IsValid: false })
        {
            if (validation.ErrorCode is not ("ImageTooLarge" or "ImageTooManyPixels"))
            {
                return (null, BuildImageValidationFailureMessage(validation));
            }

            var compress = await MessageBoxHelper.ShowConfirmAsync(
                string.Format(
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "ImageCompressionMessage"),
                    BuildImageValidationFailureMessage(validation)),
                I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "ImageCompressionTitle"),
                I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "CompressAndApplyImage"),
                I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Cancel"));
            if (!compress)
            {
                return (null, BuildImageValidationFailureMessage(validation));
            }

            compressOversizedImage = true;
        }

        try
        {
            return (await Task.Run(
                () => _localResourceStore.StoreImageWithResult(sourcePath, compressOversizedImage)), null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to store local fronted image.");
            return (null, ex.Message);
        }
    }

    /// <summary>
    /// 根据图片校验结果构建本地化的错误消息。文件大小超限和图片尺寸超限时
    /// 返回包含实际值与目标压缩值的友好提示。
    /// </summary>
    /// <param name="validation">图片校验结果。</param>
    /// <returns>用于错误提示的本地化消息。</returns>
    private static string BuildImageValidationFailureMessage(FrontedImageValidationResult validation)
    {
        if (validation is { IsValid: false, ErrorCode: "ImageTooLarge" })
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "ImageFileTooLarge"),
                FormatFileSize(validation.FileBytes),
                FormatFileSize(FrontedLayoutLimits.MaxBackgroundImageBytes));
        }

        if (validation is { IsValid: false, ErrorCode: "ImageTooManyPixels" })
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "ImageDimensionsTooLarge"),
                validation.PixelWidth,
                validation.PixelHeight,
                FrontedLayoutLimits.MaxBackgroundImageLongSide);
        }

        return validation.ErrorMessage ?? I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "FailedToApplyPicture");
    }

    /// <summary>
    /// 将字节数格式化为带二进制单位（B / KiB / MiB）的可读字符串，
    /// 整数字节数省略小数部分。
    /// </summary>
    /// <param name="bytes">字节数。</param>
    /// <returns>带单位的可读大小字符串。</returns>
    private static string FormatFileSize(long bytes)
    {
        if (bytes >= 1024 * 1024)
        {
            var mib = bytes / (1024.0 * 1024);
            return Math.Abs(mib - Math.Floor(mib)) < double.Epsilon
                ? $"{(long)mib} MiB"
                : $"{mib:F2} MiB";
        }

        if (bytes >= 1024)
        {
            var kib = bytes / 1024.0;
            return Math.Abs(kib - Math.Floor(kib)) < double.Epsilon
                ? $"{(long)kib} KiB"
                : $"{kib:F2} KiB";
        }

        return $"{bytes} B";
    }

    /// <summary>
    /// 导入本地图片，并更新选中图片动画部件的编辑缓冲。
    /// </summary>
    /// <param name="sourcePath">本地图片的绝对路径。</param>
    /// <returns>图片导入并选中时返回 <see langword="true"/>。</returns>
    public bool StoreLocalAnimationPartImage(string sourcePath)
    {
        if (_localResourceStore is null || AnimationPartEditBuffer is not { IsImage: true } editor)
        {
            return false;
        }

        var validationMessage = ValidateLocalImageForStorage(sourcePath);
        if (validationMessage is not null)
        {
            StatusMessage = $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "FailedToApplyPicture")}: {validationMessage}";
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

    /// <summary>
    /// 导入本地图片，并在超限时由用户选择是否压缩后更新选中图片动画部件的编辑缓冲。
    /// </summary>
    /// <param name="sourcePath">本地图片的绝对路径。</param>
    /// <returns>图片导入并选中时返回 <see langword="true"/>。</returns>
    public async Task<bool> StoreLocalAnimationPartImageAsync(string sourcePath)
    {
        if (AnimationPartEditBuffer is not { IsImage: true } editor)
        {
            return false;
        }

        var (result, errorMessage) = await StoreLocalImageWithOptionalCompressionAsync(sourcePath);
        if (result is null)
        {
            StatusMessage = $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "FailedToApplyPicture")}: {errorMessage}";
            return false;
        }

        editor.ImagePath = result.ResourceUri;
        RecordPendingImportedResource(result, "AnimationPart ImagePath", wasApplied: true);
        if (result.WasCompressed)
        {
            StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "ImageCompressed");
        }

        return true;
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
                entry.CanonicalWindowId,
                cancellationToken);
            if (cancellationToken.IsCancellationRequested || reloadVersion != _reloadLayoutVersion)
            {
                return;
            }

            ApplyLayoutSource(loadResult, entry);

            var windowConfig = loadResult.Config;
            var document = _designConverter.FromConfig(
                entry.CanonicalWindowId,
                FrontedLayoutConstants.BaseCanvasName,
                FrontedWindowConfigCanvasAdapter.ToCanvasConfig(windowConfig));

            _currentWindowSettings = CloneWindowSettings(windowConfig.WindowSettings);
            ControlFilterText = string.Empty;
            CurrentDocument = document;
            CurrentDocument.IsDirty = false;
            LoadWindowOptions(entry.CanonicalWindowId);
            var behaviorDocument = await _behaviorService.LoadDocumentAsync(
                entry.CanonicalWindowId,
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
                entry.CanonicalWindowId);

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
    /// <returns>保存成功完成时返回 <see langword="true"/>。</returns>
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
            if (shouldSaveLayout || wasBuiltInSource)
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
    /// <returns>重置成功时返回 <see langword="true"/>。</returns>
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
        var behaviorDocument = await _behaviorService.LoadBuiltInDocumentAsync(windowTypeName);

        ControlFilterText = string.Empty;
        CurrentDocument = document;
        ResetBehaviorDocument(behaviorDocument);
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
    /// <param name="parameter">应为 <see cref="FrontedAddControlCatalogItem"/>。</param>
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
        if (CurrentDocument is null
            || SelectedDesignItem?.Config is not { } config
            || !GlobalScoreRowCellLayoutHelper.SupportsTemplateOperations(config))
        {
            return;
        }

        CaptureUndoSnapshot();
        GlobalScoreRowCellLayoutHelper.EnsureCompleteCells(
            config,
            CurrentDocument.EditingBoModeState == FrontedCanvasBoModeState.Bo3);
        FinishGlobalScoreRowAction();
    }

    [RelayCommand]
    private void AutoArrangeGlobalScoreCellsBySpacing()
    {
        if (CurrentDocument is null
            || SelectedDesignItem?.Config is not { } config
            || !GlobalScoreRowCellLayoutHelper.SupportsTemplateOperations(config))
        {
            return;
        }

        CaptureUndoSnapshot();
        GlobalScoreRowCellLayoutHelper.AutoArrangeBySpacing(
            config,
            CurrentDocument.EditingBoModeState == FrontedCanvasBoModeState.Bo3);
        FinishGlobalScoreRowAction();
    }

    [RelayCommand]
    private void ApplyBo3GlobalScoreVisibilityTemplate()
    {
        if (CurrentDocument is null
            || SelectedDesignItem?.Config is not { } config
            || !GlobalScoreRowCellLayoutHelper.SupportsTemplateOperations(config))
        {
            return;
        }

        CaptureUndoSnapshot();
        GlobalScoreRowCellLayoutHelper.ApplyBo3VisibilityTemplate(config);
        FinishGlobalScoreRowAction();
    }

    [RelayCommand]
    private void ApplyBo5GlobalScoreVisibilityTemplate()
    {
        if (CurrentDocument is null
            || SelectedDesignItem?.Config is not { } config
            || !GlobalScoreRowCellLayoutHelper.SupportsTemplateOperations(config))
        {
            return;
        }

        CaptureUndoSnapshot();
        GlobalScoreRowCellLayoutHelper.ApplyBo5VisibilityTemplate(config);
        FinishGlobalScoreRowAction();
    }

    private void FinishGlobalScoreRowAction()
    {
        if (CurrentDocument is null)
        {
            return;
        }

        CurrentDocument.IsDirty = true;
        RebuildPropertyEditorItems();
        RefreshDirtyState();
        RequestPreviewRenderCurrentDocument();
    }

    /// <summary>
    /// 判断"应用到同类型控件"命令是否可执行：需要存在当前文档、选中根控件、
    /// 源控件 Registration 声明了 <see cref="FrontedV3ControlRegistration.SupportsPeerStyleTransfer"/>、
    /// 且文档中存在与源控件 <see cref="FrontedControlConfigBase.ControlType"/> 相同的其他控件。
    /// </summary>
    /// <returns>当可执行同类型样式传播时返回 <see langword="true"/>。</returns>
    private bool CanApplyAppearanceToSameType()
    {
        if (CurrentDocument is null)
        {
            return false;
        }

        // 子控件选中时禁用，仅根控件选中可传播同类型样式。
        if (_selectedTarget is { Kind: not FrontedV3DesignSelectionKind.Root })
        {
            return false;
        }

        var sourceConfig = SelectedDesignItem?.Config;
        if (sourceConfig is null)
        {
            return false;
        }

        // 仅当控件显式声明 SupportsPeerStyleTransfer 时才允许同类型样式传播。
        if (_selectionBuilder.ResolveRegistration(sourceConfig)?.SupportsPeerStyleTransfer != true)
        {
            return false;
        }

        return TryGetSameTypePeerDesignItems().Count > 0;
    }

    /// <summary>
    /// 将当前选中根控件的外观属性（按 <see cref="FrontedV3StyleTransferProfile.Default"/>）
    /// 传播到 <see cref="CurrentDocument"/> 中所有相同 <see cref="FrontedControlConfigBase.ControlType"/>
    /// 的其他控件上。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 传播范围由 <see cref="FrontedV3StyleTransferProfile.Default"/> 控制，仅传播
    /// <see cref="FrontedV3PropertySemantic.Appearance"/> 语义的属性；
    /// <see cref="FrontedV3PropertySemantic.DataIdentity"/> 与位置/尺寸/行为/效果等语义不会被传播。
    /// </para>
    /// <para>
    /// 完成传播后触发：Undo 快照已先于传播捕获、属性面板重建、预览刷新、文档标记为脏。
    /// </para>
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanApplyAppearanceToSameType))]
    private void ApplyAppearanceToSameType()
    {
        if (CurrentDocument is null)
        {
            return;
        }

        // 子控件选中时禁用，仅根控件选中可传播同类型样式。
        if (_selectedTarget is { Kind: not FrontedV3DesignSelectionKind.Root })
        {
            return;
        }

        var sourceDesignItem = SelectedDesignItem;
        if (sourceDesignItem?.Config is not { } sourceConfig)
        {
            return;
        }

        var sourceRegistration = _selectionBuilder.ResolveRegistration(sourceConfig);
        if (sourceRegistration is null)
        {
            return;
        }

        // 仅当控件显式声明 SupportsPeerStyleTransfer 时才允许同类型样式传播。
        // 该检查与 CanApplyAppearanceToSameType 保持一致，防止 Execute 被绕过 CanExecute 直接调用时
        // 对未声明该能力的控件类型意外传播样式。
        if (!sourceRegistration.SupportsPeerStyleTransfer)
        {
            return;
        }

        var peerDesignItems = TryGetSameTypePeerDesignItems();
        if (peerDesignItems.Count == 0)
        {
            return;
        }

        var peers = new List<PeerStyleTarget>(peerDesignItems.Count);
        foreach (var peerDesignItem in peerDesignItems)
        {
            if (peerDesignItem.Config is null)
            {
                continue;
            }

            var peerRegistration = _selectionBuilder.ResolveRegistration(peerDesignItem.Config);
            if (peerRegistration is null)
            {
                continue;
            }

            peers.Add(new PeerStyleTarget(peerRegistration, peerDesignItem.Config));
        }

        if (peers.Count == 0)
        {
            return;
        }

        CaptureUndoSnapshot();
        StyleTransferService.TransferPeerStyle(
            sourceRegistration,
            sourceConfig,
            peers,
            FrontedV3StyleTransferProfile.Default);

        CurrentDocument.IsDirty = true;
        RebuildPropertyEditorItems();
        RefreshDirtyState();
        RequestPreviewRenderCurrentDocument();
        ApplyAppearanceToSameTypeCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// 返回当前文档中与选中根控件 <see cref="FrontedControlConfigBase.ControlType"/> 相同、
    /// 但 Config 引用不同的设计项列表。仅根控件选中时返回非空列表。
    /// </summary>
    /// <returns>同类型 peer 设计项列表；无选中或无 peer 时返回空列表。</returns>
    private List<FrontedControlDesignItem> TryGetSameTypePeerDesignItems()
    {
        if (CurrentDocument is null)
        {
            return [];
        }

        // 子控件选中时不参与同类型传播，避免对 Part/CollectionItem 应用外观传播。
        if (_selectedTarget is { Kind: not FrontedV3DesignSelectionKind.Root })
        {
            return [];
        }

        var sourceDesignItem = SelectedDesignItem;
        if (sourceDesignItem?.Config is not { } sourceConfig)
        {
            return [];
        }

        var sourceControlType = sourceConfig.ControlType;
        var peers = new List<FrontedControlDesignItem>();
        foreach (var item in CurrentDocument.Controls)
        {
            if (item is null
                || ReferenceEquals(item, sourceDesignItem)
                || item.Config is null
                || !string.Equals(item.Config.ControlType, sourceControlType, StringComparison.Ordinal))
            {
                continue;
            }

            peers.Add(item);
        }

        return peers;
    }

    /// <summary>
    /// 判断"应用到所有子控件"命令是否可执行：需要选中根控件且其 Config 存在
    /// <see cref="FrontedV3PartCollectionDefinition.ItemPropertiesFactory"/> 非 <see langword="null"/>
    /// 的 PartCollection 定义。
    /// </summary>
    /// <returns>当可执行父到子外观派发时返回 <see langword="true"/>。</returns>
    private bool CanApplyParentStyleToChildren() => HasChildAppearanceProperties;

    /// <summary>
    /// 将当前选中根控件的外观属性（按 <see cref="FrontedV3StyleTransferProfile.Default"/>）
    /// 派发到所有子控件集合项（如 GlobalScoreRow 的 Cells）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 实现要点：
    /// <list type="bullet">
    /// <item>通过 <see cref="BuiltInPartCollectionDefinitionResolver"/> 查找选中控件 Config 上
    /// <see cref="FrontedV3PartCollectionDefinition.ItemPropertiesFactory"/> 非 <see langword="null"/> 的集合定义。</item>
    /// <item>对每个集合项，使用 <see cref="FrontedV3PartCollectionDefinition.ItemPropertiesFactory"/>
    /// 构建绑定到该项 <c>itemKey</c> 的子属性列表（<see cref="FrontedV3Storage.CollectionItemProperty"/> 存储）。</item>
    /// <item>调用 <see cref="FrontedV3StyleTransferService.ApplyParentStyle"/>，按 OptionsPath 匹配父子属性，
    /// 仅传播 <see cref="FrontedV3PropertySemantic.Appearance"/> 语义属性。</item>
    /// <item>由于 <see cref="FrontedV3Storage.CollectionItemProperty"/> 存储以父 Config 为载体按 itemKey 定位子项，
    /// 此处将父 Config 同时作为 parentConfig 与 childConfigs 元素传入。</item>
    /// </list>
    /// </para>
    /// <para>
    /// 完成派发后触发：Undo 快照已先于派发捕获、属性面板重建、预览刷新、文档标记为脏。
    /// </para>
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanApplyParentStyleToChildren))]
    private void ApplyParentStyleToChildren()
    {
        if (CurrentDocument is null)
        {
            return;
        }

        // 子控件选中时禁用，仅根控件选中可派发外观到子控件。
        if (_selectedTarget is { Kind: not FrontedV3DesignSelectionKind.Root })
        {
            return;
        }

        var sourceConfig = SelectedDesignItem?.Config;
        if (sourceConfig is null)
        {
            return;
        }

        var registration = _selectionBuilder.ResolveRegistration(sourceConfig);
        if (registration is null)
        {
            return;
        }

        var collection = ResolveChildAppearanceCollection(sourceConfig);
        if (collection?.ItemPropertiesFactory is null || collection.CollectionGetter is null)
        {
            return;
        }

        var childItems = collection.CollectionGetter(sourceConfig);
        if (childItems is null || childItems.Count == 0)
        {
            return;
        }

        CaptureUndoSnapshot();

        // 对每个子项构建绑定到其 itemKey 的子属性列表，并以父 Config 作为 ApplyParentStyle 的 childConfig 载体
        // （CollectionItemProperty 存储通过 itemKey 在父 Config 的集合中定位实际子项）。
        foreach (var childItem in childItems)
        {
            if (childItem is null)
            {
                continue;
            }

            var itemKey = collection.ItemKeySelector(childItem);
            var childProperties = collection.ItemPropertiesFactory(itemKey);
            if (childProperties.Count == 0)
            {
                continue;
            }

            StyleTransferService.ApplyParentStyle(
                registration.Properties,
                sourceConfig,
                childProperties,
                [sourceConfig],
                FrontedV3StyleTransferProfile.Default);
        }

        CurrentDocument.IsDirty = true;
        RebuildPropertyEditorItems();
        RefreshDirtyState();
        RequestPreviewRenderCurrentDocument();
    }

    /// <summary>
    /// 判断"清除子控件外观覆盖"命令是否可执行：需要选中根控件且其 Config 存在
    /// <see cref="FrontedV3PartCollectionDefinition.ItemPropertiesFactory"/> 非 <see langword="null"/>
    /// 的 PartCollection 定义。
    /// </summary>
    /// <returns>当可清除子控件外观覆盖时返回 <see langword="true"/>。</returns>
    private bool CanClearChildStyleOverrides() => HasChildAppearanceProperties;

    /// <summary>
    /// 清除所有子控件集合项的外观属性 override（<see cref="FrontedV3PropertyInheritance.ParentFallback"/>
    /// 与 <see cref="FrontedV3PropertyInheritance.LockedToParent"/> 模式属性），
    /// 使子控件回退到父值。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 实现要点与 <see cref="ApplyParentStyleToChildren"/> 类似，通过
    /// <see cref="BuiltInPartCollectionDefinitionResolver"/> 查找集合定义，对每个子项构建子属性列表，
    /// 调用 <see cref="FrontedV3StyleTransferService.ClearChildOverrides"/> 将可清空属性写 <see langword="null"/>。
    /// </para>
    /// <para>
    /// 完成清除后触发：Undo 快照已先于清除捕获、属性面板重建、预览刷新、文档标记为脏。
    /// </para>
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanClearChildStyleOverrides))]
    private void ClearChildStyleOverrides()
    {
        if (CurrentDocument is null)
        {
            return;
        }

        // 子控件选中时禁用，仅根控件选中可清除子控件外观覆盖。
        if (_selectedTarget is { Kind: not FrontedV3DesignSelectionKind.Root })
        {
            return;
        }

        var sourceConfig = SelectedDesignItem?.Config;
        if (sourceConfig is null)
        {
            return;
        }

        var collection = ResolveChildAppearanceCollection(sourceConfig);
        if (collection?.ItemPropertiesFactory is null || collection.CollectionGetter is null)
        {
            return;
        }

        var childItems = collection.CollectionGetter(sourceConfig);
        if (childItems is null || childItems.Count == 0)
        {
            return;
        }

        CaptureUndoSnapshot();

        foreach (var childItem in childItems)
        {
            if (childItem is null)
            {
                continue;
            }

            var itemKey = collection.ItemKeySelector(childItem);
            var childProperties = collection.ItemPropertiesFactory(itemKey);
            if (childProperties.Count == 0)
            {
                continue;
            }

            StyleTransferService.ClearChildOverrides(
                childProperties,
                [sourceConfig],
                FrontedV3StyleTransferProfile.Default);
        }

        CurrentDocument.IsDirty = true;
        RebuildPropertyEditorItems();
        RefreshDirtyState();
        RequestPreviewRenderCurrentDocument();
    }

    /// <summary>
    /// 切换子控件继承属性的"跟随父控件 / 独立设定"状态。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 当 <paramref name="propertyName"/> 对应的属性为 <see cref="FrontedV3PropertyInheritance.ParentFallback"/> 继承属性时：
    /// <list type="bullet">
    /// <item>若当前为跟随父控件（<c>IsInheritedFromParent=true</c>），切换为独立设定：将当前显示值写入子控件作为 override。</item>
    /// <item>若当前为独立设定（<c>IsInheritedFromParent=false</c>），切换为跟随父控件：清除子控件 override（写 null）。</item>
    /// </list>
    /// </para>
    /// <para>
    /// 非继承属性调用此命令为 no-op。
    /// </para>
    /// </remarks>
    /// <param name="propertyName">属性行的 OptionsPath（<see cref="FrontedPropertyEditorItem.PropertyName"/>）。</param>
    [RelayCommand]
    private void TogglePropertyInheritance(string? propertyName)
    {
        if (string.IsNullOrEmpty(propertyName)
            || CurrentDocument is null
            || _selectedTarget is null
            || _selectedTarget.DesignItem is not { } designItem)
        {
            return;
        }

        if (!_schemaPropertiesByPath.TryGetValue(propertyName, out var schemaProperty))
        {
            return;
        }

        if (schemaProperty.Metadata.Inheritance != FrontedV3PropertyInheritance.ParentFallback)
        {
            return;
        }

        var config = designItem.Config;
        var childValue = schemaProperty.GetValue(config);
        var wasMissing = FrontedV3StyleTransferService.IsOverrideMissing(childValue);

        CaptureUndoSnapshot();

        if (wasMissing)
        {
            // 跟随父控件 → 独立设定：将当前继承显示值（父值）写入子控件作为 override。
            var parentProperties = ResolveParentPropertiesForInheritance(_selectedTarget);
            var parentProperty = parentProperties is not null
                ? FindPropertyByOptionsPath(parentProperties, schemaProperty.OptionsPath)
                : null;
            var inheritedValue = parentProperty is not null
                ? parentProperty.GetValue(config)
                : childValue;
            StyleTransferService.TrySetChildValue(schemaProperty, config, inheritedValue);
        }
        else
        {
            // 独立设定 → 跟随父控件：清除子控件 override（写 null）。
            schemaProperty.SetValue(config, null);
        }

        CurrentDocument.IsDirty = true;
        RebuildPropertyEditorItems();
        RefreshDirtyState();
        RequestPreviewRenderCurrentDocument();
    }

    /// <summary>
    /// 判断"按模板重新分配"命令是否可执行：需要选中根控件且其 Config 存在
    /// <see cref="FrontedV3PartCollectionDefinition.ApplyTemplate"/> 非 <see langword="null"/> 的 PartCollection 定义。
    /// </summary>
    /// <returns>当可执行模板分配时返回 <see langword="true"/>。</returns>
    private bool CanApplyLayoutTemplate()
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

    /// <summary>
    /// 对当前选中根控件的子控件集合应用布局模板（如 GlobalScoreRow 的 BO3/BO5 模板），
    /// 重新分配子控件的位置与可见性。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 实现要点：通过 <see cref="FrontedV3DesignSelectionBuilder"/> 查找选中控件 Config 上
    /// <see cref="FrontedV3PartCollectionDefinition.ApplyTemplate"/> 非 <see langword="null"/> 的集合定义，
    /// 调用其 <see cref="FrontedV3PartCollectionDefinition.ApplyTemplate"/> 回调。
    /// 该回调由控件自身实现，决定如何按模板分配位置与可见性（不修改外观属性）。
    /// </para>
    /// <para>
    /// 通用按钮路径：<see cref="FrontedV3TemplateContext.TemplateId"/> 为 <see langword="null"/>，
    /// 控件应回退到基于 <see cref="FrontedV3TemplateContext.CurrentBoModeState"/> 的默认模板。
    /// 具名模板按钮路径：通过 <see cref="ApplyLayoutTemplateByName"/> 调用，
    /// <see cref="FrontedV3TemplateContext.TemplateId"/> 为被点击模板的 Id。
    /// </para>
    /// <para>
    /// 完成分配后触发：Undo 快照已先于分配捕获、属性面板重建、预览刷新、文档标记为脏。
    /// </para>
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanApplyLayoutTemplate))]
    private void ApplyLayoutTemplate()
        => ApplyLayoutTemplateCore(templateId: null);

    /// <summary>
    /// 对当前选中根控件的子控件集合应用指定具名布局模板（如 <c>BO3</c>、<c>BO5</c>），
    /// 重新分配子控件的位置与可见性。
    /// </summary>
    /// <param name="templateId">被点击的具名模板 Id；为 <see langword="null"/> 或空字符串时回退到通用按钮行为。</param>
    /// <remarks>
    /// 该命令由 Designer 中具名模板按钮（<see cref="LayoutTemplates"/>）触发，
    /// 将 <paramref name="templateId"/> 作为 <see cref="FrontedV3TemplateContext.TemplateId"/>
    /// 传递给 <see cref="FrontedV3PartCollectionDefinition.ApplyTemplate"/> 回调。
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanApplyLayoutTemplate))]
    private void ApplyLayoutTemplateByName(string? templateId)
        => ApplyLayoutTemplateCore(templateId);

    /// <summary>
    /// "按模板重新分配"通用/具名模板共享实现。查找首个支持 <c>ApplyTemplate</c> 的 PartCollection，
    /// 构建 <see cref="FrontedV3TemplateContext"/>（携带当前 BO 状态与 <paramref name="templateId"/>），
    /// 调用回调并触发属性面板/预览刷新。
    /// </summary>
    /// <param name="templateId">具名模板 Id；为 <see langword="null"/> 时表示通用按钮路径。</param>
    private void ApplyLayoutTemplateCore(string? templateId)
    {
        if (CurrentDocument is null)
        {
            return;
        }

        if (_selectedTarget is { Kind: not FrontedV3DesignSelectionKind.Root })
        {
            return;
        }

        var sourceConfig = SelectedDesignItem?.Config;
        if (sourceConfig is null)
        {
            return;
        }

        FrontedV3PartCollectionDefinition? targetCollection = null;
        foreach (var collection in _selectionBuilder.GetCollections(sourceConfig))
        {
            if (collection.ApplyTemplate is not null)
            {
                targetCollection = collection;
                break;
            }
        }

        if (targetCollection?.ApplyTemplate is not { } applyTemplate)
        {
            return;
        }

        var context = BuildTemplateContext(templateId);

        // CaptureUndoSnapshot 在 applyTemplate 之前调用，捕获修改前快照，
        // 这样 Undo 时能回到调用前状态。若回调返回 false（无变更），
        // 需丢弃刚捕获的快照，避免无变化操作污染 Undo 与 dirty 状态。
        var undoCountBefore = _undoStack.Count;
        CaptureUndoSnapshot();
        var pushedNewSnapshot = _undoStack.Count > undoCountBefore;

        var modified = applyTemplate(sourceConfig, context);
        if (!modified)
        {
            if (pushedNewSnapshot)
            {
                _undoStack.Pop();
                NotifyUndoRedoCommands();
            }

            return;
        }

        CurrentDocument.IsDirty = true;
        RebuildPropertyEditorItems();
        RefreshDirtyState();
        RequestPreviewRenderCurrentDocument();
    }

    /// <summary>
    /// 构建调用 <see cref="FrontedV3PartCollectionDefinition.ApplyTemplate"/> 时使用的
    /// <see cref="FrontedV3TemplateContext"/>，携带当前编辑的 BO 状态、窗口/Canvas 信息、
    /// Designer 文档实例、DI 服务提供器与可选的具名模板 Id。
    /// </summary>
    /// <param name="templateId">具名模板 Id；为 <see langword="null"/> 时表示通用按钮路径。</param>
    /// <returns>用于调用模板分配回调的上下文实例。</returns>
    private FrontedV3TemplateContext BuildTemplateContext(string? templateId)
    {
        var document = CurrentDocument;
        var boModeState = document?.EditingBoModeState ?? FrontedCanvasBoModeState.Bo5;
        var windowTypeName = document?.WindowTypeName ?? string.Empty;
        var canvasName = document?.CanvasName ?? string.Empty;
        var services = IAppHost.Host?.Services ?? (IServiceProvider)EmptyServiceProvider.Instance;

        return new FrontedV3TemplateContext(
            services,
            boModeState,
            windowTypeName,
            canvasName,
            document,
            templateId);
    }

    /// <summary>
    /// 不解析任何服务的空 <see cref="IServiceProvider"/>，作为 Designer 默认服务提供器。
    /// </summary>
    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public static readonly EmptyServiceProvider Instance = new();

        public object? GetService(Type serviceType) => null;
    }

    /// <summary>
    /// 返回给定 Config 上首个 <see cref="FrontedV3PartCollectionDefinition.ItemPropertiesFactory"/>
    /// 非 <see langword="null"/> 的 PartCollection 定义；无则返回 <see langword="null"/>。
    /// </summary>
    /// <param name="config">根控件配置实例。</param>
    /// <returns>支持外观属性派发的集合定义；无则 <see langword="null"/>。</returns>
    private FrontedV3PartCollectionDefinition? ResolveChildAppearanceCollection(FrontedControlConfigBase config)
    {
        ArgumentNullException.ThrowIfNull(config);

        foreach (var candidate in _selectionBuilder.GetCollections(config))
        {
            if (candidate.ItemPropertiesFactory is not null)
            {
                return candidate;
            }
        }

        return null;
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
    /// <returns>两个值均有效并已应用时返回 <see langword="true"/>。</returns>
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
    /// <returns>两个值均有效并已应用时返回 <see langword="true"/>。</returns>
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
    /// <param name="text">要解析的文本。</param>
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
    private async Task ApplyBackgroundImageAsync()
    {
        await ApplyCanvasBackgroundEditAsync(BackgroundImageEditText);
    }

    /// <summary>
    /// 根据文本输入应用 Canvas 背景图片 URI。
    /// </summary>
    /// <param name="backgroundImage">背景图片 URI 或路径。</param>
    /// <returns>编辑被接受时返回 <see langword="true"/>。</returns>
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

    /// <summary>
    /// 应用资源浏览器选中的 Canvas 背景图片，并在本地超限图片时提供压缩选项。
    /// </summary>
    /// <param name="selectedResourcePath">选中的资源 URI 或文件路径。</param>
    /// <returns>资源已接受时返回 <see langword="true"/>。</returns>
    public async Task<bool> ApplyCanvasBackgroundResourceSelectionAsync(string selectedResourcePath)
    {
        return IsAbsoluteFilePath(selectedResourcePath)
            ? await StoreLocalBackgroundImageAsync(selectedResourcePath)
            : ApplyCanvasBackgroundEdit(selectedResourcePath);
    }

    /// <summary>
    /// 根据文本输入应用 Canvas 背景图片 URI，并在绝对本地图片超限时提供压缩选项。
    /// </summary>
    /// <param name="backgroundImage">背景图片 URI 或路径。</param>
    /// <returns>编辑被接受时返回 <see langword="true"/>。</returns>
    public async Task<bool> ApplyCanvasBackgroundEditAsync(string? backgroundImage)
    {
        return IsAbsoluteFilePath(backgroundImage)
            ? await StoreLocalBackgroundImageAsync(backgroundImage!)
            : ApplyCanvasBackgroundEdit(backgroundImage);
    }

    [RelayCommand]
    private void ClearBackgroundImage()
    {
        ClearCanvasBackground();
    }

    /// <summary>
    /// 清除活动 Canvas 状态的背景图片。
    /// </summary>
    /// <returns>文档可用并已更新时返回 <see langword="true"/>。</returns>
    public bool ClearCanvasBackground()
    {
        return ApplyCanvasBackgroundEdit(null);
    }

    /// <summary>
    /// 将本地背景图片复制到可编辑包资源存储，并应用其 BPUI URI。
    /// </summary>
    /// <param name="sourcePath">用户选择的本地图片文件。</param>
    /// <returns>文件导入并应用时返回 <see langword="true"/>。</returns>
    public bool StoreLocalBackgroundImage(string sourcePath)
    {
        if (_localResourceStore is null)
        {
            return false;
        }

        var validationMessage = ValidateLocalImageForStorage(sourcePath);
        if (validationMessage is not null)
        {
            CanvasPropertiesStatus = $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "FailedToApplyPicture")}: {validationMessage}";
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

    /// <summary>
    /// 将本地背景图片复制到可编辑包资源存储，并在超限时由用户选择是否压缩后应用其 BPUI URI。
    /// </summary>
    /// <param name="sourcePath">用户选择的本地图片文件。</param>
    /// <returns>文件导入并应用时返回 <see langword="true"/>。</returns>
    public async Task<bool> StoreLocalBackgroundImageAsync(string sourcePath)
    {
        var (result, errorMessage) = await StoreLocalImageWithOptionalCompressionAsync(sourcePath);
        if (result is null)
        {
            CanvasPropertiesStatus = $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "FailedToApplyPicture")}: {errorMessage}";
            return false;
        }

        var applied = ApplyCanvasBackgroundEdit(result.ResourceUri);
        RecordPendingImportedResource(result, "Canvas BackgroundImage", applied);
        if (result.WasCompressed)
        {
            CanvasPropertiesStatus = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "ImageCompressed");
        }

        return applied;
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
    /// <returns>属性已更新时返回 <see langword="true"/>。</returns>
    public bool ApplyPropertyResourceSelection(FrontedPropertyEditorItem item, string selectedResourcePath)
    {
        if (IsAbsoluteFilePath(selectedResourcePath))
        {
            if (_localResourceStore is null)
            {
                return false;
            }

            var validationMessage = ValidateLocalImageForStorage(selectedResourcePath);
            if (validationMessage is not null)
            {
                SetPropertyEditError(
                    item,
                    $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "FailedToApplyPicture")}: {validationMessage}",
                    selectedResourcePath);
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
    /// 将资源浏览器结果应用到图片/资源属性行，并在本地超限图片时提供压缩选项。
    /// </summary>
    /// <param name="item">接收资源值的属性行。</param>
    /// <param name="selectedResourcePath">选中的资源 URI 或文件路径。</param>
    /// <returns>属性已更新时返回 <see langword="true"/>。</returns>
    public async Task<bool> ApplyPropertyResourceSelectionAsync(
        FrontedPropertyEditorItem item,
        string selectedResourcePath)
    {
        if (!IsAbsoluteFilePath(selectedResourcePath))
        {
            return ApplyPropertyEdit(item, selectedResourcePath);
        }

        var (result, errorMessage) = await StoreLocalImageWithOptionalCompressionAsync(selectedResourcePath);
        if (result is null)
        {
            SetPropertyEditError(
                item,
                $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "FailedToApplyPicture")}: {errorMessage}",
                selectedResourcePath);
            return false;
        }

        var applied = ApplyPropertyEdit(item, result.ResourceUri);
        RecordPendingImportedResource(result, item.PropertyName, applied);
        if (result.WasCompressed)
        {
            StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "ImageCompressed");
        }

        return applied;
    }

    /// <summary>
    /// 将字体文件导入活动布局包，并应用第一个发现的字体族。
    /// </summary>
    /// <param name="item">字体族属性行。</param>
    /// <param name="sourcePath">源字体文件路径。</param>
    /// <returns>是否已导入并应用字体。</returns>
    /// <summary>
    /// 将字体导入当前布局包，并把其 BPUI 字体 URI 应用到属性行。
    /// </summary>
    /// <param name="item">接收导入字体 URI 的字体族属性行。</param>
    /// <param name="sourcePath">本地字体文件路径。</param>
    /// <returns>导入并应用属性成功时返回 <see langword="true"/>。</returns>
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
    /// <param name="exception">渲染异常。</param>
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
    /// <param name="item">要选择的设计项，或 <see langword="null"/> 表示清除选择。</param>
    public void SelectDesignItem(FrontedControlDesignItem? item)
    {
        if (item?.IsSelectableInEditor == false)
        {
            item = null;
        }

        SetSelectedDesignItems(item is null ? [] : [item], item);
    }

    /// <summary>
    /// 选中根控件，构建 Root selection 并同步 <see cref="SelectedDesignItem"/>。
    /// </summary>
    /// <param name="designItem">要选中的设计项。</param>
    /// <remarks>
    /// 当 <paramref name="designItem"/> 无可用 Schema 属性时，<see cref="SelectedTarget"/> 会被清除。
    /// </remarks>
    public void SelectRoot(FrontedControlDesignItem designItem)
    {
        ArgumentNullException.ThrowIfNull(designItem);

        _isApplyingSelectedTarget = true;
        try
        {
            if (!ReferenceEquals(SelectedDesignItem, designItem))
            {
                SetSelectedDesignItems([designItem], designItem);
            }

            SelectedTarget = _selectionBuilder.BuildRootSelection(designItem);
            RebuildPropertyEditorItems();
            RefreshLayerNodeSelection();
        }
        finally
        {
            _isApplyingSelectedTarget = false;
        }
    }

    /// <summary>
    /// 选中控件内部的固定 Part，构建 FixedPart selection。
    /// </summary>
    /// <param name="designItem">Part 所属的父控件设计项。</param>
    /// <param name="partId">Part 标识。</param>
    /// <remarks>
    /// 当 Part 不存在或无可用 Schema 时，<see cref="SelectedTarget"/> 保持不变。
    /// 该方法会同步 <see cref="SelectedDesignItem"/> 到 <paramref name="designItem"/>，以便属性网格与画布定位到父控件。
    /// </remarks>
    public void SelectFixedPart(FrontedControlDesignItem designItem, string partId)
    {
        ArgumentNullException.ThrowIfNull(designItem);
        ArgumentNullException.ThrowIfNull(partId);

        _isApplyingSelectedTarget = true;
        try
        {
            if (!ReferenceEquals(SelectedDesignItem, designItem))
            {
                SetSelectedDesignItems([designItem], designItem);
            }

            var selection = _selectionBuilder.BuildFixedPartSelection(designItem, partId);
            if (selection is null)
            {
                return;
            }

            SelectedTarget = selection;
            RebuildPropertyEditorItems();
            RefreshLayerNodeSelection();
        }
        finally
        {
            _isApplyingSelectedTarget = false;
        }
    }

    /// <summary>
    /// 选中控件内部 PartCollection 的一个集合项，构建 CollectionItem selection。
    /// </summary>
    /// <param name="designItem">集合所属的父控件设计项。</param>
    /// <param name="collectionId">集合标识。</param>
    /// <param name="itemKey">集合项唯一键。</param>
    /// <remarks>
    /// 当集合或项不存在时，<see cref="SelectedTarget"/> 保持不变。
    /// </remarks>
    public void SelectCollectionItem(FrontedControlDesignItem designItem, string collectionId, string itemKey)
    {
        ArgumentNullException.ThrowIfNull(designItem);
        ArgumentNullException.ThrowIfNull(collectionId);
        ArgumentNullException.ThrowIfNull(itemKey);

        _isApplyingSelectedTarget = true;
        try
        {
            if (!ReferenceEquals(SelectedDesignItem, designItem))
            {
                SetSelectedDesignItems([designItem], designItem);
            }

            var selection = _selectionBuilder.BuildCollectionItemSelection(designItem, collectionId, itemKey);
            if (selection is null)
            {
                return;
            }

            SelectedTarget = selection;
            RebuildPropertyEditorItems();
            RefreshLayerNodeSelection();
        }
        finally
        {
            _isApplyingSelectedTarget = false;
        }
    }

    /// <summary>
    /// 当子控件（Part/CollectionItem）被选中时，按 Esc 回退到根控件选中。
    /// 根控件选中时调用此方法无效果。
    /// </summary>
    /// <returns>是否执行了回退（即调用前为子控件选中）。</returns>
    public bool EscapeToRootSelection()
    {
        if (_selectedTarget is not { Kind: not FrontedV3DesignSelectionKind.Root } target
            || target.DesignItem is null)
        {
            return false;
        }

        SelectRoot(target.DesignItem);
        return true;
    }

    /// <summary>
    /// 返回当前选中根控件可编辑子控件（Part/CollectionItem）的命中框与装饰器信息列表。
    /// 供 View 创建透明 hitbox；仅在根控件选中时返回非空列表，子控件选中或无选中时返回空列表。
    /// </summary>
    /// <returns>子控件目标信息列表；无可用子控件时返回空列表。</returns>
    /// <remarks>
    /// 几何值相对于父控件，View 需要叠加父控件的画布坐标得到绝对位置。
    /// 当<see cref="SelectedTarget"/> 为子控件选中时也返回空列表（不再显示同级 hitbox）。
    /// </remarks>
    public IReadOnlyList<DesignerChildTargetInfo> GetChildTargetInfos()
    {
        if (SelectedDesignItem is not { } designItem
            || _selectedTarget is not { Kind: FrontedV3DesignSelectionKind.Root })
        {
            return Array.Empty<DesignerChildTargetInfo>();
        }

        var config = designItem.Config;
        var parentBounds = FrontedDesignerBoundsResolver.Resolve(config);
        var parentWidth = parentBounds.Width;
        var parentHeight = parentBounds.Height;
        var result = new List<DesignerChildTargetInfo>();

        foreach (var part in _selectionBuilder.GetAvailableParts(designItem))
        {
            var geometry = new FixedPartGeometryTarget(part, config);
            var width = geometry.Width ?? parentWidth;
            var height = geometry.Height ?? parentHeight;
            result.Add(new DesignerChildTargetInfo
            {
                ParentItem = designItem,
                Id = part.Id,
                ItemKey = null,
                IsCollectionItem = false,
                Left = geometry.Left,
                Top = geometry.Top,
                Width = width,
                Height = height,
                CanMove = part.Capabilities.CanMove,
                CanResize = part.Capabilities.CanResize
            });
        }

        foreach (var collection in _selectionBuilder.GetAvailableCollections(designItem))
        {
            var items = collection.CollectionGetter(config);
            foreach (var item in items)
            {
                var itemKey = collection.ItemKeySelector(item);
                var geometry = new CollectionItemGeometryTarget(collection, config, itemKey);
                var width = geometry.Width ?? parentWidth;
                var height = geometry.Height ?? parentHeight;
                result.Add(new DesignerChildTargetInfo
                {
                    ParentItem = designItem,
                    Id = collection.Id,
                    ItemKey = itemKey,
                    IsCollectionItem = true,
                    Left = geometry.Left,
                    Top = geometry.Top,
                    Width = width,
                    Height = height,
                    CanMove = collection.ItemCapabilities.CanMove,
                    CanResize = collection.ItemCapabilities.CanResize
                });
            }
        }

        return result;
    }

    /// <summary>
    /// 返回当前选中子控件（Part/CollectionItem）的目标信息，供 View 绘制 selection adorner 与 resize handles。
    /// 根控件选中或无选中时返回 <see langword="null"/>。
    /// </summary>
    /// <returns>当前子控件目标信息；非子控件选中时为 <see langword="null"/>。</returns>
    public DesignerChildTargetInfo? GetCurrentSubTargetInfo()
    {
        if (_selectedTarget is not { Kind: not FrontedV3DesignSelectionKind.Root } target
            || target.DesignItem is not { } designItem)
        {
            return null;
        }

        var config = designItem.Config;
        var parentBounds = FrontedDesignerBoundsResolver.Resolve(config);
        var parentWidth = parentBounds.Width;
        var parentHeight = parentBounds.Height;
        var geometry = target.GeometryTarget;
        var width = geometry.Width ?? parentWidth;
        var height = geometry.Height ?? parentHeight;

        return target.Kind switch
        {
            FrontedV3DesignSelectionKind.FixedPart when target.SubTarget is FrontedV3FixedPartTarget partTarget
                => new DesignerChildTargetInfo
                {
                    ParentItem = designItem,
                    Id = partTarget.PartId,
                    ItemKey = null,
                    IsCollectionItem = false,
                    Left = geometry.Left,
                    Top = geometry.Top,
                    Width = width,
                    Height = height,
                    CanMove = ResolvePartCapabilities(designItem, partTarget.PartId).CanMove,
                    CanResize = ResolvePartCapabilities(designItem, partTarget.PartId).CanResize
                },
            FrontedV3DesignSelectionKind.CollectionItem
                when target.SubTarget is FrontedV3CollectionItemTarget collectionTarget
                => new DesignerChildTargetInfo
                {
                    ParentItem = designItem,
                    Id = collectionTarget.CollectionId,
                    ItemKey = collectionTarget.ItemKey,
                    IsCollectionItem = true,
                    Left = geometry.Left,
                    Top = geometry.Top,
                    Width = width,
                    Height = height,
                    CanMove = ResolveCollectionItemCapabilities(designItem, collectionTarget.CollectionId).CanMove,
                    CanResize = ResolveCollectionItemCapabilities(designItem, collectionTarget.CollectionId).CanResize
                },
            _ => null
        };
    }

    private FrontedV3PartCapabilities ResolvePartCapabilities(
        FrontedControlDesignItem designItem,
        string partId)
    {
        var part = _selectionBuilder.FindPart(designItem.Config, partId);
        return part?.Capabilities ?? FrontedV3PartCapabilities.None;
    }

    private FrontedV3PartCapabilities ResolveCollectionItemCapabilities(
        FrontedControlDesignItem designItem,
        string collectionId)
    {
        var collection = _selectionBuilder.FindCollection(designItem.Config, collectionId);
        return collection?.ItemCapabilities ?? FrontedV3PartCapabilities.None;
    }

    /// <summary>
    /// 选择多个设计控件，并将其中一个设为属性网格的主目标。
    /// </summary>
    /// <param name="items">要选中的控件。</param>
    /// <param name="primaryItem">主选中控件；省略时使用第一个选中控件。</param>
    /// <summary>
    /// 在一次选择事务中选择多个设计项。
    /// </summary>
    /// <param name="items">要选中的条目。</param>
    /// <param name="primaryItem">用于属性编辑的主条目。</param>
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
    }

    /// <summary>
    /// 从当前多选中添加或移除一个控件。
    /// </summary>
    /// <param name="item">要切换选中状态的控件。</param>
    /// <summary>
    /// 切换多选集合中的一个项目。
    /// </summary>
    /// <param name="item">要切换的条目。</param>
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
    }

    /// <summary>
    /// 选择图层树节点，并在可能时将该选择同步到设计画布。
    /// </summary>
    /// <param name="node">要选中的图层节点。</param>
    public void SelectLayerNode(DesignerLayerNode? node)
    {
        if (node is null || !node.CanSelect)
        {
            ClearSelection();
            return;
        }

        switch (node.Kind)
        {
            case DesignerLayerNodeKind.Control when node.ControlItem is not null:
                SelectDesignItem(node.ControlItem);
                break;
            case DesignerLayerNodeKind.Part when node.ControlItem is not null && node.PartId is not null:
                SelectFixedPart(node.ControlItem, node.PartId);
                break;
            case DesignerLayerNodeKind.CollectionItem
                when node.ControlItem is not null
                && node.CollectionId is not null
                && node.ItemKey is not null:
                SelectCollectionItem(node.ControlItem, node.CollectionId, node.ItemKey);
                break;
            default:
                ClearSelection();
                break;
        }
    }

    /// <summary>
    /// 切换图层树节点的展开状态。
    /// </summary>
    /// <param name="node">要展开或折叠的图层节点。</param>
    public void ToggleLayerNodeExpansion(DesignerLayerNode node)
    {
    }

    /// <summary>
    /// 清除设计画布、图层树、属性和行为选择状态。
    /// </summary>
    public void ClearSelection()
    {
        SelectDesignItem(null);
    }

    /// <summary>
    /// 应用完整选择集合，同时保留用于属性编辑的主项目。
    /// </summary>
    /// <param name="items">应被选中的条目。</param>
    /// <param name="primaryItem">主选中条目。</param>
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
    /// <returns>可移动的选中条目。</returns>
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
    /// 按逻辑增量从拖拽起点移动主选中设计项。
    /// </summary>
    /// <param name="originalLeft">拖动起始左坐标。</param>
    /// <param name="originalTop">拖动起始顶坐标。</param>
    /// <param name="deltaX">水平指针增量。</param>
    /// <param name="deltaY">垂直指针增量。</param>
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

        // 子控件（Part/CollectionItem）选中时，Move 通过 GeometryTarget 执行，
        // 坐标相对于父控件。GeometryTarget 内部遵守 Capabilities 约束（Resize-only 不写入）。
        if (_selectedTarget is { Kind: not FrontedV3DesignSelectionKind.Root } subTarget)
        {
            var geometry = subTarget.GeometryTarget;
            var newLeft = originalLeft + deltaX;
            var newTop = originalTop + deltaY;
            if (EffectiveSnapEnabled)
            {
                newLeft = FrontedDesignerGeometryHelper.Snap(newLeft);
                newTop = FrontedDesignerGeometryHelper.Snap(newTop);
            }

            geometry.MoveTo(newLeft, newTop);
            CurrentDocument.IsDirty = true;
            OnDesignItemGeometryChanged(renderPreview);
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
    /// <param name="originalBounds">拖动开始时捕获的原始边界。</param>
    /// <param name="deltaX">水平指针增量。</param>
    /// <param name="deltaY">垂直指针增量。</param>
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

        var primaryItem = SelectedDesignItem is { } primarySelectedItem && selectedItems.Contains(primarySelectedItem)
            ? primarySelectedItem
            : selectedItems[0];
        if (!originalBounds.TryGetValue(primaryItem, out var primaryBounds))
        {
            return;
        }

        var appliedDeltaX = EffectiveSnapEnabled
            ? FrontedDesignerGeometryHelper.NormalizeCoordinate(
                primaryBounds.Left + deltaX,
                effectiveSnapEnabled: true,
                SnapGridSize) - primaryBounds.Left
            : FrontedDesignerGeometryHelper.Snap(primaryBounds.Left + deltaX) - primaryBounds.Left;
        var appliedDeltaY = EffectiveSnapEnabled
            ? FrontedDesignerGeometryHelper.NormalizeCoordinate(
                primaryBounds.Top + deltaY,
                effectiveSnapEnabled: true,
                SnapGridSize) - primaryBounds.Top
            : FrontedDesignerGeometryHelper.Snap(primaryBounds.Top + deltaY) - primaryBounds.Top;

        foreach (var selectedItem in selectedItems)
        {
            if (!originalBounds.TryGetValue(selectedItem, out var bounds))
            {
                continue;
            }

            // 多选控件必须使用主选中项计算出的同一吸附增量，
            // 不能分别对每个绝对坐标取整，否则会破坏相对位置并造成拖动抖动。
            selectedItem.Config.Left = bounds.Left + appliedDeltaX;
            selectedItem.Config.Top = bounds.Top + appliedDeltaY;
            SyncLinkedOverlays(selectedItem);
        }

        CurrentDocument.IsDirty = true;
        ClearActiveSnapGuides();
        OnDesignItemGeometryChanged(renderPreview);
    }

    /// <summary>
    /// 按增量移动当前选择，通常用于键盘微调。
    /// </summary>
    /// <param name="deltaX">水平增量。</param>
    /// <param name="deltaY">垂直增量。</param>
    public void MoveSelectedDesignItemBy(double deltaX, double deltaY)
    {
        if (CurrentDocument is null || SelectedDesignItem is null)
        {
            return;
        }

        CaptureUndoSnapshot();
        ClearActiveSnapGuides();

        // 子控件选中时，键盘微调通过 GeometryTarget 执行。
        if (_selectedTarget is { Kind: not FrontedV3DesignSelectionKind.Root } subTarget)
        {
            var geometry = subTarget.GeometryTarget;
            geometry.MoveTo(geometry.Left + deltaX, geometry.Top + deltaY);
            CurrentDocument.IsDirty = true;
            OnDesignItemGeometryChanged(renderPreview: false);
            RequestDesignerGeometryPatch([SelectedDesignItem], updateSelection: true);
            return;
        }

        var selectedItems = GetMovableSelectedDesignItems();
        if (selectedItems.Count > 1)
        {
            var primaryItem = SelectedDesignItem is { } primarySelectedItem && selectedItems.Contains(primarySelectedItem)
                ? primarySelectedItem
                : selectedItems[0];
            var appliedDeltaX = EffectiveSnapEnabled
                ? FrontedDesignerGeometryHelper.NormalizeCoordinate(
                    primaryItem.Config.Left + deltaX,
                    effectiveSnapEnabled: true,
                    SnapGridSize) - primaryItem.Config.Left
                : FrontedDesignerGeometryHelper.Snap(primaryItem.Config.Left + deltaX) - primaryItem.Config.Left;
            var appliedDeltaY = EffectiveSnapEnabled
                ? FrontedDesignerGeometryHelper.NormalizeCoordinate(
                    primaryItem.Config.Top + deltaY,
                    effectiveSnapEnabled: true,
                    SnapGridSize) - primaryItem.Config.Top
                : FrontedDesignerGeometryHelper.Snap(primaryItem.Config.Top + deltaY) - primaryItem.Config.Top;
            var batchChangedItems = new List<FrontedControlDesignItem>();
            foreach (var selectedItem in selectedItems)
            {
                selectedItem.Config.Left += appliedDeltaX;
                selectedItem.Config.Top += appliedDeltaY;
                batchChangedItems.Add(selectedItem);
                foreach (var linkedOverlay in SyncLinkedOverlays(selectedItem))
                {
                    if (!batchChangedItems.Contains(linkedOverlay))
                    {
                        batchChangedItems.Add(linkedOverlay);
                    }
                }
            }

            CurrentDocument.IsDirty = true;
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
    /// <param name="handle">正在拖动的调整大小句柄。</param>
    /// <param name="originalLeft">拖动起始左坐标。</param>
    /// <param name="originalTop">拖动起始顶坐标。</param>
    /// <param name="originalWidth">拖动起始宽度。</param>
    /// <param name="originalHeight">拖动起始高度。</param>
    /// <param name="deltaX">水平拖动增量。</param>
    /// <param name="deltaY">垂直拖动增量。</param>
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

        // 子控件（Part/CollectionItem）选中时，Resize 通过 GeometryTarget 执行。
        // 几何值通过 GeometryTarget.ResizeTo 写入，坐标相对于父控件。
        // GeometryTarget 内部遵守 Capabilities 约束（Move-only 不写入尺寸）。
        if (_selectedTarget is { Kind: not FrontedV3DesignSelectionKind.Root } subTarget)
        {
            var geometry = subTarget.GeometryTarget;
            FrontedDesignerGeometryHelper.ComputeResizedBounds(
                handle,
                originalLeft,
                originalTop,
                originalWidth,
                originalHeight,
                deltaX,
                deltaY,
                EffectiveSnapEnabled,
                SnapGridSize,
                out var newLeft,
                out var newTop,
                out var newWidth,
                out var newHeight);

            geometry.ResizeTo(newLeft, newTop, newWidth, newHeight);
            CurrentDocument.IsDirty = true;
            OnDesignItemGeometryChanged(renderPreview);
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
    /// <param name="originalBounds">调整大小开始时捕获的原始边界。</param>
    /// <param name="deltaX">水平指针增量。</param>
    /// <param name="deltaY">垂直指针增量。</param>
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

    public void ClearActiveSnapGuides()
    {
        if (ActiveSnapGuides.Count > 0)
        {
            ActiveSnapGuides = [];
        }
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

        // Schema 驱动路径：子控件选中时，属性编辑通过 PropertyDefinition.Storage 写入，
        // 不通过 propertyName 字符串反射写入。
        if (_schemaPropertiesByPath.TryGetValue(item.PropertyName, out var schemaProperty)
            && _selectedTarget is not null)
        {
            return ApplySchemaPropertyEdit(item, schemaProperty, newValue);
        }

        if (item.PropertyName == nameof(FrontedControlDesignItem.Name))
        {
            return ApplyNameEdit(item, newValue);
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

    /// <summary>
    /// 应用属性编辑，并在资源属性输入超限本地图片时提供压缩选项。
    /// </summary>
    /// <param name="item">要编辑的属性行。</param>
    /// <param name="newValue">待应用的属性值。</param>
    /// <returns>属性已更新时返回 <see langword="true"/>。</returns>
    public async Task<bool> ApplyPropertyEditAsync(FrontedPropertyEditorItem item, object? newValue)
    {
        if (item.CanBrowseResource
            && newValue is string text
            && IsAbsoluteFilePath(text))
        {
            return await ApplyPropertyResourceSelectionAsync(item, text);
        }

        return ApplyPropertyEdit(item, newValue);
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

    /// <summary>
    /// 通过 <see cref="FrontedV3PropertyDefinition.Storage"/> 应用 Schema 属性编辑，
    /// 不通过 propertyName 字符串反射写入。
    /// </summary>
    /// <param name="item">属性行。</param>
    /// <param name="schemaProperty">属性定义。</param>
    /// <param name="newValue">用户输入的新值。</param>
    /// <returns>是否成功提交。</returns>
    private bool ApplySchemaPropertyEdit(
        FrontedPropertyEditorItem item,
        FrontedV3PropertyDefinition schemaProperty,
        object? newValue)
    {
        if (_selectedTarget is null || _selectedTarget.DesignItem is not { } designItem)
        {
            return false;
        }

        var config = designItem.Config;
        object? convertedValue;
        if (schemaProperty.PropertyType == typeof(double)
            || schemaProperty.PropertyType == typeof(double?))
        {
            if (schemaProperty.PropertyType == typeof(double?)
                && newValue is string sizeText
                && string.IsNullOrWhiteSpace(sizeText))
            {
                convertedValue = null;
            }
            else if (!TryConvertSchemaDoubleValue(newValue, out var doubleValue, out var errorMessage))
            {
                SetPropertyEditError(item, errorMessage, newValue);
                return false;
            }
            else
            {
                convertedValue = NormalizeSchemaGeometryValue(item.PropertyName, doubleValue);
            }
        }
        else
        {
            // 复用 FrontedV3ValueConverter 统一转换规则，覆盖 enum、nullable、Color、Brush、
            // JsonElement 与已正确类型的对象；Convert.ChangeType 仅处理 IConvertible 兜底。
            if (!FrontedV3ValueConverter.TryConvert(newValue, schemaProperty.PropertyType, out convertedValue))
            {
                var errorMessage = I18nHelper.GetLocalizedString(
                    AppI18nDictionaries.Designer,
                    "PropertyValidationErrors");
                SetPropertyEditError(item, errorMessage, newValue);
                return false;
            }
        }

        // 多选编辑：通过相同 OptionsPath 和 Schema Storage 写入所有同类型选中控件。
        var editTargets = GetPropertyEditTargets(item.PropertyName);
        var changedTargets = editTargets
            .Where(target => !ValuesEqual(schemaProperty.GetValue(target.Config), convertedValue))
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
            schemaProperty.SetValue(target.Config, convertedValue);
        }

        CurrentDocument.IsDirty = true;

        var refreshedValue = schemaProperty.GetValue(config);
        item.Value = refreshedValue;
        item.EditText = GetCommittedEditText(item, refreshedValue);

        ClearPropertyEditError(item.PropertyName);

        if (IsGeometryProperty(item.PropertyName))
        {
            foreach (var target in changedTargets)
            {
                SyncLinkedOverlays(target);
            }
        }

        FinishPropertyEdit(item.PropertyName);
        OnDesignItemGeometryChanged(renderPreview: true);
        return true;
    }

    /// <summary>
    /// 尝试将用户输入转换为 Schema 几何属性所需的 <see cref="double"/> 值。
    /// </summary>
    /// <param name="newValue">用户输入。</param>
    /// <param name="value">转换后的值。</param>
    /// <param name="errorMessage">转换失败时的错误消息。</param>
    /// <returns>是否转换成功。</returns>
    private static bool TryConvertSchemaDoubleValue(object? newValue, out double value, out string errorMessage)
    {
        value = 0D;
        errorMessage = string.Empty;

        switch (newValue)
        {
            case double d:
                value = d;
                break;
            case IConvertible convertible:
                try
                {
                    value = Convert.ToDouble(convertible, CultureInfo.InvariantCulture);
                }
                catch
                {
                    errorMessage = I18nHelper.GetLocalizedString(
                        AppI18nDictionaries.Designer,
                        "PropertyValidationErrors");
                    return false;
                }

                break;
            default:
                if (!double.TryParse(
                        Convert.ToString(newValue, CultureInfo.InvariantCulture),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out value))
                {
                    errorMessage = I18nHelper.GetLocalizedString(
                        AppI18nDictionaries.Designer,
                        "PropertyValidationErrors");
                    return false;
                }

                break;
        }

        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            errorMessage = I18nHelper.GetLocalizedString(
                AppI18nDictionaries.Designer,
                "PropertyValidationErrors");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 对 Schema 几何属性值进行归一化（对齐网格、最小尺寸约束）。
    /// </summary>
    /// <param name="propertyName">属性名（OptionsPath 末段）。</param>
    /// <param name="value">原始值。</param>
    /// <returns>归一化后的值。</returns>
    private static double NormalizeSchemaGeometryValue(string propertyName, double value)
    {
        if (propertyName is "Width" or "Height")
        {
            return Math.Max(
                FrontedDesignerGeometryHelper.MinResizeWidth,
                FrontedDesignerGeometryHelper.Snap(value));
        }

        if (propertyName is "X" or "Y" or "Left" or "Top")
        {
            return FrontedDesignerGeometryHelper.Snap(value);
        }

        return value;
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
            _ => I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "LayoutSourceError")
        };
        LayoutSourcePath = loadResult.Path
            ?? GetBuiltInPackageLayoutPath(entry.CanonicalWindowId);

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
            _schemaPropertiesByPath.Clear();

            if (CurrentDocument is null || SelectedDesignItem is null)
            {
                return;
            }

            // 子控件（Part/CollectionItem）选中时走 Schema 驱动路径：
            // 属性行由 SelectedTarget.Properties 构造，属性编辑通过 Storage 写入。
            if (_selectedTarget is { Kind: not FrontedV3DesignSelectionKind.Root } subTarget)
            {
                foreach (var row in BuildSchemaPropertyEditorItems(subTarget))
                {
                    PropertyEditorItems.Add(row);
                }

                return;
            }

            // Root 选中时走 Schema 驱动路径：属性网格由 FrontedV3PropertyDefinition 列表构造，
            // 属性值通过 Storage 读写，不通过反射扫描 Config。
            // SelectedTarget 为 null 时（Missing Plugin：Registry 中未注册）同样走 BuildFromSchema，
            // 由 AddMissingPluginRows 显示诊断行，不再回退到旧反射路径扫描 Config 公共属性。
            IEnumerable<FrontedPropertyEditorItem> rows;
            if (_selectedTarget is { Kind: FrontedV3DesignSelectionKind.Root } rootTarget)
            {
                rows = _propertyGridBuilder.BuildFromSchema(
                    CurrentDocument,
                    SelectedDesignItem,
                    _validator,
                    _referenceScanner,
                    rootTarget.Properties,
                    _schemaPropertiesByPath);
            }
            else
            {
                // Missing Plugin：控件未在 Registry 中注册，SelectedTarget 为 null。
                // 仍调用 BuildFromSchema，传入空 Schema，由 AddMissingPluginRows 显示诊断行。
                rows = _propertyGridBuilder.BuildFromSchema(
                    CurrentDocument,
                    SelectedDesignItem,
                    _validator,
                    _referenceScanner,
                    Array.Empty<FrontedV3PropertyDefinition>(),
                    _schemaPropertiesByPath);
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

    /// <summary>
    /// 为子控件选中（Part/CollectionItem）构造 Schema 驱动的属性行。
    /// 属性值通过 <see cref="FrontedV3PropertyDefinition.GetValue"/> 读取，
    /// 编辑时通过 <see cref="FrontedV3PropertyDefinition.SetValue"/> 写入，不通过 propertyName 字符串反射。
    /// </summary>
    /// <param name="selection">子控件选中目标。</param>
    /// <returns>属性行列表。</returns>
    private IEnumerable<FrontedPropertyEditorItem> BuildSchemaPropertyEditorItems(
        FrontedV3DesignSelection selection)
    {
        var config = selection.DesignItem.Config;
        var parentProperties = ResolveParentPropertiesForInheritance(selection);
        var inheritance = selection.Kind == FrontedV3DesignSelectionKind.CollectionItem
            ? ResolveCollectionInheritance(selection)
            : null;

        foreach (var property in selection.Properties)
        {
            _schemaPropertiesByPath[property.OptionsPath] = property;

            var value = ResolvePropertyValueWithInheritance(property, config, parentProperties, inheritance, out var isMissingOverride);
            var displayText = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            var groupName = property.Metadata.GroupName ?? "Layout";
            var editorKind = property.Metadata.EditorKind ?? FrontedPropertyEditorKind.Text;

            var row = new FrontedPropertyEditorItem
            {
                DisplayName = ResolveSchemaPropertyDisplayName(property),
                PropertyName = property.OptionsPath,
                PropertyType = property.PropertyType,
                EditorKind = editorKind,
                Value = value,
                DisplayValue = displayText,
                EditText = displayText,
                GroupName = groupName,
                GroupDisplayName = _localizationService.GetGroupDisplayName(groupName),
                IsGroupHeaderVisible = true,
                // 跟随父控件（无 override）时只读且禁用编辑器，切换为独立设定后允许编辑。
                IsReadOnly = isMissingOverride == true,
                IsEditingDisabled = isMissingOverride == true,
                Options = ResolveSchemaPropertyOptions(property, editorKind),
                // 仅 ParentFallback 继承属性支持"跟随父控件 / 独立设定"切换。
                CanToggleInheritance = isMissingOverride is not null,
                // 选中（true）= 跟随父控件（无 override）；未选中（false）= 独立设定（有 override）。
                IsInheritedFromParent = isMissingOverride == true
            };

            yield return row;
        }
    }

    /// <summary>
    /// 读取子控件属性值，对 ParentFallback 继承属性通过 StyleTransferService 动态回退到父控件值。
    /// </summary>
    private object? ResolvePropertyValueWithInheritance(
        FrontedV3PropertyDefinition property,
        FrontedControlConfigBase config,
        IReadOnlyList<FrontedV3PropertyDefinition>? parentProperties,
        FrontedV3PartCollectionDefinition? inheritance,
        out bool? isMissingOverride)
    {
        var isInherited = property.Metadata.Inheritance == FrontedV3PropertyInheritance.ParentFallback
                          && inheritance is not null;
        var parentProperty = isInherited && parentProperties is not null
            ? FindPropertyByOptionsPath(parentProperties, property.OptionsPath)
            : null;

        if (isInherited && parentProperty is not null)
        {
            var childValue = property.GetValue(config);
            var missing = FrontedV3StyleTransferService.IsOverrideMissing(childValue);
            isMissingOverride = missing;
            return StyleTransferService.ReadValueWithInheritance(
                property, config, config, parentProperty);
        }

        isMissingOverride = null;
        return property.GetValue(config);
    }

    private IReadOnlyList<FrontedV3PropertyDefinition>? ResolveParentPropertiesForInheritance(
        FrontedV3DesignSelection selection)
    {
        if (selection.DesignItem is not { } designItem)
        {
            return null;
        }

        return _selectionBuilder.ResolveRegistration(designItem.Config)?.Properties;
    }

    private FrontedV3PartCollectionDefinition? ResolveCollectionInheritance(
        FrontedV3DesignSelection selection)
    {
        if (selection.SubTarget is not FrontedV3CollectionItemTarget { CollectionId: { } collectionId }
            || selection.DesignItem is null)
        {
            return null;
        }

        return _selectionBuilder.FindCollection(selection.DesignItem.Config, collectionId);
    }

    private static FrontedV3PropertyDefinition? FindPropertyByOptionsPath(
        IReadOnlyList<FrontedV3PropertyDefinition> properties,
        string optionsPath)
    {
        foreach (var property in properties)
        {
            if (string.Equals(property.OptionsPath, optionsPath, StringComparison.Ordinal))
            {
                return property;
            }
        }

        return null;
    }

    /// <summary>
    /// 为 Schema 驱动的子控件属性解析下拉框选项。
    /// 优先使用属性元数据中显式声明的 <see cref="FrontedV3PropertyMetadata.Options"/>；
    /// 否则按 <paramref name="editorKind"/> 生成：
    /// <see cref="FrontedPropertyEditorKind.FontFamily"/> 复用 <see cref="FrontedPropertyGridBuilder"/> 的字体列表；
    /// <see cref="FrontedPropertyEditorKind.Boolean"/> 生成 true/false 选项；
    /// <see cref="FrontedPropertyEditorKind.Enum"/> 从属性类型（解包 <see cref="Nullable{T}"/>）生成枚举值列表。
    /// 其余编辑器类型返回 <see langword="null"/>。
    /// </summary>
    /// <param name="property">属性定义。</param>
    /// <param name="editorKind">属性使用的编辑器类型。</param>
    /// <returns>选项列表；无需选项的编辑器返回 <see langword="null"/>。</returns>
    private IReadOnlyList<object>? ResolveSchemaPropertyOptions(
        FrontedV3PropertyDefinition property,
        FrontedPropertyEditorKind editorKind)
    {
        if (property.Metadata.Options is { } metadataOptions)
        {
            return metadataOptions.Cast<object>().ToArray();
        }

        if (editorKind == FrontedPropertyEditorKind.FontFamily)
        {
            return _propertyGridBuilder.GetFontFamilyOptions();
        }

        if (editorKind == FrontedPropertyEditorKind.Boolean)
        {
            return [CreateSchemaBooleanOption(true), CreateSchemaBooleanOption(false)];
        }

        if (editorKind == FrontedPropertyEditorKind.Enum)
        {
            var enumType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            if (!enumType.IsEnum)
            {
                return null;
            }

            return Enum.GetValues(enumType)
                .Cast<object>()
                .Select(value => CreateSchemaEnumOption(property.OptionsPath, value))
                .Cast<object>()
                .ToArray();
        }

        return null;
    }

    private FrontedPropertyEditorOption CreateSchemaBooleanOption(bool value) =>
        new()
        {
            Value = value,
            DisplayName = _localizationService.GetDesignerText(
                value ? "Designer.Value.True" : "Designer.Value.False",
                value ? "true" : "false")
        };

    private FrontedPropertyEditorOption CreateSchemaEnumOption(string propertyName, object? value) =>
        new()
        {
            Value = value,
            DisplayName = _localizationService.GetOptionDisplayName(propertyName, value)
        };

    /// <summary>
    /// 解析 Schema 属性的显示名称。优先使用本地化键，回退到 OptionsPath 末段。
    /// </summary>
    /// <param name="property">属性定义。</param>
    /// <returns>显示名称。</returns>
    private string ResolveSchemaPropertyDisplayName(FrontedV3PropertyDefinition property)
    {
        var key = property.Metadata.DisplayNameKey;
        if (!string.IsNullOrWhiteSpace(key))
        {
            var localized = _localizationService.GetPropertyDisplayName(key);
            if (!string.IsNullOrWhiteSpace(localized))
            {
                return localized;
            }
        }

        var optionsPath = property.OptionsPath;
        var lastDot = optionsPath.LastIndexOf('.');
        return lastDot >= 0 && lastDot < optionsPath.Length - 1
            ? optionsPath[(lastDot + 1)..]
            : optionsPath;
    }

    private void ApplyMultiSelectionPropertyRowState(FrontedPropertyEditorItem row)
    {
        if (CurrentDocument is null
            || SelectedDesignItem is null
            || SelectedDesignItems.Count <= 1)
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

        // Schema 驱动路径：通过 Schema Storage 检查属性是否可批量编辑，不通过反射。
        if (_schemaPropertiesByPath.TryGetValue(row.PropertyName, out var schemaProperty))
        {
            return SelectedDesignItems.All(item =>
            {
                if (!item.IsEditableInEditor
                    || !item.IsSelectableInEditor
                    || !string.Equals(item.Config.ControlType, controlType, StringComparison.Ordinal))
                {
                    return false;
                }

                return !schemaProperty.Metadata.IsReadOnly
                       && IsBatchEditablePropertyType(schemaProperty.PropertyType);
            });
        }

        // 反射回退（无 Schema 的边缘情况）。
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

        // Schema 驱动路径：通过 Schema Storage 读取值，不通过反射。
        if (_schemaPropertiesByPath.TryGetValue(propertyName, out var schemaProperty))
        {
            foreach (var item in SelectedDesignItems)
            {
                var value = schemaProperty.GetValue(item.Config);
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

        // 反射回退（无 Schema 的边缘情况）。
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

            var config = await _layoutService.LoadWindowConfigAsync(SelectedWindow.WindowTypeName);
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
            var config = await _layoutService.LoadWindowConfigAsync(SelectedWindow.WindowTypeName);
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
            _selectedCatalogEntry?.CanonicalWindowId,
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
        var node = new DesignerLayerNode
        {
            Kind = DesignerLayerNodeKind.Control,
            ControlItem = item,
            CanSelect = item.IsSelectableInEditor,
            CanReorder = IsLayerReorderable(item),
            DisplayName = item.Name,
            Metadata = _localizationService.GetControlTypeDisplayName(item.Config.ControlType),
            ZIndex = item.Config.ZIndex
        };

        AppendChildLayerNodes(node, item);
        return node;
    }

    /// <summary>
    /// 为控件图层节点追加 Part/CollectionItem 子节点，使图层树可展开选中子控件。
    /// </summary>
    /// <param name="parent">父控件节点。</param>
    /// <param name="item">父控件设计项。</param>
    private void AppendChildLayerNodes(DesignerLayerNode parent, FrontedControlDesignItem item)
    {
        foreach (var part in _selectionBuilder.GetAvailableParts(item))
        {
            parent.Children.Add(new DesignerLayerNode
            {
                Kind = DesignerLayerNodeKind.Part,
                ControlItem = item,
                CanSelect = part.Capabilities.CanMove || part.Capabilities.CanResize,
                CanReorder = false,
                DisplayName = part.Id,
                Metadata = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.LayerPanel.Part"),
                ZIndex = item.Config.ZIndex,
                PartId = part.Id
            });
        }

        foreach (var collection in _selectionBuilder.GetAvailableCollections(item))
        {
            var items = collection.CollectionGetter(item.Config);
            foreach (var collectionItem in items)
            {
                var itemKey = collection.ItemKeySelector(collectionItem);
                parent.Children.Add(new DesignerLayerNode
                {
                    Kind = DesignerLayerNodeKind.CollectionItem,
                    ControlItem = item,
                    CanSelect = collection.ItemCapabilities.CanMove || collection.ItemCapabilities.CanResize,
                    CanReorder = false,
                    DisplayName = $"{collection.Id} [{itemKey}]",
                    Metadata = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.LayerPanel.CollectionItem"),
                    ZIndex = item.Config.ZIndex,
                    CollectionId = collection.Id,
                    ItemKey = itemKey
                });
            }
        }
    }

    private void RefreshLayerNodeSelection()
    {
        DesignerLayerNode? selectedNode = null;
        foreach (var node in LayerGroups.SelectMany(group => group.Items))
        {
            var isSelected = IsSelectedLayerNode(node);
            node.IsSelected = isSelected;
            if (isSelected && selectedNode is null)
            {
                selectedNode = node;
            }

            foreach (var child in node.Children)
            {
                var childSelected = IsSelectedLayerNode(child);
                child.IsSelected = childSelected;
                if (childSelected && selectedNode is null)
                {
                    selectedNode = child;
                }
            }
        }

        SelectedLayerNode = selectedNode;
    }

    private bool IsSelectedLayerNode(DesignerLayerNode node)
    {
        return node.Kind switch
        {
            DesignerLayerNodeKind.Control => node.ControlItem is not null && SelectedDesignItems.Contains(node.ControlItem),
            DesignerLayerNodeKind.Part => _selectedTarget is { Kind: FrontedV3DesignSelectionKind.FixedPart } target
                && ReferenceEquals(target.DesignItem, node.ControlItem)
                && target.SubTarget is FrontedV3FixedPartTarget partTarget
                && string.Equals(partTarget.PartId, node.PartId, StringComparison.Ordinal),
            DesignerLayerNodeKind.CollectionItem => _selectedTarget is { Kind: FrontedV3DesignSelectionKind.CollectionItem } target
                && ReferenceEquals(target.DesignItem, node.ControlItem)
                && target.SubTarget is FrontedV3CollectionItemTarget collectionTarget
                && string.Equals(collectionTarget.CollectionId, node.CollectionId, StringComparison.Ordinal)
                && string.Equals(collectionTarget.ItemKey, node.ItemKey, StringComparison.Ordinal),
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
                        WindowId = entry.CanonicalWindowId,
                        WindowTypeName = entry.CanonicalWindowId,
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

    private bool ApplyGeometryPatch(
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

        // Generic Part geometry patch: iterate Part definitions and copy
        // geometry values from target to current via storage accessors.
        // This replaces control-specific branches (e.g. BorderedImage ImageWidth/ImageHeight)
        // with a unified Part-driven approach.
        // 通过 _selectionBuilder.GetParts 走统一 Registry 链路，让插件 Part 也能被回滚时正确补齐几何。
        if (current.GetType() == target.GetType())
        {
            foreach (var part in _selectionBuilder.GetParts(current))
            {
                changed |= PatchPartGeometry(part, current, target);
            }
        }

        return changed;
    }

    private static bool PatchPartGeometry(
        FrontedV3PartDefinition part,
        FrontedControlConfigBase current,
        FrontedControlConfigBase target)
    {
        var changed = false;

        if (part.WidthStorage is not null)
        {
            var targetValue = ToNullableDouble(part.WidthStorage.GetValue(target));
            var currentValue = ToNullableDouble(part.WidthStorage.GetValue(current));
            if (!NullableDoubleEquals(currentValue, targetValue))
            {
                part.WidthStorage.SetValue(current, part.WidthStorage.GetValue(target));
                changed = true;
            }
        }

        if (part.HeightStorage is not null)
        {
            var targetValue = ToNullableDouble(part.HeightStorage.GetValue(target));
            var currentValue = ToNullableDouble(part.HeightStorage.GetValue(current));
            if (!NullableDoubleEquals(currentValue, targetValue))
            {
                part.HeightStorage.SetValue(current, part.HeightStorage.GetValue(target));
                changed = true;
            }
        }

        if (part.XStorage is not null)
        {
            var targetValue = ToNullableDouble(part.XStorage.GetValue(target));
            var currentValue = ToNullableDouble(part.XStorage.GetValue(current));
            if (!NullableDoubleEquals(currentValue, targetValue))
            {
                part.XStorage.SetValue(current, part.XStorage.GetValue(target));
                changed = true;
            }
        }

        if (part.YStorage is not null)
        {
            var targetValue = ToNullableDouble(part.YStorage.GetValue(target));
            var currentValue = ToNullableDouble(part.YStorage.GetValue(current));
            if (!NullableDoubleEquals(currentValue, targetValue))
            {
                part.YStorage.SetValue(current, part.YStorage.GetValue(target));
                changed = true;
            }
        }

        return changed;
    }

    private static double? ToNullableDouble(object? value)
    {
        return value is null ? null : Convert.ToDouble(value, CultureInfo.InvariantCulture);
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
            or "ImageWidth"
            or "ImageHeight")
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
                    entry.CanonicalWindowId,
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
            .FirstOrDefault(item => string.Equals(item.CanonicalWindowId, windowTypeName, StringComparison.Ordinal));
        return entry is null
            ? _localizationService.GetWindowDisplayName(windowTypeName)
            : ResolveEntryDisplayName(entry);
    }

    private string ResolveEntryDisplayName(FrontedDesignerLayoutCatalogEntry entry)
    {
        var settings = _settingsHostService?.Settings;
        var language = settings?.Language ?? LanguageKey.System;
        var cultureInfo = settings?.CultureInfo;

        // entry 不再携带 I18nDisplayNames；按本地化服务回退解析显示名。
        var concreteLanguage = FrontedWindowDisplayNameResolver.ResolveConcreteLanguage(language, cultureInfo);
        if (concreteLanguage.HasValue)
        {
            var localized = _localizationService.GetWindowDisplayName(entry.CanonicalWindowId);
            if (!string.IsNullOrWhiteSpace(localized))
            {
                return localized;
            }
        }

        return entry.DisplayName;
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

/// <summary>
/// Designer 中具名布局模板按钮的视图模型，包装模板 Id 与本地化显示名/描述，
/// 供 <see cref="FrontedDesignerWindowViewModel.LayoutTemplates"/> 绑定渲染。
/// </summary>
public sealed class FrontedV3LayoutTemplateViewModel
{
    /// <summary>
    /// 获取或设置模板唯一标识，作为 <see cref="FrontedV3TemplateContext.TemplateId"/> 传给回调。
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// 获取或设置模板按钮的本地化显示文本。
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// 获取或设置模板按钮的本地化工具提示；无描述时为 <see langword="null"/>。
    /// </summary>
    public string? ToolTip { get; init; }
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
        "ImageWidth",
        "ImageHeight"
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
