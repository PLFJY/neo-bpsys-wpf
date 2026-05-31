using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.ViewModels.Windows;
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

        foreach (var item in _viewModel.CurrentDocument.Controls)
        {
            var hitbox = CreateHitbox(item);
            _hitboxes[item] = hitbox;
            InteractionLayer.Children.Add(hitbox);
        }

        if (_viewModel.SelectedDesignItem is not null)
        {
            AddSelectionAdorner(_viewModel.SelectedDesignItem);
        }
    }

    private Border CreateHitbox(FrontedControlDesignItem item)
    {
        var hitbox = new Border
        {
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(1),
            Width = FrontedDesignerGeometryHelper.GetEditableWidth(item.Config),
            Height = FrontedDesignerGeometryHelper.GetEditableHeight(item.Config),
            IsHitTestVisible = true,
            Tag = item
        };

        Canvas.SetLeft(hitbox, item.Config.Left);
        Canvas.SetTop(hitbox, item.Config.Top);
        Panel.SetZIndex(hitbox, 10_000);
        hitbox.MouseLeftButtonDown += Hitbox_OnMouseLeftButtonDown;
        return hitbox;
    }

    private void AddSelectionAdorner(FrontedControlDesignItem item)
    {
        var left = item.Config.Left;
        var top = item.Config.Top;
        var width = FrontedDesignerGeometryHelper.GetEditableWidth(item.Config);
        var height = FrontedDesignerGeometryHelper.GetEditableHeight(item.Config);

        _selectionOutline = new Border
        {
            Width = width,
            Height = height,
            BorderBrush = TryFindResource("AccentFillColorDefaultBrush") as Brush ?? Brushes.DeepSkyBlue,
            BorderThickness = new Thickness(1.5),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(_selectionOutline, left);
        Canvas.SetTop(_selectionOutline, top);
        Panel.SetZIndex(_selectionOutline, 10_100);
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
        Canvas.SetLeft(_selectionLabel, left);
        Canvas.SetTop(_selectionLabel, Math.Max(0, top - 20));
        Panel.SetZIndex(_selectionLabel, 10_101);
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
            Width = 8,
            Height = 8,
            Background = TryFindResource("AccentFillColorDefaultBrush") as Brush ?? Brushes.DeepSkyBlue,
            BorderBrush = Brushes.White,
            BorderThickness = new Thickness(1),
            Cursor = GetCursor(handle),
            Tag = handle
        };

        Panel.SetZIndex(element, 10_102);
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

        var left = item.Config.Left;
        var top = item.Config.Top;
        var width = FrontedDesignerGeometryHelper.GetEditableWidth(item.Config);
        var height = FrontedDesignerGeometryHelper.GetEditableHeight(item.Config);

        if (_hitboxes.TryGetValue(item, out var hitbox))
        {
            hitbox.Width = width;
            hitbox.Height = height;
            Canvas.SetLeft(hitbox, left);
            Canvas.SetTop(hitbox, top);
        }

        if (_selectionOutline is not null)
        {
            _selectionOutline.Width = width;
            _selectionOutline.Height = height;
            Canvas.SetLeft(_selectionOutline, left);
            Canvas.SetTop(_selectionOutline, top);
        }

        if (_selectionLabel is not null)
        {
            Canvas.SetLeft(_selectionLabel, left);
            Canvas.SetTop(_selectionLabel, Math.Max(0, top - 20));
        }

        SetHandlePosition(FrontedDesignerResizeHandleKind.TopLeft, left, top);
        SetHandlePosition(FrontedDesignerResizeHandleKind.Top, left + width / 2, top);
        SetHandlePosition(FrontedDesignerResizeHandleKind.TopRight, left + width, top);
        SetHandlePosition(FrontedDesignerResizeHandleKind.Left, left, top + height / 2);
        SetHandlePosition(FrontedDesignerResizeHandleKind.Right, left + width, top + height / 2);
        SetHandlePosition(FrontedDesignerResizeHandleKind.BottomLeft, left, top + height);
        SetHandlePosition(FrontedDesignerResizeHandleKind.Bottom, left + width / 2, top + height);
        SetHandlePosition(FrontedDesignerResizeHandleKind.BottomRight, left + width, top + height);
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
        _viewModel.SelectDesignItem(item);
        RebuildInteractionLayer();
        BeginInteraction(InteractionMode.Drag, e.GetPosition(InteractionLayer), _hitboxes[item]);
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
            _viewModel?.ClearSelection();
            RebuildInteractionLayer();
            FocusDesignSurface();
            e.Handled = true;
        }
    }

    private void DesignSurface_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        FocusDesignSurface();
    }

    private void InteractionLayer_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_viewModel?.SelectedDesignItem is null
            || _capturedElement is null
            || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var currentPosition = e.GetPosition(InteractionLayer);
        var deltaX = currentPosition.X - _startMousePosition.X;
        var deltaY = currentPosition.Y - _startMousePosition.Y;

        if (_interactionMode == InteractionMode.Drag)
        {
            _viewModel.MoveSelectedDesignItem(_originalLeft, _originalTop, deltaX, deltaY, renderPreview: false);
        }
        else if (_interactionMode == InteractionMode.Resize && _activeResizeHandle is { } handle)
        {
            _viewModel.ResizeSelectedDesignItem(
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
        e.Handled = true;
    }

    private void InteractionLayer_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_capturedElement is null)
        {
            return;
        }

        _capturedElement.ReleaseMouseCapture();
        _capturedElement = null;
        _interactionMode = InteractionMode.None;
        _activeResizeHandle = null;
        _viewModel?.CommitDesignItemGeometryEdit();
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
        _originalWidth = FrontedDesignerGeometryHelper.GetEditableWidth(item.Config);
        _originalHeight = FrontedDesignerGeometryHelper.GetEditableHeight(item.Config);
        _capturedElement = element;
        element.CaptureMouse();
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
