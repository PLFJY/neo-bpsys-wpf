using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.ViewModels.Windows;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Views.Windows;

/// <summary>
/// Interaction logic for FrontedDesignerWindow.xaml.
/// </summary>
public partial class FrontedDesignerWindow : FluentWindow
{
    private readonly IFrontedRenderer? _renderer;
    private readonly ILogger<FrontedDesignerWindow>? _logger;
    private bool _isLoaded;
    private FrontedDesignerWindowViewModel? _viewModel;
    private readonly Dictionary<FrontedControlDesignItem, Border> _hitboxes = new();
    private readonly Dictionary<FrontedDesignerResizeHandleKind, Border> _resizeHandles = new();
    private Border? _selectionOutline;
    private Border? _selectionLabel;
    private FrameworkElement? _capturedElement;
    private InteractionMode _interactionMode = InteractionMode.None;
    private FrontedDesignerResizeHandleKind? _activeResizeHandle;
    private FrontedControlDesignItem? _pendingHitCandidate;
    private bool _isPendingEmptyClick;
    private bool _hasExceededClickThreshold;
    private bool _hasStartedDrag;
    private Point _startMousePosition;
    private double _originalLeft;
    private double _originalTop;
    private double _originalWidth;
    private double _originalHeight;

    public FrontedDesignerWindow()
    {
        InitializeComponent();
    }

