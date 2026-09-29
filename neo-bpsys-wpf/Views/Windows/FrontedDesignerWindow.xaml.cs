using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Events;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;
using neo_bpsys_wpf.ViewModels.Windows;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Wpf.Ui.Controls;
using MessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;

namespace neo_bpsys_wpf.Views.Windows;

/// <summary>
/// FrontedDesignerWindow.xaml 的交互逻辑。
/// </summary>
public partial class FrontedDesignerWindow : FluentWindow
{
    private const double LayerDropZoneEdgeSize = 40D;
    // Reserved top/bottom strips let users drop into a new outer layer without reviving overlay drop zones.
    private const double LayerDropZoneStripHeight = 44D;
    private const double LayerAutoScrollMaxVelocity = 18D;
    private readonly IFrontedRenderer? _renderer;
    private readonly IFrontedBehaviorAnimationPartRenderer? _animationPartRenderer;
    private readonly IFilePickerService? _filePickerService;
    private readonly FrontedBindingBrowserProvider? _bindingBrowserProvider;
    private readonly FrontedResourceBrowserProvider? _resourceBrowserProvider;
    private readonly FrontedPackageFontManagerWindowViewModel? _packageFontManagerViewModel;
    private readonly ITutorialRunner? _tutorialRunner;
    private readonly ILogger<FrontedDesignerWindow>? _logger;
    private readonly ISettingsHostService? _settingsHostService;
    private DispatcherTimer? _propertyAutoCommitTimer;
    private FrameworkElement? _pendingAutoCommitEditor;
    private bool _isLoaded;
    private bool _suppressPropertyEditorCommit;
    private FrontedDesignerWindowViewModel? _viewModel;
    private ValidationDetailsWindow? _validationDetailsWindow;
    private FrontedDesignerHelpWindow? _helpWindow;
    private readonly Dictionary<FrontedControlDesignItem, Border> _hitboxes = new();
    private readonly Dictionary<string, FrameworkElement> _previewElementsByControlName = new(StringComparer.Ordinal);
    private readonly Dictionary<FrontedDesignerResizeHandleKind, Border> _resizeHandles = new();
    private readonly Dictionary<int, FrameworkElement> _polygonVertexHandles = new();
    private readonly List<Line> _snapGuideLines = [];
    private readonly List<Border> _multiSelectionOutlines = [];
    private readonly Dictionary<FrontedControlDesignItem, Border> _marqueeSelectionPreviewOutlines = [];
    private readonly Dictionary<FrontedControlDesignItem, long> _marqueeSelectionEntryOrders = [];
    private readonly Dictionary<FrontedControlDesignItem, FrontedDesignerResolvedBounds> _originalSelectedBounds = new();
    private Border? _selectionOutline;
    private Border? _parentSelectionOutline;
    private Border? _selectionLabel;
    private Border? _marqueeSelectionOutline;
    private FrontedControlDesignItem? _lastMarqueeEnteredItem;
    private long _marqueeSelectionEntrySequence;
    private FrameworkElement? _capturedElement;
    private InteractionMode _interactionMode = InteractionMode.None;
    private FrontedDesignerResizeHandleKind? _activeResizeHandle;
    private int? _activePolygonVertexIndex;
    private FrontedControlDesignItem? _pendingHitCandidate;
    private bool _isPendingEmptyClick;
    private bool _hasExceededClickThreshold;
    private bool _hasStartedDrag;
    private Point _startMousePosition;
    private double _originalLeft;
    private double _originalTop;
    private double _originalWidth;
    private double _originalHeight;
    private bool _isPanningViewport;
    private Point _panStartViewportPosition;
    private double _panStartTranslationX;
    private double _panStartTranslationY;
    private Cursor? _cursorBeforePan;
    private bool _selectorReloadScheduled;
    private bool _selectorReloadInProgress;
    private bool _selectorReloadRequested;
    private bool _previewRenderScheduled;
    private bool _suppressSelectorReload;
    private bool _forceCloseAfterDirtyPrompt;
    private bool _isDirtyClosePromptOpen;
    private FrontedDesignerWindowOption? _lastAcceptedWindow;
    private Point _layerDragStartPoint;
    private DesignerLayerNode? _pendingLayerDragNode;
    private DesignerLayerNode? _activeLayerDragNode;
    private readonly DispatcherTimer _layerAutoScrollTimer;
    private double _layerAutoScrollVelocity;
    private Point? _lastLayerDragPosition;
    private FrontedDesignerPreviewRenderRequestedEventArgs? _pendingPreviewRenderArgs;
    private readonly CancellationTokenSource _tutorialLifetime = new();
    private readonly TaskCompletionSource _initialPreviewReady =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private FrontedControlDesignItem? _lastSeenSelectedDesignItem;
    private TaskCompletionSource? _propertyGridReady;
    private Task<TutorialRunResult>? _designerTutorialTask;
    private bool _propertyPanelTutorialTriggered;
    private Task<TutorialRunResult>? _propertyPanelTutorialTask;
    private Task<TutorialRunResult>? _behaviorPanelTutorialTask;
    private bool _initialLayoutLoaded;
    private int _userSelectionDepth;
    private readonly Dictionary<FrontedDesignerResizeHandleKind, Border> _childResizeHandles = new();
    private Border? _childSelectionOutline;
    private Border? _childSelectionLabel;
    private DesignerChildTargetInfo? _currentSubTargetInfo;

