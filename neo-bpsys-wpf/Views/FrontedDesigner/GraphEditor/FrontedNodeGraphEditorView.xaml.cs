using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using neo_bpsys_wpf.ViewModels.FrontedDesigner.GraphEditor;

namespace neo_bpsys_wpf.Views.FrontedDesigner.GraphEditor;

public partial class FrontedNodeGraphEditorView : UserControl
{
    private const string CatalogNodeDragFormat = "neo-bpsys-wpf.fronted-node-type";
    private const string CatalogNodeDragTokenFormat = "neo-bpsys-wpf.fronted-node-drag-token";
    private const double ZoomMin = 0.2;
    private const double ZoomMax = 1.5;
    private const double ZoomStep = 0.1;
    private FrontedNodePortViewModel? _dragSourcePort;
    private Point _dragStartPoint;
    private Point? _catalogDragStartPoint;
    private string? _activeCatalogDragToken;
    private bool _isPanning;
    private Point _panStartPoint;
    private double _panStartHorizontalOffset;
    private double _panStartVerticalOffset;
    private double _zoomLevel = 1.0;
    private FrontedNodeGraphEditorViewModel? _subscribedViewModel;
    private bool _isMinimapDragging;
    private const double PreviewZoomMax = 3.0;
    private const double PreviewZoomMin = 0.2;
    private double _previewZoomLevel = 1.0;
    private bool _previewIsPanning;
    private Point _previewPanStartPoint;
    private double _previewPanStartTranslateX;
    private double _previewPanStartTranslateY;

    public FrontedNodeGraphEditorView()
    {
        InitializeComponent();
        PopulateZoomCombo(EditorZoomCombo, ZoomMax);
        PopulateZoomCombo(PreviewZoomCombo, PreviewZoomMax);
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
            _subscribedViewModel = null;
        }

        if (DataContext is FrontedNodeGraphEditorViewModel viewModel)
        {
            _subscribedViewModel = viewModel;
            viewModel.PropertyChanged += ViewModel_OnPropertyChanged;
            UpdatePreviewVisual(viewModel.PreviewRoot);
        }