    public FrontedDesignerWindow(
        FrontedDesignerWindowViewModel viewModel,
        IFrontedRenderer renderer,
        ILogger<FrontedDesignerWindow> logger)
    {
        _renderer = renderer;
        _logger = logger;

        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        AttachViewModel();
        _viewModel?.ReloadLayoutCommand.Execute(null);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PreviewRenderRequested -= OnPreviewRenderRequested;
            _viewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
        }
    }

    private void Selector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        _viewModel?.ReloadLayoutCommand.Execute(null);
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
        _viewModel.PropertyChanged += ViewModel_OnPropertyChanged;
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FrontedDesignerWindowViewModel.SelectedDesignItem))
        {
            RebuildInteractionLayer();
            FocusDesignSurface();
        }
    }

    private void OnPreviewRenderRequested(
        object? sender,
        FrontedDesignerPreviewRenderRequestedEventArgs e)
    {
        if (_renderer is null || e.Config is null || e.Context is null)
        {
            ClearPreviewCanvas();
            return;
        }

        try
        {
            ConfigureDesignSurface(e.Config.CanvasWidth, e.Config.CanvasHeight);
            _renderer.RenderToCanvas(PreviewCanvas, e.Config, e.Context);
            PreviewCanvas.UpdateLayout();
            RebuildInteractionLayer();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to render fronted designer preview.");
            ClearPreviewCanvas();
            _viewModel?.ReportRenderFailure(ex);
        }
    }

    private void ClearPreviewCanvas()
    {
        PreviewCanvas.Children.Clear();
        PreviewCanvas.Background = null;
        ConfigureDesignSurface(640, 360);
        InteractionLayer.Children.Clear();
        _hitboxes.Clear();
        _resizeHandles.Clear();
        _selectionOutline = null;
        _selectionLabel = null;
        ResetPointerInteraction();
    }

    private void ConfigureDesignSurface(double width, double height)
    {
        DesignSurfaceGrid.Width = width;
        DesignSurfaceGrid.Height = height;
        PreviewCanvas.Width = width;
        PreviewCanvas.Height = height;
        InteractionLayer.Width = width;
        InteractionLayer.Height = height;
    }

    private void RebuildInteractionLayer()
    {
        InteractionLayer.Children.Clear();
        _hitboxes.Clear();
        _resizeHandles.Clear();
        _selectionOutline = null;
        _selectionLabel = null;

        if (_viewModel?.CurrentDocument is null)
        {
            return;
        }

        foreach (var entry in _viewModel.CurrentDocument.Controls.Select((item, index) => new { Item = item, Index = index }))
        {
            var hitbox = CreateHitbox(entry.Item, entry.Index);
            _hitboxes[entry.Item] = hitbox;
            InteractionLayer.Children.Add(hitbox);
        }

        if (_viewModel.SelectedDesignItem is not null)
        {
            AddSelectionAdorner(_viewModel.SelectedDesignItem);
        }
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
        var bounds = ResolveItemBounds(item);

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
                Text = item.Name,
                FontSize = 11,
                Foreground = Brushes.White
            },
            IsHitTestVisible = false
        };
        Canvas.SetLeft(_selectionLabel, bounds.Left);
        Canvas.SetTop(_selectionLabel, Math.Max(0, bounds.Top - 18));
        Panel.SetZIndex(_selectionLabel, FrontedDesignerEditorVisualHelper.SelectedOutlineZIndex + 1);
        InteractionLayer.Children.Add(_selectionLabel);

        foreach (var handle in Enum.GetValues<FrontedDesignerResizeHandleKind>())
        {
            var handleElement = CreateResizeHandle(handle);
            _resizeHandles[handle] = handleElement;
            InteractionLayer.Children.Add(handleElement);
        }

        UpdateSelectedInteractionVisuals();
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

        var bounds = ResolveItemBounds(item);

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

        if (_selectionLabel is not null)
        {
            Canvas.SetLeft(_selectionLabel, bounds.Left);
            Canvas.SetTop(_selectionLabel, Math.Max(0, bounds.Top - 18));
        }

        SetHandlePosition(FrontedDesignerResizeHandleKind.TopLeft, bounds.Left, bounds.Top);
        SetHandlePosition(FrontedDesignerResizeHandleKind.Top, bounds.Left + bounds.Width / 2, bounds.Top);
        SetHandlePosition(FrontedDesignerResizeHandleKind.TopRight, bounds.Left + bounds.Width, bounds.Top);
        SetHandlePosition(FrontedDesignerResizeHandleKind.Left, bounds.Left, bounds.Top + bounds.Height / 2);
        SetHandlePosition(FrontedDesignerResizeHandleKind.Right, bounds.Left + bounds.Width, bounds.Top + bounds.Height / 2);
        SetHandlePosition(FrontedDesignerResizeHandleKind.BottomLeft, bounds.Left, bounds.Top + bounds.Height);
        SetHandlePosition(FrontedDesignerResizeHandleKind.Bottom, bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height);
        SetHandlePosition(FrontedDesignerResizeHandleKind.BottomRight, bounds.Left + bounds.Width, bounds.Top + bounds.Height);
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
        BeginPendingHitboxClick(item, e.GetPosition(InteractionLayer), hitbox);
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

    private void InteractionLayer_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, InteractionLayer))
        {
            FocusDesignSurface();
            BeginPendingEmptyClick(e.GetPosition(InteractionLayer));
            e.Handled = true;
        }
    }

    private void DesignSurface_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        FocusDesignSurface();
    }

    private void InteractionLayer_OnMouseMove(object sender, MouseEventArgs e)
    {
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
        else if (_isPendingEmptyClick)
        {
            _hasExceededClickThreshold |= FrontedDesignerInteractionHelper.ExceedsClickThreshold(deltaX, deltaY);
        }
        else if (_interactionMode == InteractionMode.Resize && _activeResizeHandle is { } handle)
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
            _hasStartedDrag = true;
            _interactionMode = InteractionMode.Drag;
        }

        if (action is FrontedDesignerPointerAction.BeginDragSelected or FrontedDesignerPointerAction.DragSelected)
        {
            _viewModel.MoveSelectedDesignItem(
                _originalLeft,
                _originalTop,
                deltaX,
                deltaY,
                renderPreview: false);
            UpdateSelectedInteractionVisuals();
            UpdateSelectedPreviewElement();
        }
    }

    private void InteractionLayer_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
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
        else if (_isPendingEmptyClick)
        {
            if (!_hasExceededClickThreshold)
            {
                _viewModel?.ClearSelection();
            }
        }
        else if (_interactionMode == InteractionMode.Resize)
        {
            _viewModel?.CommitDesignItemGeometryEdit();
        }

        ResetPointerInteraction();
        e.Handled = true;
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
            default:
                handled = false;
                break;
        }

        if (handled)
        {
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

        _interactionMode = mode;
        _startMousePosition = startMousePosition;
        _originalLeft = item.Config.Left;
        _originalTop = item.Config.Top;
        var bounds = ResolveItemBounds(item);
        if (!item.Config.Width.HasValue)
        {
            item.Config.Width = bounds.Width;
        }

        if (!item.Config.Height.HasValue)
        {
            item.Config.Height = bounds.Height;
        }

        _originalWidth = bounds.Width;
        _originalHeight = bounds.Height;
        _capturedElement = element;
        element.CaptureMouse();
    }

    private void BeginPendingHitboxClick(
        FrontedControlDesignItem item,
        Point startMousePosition,
        FrameworkElement element)
    {
        ResetPointerInteraction();
        _pendingHitCandidate = item;
        _startMousePosition = startMousePosition;
        _originalLeft = item.Config.Left;
        _originalTop = item.Config.Top;
        var bounds = ResolveItemBounds(item);
        _originalWidth = bounds.Width;
        _originalHeight = bounds.Height;
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

    private void ResetPointerInteraction()
    {
        _capturedElement = null;
        _interactionMode = InteractionMode.None;
        _activeResizeHandle = null;
        _pendingHitCandidate = null;
        _isPendingEmptyClick = false;
        _hasExceededClickThreshold = false;
        _hasStartedDrag = false;
    }

    private FrontedDesignerResolvedBounds ResolveItemBounds(FrontedControlDesignItem item)
    {
        var previewElement = FindPreviewElement(item.Name);
        return FrontedDesignerBoundsResolver.Resolve(
            item.Config,
            previewElement?.ActualWidth,
            previewElement?.ActualHeight);
    }

    private void UpdateSelectedPreviewElement()
    {
        var item = _viewModel?.SelectedDesignItem;
        if (item is null)
        {
            return;
        }

        var element = FindPreviewElement(item.Name);
        if (element is null)
        {
            return;
        }

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
    }

    private FrameworkElement? FindPreviewElement(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        if (PreviewCanvas.FindName(name) is FrameworkElement canvasNameMatch)
        {
            return canvasNameMatch;
        }

        if (Window.GetWindow(PreviewCanvas) is FrameworkElement window
            && window.FindName(name) is FrameworkElement windowNameMatch)
        {
            return windowNameMatch;
        }

        return FindGeneratedPreviewElement(PreviewCanvas, name);
    }

    private static FrameworkElement? FindGeneratedPreviewElement(DependencyObject parent, string name)
    {
        var childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is FrameworkElement element
                && (element.Name == name || FrontedRendererProperties.GetRegisteredName(element) == name))
            {
                return element;
            }

            var nested = FindGeneratedPreviewElement(child, name);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private void FocusDesignSurface()
    {
        InteractionLayer.Focus();
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

    private static double GetKeyboardMoveStep()
    {
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

    private static bool ShouldIgnoreKeyboardInput()
    {
        if (Keyboard.FocusedElement is not DependencyObject focused)
        {
            return false;
        }

        return FindAncestorOrSelf<System.Windows.Controls.TextBox>(focused) is not null
               || FindAncestorOrSelf<System.Windows.Controls.ComboBox>(focused) is not null
               || FindAncestorOrSelf<System.Windows.Controls.DataGrid>(focused) is not null;
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

    private enum InteractionMode
    {
        None,
        Drag,
        Resize
    }
}
