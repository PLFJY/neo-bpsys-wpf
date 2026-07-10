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
/// Interaction logic for FrontedDesignerWindow.xaml.
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
    private DispatcherTimer? _propertyAutoCommitTimer;
    private FrameworkElement? _pendingAutoCommitEditor;
    private bool _isLoaded;
    private bool _suppressPropertyEditorCommit;
    private FrontedDesignerWindowViewModel? _viewModel;
    private ValidationDetailsWindow? _validationDetailsWindow;
    private FrontedDesignerHelpWindow? _helpWindow;
    private readonly Dictionary<FrontedControlDesignItem, Border> _hitboxes = new();
    private readonly Dictionary<GlobalScoreCellHitTarget, Border> _globalScoreCellHitboxes = new();
    private readonly Dictionary<string, FrameworkElement> _previewElementsByControlName = new(StringComparer.Ordinal);
    private readonly Dictionary<FrontedDesignerResizeHandleKind, Border> _resizeHandles = new();
    private readonly Dictionary<int, FrameworkElement> _polygonVertexHandles = new();
    private readonly List<Line> _snapGuideLines = [];
    private readonly List<Border> _multiSelectionOutlines = [];
    private readonly Dictionary<FrontedControlDesignItem, FrontedDesignerResolvedBounds> _originalSelectedBounds = new();
    private Border? _selectionOutline;
    private Border? _parentSelectionOutline;
    private Border? _selectionLabel;
    private Border? _marqueeSelectionOutline;
    private FrameworkElement? _capturedElement;
    private InteractionMode _interactionMode = InteractionMode.None;
    private FrontedDesignerResizeHandleKind? _activeResizeHandle;
    private int? _activePolygonVertexIndex;
    private FrontedControlDesignItem? _pendingHitCandidate;
    private GlobalScoreCellHitTarget? _pendingGlobalScoreCellHitCandidate;
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
    private double _panStartHorizontalOffset;
    private double _panStartVerticalOffset;
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
    private FrontedControlDesignItem? _lastSeenSelectedDesignItem;
    private bool _propertyPanelTutorialTriggered;
    private Task<TutorialRunResult>? _propertyPanelTutorialTask;

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
        try
        {
            _isLoaded = true;
            TutorialSignalPublisher.Publish(TutorialSignalIds.DesignerV3Opened);
            AttachViewModel();
            await LoadInitialLayoutAsync();
            await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.ContextIdle, _tutorialLifetime.Token);
            var runner = _tutorialRunner
                ?? IAppHost.Host?.Services.GetService(typeof(ITutorialRunner)) as ITutorialRunner;
            if (runner != null)
            {
                await runner.RunSequenceAsync(this, TutorialPageKeys.DesignerV3, _tutorialLifetime.Token);
            }
        }
        catch (OperationCanceledException) when (_tutorialLifetime.IsCancellationRequested)
        {
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
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
        _lastSeenSelectedDesignItem = null;
    }

    private void TryQueuePropertyPanelTutorial()
    {
        if (_propertyPanelTutorialTriggered)
        {
            return;
        }

        if (_propertyPanelTutorialTask is { IsCompleted: false })
        {
            return;
        }

        _propertyPanelTutorialTriggered = true;
        _propertyPanelTutorialTask = RunPropertyPanelTutorialAsync();
    }

    private async Task<TutorialRunResult> RunPropertyPanelTutorialAsync()
    {
        try
        {
            await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.ContextIdle, _tutorialLifetime.Token);
            await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Render, _tutorialLifetime.Token);
            var runner = _tutorialRunner
                ?? IAppHost.Host?.Services.GetService(typeof(ITutorialRunner)) as ITutorialRunner;
            if (runner == null)
            {
                return TutorialRunResult.NotReady;
            }

            return await runner.RunPackageAsync(this, Tours.PropertyPanelBasic, _tutorialLifetime.Token);
        }
        catch (OperationCanceledException) when (_tutorialLifetime.IsCancellationRequested)
        {
            return TutorialRunResult.Canceled;
        }
    }

    private async Task LoadInitialLayoutAsync()
    {
        if (_viewModel is null)
        {
            return;
        }

        await _viewModel.ReloadLayoutCoreAsync();
        _lastAcceptedWindow = _viewModel.SelectedWindow;
    }

    private void Selector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded || _suppressSelectorReload)
        {
            return;
        }

        ScheduleSelectorReload();
    }

    private void ScheduleSelectorReload()
    {
        if (_selectorReloadInProgress)
        {
            _selectorReloadRequested = true;
            return;
        }

        if (_selectorReloadScheduled)
        {
            return;
        }

        _selectorReloadScheduled = true;
        Dispatcher.BeginInvoke(
            new Action(async () => await HandleScheduledSelectorReloadAsync()),
            DispatcherPriority.Background);
    }

    private async Task HandleScheduledSelectorReloadAsync()
    {
        _selectorReloadScheduled = false;
        if (_viewModel is null)
        {
            return;
        }

        if (_selectorReloadInProgress)
        {
            _selectorReloadRequested = true;
            return;
        }

        _selectorReloadInProgress = true;
        try
        {
            do
            {
                _selectorReloadRequested = false;
                if (ReferenceEquals(_lastAcceptedWindow, _viewModel.SelectedWindow))
                {
                    continue;
                }

                if (!await ConfirmDirtyDocumentCanContinueAsync("SaveBeforeSwitch"))
                {
                    RestoreAcceptedSelection();
                    return;
                }

                var loadingWindow = _viewModel.SelectedWindow;
                await _viewModel.ReloadLayoutCoreAsync();
                _lastAcceptedWindow = loadingWindow;
            }
            while (_selectorReloadRequested);
        }
        finally
        {
            _selectorReloadInProgress = false;
        }
    }

    private void RestoreAcceptedSelection()
    {
        if (_viewModel is null)
        {
            return;
        }

        _suppressSelectorReload = true;
        try
        {
            _viewModel.SelectedWindow = _lastAcceptedWindow;
        }
        finally
        {
            _suppressSelectorReload = false;
        }
    }

    private void ControlList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0 || _viewModel is null)
        {
            return;
        }

        if (e.AddedItems[0] is FrontedControlDesignItem item)
        {
            _viewModel.SelectDesignItem(item);
        }
    }

    private void ControlListItem_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: FrontedControlDesignItem item })
        {
            _viewModel?.SelectDesignItem(item);
        }
    }

    private DispatcherTimer CreateLayerAutoScrollTimer()
    {
        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(24)
        };
        timer.Tick += (_, _) =>
        {
            if (Math.Abs(_layerAutoScrollVelocity) < 0.01D)
            {
                return;
            }

            LayerPanelScrollViewer.ScrollToVerticalOffset(
                LayerPanelScrollViewer.VerticalOffset + _layerAutoScrollVelocity);
            UpdateLayerDropZoneVisibility();
        };
        return timer;
    }

    private void LayerItem_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: DesignerLayerNode node })
        {
            return;
        }

        _viewModel?.SelectLayerNode(node);
        _pendingLayerDragNode = node.CanReorder ? node : null;
        _layerDragStartPoint = e.GetPosition(this);
    }

    private void LayerItem_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed
            || _viewModel is null
            || _pendingLayerDragNode is null
            || sender is not FrameworkElement dragSource)
        {
            return;
        }

        var currentPosition = e.GetPosition(this);
        if (Math.Abs(currentPosition.X - _layerDragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(currentPosition.Y - _layerDragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        if (!_viewModel.CanReorderLayers
            || !_pendingLayerDragNode.CanReorder
            || _pendingLayerDragNode.ControlItem is null
            || !_viewModel.IsLayerReorderable(_pendingLayerDragNode.ControlItem))
        {
            _viewModel.StatusMessage = I18nHelper.GetLocalizedString("Designer.LayerPanel.ReorderBlocked");
            _pendingLayerDragNode = null;
            return;
        }

        _activeLayerDragNode = _pendingLayerDragNode;
        _pendingLayerDragNode = null;
        var data = new DataObject(typeof(FrontedControlDesignItem), _activeLayerDragNode.ControlItem);
        ShowLayerDragGhost(_activeLayerDragNode!, e.GetPosition(LayerPanelHostGrid));
        try
        {
            DragDrop.DoDragDrop(dragSource, data, DragDropEffects.Move);
        }
        finally
        {
            _activeLayerDragNode = null;
            HideLayerDragGhost();
            StopLayerAutoScroll();
            HideLayerDropZones();
        }
    }

    private void LayerItem_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DesignerLayerNode node })
        {
            _viewModel?.SelectLayerNode(node);
        }
    }

    private void LayerControlDeleteMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        var contextMenu = (sender as System.Windows.Controls.MenuItem)?.Parent as ContextMenu;
        if (contextMenu?.PlacementTarget is FrameworkElement
            {
                DataContext: DesignerLayerNode
                {
                    Kind: DesignerLayerNodeKind.Control,
                    ControlItem: { } item
                }
            })
        {
            _viewModel?.SelectDesignItem(item);
            _viewModel?.DeleteSelectedControlCommand.Execute(null);
        }
    }

    private void LayerNodeChevron_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: DesignerLayerNode node })
        {
            _viewModel?.ToggleLayerNodeExpansion(node);
            e.Handled = true;
        }
    }

    private void LayerItem_OnDrop(object sender, DragEventArgs e)
    {
        if (_viewModel is null
            || sender is not FrameworkElement
            {
                DataContext: DesignerLayerNode
                {
                    Kind: DesignerLayerNodeKind.Control,
                    ControlItem: { } targetItem
                } targetNode
            }
            || !TryGetLayerDragItem(e, out var source))
        {
            return;
        }

        var position = e.GetPosition((IInputElement)sender);
        var insertAfter = sender is FrameworkElement element && position.Y > element.ActualHeight / 2D;
        _viewModel.CommitLayerDrop(source, targetItem.Config.ZIndex, targetItem, insertAfter);
        StopLayerDrag(e);
    }

    private void LayerItem_OnDragOver(object sender, DragEventArgs e)
    {
        UpdateLayerDragOver(e);
    }

    private void LayerGroup_OnDragOver(object sender, DragEventArgs e)
    {
        UpdateLayerDragOver(e);
    }

    private void LayerGroup_OnDrop(object sender, DragEventArgs e)
    {
        if (_viewModel is null
            || sender is not FrameworkElement { DataContext: FrontedLayerGroup group }
            || !TryGetLayerDragItem(e, out var source))
        {
            return;
        }

        _viewModel.CommitLayerDrop(source, group.ZIndex, null, insertAfter: true);
        StopLayerDrag(e);
    }

    private void LayerPanel_OnDragOver(object sender, DragEventArgs e)
    {
        UpdateLayerDragOver(e);
    }

    private void LayerPanel_OnDrop(object sender, DragEventArgs e)
    {
        if (_viewModel is not null
            && TryGetLayerDragItem(e, out var source)
            && _viewModel.CanReorderLayers
            && _viewModel.IsLayerReorderable(source))
        {
            var position = e.GetPosition(LayerPanelScrollViewer);
            var isTop = position.Y <= LayerDropZoneEdgeSize
                        && LayerPanelScrollViewer.VerticalOffset <= 0.1D;
            var isBottom = position.Y >= LayerPanelScrollViewer.ViewportHeight - LayerDropZoneEdgeSize
                           && LayerPanelScrollViewer.VerticalOffset >= LayerPanelScrollViewer.ScrollableHeight - 0.1D;

            if (isTop)
            {
                _viewModel.CommitLayerDrop(source, null, null, insertAfter: false, moveToNewTopLayer: true);
            }
            else if (isBottom)
            {
                _viewModel.CommitLayerDrop(source, null, null, insertAfter: true, moveToNewBottomLayer: true);
            }
        }

        StopLayerDrag(e);
    }

    private void LayerPanel_OnDragLeave(object sender, DragEventArgs e)
    {
        // WPF DragDrop can raise transient DragLeave while moving between nested layer elements.
        // Cleanup is handled by Drop and the DoDragDrop finally block to avoid flicker loops.
        e.Handled = true;
    }

    private void LayerTopDropZone_OnDragOver(object sender, DragEventArgs e)
    {
        UpdateLayerDragOver(e);
    }

    private void LayerTopDropZone_OnDrop(object sender, DragEventArgs e)
    {
        if (_viewModel is not null && TryGetLayerDragItem(e, out var source))
        {
            _viewModel.CommitLayerDrop(source, null, null, insertAfter: false, moveToNewTopLayer: true);
        }

        StopLayerDrag(e);
    }

    private void LayerBottomDropZone_OnDragOver(object sender, DragEventArgs e)
    {
        UpdateLayerDragOver(e);
    }

    private void LayerBottomDropZone_OnDrop(object sender, DragEventArgs e)
    {
        if (_viewModel is not null && TryGetLayerDragItem(e, out var source))
        {
            _viewModel.CommitLayerDrop(source, null, null, insertAfter: true, moveToNewBottomLayer: true);
        }

        StopLayerDrag(e);
    }

    private void UpdateLayerDragOver(DragEventArgs e)
    {
        if (_viewModel is null
            || !_viewModel.CanReorderLayers
            || !TryGetLayerDragItem(e, out var source)
            || !_viewModel.IsLayerReorderable(source))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.Move;
        // DragOver is preview-only: mutating the document here would reorder layers while the pointer is still exploring.
        UpdateLayerAutoScroll(e.GetPosition(LayerPanelScrollViewer));
        UpdateLayerDragGhost(e.GetPosition(LayerPanelHostGrid));
        e.Handled = true;
    }

    private void UpdateLayerAutoScroll(Point position)
    {
        _lastLayerDragPosition = position;
        _layerAutoScrollVelocity = 0D;

        if (position.Y < LayerDropZoneEdgeSize)
        {
            _layerAutoScrollVelocity = -LayerAutoScrollMaxVelocity
                                       * (1D - Math.Max(0D, position.Y) / LayerDropZoneEdgeSize);
        }
        else if (position.Y > LayerPanelScrollViewer.ViewportHeight - LayerDropZoneEdgeSize)
        {
            var distance = Math.Max(0D, LayerPanelScrollViewer.ViewportHeight - position.Y);
            _layerAutoScrollVelocity = LayerAutoScrollMaxVelocity
                                       * (1D - Math.Min(LayerDropZoneEdgeSize, distance) / LayerDropZoneEdgeSize);
        }

        if (Math.Abs(_layerAutoScrollVelocity) > 0.01D && !_layerAutoScrollTimer.IsEnabled)
        {
            _layerAutoScrollTimer.Start();
        }
        else
        {
            StopLayerAutoScroll();
        }

        UpdateLayerDropZoneVisibility();
    }

    private void UpdateLayerDropZoneVisibility()
    {
        var isDragging = _activeLayerDragNode is not null;
        var hasPointer = _lastLayerDragPosition is { } pointer;
        var showTop = isDragging
                      && hasPointer
                      && pointer.Y <= LayerDropZoneEdgeSize
                      && LayerPanelScrollViewer.VerticalOffset <= 0.1D;
        var bottomEdgeTolerance = LayerBottomDropZone.Visibility == Visibility.Visible
            ? LayerDropZoneStripHeight + 0.1D
            : 0.1D;
        var showBottom = isDragging
                         && hasPointer
                         && pointer.Y >= LayerPanelScrollViewer.ViewportHeight - LayerDropZoneEdgeSize
                         && LayerPanelScrollViewer.VerticalOffset >= LayerPanelScrollViewer.ScrollableHeight - bottomEdgeTolerance;

        SetDropZoneVisibility(LayerTopDropZone, LayerTopDropZoneRow, showTop);
        SetDropZoneVisibility(LayerBottomDropZone, LayerBottomDropZoneRow, showBottom);
    }

    private void StopLayerDrag(DragEventArgs e)
    {
        HideLayerDragGhost();
        StopLayerAutoScroll();
        HideLayerDropZones();
        e.Handled = true;
    }

    private void ShowLayerDragGhost(DesignerLayerNode node, Point panelPosition)
    {
        LayerDragGhostNameText.Text = node.DisplayName;
        LayerDragGhostMetaText.Text = $"{node.Metadata}  {I18nHelper.GetLocalizedString("ZIndexShort")} {node.ZIndex}";
        LayerDragGhost.Visibility = Visibility.Visible;
        UpdateLayerDragGhost(panelPosition);
    }

    private void UpdateLayerDragGhost(Point panelPosition)
    {
        if (LayerDragGhost.Visibility != Visibility.Visible)
        {
            return;
        }

        LayerDragGhost.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var ghostWidth = LayerDragGhost.DesiredSize.Width;
        var ghostHeight = LayerDragGhost.DesiredSize.Height;

        var x = panelPosition.X - ghostWidth / 2D;
        var y = panelPosition.Y - ghostHeight / 2D;

        if (LayerPanelHostGrid.ActualWidth > 0D && ghostWidth > 0D)
        {
            x = Math.Clamp(x, 0D, Math.Max(0D, LayerPanelHostGrid.ActualWidth - ghostWidth));
        }

        if (LayerPanelHostGrid.ActualHeight > 0D && ghostHeight > 0D)
        {
            y = Math.Clamp(y, 0D, Math.Max(0D, LayerPanelHostGrid.ActualHeight - ghostHeight));
        }

        LayerDragGhostTransform.X = x;
        LayerDragGhostTransform.Y = y;
    }

    private void HideLayerDragGhost()
    {
        LayerDragGhost.Visibility = Visibility.Collapsed;
        LayerDragGhostTransform.X = 0D;
        LayerDragGhostTransform.Y = 0D;
        LayerDragGhostNameText.Text = string.Empty;
        LayerDragGhostMetaText.Text = string.Empty;
    }

    private static string GetControlTypeDisplay(string? controlType)
    {
        if (string.IsNullOrWhiteSpace(controlType))
        {
            return string.Empty;
        }

        var key = $"Designer.ControlType.{controlType}";
        var localized = I18nHelper.GetLocalizedString(key);
        return string.Equals(localized, key, StringComparison.Ordinal) ? controlType : localized;
    }

    private void StopLayerAutoScroll()
    {
        _layerAutoScrollVelocity = 0D;
        _layerAutoScrollTimer.Stop();
    }

    private void HideLayerDropZones()
    {
        _lastLayerDragPosition = null;
        SetDropZoneVisibility(LayerTopDropZone, LayerTopDropZoneRow, false);
        SetDropZoneVisibility(LayerBottomDropZone, LayerBottomDropZoneRow, false);
    }

    private static void SetDropZoneVisibility(Border zone, RowDefinition row, bool visible)
    {
        var desiredVisibility = visible ? Visibility.Visible : Visibility.Hidden;
        if (zone.Visibility != desiredVisibility)
        {
            zone.Visibility = desiredVisibility;
        }

        // Rows collapse to zero when idle so the ScrollViewer keeps the full layer panel height.
        var desiredHeight = visible
            ? new GridLength(LayerDropZoneStripHeight)
            : new GridLength(0D);
        if (!GridLengthEquals(row.Height, desiredHeight))
        {
            row.Height = desiredHeight;
        }
    }

    private static bool GridLengthEquals(GridLength first, GridLength second)
    {
        return first.GridUnitType == second.GridUnitType
               && Math.Abs(first.Value - second.Value) < 0.01D;
    }

    private static bool TryGetLayerDragItem(DragEventArgs e, out FrontedControlDesignItem item)
    {
        item = null!;
        if (!e.Data.GetDataPresent(typeof(FrontedControlDesignItem)))
        {
            return false;
        }

        item = (FrontedControlDesignItem)e.Data.GetData(typeof(FrontedControlDesignItem))!;
        return item is not null;
    }

    private void Window_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        UpdateShiftSnapState();

        if (_viewModel is null || ShouldIgnoreKeyboardInput())
        {
            return;
        }

        var isControl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        var isShift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        if (!isControl)
        {
            return;
        }

        if (e.Key == Key.S && !isShift)
        {
            if (_viewModel.CanSaveLayout)
            {
                _viewModel.SaveLayoutCommand.Execute(null);
            }

            e.Handled = true;
        }
        else if (e.Key == Key.Z && !isShift)
        {
            _viewModel.UndoCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Y || (e.Key == Key.Z && isShift))
        {
            _viewModel.RedoCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.C && !isShift)
        {
            _viewModel.CopySelectedControlCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.V && !isShift)
        {
            _viewModel.PasteControlCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void PropertyTextBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        var committed = ApplyPropertyEditorValue(sender);
        if (committed)
        {
            FocusDesignSurface();
        }

        e.Handled = true;
    }

    private void PropertyTextApplyButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (ApplyPropertyEditorValue(sender))
        {
            FocusDesignSurface();
        }
    }

    private void PropertyTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not FrameworkElement editor || !ShouldAutoCommitPropertyEditor(editor))
        {
            return;
        }

        SchedulePropertyAutoCommit(editor);
    }

    private async void WindowBackgroundColorTextBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        if (_viewModel is not null
            && await _viewModel.ApplyWindowBackgroundColorEditAsync())
        {
            FocusDesignSurface();
        }

        e.Handled = true;
    }

    private async void WindowBackgroundColorApplyButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null
            && await _viewModel.ApplyWindowBackgroundColorEditAsync())
        {
            FocusDesignSurface();
        }
    }

    private void Window_OnPreviewKeyUp(object sender, KeyEventArgs e)
    {
        UpdateShiftSnapState();
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        _viewModel?.UpdateShiftSnapActive(false);
        _viewModel?.ClearActiveSnapGuides();
    }

    private void BrowseBindingButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_bindingBrowserProvider is null
            || sender is not FrameworkElement { DataContext: FrontedPropertyEditorItem item })
        {
            return;
        }

        var viewModel = new FrontedBindingBrowserWindowViewModel(
            _bindingBrowserProvider,
            new FrontedBindingTypeFilter(item.BindingTargetKind));
        var window = new FrontedBindingBrowserWindow
        {
            Owner = this,
            DataContext = viewModel
        };
        window.InitializeSelection(item.EditText);

        if (window.ShowDialog() == true && !string.IsNullOrWhiteSpace(window.SelectedBindingPath))
        {
            item.EditText = window.SelectedBindingPath;
            _viewModel?.ClearPropertyEditErrorForBufferUpdate(item.PropertyName);
        }
    }

    private void BrowseResourceButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_resourceBrowserProvider is null
            || sender is not FrameworkElement { DataContext: FrontedPropertyEditorItem item })
        {
            return;
        }

        var viewModel = new FrontedResourceBrowserWindowViewModel(_resourceBrowserProvider);
        var window = new FrontedResourceBrowserWindow
        {
            Owner = this,
            DataContext = viewModel
        };
        window.InitializeSelection(item.EditText);

        if (window.ShowDialog() == true && !string.IsNullOrWhiteSpace(window.SelectedResourcePath))
        {
            item.EditText = window.SelectedResourcePath;
            _viewModel?.ApplyPropertyResourceSelection(item, window.SelectedResourcePath);
            FocusDesignSurface();
        }
    }

    private void BrowseCanvasBackgroundResourceButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_resourceBrowserProvider is null || _viewModel is null)
        {
            return;
        }

        var viewModel = new FrontedResourceBrowserWindowViewModel(_resourceBrowserProvider);
        var window = new FrontedResourceBrowserWindow
        {
            Owner = this,
            DataContext = viewModel
        };
        window.InitializeSelection(_viewModel.BackgroundImageEditText);

        if (window.ShowDialog() == true && !string.IsNullOrWhiteSpace(window.SelectedResourcePath))
        {
            _viewModel.ApplyCanvasBackgroundResourceSelection(window.SelectedResourcePath);
            FocusDesignSurface();
        }
    }

    private void BrowseAnimationPartImageResourceButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_resourceBrowserProvider is null
            || _viewModel?.AnimationPartEditBuffer is not { IsImage: true } editor)
        {
            return;
        }

        var viewModel = new FrontedResourceBrowserWindowViewModel(_resourceBrowserProvider);
        var window = new FrontedResourceBrowserWindow
        {
            Owner = this,
            DataContext = viewModel
        };
        window.InitializeSelection(editor.ImagePath);

        if (window.ShowDialog() == true && !string.IsNullOrWhiteSpace(window.SelectedResourcePath))
        {
            _viewModel.ApplyAnimationPartImageResourceSelection(window.SelectedResourcePath);
        }
    }

    private void ChooseLocalAnimationPartImageButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_filePickerService is null || _viewModel?.AnimationPartEditBuffer is not { IsImage: true })
        {
            return;
        }

        var file = _filePickerService.PickImage();
        if (!string.IsNullOrWhiteSpace(file))
        {
            _viewModel.StoreLocalAnimationPartImage(file);
        }
    }

    private void ChooseLocalCanvasBackgroundButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_filePickerService is null || _viewModel is null)
        {
            return;
        }

        var file = _filePickerService.PickImage();
        if (!string.IsNullOrWhiteSpace(file))
        {
            _viewModel.StoreLocalBackgroundImage(file);
        }
    }

    private void PropertyCheckBox_OnClick(object sender, RoutedEventArgs e)
    {
        ApplyPropertyEditorValue(sender);
    }

    private void PropertyComboBox_OnDropDownClosed(object sender, EventArgs e)
    {
        if (sender is not ComboBox comboBox || !comboBox.IsKeyboardFocusWithin)
        {
            return;
        }

        ApplyPropertyEditorValue(sender);
    }

    private void PropertyFontComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox comboBox
            || e.AddedItems.Count == 0
            || !comboBox.IsDropDownOpen
            || !comboBox.IsKeyboardFocusWithin)
        {
            return;
        }

        ApplyFontComboBoxValue(comboBox, useSelectedOption: true);
    }

    private void PropertyFontComboBox_OnDropDownClosed(object sender, EventArgs e)
    {
        if (sender is not ComboBox comboBox || !comboBox.IsKeyboardFocusWithin)
        {
            return;
        }

        ApplyFontComboBoxValue(comboBox, useSelectedOption: true);
    }

    private void PropertyFontComboBox_OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is ComboBox comboBox && !comboBox.IsDropDownOpen)
        {
            ApplyFontComboBoxValue(comboBox, useSelectedOption: false);
        }
    }

    private void PropertyFontComboBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not ComboBox comboBox)
        {
            return;
        }

        var committed = ApplyFontComboBoxValue(comboBox, useSelectedOption: false);
        if (committed)
        {
            FocusDesignSurface();
        }

        e.Handled = true;
    }

    private bool ApplyFontComboBoxValue(ComboBox comboBox, bool useSelectedOption)
    {
        if (IsPropertyEditorCommitSuppressed()
            || _viewModel is null
            || comboBox.DataContext is not FrontedPropertyEditorItem item)
        {
            return false;
        }

        var value = ResolveFontComboBoxValue(comboBox, useSelectedOption);
        item.Value = value;
        item.EditText = comboBox.Text;
        return _viewModel.ApplyPropertyEdit(item, value);
    }

    private static string ResolveFontComboBoxValue(ComboBox comboBox, bool useSelectedOption)
    {
        if (useSelectedOption && comboBox.SelectedItem is FrontedFontFamilyOption selectedOption)
        {
            return selectedOption.Value;
        }

        var text = comboBox.Text;
        var matchingOption = comboBox.Items
            .OfType<FrontedFontFamilyOption>()
            .FirstOrDefault(option => string.Equals(option.DisplayName, text, StringComparison.CurrentCultureIgnoreCase));
        return matchingOption?.Value ?? text;
    }

    private async void ImportFontButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_filePickerService is null
            || _viewModel is null
            || sender is not FrameworkElement { DataContext: FrontedPropertyEditorItem item })
        {
            return;
        }

        var path = _filePickerService.PickFontFile();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await _viewModel.ImportAndApplyPackageFontAsync(item, path);
    }

    private void ManagePackageFontsButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_packageFontManagerViewModel is null || _viewModel is null)
        {
            return;
        }

        var window = new FrontedPackageFontManagerWindow(_packageFontManagerViewModel)
        {
            Owner = this
        };
        window.ShowDialog();
        _viewModel.RefreshFontFamilyEditorOptions();
    }

    private void PropertyColorPicker_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not DependencyObject picker)
        {
            return;
        }

        DependencyPropertyDescriptor
            .FromName("SelectedColor", picker.GetType(), picker.GetType())
            ?.AddValueChanged(picker, PropertyColorPicker_OnSelectedColorChanged);
    }

    private void PropertyColorPicker_OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not DependencyObject picker)
        {
            return;
        }

        DependencyPropertyDescriptor
            .FromName("SelectedColor", picker.GetType(), picker.GetType())
            ?.RemoveValueChanged(picker, PropertyColorPicker_OnSelectedColorChanged);
    }

    private void PropertyColorPicker_OnSelectedColorChanged(object? sender, EventArgs e)
    {
        if (sender is not FrameworkElement picker || !picker.IsKeyboardFocusWithin)
        {
            return;
        }

        if (IsPropertyEditorCommitSuppressed())
        {
            return;
        }

        if (picker.DataContext is FrontedPropertyEditorItem item)
        {
            if (picker.GetType().GetProperty("SelectedColor")?.GetValue(picker) is Color selectedColor)
            {
                item.ColorValue = selectedColor;
            }

            item.EditText = FrontedPropertyColorHelper.ToArgbString(item.ColorValue);
            if (!item.RequiresExplicitCommit)
            {
                ApplyPropertyEditorValue(picker);
            }
        }
    }

    private bool ApplyPropertyEditorValue(object sender)
    {
        if (IsPropertyEditorCommitSuppressed()
            || _viewModel is null
            || sender is not FrameworkElement { DataContext: FrontedPropertyEditorItem item })
        {
            return false;
        }

        var value = sender is System.Windows.Controls.TextBox textBox
            ? textBox.Text
            : item.EditorKind is FrontedPropertyEditorKind.Text
                or FrontedPropertyEditorKind.Number
                ? item.EditText
                : item.Value;

        return _viewModel.ApplyPropertyEdit(item, value);
    }

    private bool IsPropertyEditorCommitSuppressed()
    {
        return !_isLoaded || _suppressPropertyEditorCommit || _viewModel?.IsRebuildingPropertyGrid == true;
    }

    private void InitializePropertyAutoCommitTimer()
    {
        _propertyAutoCommitTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _propertyAutoCommitTimer.Tick += PropertyAutoCommitTimer_OnTick;
    }

    private void SchedulePropertyAutoCommit(FrameworkElement editor)
    {
        if (_propertyAutoCommitTimer is null)
        {
            return;
        }

        _pendingAutoCommitEditor = editor;
        _propertyAutoCommitTimer.Stop();
        _propertyAutoCommitTimer.Start();
    }

    private void PropertyAutoCommitTimer_OnTick(object? sender, EventArgs e)
    {
        _propertyAutoCommitTimer?.Stop();
        var editor = _pendingAutoCommitEditor;
        _pendingAutoCommitEditor = null;
        if (editor is not null && ShouldAutoCommitPropertyEditor(editor))
        {
            ApplyPropertyEditorValue(editor);
        }
    }

    private bool ShouldAutoCommitPropertyEditor(FrameworkElement editor)
    {
        return !IsPropertyEditorCommitSuppressed()
               && editor.IsKeyboardFocusWithin
               && editor.DataContext is FrontedPropertyEditorItem
               {
                   IsReadOnly: false,
                   RequiresExplicitCommit: false,
                   EditorKind: FrontedPropertyEditorKind.Text
                       or FrontedPropertyEditorKind.Number
                       or FrontedPropertyEditorKind.Color
               };
    }

    private void SuppressPropertyEditorCommitForLayoutPass()
    {
        _suppressPropertyEditorCommit = true;
        Dispatcher.BeginInvoke(
            () => _suppressPropertyEditorCommit = false,
            DispatcherPriority.Loaded);
    }

    private void AttachViewModel()
    {
        if (_viewModel is not null)
        {
            return;
        }

        if (DataContext is not FrontedDesignerWindowViewModel viewModel)
        {
            return;
        }

        _viewModel = viewModel;
        _viewModel.PreviewRenderRequested += OnPreviewRenderRequested;
        _viewModel.DesignerGeometryPatchRequested += OnDesignerGeometryPatchRequested;
        _viewModel.PropertyChanged += ViewModel_OnPropertyChanged;
        _viewModel.PropertyEditorItems.CollectionChanged += PropertyEditorItems_OnCollectionChanged;
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FrontedDesignerWindowViewModel.IsRebuildingPropertyGrid)
            && _viewModel?.IsRebuildingPropertyGrid == true)
        {
            SuppressPropertyEditorCommitForLayoutPass();
        }

        if (e.PropertyName == nameof(FrontedDesignerWindowViewModel.SelectedDesignItem))
        {
            SuppressPropertyEditorCommitForLayoutPass();
            var currentItem = _viewModel?.SelectedDesignItem;
            var wasNull = _lastSeenSelectedDesignItem is null;
            var isNowNonNull = currentItem is not null;
            _lastSeenSelectedDesignItem = currentItem;

            if (_viewModel?.IsRestoringSnapshotVisuals == true)
            {
                return;
            }

            RebuildInteractionLayer();
            _viewModel?.UpdateBehaviorPreviewAnimationScope(PreviewCanvas);
            FocusDesignSurface();

            if (wasNull && isNowNonNull)
            {
                TryQueuePropertyPanelTutorial();
            }
        }

        if (e.PropertyName == nameof(FrontedDesignerWindowViewModel.SelectedDesignItems))
        {
            if (_viewModel?.IsRestoringSnapshotVisuals == true)
            {
                return;
            }

            RebuildInteractionLayer();
            FocusDesignSurface();
        }

        if (e.PropertyName == nameof(FrontedDesignerWindowViewModel.SelectedGlobalScoreCell))
        {
            SuppressPropertyEditorCommitForLayoutPass();
            RebuildInteractionLayer();
            FocusDesignSurface();
        }

        if (e.PropertyName == nameof(FrontedDesignerWindowViewModel.SelectedMapV2InternalStylePart))
        {
            SuppressPropertyEditorCommitForLayoutPass();
            RebuildInteractionLayer();
            FocusDesignSurface();
        }

        if (e.PropertyName == nameof(FrontedDesignerWindowViewModel.BorderedImageResizeTarget))
        {
            RebuildInteractionLayer();
            FocusDesignSurface();
        }

        if (e.PropertyName == nameof(FrontedDesignerWindowViewModel.ZoomScale))
        {
            UpdateSelectedInteractionVisuals();
            RenderSnapGuides();
            ResetPreviewScrollOffsetForFitMode();
        }

        if (e.PropertyName == nameof(FrontedDesignerWindowViewModel.ActiveSnapGuides))
        {
            RenderSnapGuides();
        }
    }

    private void PropertyEditorItems_OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SuppressPropertyEditorCommitForLayoutPass();
    }

    private void OpenValidationDetails_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        if (_validationDetailsWindow is null || !_validationDetailsWindow.IsVisible)
        {
            _validationDetailsWindow = new ValidationDetailsWindow
            {
                Owner = this,
                DataContext = _viewModel
            };
            _validationDetailsWindow.Closed += ValidationDetailsWindow_OnClosed;
            _validationDetailsWindow.Show();
            return;
        }

        _validationDetailsWindow.Activate();
    }

    private void BehaviorExpander_OnExpanded(object sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(() =>
            {
                if (FindVisibleDescendant<neo_bpsys_wpf.Views.FrontedDesigner.BehaviorPanelView>(BehaviorExpander)
                    is { } panel)
                {
                    var runner = _tutorialRunner
                        ?? IAppHost.Host?.Services.GetService(typeof(ITutorialRunner)) as ITutorialRunner;
                    _ = runner?.RunSequenceAsync(
                        panel,
                        neo_bpsys_wpf.Views.FrontedDesigner.BehaviorPanelView.TutorialPageKey,
                        _tutorialLifetime.Token);
                }
            }));
    }

    private void ZoomComboBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is ComboBox comboBox)
        {
            _viewModel?.TryApplyZoomText(comboBox.Text);
            e.Handled = true;
        }
    }

    private void ZoomComboBox_OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is ComboBox comboBox)
        {
            _viewModel?.TryApplyZoomText(comboBox.Text);
        }
    }

    private void ZoomComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0
            && e.AddedItems[0] is FrontedDesignerZoomPreset preset
            && sender is ComboBox)
        {
            _viewModel?.ApplyManualZoom(preset.Scale);
        }
    }

    private void ValidationDetailsWindow_OnClosed(object? sender, EventArgs e)
    {
        if (sender is ValidationDetailsWindow window && ReferenceEquals(window, _validationDetailsWindow))
        {
            window.Closed -= ValidationDetailsWindow_OnClosed;
            _validationDetailsWindow = null;
        }
    }

    private void CloseValidationDetailsWindowSafely()
    {
        var window = _validationDetailsWindow;
        if (window is null)
        {
            return;
        }

        window.Closed -= ValidationDetailsWindow_OnClosed;
        if (!window.IsVisible)
        {
            return;
        }

        try
        {
            window.Owner = null;
            window.Close();
        }
        catch (InvalidOperationException ex)
        {
            _logger?.LogWarning(ex, "Failed to close fronted designer validation details window safely.");
        }
    }

    private void OpenDesignerHelp_OnClick(object sender, RoutedEventArgs e)
    {
        if (_helpWindow is null || !_helpWindow.IsVisible)
        {
            _helpWindow = new FrontedDesignerHelpWindow
            {
                Owner = this
            };
            _helpWindow.Closed += HelpWindow_OnClosed;
            _helpWindow.Show();
            return;
        }

        _helpWindow.Activate();
    }

    private void HelpWindow_OnClosed(object? sender, EventArgs e)
    {
        if (sender is FrontedDesignerHelpWindow window && ReferenceEquals(window, _helpWindow))
        {
            window.Closed -= HelpWindow_OnClosed;
            _helpWindow = null;
        }
    }

    private void CloseHelpWindowSafely()
    {
        var window = _helpWindow;
        if (window is null)
        {
            return;
        }

        window.Closed -= HelpWindow_OnClosed;
        if (!window.IsVisible)
        {
            return;
        }

        try
        {
            window.Owner = null;
            window.Close();
        }
        catch (InvalidOperationException ex)
        {
            _logger?.LogWarning(ex, "Failed to close fronted designer help window safely.");
        }
    }

    private void AddControlButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (AddControlButton.ContextMenu is null)
        {
            return;
        }

        RebuildAddControlContextMenu();
        AddControlButton.ContextMenu.PlacementTarget = AddControlButton;
        AddControlButton.ContextMenu.IsOpen = true;
    }

    private void RebuildAddControlContextMenu()
    {
        if (_viewModel is null || AddControlButton.ContextMenu is null)
        {
            return;
        }

        AddControlButton.ContextMenu.Items.Clear();
        foreach (var group in _viewModel.AddControlCatalogGroups)
        {
            if (AddControlButton.ContextMenu.Items.Count > 0)
            {
                AddControlButton.ContextMenu.Items.Add(new Separator());
            }

            AddControlButton.ContextMenu.Items.Add(new System.Windows.Controls.MenuItem
            {
                Header = group.DisplayName,
                IsEnabled = false
            });

            foreach (var item in group.Items)
            {
                var menuItem = new System.Windows.Controls.MenuItem
                {
                    Header = item.DisplayName,
                    Tag = item.ControlType,
                    ToolTip = string.IsNullOrWhiteSpace(item.Description) ? item.ControlType : item.Description,
                    IsEnabled = item.IsAvailable
                };
                menuItem.Click += AddControlMenuItem_OnClick;
                AddControlButton.ContextMenu.Items.Add(menuItem);
            }
        }
    }

    private void AddControlMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || sender is not System.Windows.Controls.MenuItem { Tag: string controlType })
        {
            return;
        }

        _viewModel.AddControlCommand.Execute(new FrontedAddControlRequest
        {
            ControlType = controlType,
            CenterX = GetViewportCenterX(),
            CenterY = GetViewportCenterY()
        });
        FocusDesignSurface();
    }

    private async void ReloadLayoutButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || !await ConfirmDirtyDocumentCanContinueAsync("SaveBeforeSwitch"))
        {
            return;
        }

        await _viewModel.ReloadLayoutCoreAsync();
        _lastAcceptedWindow = _viewModel.SelectedWindow;
    }

    private async void SaveLayoutButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var saved = await _viewModel.SaveCurrentLayoutAsync();
        if (!saved && _viewModel.ErrorCount > 0)
        {
            OpenValidationDetails_OnClick(sender, e);
        }
    }

    private async void ResetToBuiltInButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || !await ConfirmDirtyDocumentCanContinueAsync("SaveBeforeSwitch"))
        {
            return;
        }

        if (!await ConfirmResetToBuiltInAsync())
        {
            return;
        }

        await _viewModel.ResetToBuiltInCoreAsync();
        _lastAcceptedWindow = _viewModel.SelectedWindow;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_forceCloseAfterDirtyPrompt || _viewModel?.HasUnsavedChanges != true)
        {
            return;
        }

        e.Cancel = true;
        if (_isDirtyClosePromptOpen)
        {
            return;
        }

        _isDirtyClosePromptOpen = true;
        Dispatcher.BeginInvoke(
            new Action(async () => await PromptDirtyCloseAfterCancelAsync()),
            DispatcherPriority.Background);
    }

    private async Task PromptDirtyCloseAfterCancelAsync()
    {
        try
        {
            var result = await ShowDirtyPromptAsync("SaveBeforeClose");
            if (result == MessageBoxResult.Primary)
            {
                if (_viewModel is not null && await _viewModel.SaveCurrentLayoutAsync())
                {
                    _forceCloseAfterDirtyPrompt = true;
                    Close();
                }
            }
            else if (result == MessageBoxResult.Secondary)
            {
                _viewModel?.DiscardPendingResourceImports();
                _forceCloseAfterDirtyPrompt = true;
                Close();
            }
        }
        finally
        {
            _isDirtyClosePromptOpen = false;
        }
    }

    private async Task<bool> ConfirmDirtyDocumentCanContinueAsync(string messageKey)
    {
        if (_viewModel?.HasUnsavedChanges != true)
        {
            return true;
        }

        var result = await ShowDirtyPromptAsync(messageKey);
        if (result == MessageBoxResult.Primary)
        {
            return await _viewModel.SaveCurrentLayoutAsync();
        }

        if (result == MessageBoxResult.Secondary)
        {
            _viewModel.DiscardPendingResourceImports();
        }

        return result == MessageBoxResult.Secondary;
    }

    private Task<MessageBoxResult> ShowDirtyPromptAsync(string messageKey)
    {
        return MessageBoxHelper.ShowThreeOptionAsync(
            I18nHelper.GetLocalizedString(messageKey),
            I18nHelper.GetLocalizedString("UnsavedChanges"),
            I18nHelper.GetLocalizedString("Save"),
            I18nHelper.GetLocalizedString("DiscardChanges"),
            I18nHelper.GetLocalizedString("Cancel"),
            width: 600,
            minWidth: 560,
            primaryButtonIcon: SymbolRegular.Save24,
            secondaryButtonIcon: SymbolRegular.Delete24,
            closeButtonIcon: SymbolRegular.Dismiss24);
    }

    private async Task<bool> ConfirmResetToBuiltInAsync()
    {
        var messageBox = new Wpf.Ui.Controls.MessageBox
        {
            Owner = this,
            Title = I18nHelper.GetLocalizedString("ResetToBuiltIn"),
            Content = I18nHelper.GetLocalizedString("ResetLayoutConfirm"),
            PrimaryButtonText = I18nHelper.GetLocalizedString("Confirm"),
            PrimaryButtonIcon = new SymbolIcon { Symbol = SymbolRegular.ArrowClockwise24 },
            CloseButtonText = I18nHelper.GetLocalizedString("Cancel"),
            CloseButtonIcon = new SymbolIcon { Symbol = SymbolRegular.Dismiss24 }
        };

        return await messageBox.ShowDialogAsync() == MessageBoxResult.Primary;
    }

    private void UpdateShiftSnapState()
    {
        _viewModel?.UpdateShiftSnapActive(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
    }

    private void OnPreviewRenderRequested(
        object? sender,
        FrontedDesignerPreviewRenderRequestedEventArgs e)
    {
        _pendingPreviewRenderArgs = e;
        if (_previewRenderScheduled)
        {
            return;
        }

        _previewRenderScheduled = true;
        Dispatcher.BeginInvoke(
            new Action(ExecutePendingPreviewRender),
            DispatcherPriority.Background);
    }

    private void ExecutePendingPreviewRender()
    {
        _previewRenderScheduled = false;
        var args = _pendingPreviewRenderArgs;
        _pendingPreviewRenderArgs = null;
        if (!_isLoaded || args is null)
        {
            return;
        }

        RenderPreview(args);
    }

    private void RenderPreview(FrontedDesignerPreviewRenderRequestedEventArgs e)
    {
        var total = StartDesignerPerfTrace();
        if (_renderer is null || e.Config is null || e.Context is null)
        {
            ClearPreviewCanvas();
            LogDesignerPerf("PreviewRender", "clear", Elapsed(total));
            return;
        }

        try
        {
            ConfigureDesignSurface(e.Config.CanvasWidth, e.Config.CanvasHeight);
            LogDesignerPerf("PreviewRender", "configure surface", Elapsed(total));
            _renderer.RenderToCanvas(PreviewCanvas, e.Config, e.Context);
            if (e.BehaviorDocument is not null)
            {
                _animationPartRenderer?.ApplyAnimationParts(PreviewCanvas, e.BehaviorDocument);
            }
            LogDesignerPerf("PreviewRender", "render canvas", Elapsed(total));
            PopulatePreviewElementRegistry();
            LogDesignerPerf("PreviewRender", "populate element registry", Elapsed(total));
            PreviewCanvas.UpdateLayout();
            _viewModel?.UpdateBehaviorPreviewAnimationScope(PreviewCanvas);
            LogDesignerPerf("PreviewRender", "update layout", Elapsed(total));
            RebuildInteractionLayer();
            ScheduleSelectedInteractionVisualRefresh();
            LogDesignerPerf("PreviewRender", "rebuild interaction layer", Elapsed(total));
            LogDesignerPerf("PreviewRender", "total", Elapsed(total));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to render fronted designer preview.");
            ClearPreviewCanvas();
            _viewModel?.ReportRenderFailure(ex);
        }
    }

    private Stopwatch? StartDesignerPerfTrace()
    {
        return _logger?.IsEnabled(LogLevel.Debug) == true ? Stopwatch.StartNew() : null;
    }

    private static TimeSpan Elapsed(Stopwatch? stopwatch)
    {
        return stopwatch?.Elapsed ?? TimeSpan.Zero;
    }

    [Conditional("DEBUG")]
    private void LogDesignerPerf(string operation, string stage, TimeSpan elapsed)
    {
        if (_logger?.IsEnabled(LogLevel.Debug) == true)
        {
            _logger.LogDebug(
                "FrontedDesigner perf {Operation}: {Stage} at {ElapsedMilliseconds:F2} ms",
                operation,
                stage,
                elapsed.TotalMilliseconds);
        }
    }

    private void ClearPreviewCanvas()
    {
        _viewModel?.ClearActiveSnapGuides();
        PreviewCanvas.Children.Clear();
        _previewElementsByControlName.Clear();
        _viewModel?.ClearBehaviorPreviewAnimationScope();
        PreviewCanvas.Background = null;
        ConfigureDesignSurface(640, 360);
        InteractionLayer.Children.Clear();
        _snapGuideLines.Clear();
        _hitboxes.Clear();
        _globalScoreCellHitboxes.Clear();
        _resizeHandles.Clear();
        _polygonVertexHandles.Clear();
        _selectionOutline = null;
        _parentSelectionOutline = null;
        _selectionLabel = null;
        ResetPointerInteraction();
    }

    private void PopulatePreviewElementRegistry()
    {
        _previewElementsByControlName.Clear();
        foreach (var element in EnumerateFrameworkElements(PreviewCanvas))
        {
            var name = FrontedRendererProperties.GetRegisteredName(element);
            if (string.IsNullOrWhiteSpace(name))
            {
                name = element.Name;
            }

            if (!string.IsNullOrWhiteSpace(name)
                && FrontedRendererProperties.GetIsGeneratedControl(element)
                && !_previewElementsByControlName.ContainsKey(name))
            {
                _previewElementsByControlName[name] = element;
            }
        }
    }

    private void OnDesignerGeometryPatchRequested(
        object? sender,
        FrontedDesignerGeometryPatchRequestedEventArgs e)
    {
        var total = StartDesignerPerfTrace();
        if (_viewModel?.CurrentDocument is null)
        {
            e.RequestFullRenderFallback("document missing");
            return;
        }

        if (!TryApplyPreviewGeometryPatch(e.ChangedItems, e.ZIndexChanged, out var failureReason))
        {
            e.RequestFullRenderFallback(failureReason);
            LogDesignerPerf("PreviewPatch", $"fallback: {failureReason}", Elapsed(total));
            return;
        }

        if (e.RebuildLayerPanel || e.ZIndexChanged)
        {
            ReorderPreviewChildrenToDocument();
        }

        if (e.RebuildInteractionLayer)
        {
            RebuildInteractionLayer();
        }
        else
        {
            UpdatePatchedHitboxes(e.ChangedItems);
            if (e.UpdateSelection)
            {
                UpdateSelectedInteractionVisuals();
            }
        }

        _viewModel.ClearActiveSnapGuides();
        RenderSnapGuides();
        LogDesignerPerf("PreviewPatch", $"update element count {e.ChangedItems.Count}", Elapsed(total));
    }

    private bool TryApplyPreviewGeometryPatch(
        IReadOnlyList<FrontedControlDesignItem> changedItems,
        bool zIndexChanged,
        out string failureReason)
    {
        failureReason = string.Empty;
        foreach (var item in changedItems)
        {
            if (FindPreviewElement(item.Name) is null)
            {
                failureReason = $"preview element missing: {item.Name}";
                return false;
            }
        }

        foreach (var item in changedItems)
        {
            var element = FindPreviewElement(item.Name)!;
            ApplyPreviewElementGeometry(element, item);
            if (zIndexChanged)
            {
                Panel.SetZIndex(element, item.Config.ZIndex);
            }
        }

        return true;
    }

    private void ApplyPreviewElementGeometry(FrameworkElement element, FrontedControlDesignItem item)
    {
        Canvas.SetLeft(element, item.Config.Left);
        Canvas.SetTop(element, item.Config.Top);

        if (item.Config.Width.HasValue)
        {
            element.Width = item.Config.Width.Value;
        }

        if (item.Config.Height.HasValue)
        {
            element.Height = item.Config.Height.Value;
        }

        if (item.Config is BorderedImageFrontedControlConfig imageConfig)
        {
            UpdateBorderedImageInnerPreviewElement(element, imageConfig);
        }

        if (element is BackgroundTintControlHost tintHost
            && item.Config is BackgroundTintFrontedControlConfigBase)
        {
            tintHost.TintedImage.Margin = new Thickness(-item.Config.Left, -item.Config.Top, 0, 0);
        }

        if (item.Config is BackgroundTintRectangleFrontedControlConfig tintRectangleConfig)
        {
            element.Clip = new RectangleGeometry(
                new Rect(0, 0, tintRectangleConfig.Width ?? 1D, tintRectangleConfig.Height ?? 1D),
                Math.Max(0, tintRectangleConfig.RadiusX),
                Math.Max(0, tintRectangleConfig.RadiusY));
        }
        else if (item.Config is PolygonFrontedControlConfig polygonConfig)
        {
            var polygon = element as Polygon ?? FindDescendant<Polygon>(element);
            if (polygon is not null)
            {
                polygon.Points = PolygonFrontedControl.CreatePointCollection(polygonConfig);
            }
        }
        else if (item.Config is BackgroundTintPolygonFrontedControlConfig tintPolygonConfig)
        {
            element.Clip = BackgroundTintPolygonFrontedControl.CreateGeometry(tintPolygonConfig, element);
        }
    }

    private void UpdatePatchedHitboxes(IReadOnlyList<FrontedControlDesignItem> changedItems)
    {
        foreach (var item in changedItems)
        {
            if (!_hitboxes.TryGetValue(item, out var hitbox))
            {
                continue;
            }

            var bounds = ResolveItemBounds(item);
            hitbox.Width = bounds.Width;
            hitbox.Height = bounds.Height;
            Canvas.SetLeft(hitbox, bounds.Left);
            Canvas.SetTop(hitbox, bounds.Top);
        }
    }

    private void ReorderPreviewChildrenToDocument()
    {
        if (_viewModel?.CurrentDocument is null)
        {
            return;
        }

        var desiredOrder = _viewModel.CurrentDocument.Controls
            .Select((item, index) => new { item.Name, item.Config.ZIndex, Index = index })
            .OrderBy(entry => entry.ZIndex)
            .ThenBy(entry => entry.Index)
            .Select(entry => entry.Name)
            .ToList();
        var generatedChildren = desiredOrder
            .Select(FindPreviewElement)
            .Where(element => element is not null)
            .Cast<UIElement>()
            .ToList();

        foreach (var child in generatedChildren)
        {
            PreviewCanvas.Children.Remove(child);
        }

        foreach (var child in generatedChildren)
        {
            PreviewCanvas.Children.Add(child);
        }
    }

    private static IEnumerable<FrameworkElement> EnumerateFrameworkElements(DependencyObject parent)
    {
        var childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is FrameworkElement element)
            {
                yield return element;
            }

            foreach (var nested in EnumerateFrameworkElements(child))
            {
                yield return nested;
            }
        }
    }

    private void ConfigureDesignSurface(double width, double height)
    {
        DesignSurfaceGrid.Width = width;
        DesignSurfaceGrid.Height = height;
        PreviewCanvas.Width = width;
        PreviewCanvas.Height = height;
        InteractionLayer.Width = width;
        InteractionLayer.Height = height;
        UpdatePreviewWorkspaceSize();
        _viewModel?.UpdateFitZoom(PreviewScrollViewer.ViewportWidth, PreviewScrollViewer.ViewportHeight, width, height);
    }

    private void RebuildInteractionLayer()
    {
        _viewModel?.ClearActiveSnapGuides();
        InteractionLayer.Children.Clear();
        _snapGuideLines.Clear();
        _hitboxes.Clear();
        _globalScoreCellHitboxes.Clear();
        _resizeHandles.Clear();
        _multiSelectionOutlines.Clear();
        _selectionOutline = null;
        _parentSelectionOutline = null;
        _selectionLabel = null;
        _marqueeSelectionOutline = null;

        if (_viewModel?.CurrentDocument is null)
        {
            return;
        }

        foreach (var entry in _viewModel.CurrentDocument.Controls.Select((item, index) => new { Item = item, Index = index }))
        {
            if (!entry.Item.IsSelectableInEditor)
            {
                continue;
            }

            var hitbox = CreateHitbox(entry.Item, entry.Index);
            _hitboxes[entry.Item] = hitbox;
            InteractionLayer.Children.Add(hitbox);

            if (entry.Item.Config is GlobalScoreRowControlConfig rowConfig
                && ReferenceEquals(entry.Item, _viewModel.SelectedDesignItem))
            {
                foreach (var cell in rowConfig.Cells)
                {
                    var cellTarget = new GlobalScoreCellHitTarget(entry.Item, cell);
                    var cellHitbox = CreateGlobalScoreCellHitbox(cellTarget, entry.Index);
                    _globalScoreCellHitboxes[cellTarget] = cellHitbox;
                    InteractionLayer.Children.Add(cellHitbox);
                }
            }
        }

        if (_viewModel.SelectedDesignItems.Count > 0)
        {
            AddSelectionAdorners();
        }
    }

    private void AddSelectionAdorners()
    {
        if (_viewModel is null)
        {
            return;
        }

        foreach (var item in _viewModel.SelectedDesignItems)
        {
            if (ReferenceEquals(item, _viewModel.SelectedDesignItem))
            {
                continue;
            }

            AddMultiSelectionOutline(item);
        }

        if (_viewModel.SelectedDesignItem is not null)
        {
            AddSelectionAdorner(_viewModel.SelectedDesignItem);
        }
    }

    private void AddMultiSelectionOutline(FrontedControlDesignItem item)
    {
        var bounds = ResolveItemBounds(item);
        var outline = new Border
        {
            Width = bounds.Width,
            Height = bounds.Height,
            BorderBrush = TryFindResource("AccentFillColorDefaultBrush") as Brush ?? Brushes.DeepSkyBlue,
            BorderThickness = new Thickness(FrontedDesignerEditorVisualHelper.SelectionBorderThickness),
            Opacity = 0.65D,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(outline, bounds.Left);
        Canvas.SetTop(outline, bounds.Top);
        Panel.SetZIndex(outline, FrontedDesignerEditorVisualHelper.SelectedOutlineZIndex - 1);
        _multiSelectionOutlines.Add(outline);
        InteractionLayer.Children.Add(outline);
    }

    private Border CreateGlobalScoreCellHitbox(GlobalScoreCellHitTarget target, int layoutOrder)
    {
        var bounds = ResolveGlobalScoreCellBounds(target.Parent, target.Cell);
        var hitbox = new Border
        {
            Background = Brushes.Transparent,
            BorderBrush = new SolidColorBrush(Color.FromArgb(80, 64, 200, 255)),
            BorderThickness = new Thickness(1),
            Width = bounds.Width,
            Height = bounds.Height,
            IsHitTestVisible = true,
            Tag = target
        };

        Canvas.SetLeft(hitbox, bounds.Left);
        Canvas.SetTop(hitbox, bounds.Top);
        Panel.SetZIndex(
            hitbox,
            FrontedDesignerEditorVisualHelper.GetHitboxZIndex(
                target.Parent.Config.ZIndex,
                layoutOrder,
                ReferenceEquals(target.Cell, _viewModel?.SelectedGlobalScoreCell)) + 1);
        hitbox.MouseLeftButtonDown += GlobalScoreCellHitbox_OnMouseLeftButtonDown;
        return hitbox;
    }

    private Border CreateHitbox(FrontedControlDesignItem item, int layoutOrder)
    {
        var bounds = ResolveItemBounds(item);
        var hitbox = new Border
        {
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(1),
            Width = bounds.Width,
            Height = bounds.Height,
            IsHitTestVisible = true,
            Tag = item
        };

        Canvas.SetLeft(hitbox, bounds.Left);
        Canvas.SetTop(hitbox, bounds.Top);
        Panel.SetZIndex(
            hitbox,
            FrontedDesignerEditorVisualHelper.GetHitboxZIndex(
                item.Config.ZIndex,
                layoutOrder,
                ReferenceEquals(item, _viewModel?.SelectedDesignItem)));
        hitbox.MouseLeftButtonDown += Hitbox_OnMouseLeftButtonDown;
        return hitbox;
    }

    private void AddSelectionAdorner(FrontedControlDesignItem item)
    {
        var parentBounds = ResolveItemBounds(item);
        var bounds = _viewModel?.SelectedGlobalScoreCell is { } cell
                     && item.Config is GlobalScoreRowControlConfig
            ? ResolveGlobalScoreCellBounds(item, cell)
            : _viewModel?.SelectedMapV2InternalPartLayout is { } internalPart
              && item.Config is MapV2DisplayControlConfig
                ? ResolveMapV2InternalPartBounds(item, internalPart)
                : parentBounds;

        if (_viewModel?.SelectedGlobalScoreCell is not null
            || _viewModel?.SelectedMapV2InternalPartLayout is not null)
        {
            _parentSelectionOutline = new Border
            {
                Width = parentBounds.Width,
                Height = parentBounds.Height,
                BorderBrush = new SolidColorBrush(Color.FromArgb(180, 64, 200, 255)),
                BorderThickness = new Thickness(1),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(_parentSelectionOutline, parentBounds.Left);
            Canvas.SetTop(_parentSelectionOutline, parentBounds.Top);
            Panel.SetZIndex(_parentSelectionOutline, FrontedDesignerEditorVisualHelper.SelectedOutlineZIndex - 1);
            InteractionLayer.Children.Add(_parentSelectionOutline);
        }

        _selectionOutline = new Border
        {
            Width = bounds.Width,
            Height = bounds.Height,
            BorderBrush = TryFindResource("AccentFillColorDefaultBrush") as Brush ?? Brushes.DeepSkyBlue,
            BorderThickness = new Thickness(FrontedDesignerEditorVisualHelper.SelectionBorderThickness),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(_selectionOutline, bounds.Left);
        Canvas.SetTop(_selectionOutline, bounds.Top);
        Panel.SetZIndex(_selectionOutline, FrontedDesignerEditorVisualHelper.SelectedOutlineZIndex);
        InteractionLayer.Children.Add(_selectionOutline);

        _selectionLabel = new Border
        {
            Background = TryFindResource("AccentFillColorDefaultBrush") as Brush ?? Brushes.DeepSkyBlue,
            Padding = new Thickness(4, 1, 4, 1),
            CornerRadius = new CornerRadius(2),
            Child = new System.Windows.Controls.TextBlock
            {
                Text = _viewModel?.SelectedGlobalScoreCell is { } selectedCell
                    ? selectedCell.Id
                    : _viewModel?.SelectedMapV2InternalStylePart is { } selectedPart
                        ? selectedPart.DisplayName
                    : item.Name,
                FontSize = FrontedDesignerEditorVisualHelper.SelectionLabelBaseFontSize,
                Foreground = Brushes.White
            },
            IsHitTestVisible = false
        };
        Panel.SetZIndex(_selectionLabel, FrontedDesignerEditorVisualHelper.SelectedOutlineZIndex + 1);
        InteractionLayer.Children.Add(_selectionLabel);

        foreach (var handle in Enum.GetValues<FrontedDesignerResizeHandleKind>())
        {
            var handleElement = CreateResizeHandle(handle);
            _resizeHandles[handle] = handleElement;
            InteractionLayer.Children.Add(handleElement);
        }

        if (item.Config is IPolygonFrontedControlConfig polygon)
        {
            for (var index = 0; index < polygon.Points.Count; index++)
            {
                var handleElement = CreatePolygonVertexHandle(index);
                _polygonVertexHandles[index] = handleElement;
                InteractionLayer.Children.Add(handleElement);
            }
        }

        UpdateSelectedInteractionVisuals();
    }

    private FrameworkElement CreatePolygonVertexHandle(int index)
    {
        var element = new Border
        {
            Width = 16,
            Height = 16,
            Background = Brushes.Transparent,
            Child = new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = Brushes.Orange,
                Stroke = Brushes.White,
                StrokeThickness = 2
            },
            Cursor = Cursors.Cross,
            Tag = index
        };
        Panel.SetZIndex(element, FrontedDesignerEditorVisualHelper.SelectedHandleZIndex + 1);
        element.MouseLeftButtonDown += PolygonVertexHandle_OnMouseLeftButtonDown;
        return element;
    }

    private Border CreateResizeHandle(FrontedDesignerResizeHandleKind handle)
    {
        var element = new Border
        {
            Width = FrontedDesignerEditorVisualHelper.HandleHitTargetSize,
            Height = FrontedDesignerEditorVisualHelper.HandleHitTargetSize,
            Background = Brushes.Transparent,
            Child = new Border
            {
                Width = FrontedDesignerEditorVisualHelper.HandleVisualSize,
                Height = FrontedDesignerEditorVisualHelper.HandleVisualSize,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Background = TryFindResource("AccentFillColorDefaultBrush") as Brush ?? Brushes.DeepSkyBlue,
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(FrontedDesignerEditorVisualHelper.HandleBorderThickness)
            },
            Cursor = GetCursor(handle),
            Tag = handle
        };

        Panel.SetZIndex(element, FrontedDesignerEditorVisualHelper.SelectedHandleZIndex);
        element.MouseLeftButtonDown += ResizeHandle_OnMouseLeftButtonDown;
        return element;
    }

    private void UpdateSelectedInteractionVisuals()
    {
        var item = _viewModel?.SelectedDesignItem;
        if (item is null)
        {
            return;
        }

        UpdateMultiSelectionOutlines();
        var bounds = _viewModel?.SelectedGlobalScoreCell is { } cell
                     && item.Config is GlobalScoreRowControlConfig
            ? ResolveGlobalScoreCellBounds(item, cell)
            : _viewModel?.SelectedMapV2InternalPartLayout is { } internalPart
              && item.Config is MapV2DisplayControlConfig
                ? ResolveMapV2InternalPartBounds(item, internalPart)
                : ResolveItemBounds(item);
        if (_parentSelectionOutline is not null)
        {
            var parentBounds = ResolveItemBounds(item);
            _parentSelectionOutline.Width = parentBounds.Width;
            _parentSelectionOutline.Height = parentBounds.Height;
            Canvas.SetLeft(_parentSelectionOutline, parentBounds.Left);
            Canvas.SetTop(_parentSelectionOutline, parentBounds.Top);
        }

        UpdateGlobalScoreCellHitboxes(item);

        if (_hitboxes.TryGetValue(item, out var hitbox))
        {
            hitbox.Width = bounds.Width;
            hitbox.Height = bounds.Height;
            Canvas.SetLeft(hitbox, bounds.Left);
            Canvas.SetTop(hitbox, bounds.Top);
            Panel.SetZIndex(hitbox, FrontedDesignerEditorVisualHelper.SelectedHitboxZIndex);
        }

        if (_selectionOutline is not null)
        {
            _selectionOutline.Width = bounds.Width;
            _selectionOutline.Height = bounds.Height;
            Canvas.SetLeft(_selectionOutline, bounds.Left);
            Canvas.SetTop(_selectionOutline, bounds.Top);
        }

        ApplySelectionLabelZoomMetrics(bounds);

        SetHandlePosition(FrontedDesignerResizeHandleKind.TopLeft, bounds.Left, bounds.Top);
        SetHandlePosition(FrontedDesignerResizeHandleKind.Top, bounds.Left + bounds.Width / 2, bounds.Top);
        SetHandlePosition(FrontedDesignerResizeHandleKind.TopRight, bounds.Left + bounds.Width, bounds.Top);
        SetHandlePosition(FrontedDesignerResizeHandleKind.Left, bounds.Left, bounds.Top + bounds.Height / 2);
        SetHandlePosition(FrontedDesignerResizeHandleKind.Right, bounds.Left + bounds.Width, bounds.Top + bounds.Height / 2);
        SetHandlePosition(FrontedDesignerResizeHandleKind.BottomLeft, bounds.Left, bounds.Top + bounds.Height);
        SetHandlePosition(FrontedDesignerResizeHandleKind.Bottom, bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height);
        SetHandlePosition(FrontedDesignerResizeHandleKind.BottomRight, bounds.Left + bounds.Width, bounds.Top + bounds.Height);

        if (item.Config is IPolygonFrontedControlConfig polygon)
        {
            foreach (var (index, handle) in _polygonVertexHandles)
            {
                if (index >= polygon.Points.Count)
                {
                    continue;
                }

                var point = PolygonVertexGeometryHelper.ToCanvasPoint(item.Config, polygon.Points[index]);
                Canvas.SetLeft(handle, point.X - handle.Width / 2D);
                Canvas.SetTop(handle, point.Y - handle.Height / 2D);
            }
        }
    }

    private void UpdateMultiSelectionOutlines()
    {
        if (_viewModel is null)
        {
            return;
        }

        var outlineIndex = 0;
        foreach (var selectedItem in _viewModel.SelectedDesignItems)
        {
            if (ReferenceEquals(selectedItem, _viewModel.SelectedDesignItem))
            {
                continue;
            }

            if (outlineIndex >= _multiSelectionOutlines.Count)
            {
                break;
            }

            var bounds = ResolveItemBounds(selectedItem);
            var outline = _multiSelectionOutlines[outlineIndex];
            outline.Width = bounds.Width;
            outline.Height = bounds.Height;
            Canvas.SetLeft(outline, bounds.Left);
            Canvas.SetTop(outline, bounds.Top);

            if (_hitboxes.TryGetValue(selectedItem, out var hitbox))
            {
                hitbox.Width = bounds.Width;
                hitbox.Height = bounds.Height;
                Canvas.SetLeft(hitbox, bounds.Left);
                Canvas.SetTop(hitbox, bounds.Top);
            }

            outlineIndex++;
        }
    }

    private void UpdateGlobalScoreCellHitboxes(FrontedControlDesignItem parent)
    {
        if (parent.Config is not GlobalScoreRowControlConfig)
        {
            return;
        }

        foreach (var (target, hitbox) in _globalScoreCellHitboxes)
        {
            if (!ReferenceEquals(target.Parent, parent))
            {
                continue;
            }

            var bounds = ResolveGlobalScoreCellBounds(target.Parent, target.Cell);
            hitbox.Width = bounds.Width;
            hitbox.Height = bounds.Height;
            Canvas.SetLeft(hitbox, bounds.Left);
            Canvas.SetTop(hitbox, bounds.Top);
        }
    }

    private void RenderSnapGuides()
    {
        ClearSnapGuideLines();

        if (_viewModel?.ActiveSnapGuides.Count is null or 0)
        {
            return;
        }

        var brush = TryFindResource("AccentFillColorDefaultBrush") as Brush ?? Brushes.DeepSkyBlue;
        var thickness = Math.Max(1D, 1D / Math.Max(0.01D, _viewModel.ZoomScale));
        foreach (var guide in _viewModel.ActiveSnapGuides)
        {
            var line = new Line
            {
                Stroke = brush,
                StrokeThickness = thickness,
                StrokeDashArray = new DoubleCollection { 4D, 2D },
                IsHitTestVisible = false,
                SnapsToDevicePixels = true
            };

            if (guide.Orientation == FrontedDesignerSnapGuideOrientation.Vertical)
            {
                line.X1 = guide.Position;
                line.X2 = guide.Position;
                line.Y1 = guide.Start;
                line.Y2 = guide.End;
            }
            else
            {
                line.X1 = guide.Start;
                line.X2 = guide.End;
                line.Y1 = guide.Position;
                line.Y2 = guide.Position;
            }

            Panel.SetZIndex(line, FrontedDesignerEditorVisualHelper.SelectedOutlineZIndex - 1);
            _snapGuideLines.Add(line);
            InteractionLayer.Children.Add(line);
        }
    }

    private void ClearSnapGuideLines()
    {
        foreach (var line in _snapGuideLines)
        {
            InteractionLayer.Children.Remove(line);
        }

        _snapGuideLines.Clear();
    }

    private void ApplySelectionLabelZoomMetrics(FrontedDesignerResolvedBounds bounds)
    {
        if (_selectionLabel is null)
        {
            return;
        }

        var zoomScale = _viewModel?.ZoomScale ?? 1D;
        if (_selectionLabel.Child is System.Windows.Controls.TextBlock textBlock)
        {
            textBlock.FontSize = FrontedDesignerEditorVisualHelper.GetEffectiveSelectionLabelFontSize(zoomScale);
        }

        var topOffset = FrontedDesignerEditorVisualHelper.GetEffectiveSelectionLabelTopOffset(zoomScale);
        Canvas.SetLeft(_selectionLabel, bounds.Left);
        Canvas.SetTop(_selectionLabel, Math.Max(0, bounds.Top - topOffset));
    }

    private void SetHandlePosition(FrontedDesignerResizeHandleKind handle, double x, double y)
    {
        if (!_resizeHandles.TryGetValue(handle, out var element))
        {
            return;
        }

        Canvas.SetLeft(element, x - element.Width / 2);
        Canvas.SetTop(element, y - element.Height / 2);
    }

    private void Hitbox_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: FrontedControlDesignItem item } hitbox
            || _viewModel is null)
        {
            return;
        }

        FocusDesignSurface();
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            _viewModel.ToggleDesignItemSelection(item);
            e.Handled = true;
            return;
        }

        BeginPendingHitboxClick(item, e.GetPosition(InteractionLayer), hitbox);
        e.Handled = true;
    }

    private void GlobalScoreCellHitbox_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: GlobalScoreCellHitTarget target } hitbox
            || _viewModel is null)
        {
            return;
        }

        FocusDesignSurface();
        _viewModel.SelectGlobalScoreCell(target.Parent, target.Cell);
        BeginPendingGlobalScoreCellClick(target, e.GetPosition(InteractionLayer), hitbox);
        e.Handled = true;
    }

    private void ResizeHandle_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: FrontedDesignerResizeHandleKind handle } element
            || _viewModel?.SelectedDesignItem is null)
        {
            return;
        }

        FocusDesignSurface();
        _activeResizeHandle = handle;
        BeginInteraction(InteractionMode.Resize, e.GetPosition(InteractionLayer), element);
        e.Handled = true;
    }

    private void PolygonVertexHandle_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: int index } element
            || _viewModel?.SelectedDesignItem?.Config is not IPolygonFrontedControlConfig)
        {
            return;
        }

        FocusDesignSurface();
        _viewModel.SelectPolygonVertex(index);
        _activePolygonVertexIndex = index;
        BeginInteraction(InteractionMode.PolygonVertex, e.GetPosition(InteractionLayer), element);
        e.Handled = true;
    }

    private void InteractionLayer_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, InteractionLayer))
        {
            FocusDesignSurface();
            BeginMarqueeSelection(e.GetPosition(InteractionLayer));
            e.Handled = true;
        }
    }

    private void InteractionLayer_OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        BeginViewportPan(e);
        e.Handled = true;
    }

    private void DesignSurface_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        FocusDesignSurface();
    }

    private void InteractionLayer_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_isPanningViewport)
        {
            UpdateViewportPan(e);
            e.Handled = true;
            return;
        }

        if (_capturedElement is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var currentPosition = e.GetPosition(InteractionLayer);
        var deltaX = currentPosition.X - _startMousePosition.X;
        var deltaY = currentPosition.Y - _startMousePosition.Y;

        if (_pendingHitCandidate is not null)
        {
            HandlePendingHitboxMove(deltaX, deltaY);
        }
        else if (_pendingGlobalScoreCellHitCandidate is not null)
        {
            HandlePendingGlobalScoreCellMove(deltaX, deltaY);
        }
        else if (_isPendingEmptyClick)
        {
            _hasExceededClickThreshold |= FrontedDesignerInteractionHelper.ExceedsClickThreshold(deltaX, deltaY);
        }
        else if (_interactionMode == InteractionMode.Marquee)
        {
            _hasExceededClickThreshold |= FrontedDesignerInteractionHelper.ExceedsClickThreshold(deltaX, deltaY);
            UpdateMarqueeSelection(currentPosition);
        }
        else if (_interactionMode == InteractionMode.Resize && _activeResizeHandle is { } handle)
        {
            if (_originalSelectedBounds.Count > 1)
            {
                _viewModel?.ResizeSelectedDesignItems(handle, _originalSelectedBounds, deltaX, deltaY, renderPreview: false);
            }
            else
            {
                _viewModel?.ResizeSelectedDesignItem(
                    handle,
                    _originalLeft,
                    _originalTop,
                    _originalWidth,
                    _originalHeight,
                    deltaX,
                    deltaY,
                    renderPreview: false);
            }

            UpdateSelectedInteractionVisuals();
            UpdateSelectedPreviewElement();
        }
        else if (_interactionMode == InteractionMode.PolygonVertex && _activePolygonVertexIndex.HasValue)
        {
            _viewModel?.MoveSelectedPolygonVertex(currentPosition, renderPreview: false);
            UpdateSelectedInteractionVisuals();
            UpdateSelectedPreviewElement();
        }

        e.Handled = true;
    }

    private void HandlePendingHitboxMove(double deltaX, double deltaY)
    {
        if (_viewModel?.SelectedDesignItem is null || _pendingHitCandidate is null)
        {
            return;
        }

        _hasExceededClickThreshold |= FrontedDesignerInteractionHelper.ExceedsClickThreshold(deltaX, deltaY);
        var action = FrontedDesignerInteractionHelper.ResolvePointerAction(
            _hasExceededClickThreshold,
            ReferenceEquals(_pendingHitCandidate, _viewModel.SelectedDesignItem),
            _hasStartedDrag);

        if (action == FrontedDesignerPointerAction.BeginDragSelected)
        {
            _viewModel.CaptureUndoSnapshot();
            _hasStartedDrag = true;
            _interactionMode = InteractionMode.Drag;
        }

        if (action is FrontedDesignerPointerAction.BeginDragSelected or FrontedDesignerPointerAction.DragSelected)
        {
            if (_originalSelectedBounds.Count > 1)
            {
                _viewModel.MoveSelectedDesignItems(_originalSelectedBounds, deltaX, deltaY, renderPreview: false);
            }
            else
            {
                _viewModel.MoveSelectedDesignItem(
                    _originalLeft,
                    _originalTop,
                    deltaX,
                    deltaY,
                    renderPreview: false);
            }

            UpdateSelectedInteractionVisuals();
            UpdateSelectedPreviewElement();
        }
    }

    private void HandlePendingGlobalScoreCellMove(double deltaX, double deltaY)
    {
        if (_viewModel?.SelectedGlobalScoreCell is null || _pendingGlobalScoreCellHitCandidate is null)
        {
            return;
        }

        _hasExceededClickThreshold |= FrontedDesignerInteractionHelper.ExceedsClickThreshold(deltaX, deltaY);
        var action = FrontedDesignerInteractionHelper.ResolvePointerAction(
            _hasExceededClickThreshold,
            true,
            _hasStartedDrag);

        if (action == FrontedDesignerPointerAction.BeginDragSelected)
        {
            _viewModel.CaptureUndoSnapshot();
            _hasStartedDrag = true;
            _interactionMode = InteractionMode.Drag;
        }

        if (action is FrontedDesignerPointerAction.BeginDragSelected or FrontedDesignerPointerAction.DragSelected)
        {
            _viewModel.MoveSelectedGlobalScoreCell(
                _originalLeft,
                _originalTop,
                deltaX,
                deltaY,
                renderPreview: false);
            UpdateSelectedInteractionVisuals();
        }
    }

    private void InteractionLayer_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPanningViewport)
        {
            EndViewportPan();
            e.Handled = true;
            return;
        }

        if (_capturedElement is null)
        {
            return;
        }

        _capturedElement.ReleaseMouseCapture();

        if (_pendingHitCandidate is not null)
        {
            if (!_hasExceededClickThreshold)
            {
                _viewModel?.SelectDesignItem(_pendingHitCandidate);
            }
            else if (_hasStartedDrag)
            {
                _viewModel?.CommitDesignItemGeometryEdit();
            }
        }
        else if (_pendingGlobalScoreCellHitCandidate is not null)
        {
            if (!_hasExceededClickThreshold)
            {
                _viewModel?.SelectGlobalScoreCell(
                    _pendingGlobalScoreCellHitCandidate.Parent,
                    _pendingGlobalScoreCellHitCandidate.Cell);
            }
            else if (_hasStartedDrag)
            {
                _viewModel?.CommitDesignItemGeometryEdit();
            }
        }
        else if (_isPendingEmptyClick)
        {
            if (!_hasExceededClickThreshold)
            {
                _viewModel?.ClearSelection();
            }
        }
        else if (_interactionMode == InteractionMode.Marquee)
        {
            CommitMarqueeSelection();
        }
        else if (_interactionMode == InteractionMode.Resize)
        {
            _viewModel?.CommitDesignItemGeometryEdit();
        }
        else if (_interactionMode == InteractionMode.PolygonVertex)
        {
            _viewModel?.CommitDesignItemGeometryEdit();
        }

        ResetPointerInteraction();
        ScheduleSelectedInteractionVisualRefresh();
        e.Handled = true;
    }

    private void InteractionLayer_OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPanningViewport)
        {
            EndViewportPan();
            e.Handled = true;
        }
    }

    private void PreviewScrollViewer_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        BeginViewportPan(e);
        e.Handled = true;
    }

    private void PreviewScrollViewer_OnPreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPanningViewport)
        {
            EndViewportPan();
            e.Handled = true;
        }
    }

    private void PreviewScrollViewer_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPanningViewport)
        {
            EndViewportPan();
            e.Handled = true;
        }
    }

    private void PreviewScrollViewer_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPanningViewport)
        {
            return;
        }

        UpdateViewportPan(e);
        e.Handled = true;
    }

    private void PreviewScrollViewer_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_viewModel is null || !Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            return;
        }

        var oldScale = _viewModel.ZoomScale;
        var cursorPosition = e.GetPosition(PreviewScrollViewer);
        var oldHorizontalOffset = PreviewScrollViewer.HorizontalOffset;
        var oldVerticalOffset = PreviewScrollViewer.VerticalOffset;

        _viewModel.ZoomByWheelDelta(e.Delta);
        PreviewScrollViewer.UpdateLayout();

        if (oldScale > 0D && Math.Abs(_viewModel.ZoomScale - oldScale) > 0.0001D)
        {
            var ratio = _viewModel.ZoomScale / oldScale;
            PreviewScrollViewer.ScrollToHorizontalOffset((oldHorizontalOffset + cursorPosition.X) * ratio - cursorPosition.X);
            PreviewScrollViewer.ScrollToVerticalOffset((oldVerticalOffset + cursorPosition.Y) * ratio - cursorPosition.Y);
        }

        e.Handled = true;
    }

    private void PreviewScrollViewer_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdatePreviewWorkspaceSize();
        _viewModel?.UpdateFitZoom(PreviewScrollViewer.ViewportWidth, PreviewScrollViewer.ViewportHeight);
        ResetPreviewScrollOffsetForFitMode();
    }

    /// <summary>
    /// Ensures Fit zoom is recalculated when the window is maximized or restored.
    /// StateChanged fires before layout updates, so the recalculation is deferred
    /// to after the layout pass via Dispatcher.Background.
    /// </summary>
    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Maximized || WindowState == WindowState.Normal)
        {
            Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Background,
                () =>
                {
                    _viewModel?.UpdateFitZoom(
                        PreviewScrollViewer.ViewportWidth,
                        PreviewScrollViewer.ViewportHeight);
                    ResetPreviewScrollOffsetForFitMode();
                });
        }
    }

    private void DesignSurface_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (_viewModel?.SelectedDesignItem is null || ShouldIgnoreKeyboardInput())
        {
            return;
        }

        var step = GetKeyboardMoveStep();
        var handled = true;
        switch (e.Key)
        {
            case Key.Left:
                _viewModel.MoveSelectedDesignItemBy(-step, 0);
                break;
            case Key.Right:
                _viewModel.MoveSelectedDesignItemBy(step, 0);
                break;
            case Key.Up:
                _viewModel.MoveSelectedDesignItemBy(0, -step);
                break;
            case Key.Down:
                _viewModel.MoveSelectedDesignItemBy(0, step);
                break;
            case Key.Delete:
                _viewModel.DeleteSelectedControlCommand.Execute(null);
                break;
            default:
                handled = false;
                break;
        }

        if (handled)
        {
            if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
            {
                UpdateSelectedInteractionVisuals();
                UpdateSelectedPreviewElement();
                RenderSnapGuides();
            }

            FocusDesignSurface();
            e.Handled = true;
        }
    }

    private void BeginInteraction(InteractionMode mode, Point startMousePosition, FrameworkElement element)
    {
        var item = _viewModel?.SelectedDesignItem;
        if (item is null)
        {
            return;
        }

        _viewModel?.CaptureUndoSnapshot();
        _interactionMode = mode;
        _startMousePosition = startMousePosition;
        _originalLeft = _viewModel?.SelectedGlobalScoreCell?.X
                        ?? _viewModel?.SelectedMapV2InternalPartLayout?.X
                        ?? item.Config.Left;
        _originalTop = _viewModel?.SelectedGlobalScoreCell?.Y
                       ?? _viewModel?.SelectedMapV2InternalPartLayout?.Y
                       ?? item.Config.Top;
        var bounds = ResolveItemBounds(item);
        if (_viewModel?.SelectedGlobalScoreCell is { } selectedCell
            && item.Config is GlobalScoreRowControlConfig)
        {
            bounds = new FrontedDesignerResolvedBounds(
                item.Config.Left + selectedCell.X,
                item.Config.Top + selectedCell.Y,
                selectedCell.Width,
                selectedCell.Height);
        }
        else if (_viewModel?.SelectedMapV2InternalPartLayout is { } internalPart
                 && item.Config is MapV2DisplayControlConfig)
        {
            bounds = ResolveMapV2InternalPartBounds(item, internalPart);
        }
        if (item.Config is BorderedImageFrontedControlConfig imageConfig
            && _viewModel?.BorderedImageResizeTarget == FrontedDesignerResizeTarget.Image)
        {
            imageConfig.ImageWidth ??= bounds.Width;
            imageConfig.ImageHeight ??= bounds.Height;
        }
        else if (!item.Config.Width.HasValue)
        {
            item.Config.Width = bounds.Width;

            if (!item.Config.Height.HasValue)
            {
                item.Config.Height = bounds.Height;
            }
        }
        else if (!item.Config.Height.HasValue)
        {
            item.Config.Height = bounds.Height;
        }

        _originalWidth = bounds.Width;
        _originalHeight = bounds.Height;
        CaptureOriginalSelectedBounds();
        _capturedElement = element;
        element.CaptureMouse();
    }

    private void BeginViewportPan(MouseEventArgs e)
    {
        if (_capturedElement is not null && _capturedElement.IsMouseCaptured)
        {
            _capturedElement.ReleaseMouseCapture();
        }

        ResetPointerInteraction();
        _isPanningViewport = true;
        _panStartViewportPosition = e.GetPosition(PreviewScrollViewer);
        _panStartHorizontalOffset = PreviewScrollViewer.HorizontalOffset;
        _panStartVerticalOffset = PreviewScrollViewer.VerticalOffset;
        _capturedElement = PreviewScrollViewer;
        _cursorBeforePan = PreviewScrollViewer.Cursor;
        PreviewScrollViewer.Cursor = Cursors.SizeAll;
        PreviewScrollViewer.CaptureMouse();
        FocusDesignSurface();
    }

    private void UpdateViewportPan(MouseEventArgs e)
    {
        var currentPosition = e.GetPosition(PreviewScrollViewer);
        var deltaX = currentPosition.X - _panStartViewportPosition.X;
        var deltaY = currentPosition.Y - _panStartViewportPosition.Y;
        PreviewScrollViewer.ScrollToHorizontalOffset(_panStartHorizontalOffset - deltaX);
        PreviewScrollViewer.ScrollToVerticalOffset(_panStartVerticalOffset - deltaY);
    }

    private void EndViewportPan()
    {
        if (_capturedElement is not null)
        {
            _capturedElement.ReleaseMouseCapture();
        }

        PreviewScrollViewer.Cursor = _cursorBeforePan;
        ResetPointerInteraction();
    }

    private void BeginPendingHitboxClick(
        FrontedControlDesignItem item,
        Point startMousePosition,
        FrameworkElement element)
    {
        ResetPointerInteraction();
        _pendingHitCandidate = item;
        _startMousePosition = startMousePosition;
        _originalLeft = _viewModel?.SelectedMapV2InternalPartLayout?.X ?? item.Config.Left;
        _originalTop = _viewModel?.SelectedMapV2InternalPartLayout?.Y ?? item.Config.Top;
        var bounds = _viewModel?.SelectedMapV2InternalPartLayout is { } internalPart
                     && item.Config is MapV2DisplayControlConfig
            ? ResolveMapV2InternalPartBounds(item, internalPart)
            : ResolveItemBounds(item);
        _originalWidth = bounds.Width;
        _originalHeight = bounds.Height;
        CaptureOriginalSelectedBounds();
        _capturedElement = element;
        element.CaptureMouse();
    }

    private void BeginPendingGlobalScoreCellClick(
        GlobalScoreCellHitTarget target,
        Point startMousePosition,
        FrameworkElement element)
    {
        ResetPointerInteraction();
        _pendingGlobalScoreCellHitCandidate = target;
        _startMousePosition = startMousePosition;
        _originalLeft = target.Cell.X;
        _originalTop = target.Cell.Y;
        _originalWidth = target.Cell.Width;
        _originalHeight = target.Cell.Height;
        _capturedElement = element;
        element.CaptureMouse();
    }

    private void BeginPendingEmptyClick(Point startMousePosition)
    {
        ResetPointerInteraction();
        _isPendingEmptyClick = true;
        _startMousePosition = startMousePosition;
        _capturedElement = InteractionLayer;
        InteractionLayer.CaptureMouse();
    }

    private void BeginMarqueeSelection(Point startMousePosition)
    {
        ResetPointerInteraction();
        _interactionMode = InteractionMode.Marquee;
        _startMousePosition = startMousePosition;
        _marqueeSelectionOutline = new Border
        {
            BorderBrush = TryFindResource("AccentFillColorDefaultBrush") as Brush ?? Brushes.DeepSkyBlue,
            BorderThickness = new Thickness(1D),
            Background = new SolidColorBrush(Color.FromArgb(32, 64, 200, 255)),
            IsHitTestVisible = false
        };
        Panel.SetZIndex(_marqueeSelectionOutline, FrontedDesignerEditorVisualHelper.SelectedOutlineZIndex + 2);
        InteractionLayer.Children.Add(_marqueeSelectionOutline);
        UpdateMarqueeSelection(startMousePosition);
        _capturedElement = InteractionLayer;
        InteractionLayer.CaptureMouse();
    }

    private void UpdateMarqueeSelection(Point currentPosition)
    {
        if (_marqueeSelectionOutline is null)
        {
            return;
        }

        var left = Math.Min(_startMousePosition.X, currentPosition.X);
        var top = Math.Min(_startMousePosition.Y, currentPosition.Y);
        var width = Math.Abs(currentPosition.X - _startMousePosition.X);
        var height = Math.Abs(currentPosition.Y - _startMousePosition.Y);
        _marqueeSelectionOutline.Width = width;
        _marqueeSelectionOutline.Height = height;
        Canvas.SetLeft(_marqueeSelectionOutline, left);
        Canvas.SetTop(_marqueeSelectionOutline, top);
    }

    private void CommitMarqueeSelection()
    {
        if (_viewModel is null)
        {
            return;
        }

        if (!_hasExceededClickThreshold)
        {
            _viewModel.ClearSelection();
            return;
        }

        if (_marqueeSelectionOutline is null)
        {
            return;
        }

        var marqueeBounds = new Rect(
            Canvas.GetLeft(_marqueeSelectionOutline),
            Canvas.GetTop(_marqueeSelectionOutline),
            _marqueeSelectionOutline.Width,
            _marqueeSelectionOutline.Height);
        var selectedItems = _hitboxes
            .Where(pair => marqueeBounds.IntersectsWith(new Rect(
                Canvas.GetLeft(pair.Value),
                Canvas.GetTop(pair.Value),
                pair.Value.Width,
                pair.Value.Height)))
            .Select(pair => pair.Key)
            .ToList();
        _viewModel.SelectDesignItems(selectedItems, selectedItems.LastOrDefault());
    }

    private void CaptureOriginalSelectedBounds()
    {
        _originalSelectedBounds.Clear();
        if (_viewModel is null)
        {
            return;
        }

        foreach (var item in _viewModel.SelectedDesignItems)
        {
            _originalSelectedBounds[item] = ResolveItemBounds(item);
        }
    }

    private void ResetPointerInteraction()
    {
        _viewModel?.ClearActiveSnapGuides();
        _capturedElement = null;
        _interactionMode = InteractionMode.None;
        _activeResizeHandle = null;
        _activePolygonVertexIndex = null;
        _pendingHitCandidate = null;
        _pendingGlobalScoreCellHitCandidate = null;
        _isPendingEmptyClick = false;
        _hasExceededClickThreshold = false;
        _hasStartedDrag = false;
        _isPanningViewport = false;
        _cursorBeforePan = null;
        _originalSelectedBounds.Clear();
        if (_marqueeSelectionOutline is not null)
        {
            InteractionLayer.Children.Remove(_marqueeSelectionOutline);
            _marqueeSelectionOutline = null;
        }
    }

    private void ScheduleSelectedInteractionVisualRefresh()
    {
        if (!_isLoaded || _viewModel?.SelectedDesignItem is null)
        {
            return;
        }

        Dispatcher.BeginInvoke(
            new Action(() =>
            {
                if (!_isLoaded || _viewModel?.SelectedDesignItem is null)
                {
                    return;
                }

                UpdateSelectedPreviewElement();
                UpdateSelectedInteractionVisuals();
                RenderSnapGuides();
            }),
            DispatcherPriority.Loaded);
    }

    private void UpdatePreviewWorkspaceSize()
    {
        PreviewWorkspace.MinWidth = Math.Max(1D, PreviewScrollViewer.ViewportWidth);
        PreviewWorkspace.MinHeight = Math.Max(1D, PreviewScrollViewer.ViewportHeight);
    }

    private void ResetPreviewScrollOffsetForFitMode()
    {
        if (_viewModel?.IsFitMode != true)
        {
            return;
        }

        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(() =>
            {
                PreviewScrollViewer.ScrollToHorizontalOffset(0D);
                PreviewScrollViewer.ScrollToVerticalOffset(0D);
            }));
    }

    private FrontedDesignerResolvedBounds ResolveItemBounds(FrontedControlDesignItem item)
    {
        var previewElement = FindPreviewElement(item.Name);
        if (item.Config is BorderedImageFrontedControlConfig imageConfig
            && _viewModel?.BorderedImageResizeTarget == FrontedDesignerResizeTarget.Image)
        {
            return ResolveBorderedImageInnerBounds(
                imageConfig,
                previewElement?.ActualWidth,
                previewElement?.ActualHeight);
        }

        return FrontedDesignerBoundsResolver.Resolve(
            item.Config,
            previewElement?.ActualWidth,
            previewElement?.ActualHeight);
    }

    private static FrontedDesignerResolvedBounds ResolveGlobalScoreCellBounds(
        FrontedControlDesignItem parent,
        GlobalScoreCellConfig cell) =>
        new(
            parent.Config.Left + cell.X,
            parent.Config.Top + cell.Y,
            Math.Max(FrontedDesignerGeometryHelper.MinHitWidth, cell.Width),
            Math.Max(FrontedDesignerGeometryHelper.MinHitHeight, cell.Height));

    private static FrontedDesignerResolvedBounds ResolveMapV2InternalPartBounds(
        FrontedControlDesignItem parent,
        MapV2InternalPartLayoutConfig part) =>
        new(
            parent.Config.Left + part.X,
            parent.Config.Top + part.Y,
            Math.Max(FrontedDesignerGeometryHelper.MinHitWidth, part.Width),
            Math.Max(FrontedDesignerGeometryHelper.MinHitHeight, part.Height));

    private static FrontedDesignerResolvedBounds ResolveBorderedImageInnerBounds(
        BorderedImageFrontedControlConfig config,
        double? actualWidth,
        double? actualHeight)
    {
        var borderBounds = FrontedDesignerBoundsResolver.Resolve(config, actualWidth, actualHeight);
        var imageWidth = config.ImageWidth ?? borderBounds.Width;
        var imageHeight = config.ImageHeight ?? borderBounds.Height;
        var offsetX = ResolveAlignedOffset(borderBounds.Width, imageWidth, config.HorizontalAlignment, isHorizontal: true);
        var offsetY = ResolveAlignedOffset(borderBounds.Height, imageHeight, config.VerticalAlignment, isHorizontal: false);

        return new FrontedDesignerResolvedBounds(
            borderBounds.Left + offsetX,
            borderBounds.Top + offsetY,
            imageWidth,
            imageHeight);
    }

    private static double ResolveAlignedOffset(double slotSize, double elementSize, string? alignment, bool isHorizontal)
    {
        var normalized = alignment?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = "Center";
        }

        return normalized switch
        {
            "Right" when isHorizontal => slotSize - elementSize,
            "Bottom" when !isHorizontal => slotSize - elementSize,
            "Center" => (slotSize - elementSize) / 2D,
            _ => 0D
        };
    }

    private void UpdateSelectedPreviewElement()
    {
        if (_viewModel is null)
        {
            return;
        }

        var selectedItems = _viewModel.SelectedDesignItems.Count > 0
            ? _viewModel.SelectedDesignItems
            : _viewModel.SelectedDesignItem is null
                ? []
                : [_viewModel.SelectedDesignItem];
        foreach (var item in selectedItems)
        {
            UpdatePreviewElement(item);
        }
    }

    private static void UpdateBorderedImageInnerPreviewElement(
        FrameworkElement rootElement,
        BorderedImageFrontedControlConfig config)
    {
        var innerImage = FindDescendant<System.Windows.Controls.Image>(rootElement);
        if (innerImage is null)
        {
            return;
        }

        if (config.ImageWidth.HasValue)
        {
            innerImage.Width = config.ImageWidth.Value;
        }

        if (config.ImageHeight.HasValue)
        {
            innerImage.Height = config.ImageHeight.Value;
        }
    }

    private void UpdatePreviewElement(FrontedControlDesignItem item, bool syncLinkedOverlays = true)
    {
        var element = FindPreviewElement(item.Name);
        if (element is null)
        {
            return;
        }

        ApplyPreviewElementGeometry(element, item);
        if (!syncLinkedOverlays)
        {
            return;
        }

        var bounds = ResolveItemBounds(item);
        var linkedOverlays = _viewModel?.SyncLinkedOverlays(item, bounds) ?? [];
        foreach (var linkedOverlay in linkedOverlays)
        {
            UpdatePreviewElement(linkedOverlay, syncLinkedOverlays: false);
        }
    }

    private FrameworkElement? FindPreviewElement(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return _previewElementsByControlName.TryGetValue(name, out var registeredElement)
            ? registeredElement
            : null;
    }

    private void EditTextBindingButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_bindingBrowserProvider is null
            || _viewModel is null
            || sender is not FrameworkElement { DataContext: FrontedPropertyEditorItem item })
        {
            return;
        }

        var window = new FrontedTextBindingEditorWindow(
            item.Value as Core.Models.FrontedLayout.Binding.FrontedTextBindingExpression,
            _bindingBrowserProvider)
        {
            Owner = this
        };

        if (window.ShowDialog() == true && window.Result is not null)
        {
            _viewModel.ApplyTextBindingEdit(item, window.Result);
        }
    }

    private static T? FindDescendant<T>(DependencyObject parent)
        where T : DependencyObject
    {
        var childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
            {
                return match;
            }

            var nested = FindDescendant<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private static T? FindVisibleDescendant<T>(DependencyObject parent)
        where T : FrameworkElement
    {
        if (parent is T { IsVisible: true } typed)
        {
            return typed;
        }

        var childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < childCount; i++)
        {
            var nested = FindVisibleDescendant<T>(VisualTreeHelper.GetChild(parent, i));
            if (nested != null)
            {
                return nested;
            }
        }

        if (parent is ContentControl { Content: DependencyObject content })
        {
            return FindVisibleDescendant<T>(content);
        }

        return null;
    }

    private void FocusDesignSurface()
    {
        InteractionLayer.Focus();
    }

    private double? GetViewportCenterX()
    {
        if (_viewModel?.CurrentDocument is null || _viewModel.ZoomScale <= 0D)
        {
            return null;
        }

        var value = (PreviewScrollViewer.HorizontalOffset + PreviewScrollViewer.ViewportWidth / 2D) / _viewModel.ZoomScale;
        return double.IsFinite(value) ? value : null;
    }

    private double? GetViewportCenterY()
    {
        if (_viewModel?.CurrentDocument is null || _viewModel.ZoomScale <= 0D)
        {
            return null;
        }

        var value = (PreviewScrollViewer.VerticalOffset + PreviewScrollViewer.ViewportHeight / 2D) / _viewModel.ZoomScale;
        return double.IsFinite(value) ? value : null;
    }

    private static Cursor GetCursor(FrontedDesignerResizeHandleKind handle)
    {
        return handle switch
        {
            FrontedDesignerResizeHandleKind.TopLeft
                or FrontedDesignerResizeHandleKind.BottomRight => Cursors.SizeNWSE,
            FrontedDesignerResizeHandleKind.TopRight
                or FrontedDesignerResizeHandleKind.BottomLeft => Cursors.SizeNESW,
            FrontedDesignerResizeHandleKind.Left
                or FrontedDesignerResizeHandleKind.Right => Cursors.SizeWE,
            FrontedDesignerResizeHandleKind.Top
                or FrontedDesignerResizeHandleKind.Bottom => Cursors.SizeNS,
            _ => Cursors.Arrow
        };
    }

    private double GetKeyboardMoveStep()
    {
        if (_viewModel?.EffectiveSnapEnabled == true)
        {
            return _viewModel.SnapGridSize;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            return 10D;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            return 1D;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
        {
            return 0.1D;
        }

        return FrontedDesignerGeometryHelper.CoordinateStep;
    }

    private static bool IsSpacePressed()
    {
        return Keyboard.IsKeyDown(Key.Space);
    }

    private static bool ShouldIgnoreKeyboardInput()
    {
        if (Keyboard.FocusedElement is not DependencyObject focused)
        {
            return false;
        }

        return FindAncestorOrSelf<System.Windows.Controls.TextBox>(focused) is not null
               || FindAncestorOrSelf<System.Windows.Controls.ComboBox>(focused) is not null
               || FindAncestorOrSelf<System.Windows.Controls.DataGrid>(focused) is not null
               || HasAncestorInNamespace(focused, "ColorPicker");
    }

    private static T? FindAncestorOrSelf<T>(DependencyObject current)
        where T : DependencyObject
    {
        var node = current;
        while (node is not null)
        {
            if (node is T match)
            {
                return match;
            }

            node = VisualTreeHelper.GetParent(node);
        }

        return null;
    }

    private static bool HasAncestorInNamespace(DependencyObject current, string namespacePrefix)
    {
        var node = current;
        while (node is not null)
        {
            if (node.GetType().Namespace?.StartsWith(namespacePrefix, StringComparison.Ordinal) == true)
            {
                return true;
            }

            node = VisualTreeHelper.GetParent(node);
        }

        return false;
    }

    private enum InteractionMode
    {
        None,
        Drag,
        Resize,
        PolygonVertex,
        Marquee
    }

    private sealed record GlobalScoreCellHitTarget(
        FrontedControlDesignItem Parent,
        GlobalScoreCellConfig Cell);
}