        // 加载新节点后居中视野
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)CenterGraphView);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is FrontedNodeGraphEditorViewModel viewModel)
        {
            UpdatePreviewVisual(viewModel.PreviewRoot);
        }

        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)CenterGraphView);
    }

    private void CenterGraphView()
    {
        var scrollableW = GraphScrollViewer.ScrollableWidth;
        var scrollableH = GraphScrollViewer.ScrollableHeight;
        GraphScrollViewer.ScrollToHorizontalOffset(Math.Max(0, scrollableW / 2));
        GraphScrollViewer.ScrollToVerticalOffset(Math.Max(0, scrollableH / 2));
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FrontedNodeGraphEditorViewModel.PreviewRoot)
            && DataContext is FrontedNodeGraphEditorViewModel viewModel)
        {
            UpdatePreviewVisual(viewModel.PreviewRoot);
        }
    }

    private void UpdatePreviewVisual(FrameworkElement? root)
    {
        if (root is not null)
        {
            PreviewVisualBrush.Visual = root;
            PreviewViewport.Visibility = Visibility.Visible;
            PreviewPlaceholder.Visibility = Visibility.Collapsed;
        }
        else
        {
            PreviewVisualBrush.Visual = null;
            PreviewViewport.Visibility = Visibility.Collapsed;
            PreviewPlaceholder.Visibility = Visibility.Visible;
        }
    }

    private void NodeCard_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: FrontedNodeEditorViewModel node }
            && DataContext is FrontedNodeGraphEditorViewModel editor)
        {
            editor.SelectNode(node);
            Focus();
        }
    }

    private void NodeCard_OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: FrontedNodeEditorViewModel node }
            && DataContext is FrontedNodeGraphEditorViewModel editor)
        {
            editor.SelectNode(node);
        }
    }

    private void DeleteNodeMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is FrontedNodeGraphEditorViewModel editor)
        {
            editor.DeleteNode(editor.SelectedNode);
        }
    }

    private void NodeHeader_OnDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (sender is Thumb { DataContext: FrontedNodeEditorViewModel node }
            && DataContext is FrontedNodeGraphEditorViewModel editor)
        {
            editor.MoveNode(node, node.X + e.HorizontalChange, node.Y + e.VerticalChange);
        }
    }

    private void CatalogItem_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _catalogDragStartPoint = e.GetPosition(this);
    }

    private void CatalogItem_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _catalogDragStartPoint = null;
    }

    private void CatalogItem_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_catalogDragStartPoint is null
            || e.LeftButton != MouseButtonState.Pressed
            || sender is not FrameworkElement { DataContext: FrontedNodeCatalogItemViewModel item })
        {
            return;
        }

        var current = e.GetPosition(this);
        if (Math.Abs(current.X - _catalogDragStartPoint.Value.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(current.Y - _catalogDragStartPoint.Value.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _activeCatalogDragToken = Guid.NewGuid().ToString("N");
        var data = new DataObject();
        data.SetData(CatalogNodeDragFormat, item.NodeType);
        data.SetData(CatalogNodeDragTokenFormat, _activeCatalogDragToken);

        try
        {
            DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Copy);
        }
        finally
        {
            _activeCatalogDragToken = null;
            _catalogDragStartPoint = null;
        }

        e.Handled = true;
    }

    private void GraphCanvas_OnDrop(object sender, DragEventArgs e)
    {
        if (!IsCurrentCatalogDrag(e)
            || !e.Data.GetDataPresent(CatalogNodeDragFormat)
            || e.Data.GetData(CatalogNodeDragFormat) is not string nodeType
            || DataContext is not FrontedNodeGraphEditorViewModel editor)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        var point = e.GetPosition(GraphCanvas);
        editor.AddNodeAt(nodeType, point.X - FrontedNodeEditorViewModel.Width / 2D, point.Y - 18D);
        Focus();
        e.Handled = true;
    }

    private void GraphCanvas_OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = IsCurrentCatalogDrag(e)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OutputPort_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: FrontedNodePortViewModel port })
        {
            return;
        }

        _dragSourcePort = port;
        _dragStartPoint = GetPortPoint(sender, isOutput: true);
        ConnectionPreviewPath.Data = Geometry.Parse(CreateBezierPathData(_dragStartPoint, _dragStartPoint));
        ConnectionPreviewPath.Visibility = Visibility.Visible;
        GraphCanvas.CaptureMouse();
        e.Handled = true;
    }

    private void InputPort_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragSourcePort is null
            || sender is not FrameworkElement { DataContext: FrontedNodePortViewModel target }
            || DataContext is not FrontedNodeGraphEditorViewModel editor)
        {
            return;
        }

        editor.AddConnection(_dragSourcePort, target);
        EndConnectionDrag();
        e.Handled = true;
    }

    private void GraphScrollViewer_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_dragSourcePort is not null || !CanStartPan(e.OriginalSource) || IsWithinMinimap(e.OriginalSource))
        {
            return;
        }

        if (e.ChangedButton is not (MouseButton.Left or MouseButton.Right))
        {
            return;
        }

        _isPanning = true;
        _panStartPoint = e.GetPosition(GraphScrollViewer);
        _panStartHorizontalOffset = GraphScrollViewer.HorizontalOffset;
        _panStartVerticalOffset = GraphScrollViewer.VerticalOffset;
        GraphScrollViewer.CaptureMouse();
        GraphScrollViewer.Cursor = Cursors.Hand;
        Focus();
        e.Handled = true;
    }

    private void GraphScrollViewer_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_isPanning)
        {
            if (e.LeftButton != MouseButtonState.Pressed && e.RightButton != MouseButtonState.Pressed)
            {
                EndPan();
                return;
            }

            var panPoint = e.GetPosition(GraphScrollViewer);
            GraphScrollViewer.ScrollToHorizontalOffset(_panStartHorizontalOffset - (panPoint.X - _panStartPoint.X));
            GraphScrollViewer.ScrollToVerticalOffset(_panStartVerticalOffset - (panPoint.Y - _panStartPoint.Y));
            e.Handled = true;
            return;
        }

        if (_dragSourcePort is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(GraphCanvas);
        ConnectionPreviewPath.Data = Geometry.Parse(CreateBezierPathData(_dragStartPoint, current));
        e.Handled = true;
    }

    private void GraphScrollViewer_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
        {
            e.Handled = true;
            ApplyEditorZoom(_zoomLevel + (e.Delta > 0 ? ZoomStep : -ZoomStep));
        }
    }

    private void GraphScrollViewer_OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        UpdateMinimapViewport();
    }

    private void GraphScrollViewer_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPanning)
        {
            EndPan();
            e.Handled = true;
            return;
        }

        if (_dragSourcePort is null)
        {
            return;
        }

        var target = FindPortAt(e.GetPosition(GraphCanvas));
        if (target is not null && DataContext is FrontedNodeGraphEditorViewModel editor)
        {
            editor.AddConnection(_dragSourcePort, target);
        }

        EndConnectionDrag();
        e.Handled = true;
    }

    private void GraphScrollViewer_OnPreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isPanning)
        {
            return;
        }

        EndPan();
        e.Handled = true;
    }

    private void GraphScrollViewer_OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (_isPanning && e.LeftButton != MouseButtonState.Pressed && e.RightButton != MouseButtonState.Pressed)
        {
            EndPan();
        }
    }

    private void FrontedNodeGraphEditorView_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Delete || DataContext is not FrontedNodeGraphEditorViewModel editor)
        {
            return;
        }

        editor.DeleteNode(editor.SelectedNode);
        e.Handled = true;
    }

    private void EndPan()
    {
        _isPanning = false;
        GraphScrollViewer.Cursor = null;
        if (GraphScrollViewer.IsMouseCaptured)
        {
            GraphScrollViewer.ReleaseMouseCapture();
        }
    }

    private void EndConnectionDrag()
    {
        _dragSourcePort = null;
        ConnectionPreviewPath.Data = null;
        ConnectionPreviewPath.Visibility = Visibility.Collapsed;
        if (GraphCanvas.IsMouseCaptured)
        {
            GraphCanvas.ReleaseMouseCapture();
        }
    }

    private void DuplicateNodeMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is FrontedNodeGraphEditorViewModel editor)
        {
            editor.DuplicateSelectedNodeCommand.Execute(null);
        }
    }

    private void ConnectionPath_OnMouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is DependencyObject element)
        {
            ShowConnectionDeleteButton(element, true);
        }
    }

    private void ConnectionPath_OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is DependencyObject element)
        {
            ShowConnectionDeleteButton(element, false);
        }
    }

    private void ConnectionDeleteBtn_OnMouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is DependencyObject element)
        {
            ShowConnectionDeleteButton(element, true);
        }
    }

    private void ConnectionDeleteBtn_OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is DependencyObject element)
        {
            // 延迟隐藏，允许鼠标移回 Path
            var dispatcherTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
            dispatcherTimer.Tick += (_, _) =>
            {
                dispatcherTimer.Stop();
                ShowConnectionDeleteButton(element, false);
            };
            dispatcherTimer.Start();
        }
    }

    private static void ShowConnectionDeleteButton(DependencyObject element, bool show)
    {
        var parent = VisualTreeHelper.GetParent(element) as Panel;
        var button = parent?.Children.OfType<ButtonBase>().FirstOrDefault();
        if (button is not null)
        {
            button.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private static bool IsWithinMinimap(object? originalSource)
    {
        var current = originalSource as DependencyObject;
        while (current is not null)
        {
            if (current is Canvas canvas && canvas.Name is "Minimap")
            {
                return true;
            }
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }

    private void PreviewContent_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!Keyboard.IsKeyDown(Key.LeftCtrl) && !Keyboard.IsKeyDown(Key.RightCtrl))
        {
            return;
        }

        e.Handled = true;
        ApplyPreviewZoom(_previewZoomLevel + (e.Delta > 0 ? 0.1 : -0.1));
    }

    private void PreviewContent_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        // 子元素（Button/ComboBox）已处理的事件不触发平移
        if (e.Handled) return;

        if (e.ChangedButton is not (MouseButton.Left or MouseButton.Right))
        {
            return;
        }

        _previewIsPanning = true;
        _previewPanStartPoint = e.GetPosition(PreviewBorder);
        _previewPanStartTranslateX = PreviewTranslateTransform.X;
        _previewPanStartTranslateY = PreviewTranslateTransform.Y;
        PreviewBorder.CaptureMouse();
        PreviewBorder.Cursor = Cursors.Hand;
        e.Handled = true;
    }

    private void PreviewContent_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_previewIsPanning)
        {
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed && e.RightButton != MouseButtonState.Pressed)
        {
            EndPreviewPan();
            return;
        }

        var pos = e.GetPosition(PreviewBorder);
        PreviewTranslateTransform.X = _previewPanStartTranslateX + (pos.X - _previewPanStartPoint.X);
        PreviewTranslateTransform.Y = _previewPanStartTranslateY + (pos.Y - _previewPanStartPoint.Y);
        ClampPreviewPan();
        e.Handled = true;
    }

    private void ClampPreviewPan()
    {
        var viewW = PreviewBorder.ActualWidth;
        var viewH = PreviewBorder.ActualHeight;
        var contentW = viewW * _previewZoomLevel;
        var contentH = viewH * _previewZoomLevel;

        var maxX = Math.Max(0, contentW - viewW);
        var maxY = Math.Max(0, contentH - viewH);

        PreviewTranslateTransform.X = Math.Clamp(PreviewTranslateTransform.X, -maxX, 0);
        PreviewTranslateTransform.Y = Math.Clamp(PreviewTranslateTransform.Y, -maxY, 0);
    }

    private void PreviewContent_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_previewIsPanning)
        {
            return;
        }

        EndPreviewPan();
        e.Handled = true;
    }

    private void PreviewContent_OnPreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_previewIsPanning)
        {
            return;
        }

        EndPreviewPan();
        e.Handled = true;
    }

    private void PreviewContent_OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (_previewIsPanning && e.LeftButton != MouseButtonState.Pressed && e.RightButton != MouseButtonState.Pressed)
        {
            EndPreviewPan();
        }
    }

    private void EndPreviewPan()
    {
        _previewIsPanning = false;
        PreviewBorder.Cursor = null;
        if (PreviewBorder.IsMouseCaptured)
        {
            PreviewBorder.ReleaseMouseCapture();
        }
    }

    private static void PopulateZoomCombo(ComboBox combo, double max)
    {
        combo.Items.Clear();
        combo.Items.Add(0.25);
        combo.Items.Add(0.50);
        combo.Items.Add(0.75);
        combo.Items.Add(1.00);
        if (max > 1.25) combo.Items.Add(1.25);
        if (max > 1.50) combo.Items.Add(1.50);
        if (max > 1.50) combo.Items.Add(2.00);
        if (max > 2.50) combo.Items.Add(3.00);
    }

    private void ApplyEditorZoom(double zoom)
    {
        _zoomLevel = Math.Clamp(zoom, ZoomMin, ZoomMax);
        ZoomContainer.LayoutTransform = new ScaleTransform(_zoomLevel, _zoomLevel);
        UpdateEditorZoomCombo();
        UpdateMinimapViewport();
    }

    private void UpdateEditorZoomCombo()
    {
        var items = EditorZoomCombo.Items.Cast<double>().ToList();
        var nearest = items.OrderBy(v => Math.Abs(v - _zoomLevel)).First();
        EditorZoomCombo.SelectedItem = nearest;
    }

    private void ApplyPreviewZoom(double zoom)
    {
        _previewZoomLevel = Math.Clamp(zoom, PreviewZoomMin, PreviewZoomMax);
        PreviewScaleTransform.ScaleX = _previewZoomLevel;
        PreviewScaleTransform.ScaleY = _previewZoomLevel;
        UpdatePreviewZoomCombo();
    }

    private void UpdatePreviewZoomCombo()
    {
        var items = PreviewZoomCombo.Items.Cast<double>().ToList();
        var nearest = items.OrderBy(v => Math.Abs(v - _previewZoomLevel)).First();
        PreviewZoomCombo.SelectedItem = nearest;
    }

    private void EditorZoomIn_OnClick(object sender, RoutedEventArgs e)
    {
        ApplyEditorZoom(_zoomLevel + ZoomStep);
    }

    private void EditorZoomOut_OnClick(object sender, RoutedEventArgs e)
    {
        ApplyEditorZoom(_zoomLevel - ZoomStep);
    }

    private void EditorZoomReset_OnClick(object sender, RoutedEventArgs e)
    {
        ApplyEditorZoom(1.0);

        // 重置缩放同时居中视野
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)CenterGraphView);
    }

    private void EditorZoomCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is double zoom && Math.Abs(zoom - _zoomLevel) > 0.001)
        {
            ApplyEditorZoom(zoom);
        }
    }

    private void PreviewZoomIn_OnClick(object sender, RoutedEventArgs e)
    {
        ApplyPreviewZoom(_previewZoomLevel + 0.1);
    }

    private void PreviewZoomOut_OnClick(object sender, RoutedEventArgs e)
    {
        ApplyPreviewZoom(_previewZoomLevel - 0.1);
    }

    private void PreviewZoomReset_OnClick(object sender, RoutedEventArgs e)
    {
        ApplyPreviewZoom(1.0);
        PreviewTranslateTransform.X = 0;
        PreviewTranslateTransform.Y = 0;
    }

    private void PreviewZoomCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is double zoom && Math.Abs(zoom - _previewZoomLevel) > 0.001)
        {
            ApplyPreviewZoom(zoom);
        }
    }

    private void Minimap_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isMinimapDragging = true;
        Minimap.CaptureMouse();
        ScrollToMinimapPosition(e.GetPosition(Minimap));
        e.Handled = true;
    }

    private void Minimap_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isMinimapDragging)
        {
            return;
        }

        ScrollToMinimapPosition(e.GetPosition(Minimap));
        e.Handled = true;
    }

    private void Minimap_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isMinimapDragging = false;
        Minimap.ReleaseMouseCapture();
        e.Handled = true;
    }

    /// <summary>根据总览图中的鼠标位置更新 ScrollViewer 视野</summary>
    private void ScrollToMinimapPosition(Point minimapPos)
    {
        var canvasWidth = GraphCanvas.ActualWidth;
        var canvasHeight = GraphCanvas.ActualHeight;
        if (canvasWidth < 1 || canvasHeight < 1)
        {
            return;
        }

        var ratioX = minimapPos.X / Minimap.ActualWidth;
        var ratioY = minimapPos.Y / Minimap.ActualHeight;
        GraphScrollViewer.ScrollToHorizontalOffset(
            Math.Max(0, ratioX * canvasWidth * _zoomLevel - GraphScrollViewer.ViewportWidth / 2D));
        GraphScrollViewer.ScrollToVerticalOffset(
            Math.Max(0, ratioY * canvasHeight * _zoomLevel - GraphScrollViewer.ViewportHeight / 2D));
    }

    /// <summary>更新总览图中的视野框位置和大小</summary>
    private void UpdateMinimapViewport()
    {
        var canvasWidth = GraphCanvas.ActualWidth;
        var canvasHeight = GraphCanvas.ActualHeight;
        if (canvasWidth < 1 || canvasHeight < 1 || Minimap.ActualWidth < 1 || Minimap.ActualHeight < 1)
        {
            return;
        }

        var viewX = GraphScrollViewer.HorizontalOffset / _zoomLevel / canvasWidth * Minimap.ActualWidth;
        var viewY = GraphScrollViewer.VerticalOffset / _zoomLevel / canvasHeight * Minimap.ActualHeight;
        var viewW = Math.Min(GraphScrollViewer.ViewportWidth / _zoomLevel / canvasWidth * Minimap.ActualWidth, Minimap.ActualWidth - viewX);
        var viewH = Math.Min(GraphScrollViewer.ViewportHeight / _zoomLevel / canvasHeight * Minimap.ActualHeight, Minimap.ActualHeight - viewY);

        Canvas.SetLeft(MinimapViewport, viewX);
        Canvas.SetTop(MinimapViewport, viewY);
        MinimapViewport.Width = Math.Max(8, viewW);
        MinimapViewport.Height = Math.Max(6, viewH);
    }

    /// <summary>生成从起点到终点的三次贝塞尔曲线 Path 数据（StreamGeometry 小语言格式）</summary>
    private static string CreateBezierPathData(Point start, Point end)
    {
        var offset = Math.Max(60, Math.Abs(end.X - start.X) * 0.45);
        return $"M {start.X:F1},{start.Y:F1} C {start.X + offset:F1},{start.Y:F1} {end.X - offset:F1},{end.Y:F1} {end.X:F1},{end.Y:F1}";
    }

    private Point GetPortPoint(object sender, bool isOutput)
    {
        if (sender is not FrameworkElement element)
        {
            return default;
        }

        return element.TranslatePoint(
            new Point(isOutput ? element.ActualWidth : 0, element.ActualHeight / 2D),
            GraphCanvas);
    }

    private FrontedNodePortViewModel? FindPortAt(Point point)
    {
        var hit = VisualTreeHelper.HitTest(GraphCanvas, point)?.VisualHit as DependencyObject;
        while (hit is not null)
        {
            if (hit is FrameworkElement { DataContext: FrontedNodePortViewModel port }
                && port.Descriptor.PortKind is Core.Models.FrontedLayout.Behaviors.FrontedNodePortKind.FlowIn
                    or Core.Models.FrontedLayout.Behaviors.FrontedNodePortKind.ValueIn)
            {
                return port;
            }

            hit = VisualTreeHelper.GetParent(hit);
        }

        return null;
    }

    private static bool CanStartPan(object originalSource)
    {
        var current = originalSource as DependencyObject;
        while (current is not null)
        {
            if (current is ButtonBase or TextBoxBase or Selector or Thumb)
            {
                return false;
            }

            if (current is FrameworkElement { DataContext: FrontedNodeEditorViewModel or FrontedNodePortViewModel or FrontedNodeConnectionViewModel })
            {
                return false;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return true;
    }

    private bool IsCurrentCatalogDrag(DragEventArgs e) =>
        _activeCatalogDragToken is not null
        && e.Data.GetDataPresent(CatalogNodeDragTokenFormat)
        && string.Equals(
            e.Data.GetData(CatalogNodeDragTokenFormat) as string,
            _activeCatalogDragToken,
            StringComparison.Ordinal);
}
