using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using neo_bpsys_wpf.ViewModels.FrontedDesigner.GraphEditor;

namespace neo_bpsys_wpf.Views.FrontedDesigner.GraphEditor;

public partial class FrontedNodeGraphEditorView : UserControl
{
    private const string CatalogNodeDragFormat = "neo-bpsys-wpf.fronted-node-type";
    private const string CatalogNodeDragTokenFormat = "neo-bpsys-wpf.fronted-node-drag-token";
    private FrontedNodePortViewModel? _dragSourcePort;
    private Point? _catalogDragStartPoint;
    private string? _activeCatalogDragToken;
    private bool _isPanning;
    private Point _panStartPoint;
    private double _panStartHorizontalOffset;
    private double _panStartVerticalOffset;
    private FrontedNodeGraphEditorViewModel? _subscribedViewModel;

    public FrontedNodeGraphEditorView()
    {
        InitializeComponent();
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
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is FrontedNodeGraphEditorViewModel viewModel)
        {
            UpdatePreviewVisual(viewModel.PreviewRoot);
        }
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
        var start = GetPortPoint(sender, isOutput: true);
        ConnectionPreviewLine.X1 = start.X;
        ConnectionPreviewLine.Y1 = start.Y;
        ConnectionPreviewLine.X2 = start.X;
        ConnectionPreviewLine.Y2 = start.Y;
        ConnectionPreviewLine.Visibility = Visibility.Visible;
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
        if (_dragSourcePort is not null || !CanStartPan(e.OriginalSource))
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
        ConnectionPreviewLine.X2 = current.X;
        ConnectionPreviewLine.Y2 = current.Y;
        e.Handled = true;
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
        if (GraphScrollViewer.IsMouseCaptured)
        {
            GraphScrollViewer.ReleaseMouseCapture();
        }
    }

    private void EndConnectionDrag()
    {
        _dragSourcePort = null;
        ConnectionPreviewLine.Visibility = Visibility.Collapsed;
        if (GraphCanvas.IsMouseCaptured)
        {
            GraphCanvas.ReleaseMouseCapture();
        }
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