    public FrontedDesignerWindow()
    {
        InitializeComponent();
        _layerAutoScrollTimer = CreateLayerAutoScrollTimer();
        InitializePropertyAutoCommitTimer();
    }

    public FrontedDesignerWindow(
        FrontedDesignerWindowViewModel viewModel,
        IFrontedRenderer renderer,
        IFrontedBehaviorAnimationPartRenderer animationPartRenderer,
        IFilePickerService filePickerService,
        FrontedBindingBrowserProvider bindingBrowserProvider,
        FrontedResourceBrowserProvider resourceBrowserProvider,
        FrontedPackageFontManagerWindowViewModel packageFontManagerViewModel,
        ITutorialRunner tutorialRunner,
        ILogger<FrontedDesignerWindow> logger,
        ISettingsHostService settingsHostService)
    {
        _renderer = renderer;
        _animationPartRenderer = animationPartRenderer;
        _filePickerService = filePickerService;
        _bindingBrowserProvider = bindingBrowserProvider;
        _resourceBrowserProvider = resourceBrowserProvider;
        _packageFontManagerViewModel = packageFontManagerViewModel;
        _tutorialRunner = tutorialRunner;
        _logger = logger;
        _settingsHostService = settingsHostService;

        InitializeComponent();
        _layerAutoScrollTimer = CreateLayerAutoScrollTimer();
        InitializePropertyAutoCommitTimer();
        DataContext = viewModel;
        Loaded += OnLoaded;
        Closed += OnClosed;
        Closing += OnClosing;
        Deactivated += OnDeactivated;
        StateChanged += OnWindowStateChanged;
        settingsHostService.LanguageSettingChanged += OnLanguageSettingChanged;
    }

    private void OnLanguageSettingChanged(object? sender, LanguageChangedEventArgs e)
    {
        if (_viewModel is { } viewModel)
        {
            // Refresh behavior panel localization on a layout-managed dispatcher
            // to avoid re-entrancy with the language-setting change propagation.
            _ = Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() =>
                {
                    _suppressSelectorReload = true;
                    try
                    {
                        viewModel.RefreshWindowDisplayNames();
                        _lastAcceptedWindow = viewModel.SelectedWindow;
                    }
                    finally
                    {
                        _suppressSelectorReload = false;
                    }

                    viewModel.BehaviorPanel.RefreshLocalization();
                }));
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var token = _tutorialLifetime.Token;
        try
        {
            _isLoaded = true;
            _logger?.LogInformation("Designer loaded.");
            TutorialSignalPublisher.Publish(TutorialSignalIds.DesignerV3Opened);
            AttachViewModel();
            await RefreshWindowCatalogAsync();
            await LoadInitialLayoutAsync();
            _logger?.LogInformation("Initial layout loaded.");
            QueueDesignerTutorial();
            await _designerTutorialTask!;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
    }

    /// <summary>
    /// 重新读取当前注册表中的窗口目录和布局显示名称，并保持窗口选择器状态一致。
    /// </summary>
    /// <param name="reloadSelectedLayout">是否在目录刷新后从当前活动包重新加载所选布局。</param>
    /// <returns>目录刷新完成后结束的任务。</returns>
    public async Task RefreshWindowCatalogAsync(bool reloadSelectedLayout = false)
    {
        if (_viewModel is null)
        {
            return;
        }

        _suppressSelectorReload = true;
        try
        {
            await _viewModel.RefreshWindowDisplayNamesAsync();
            _lastAcceptedWindow = _viewModel.SelectedWindow;
            if (reloadSelectedLayout)
            {
                await _viewModel.ReloadLayoutCoreAsync();
            }
        }
        finally
        {
            _suppressSelectorReload = false;
        }
    }

    /// <summary>
    /// 在活动布局包即将改变前处理设计器中尚未保存的修改。
    /// </summary>
    /// <returns>可以继续切换包时为 <see langword="true"/>；用户取消时为 <see langword="false"/>。</returns>
    public Task<bool> PrepareForPackageChangeAsync()
    {
        return ConfirmDirtyDocumentCanContinueAsync("SaveBeforeSwitch");
    }

    /// <summary>
    /// 在窗口布局即将删除前，仅当设计器正在编辑该窗口时处理尚未保存的修改。
    /// </summary>
    /// <param name="canonicalWindowId">即将删除的窗口 Canonical ID。</param>
    /// <returns>可以继续删除时为 <see langword="true"/>；用户取消时为 <see langword="false"/>。</returns>
    public Task<bool> PrepareForWindowRemovalAsync(string canonicalWindowId)
    {
        if (_viewModel?.SelectedWindow?.WindowTypeName is not { } selectedWindowId
            || !string.Equals(selectedWindowId, canonicalWindowId, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(true);
        }

        return ConfirmDirtyDocumentCanContinueAsync("SaveBeforeSwitch");
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        // Unsubscribe from the Singleton ISettingsHostService first so a closed window
        // is never retained by the long-lived language-setting event. This must happen
        // even if subsequent cleanup throws.
        if (_settingsHostService is not null)
        {
            _settingsHostService.LanguageSettingChanged -= OnLanguageSettingChanged;
        }

        _isLoaded = false;
        _tutorialLifetime.Cancel();
        _pendingPreviewRenderArgs = null;
        _previewRenderScheduled = false;
        _propertyAutoCommitTimer?.Stop();
        _pendingAutoCommitEditor = null;
        HideLayerDragGhost();
        StopLayerAutoScroll();
        if (_viewModel is not null)
        {
            _viewModel.PreviewRenderRequested -= OnPreviewRenderRequested;
            _viewModel.DesignerGeometryPatchRequested -= OnDesignerGeometryPatchRequested;
            _viewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
            _viewModel.PropertyEditorItems.CollectionChanged -= PropertyEditorItems_OnCollectionChanged;
        }

        CloseValidationDetailsWindowSafely();
        _validationDetailsWindow = null;
        CloseHelpWindowSafely();
        _helpWindow = null;
        _propertyPanelTutorialTriggered = false;
        _propertyPanelTutorialTask = null;
        _behaviorPanelTutorialTask = null;
        _designerTutorialTask = null;
        _propertyGridReady?.TrySetCanceled();
        _propertyGridReady = null;
        _lastSeenSelectedDesignItem = null;
    }

}
