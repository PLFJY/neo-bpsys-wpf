using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Helpers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace neo_bpsys_wpf.Views.Windows;

public partial class FrontedDesignerWindow
{
    private sealed record LayerDropTarget(
        int? ZIndex,
        FrontedControlDesignItem? Item,
        bool InsertAfter,
        bool NewTop = false,
        bool NewBottom = false);

    private FrameworkElement? _pendingLayerDragElement;
    private DispatcherOperation? _layerSelectionOperation;
    private LayerDropTarget? _layerDropTarget;
    private Border? _layerInsertionPreview;

    private void Window_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        _layerSelectionOperation?.Abort();
        _layerSelectionOperation = null;
    }

    private void ResetPendingLayerPointer()
    {
        var element = _pendingLayerDragElement;
        _pendingLayerDragElement = null;
        _pendingLayerDragNode = null;
        if (element?.IsMouseCaptured == true) element.ReleaseMouseCapture();
    }

    private void UpdateLayerDropPreview(object sender, DragEventArgs e)
    {
        if (sender is not FrameworkElement element || _activeLayerDragNode is null) return;
        if (ReferenceEquals(element, _layerInsertionPreview)) return;

        if (ReferenceEquals(element, LayerTopDropZone))
        {
            ShowLayerInsertionPreview(LayerPanelContent, LayerGroupsItemsControl, new(null, null, false, NewTop: true));
        }
        else if (ReferenceEquals(element, LayerBottomDropZone))
        {
            ShowLayerInsertionPreview(LayerPanelContent, null, new(null, null, true, NewBottom: true));
        }
        else if (element.DataContext is DesignerLayerNode { Kind: DesignerLayerNodeKind.Control, ControlItem: { } item })
        {
            if (ReferenceEquals(item, _activeLayerDragNode.ControlItem))
            {
                HideLayerInsertionPreview();
                _layerDropTarget = null;
                return;
            }

            if (element.Parent is not StackPanel panel) return;
            var after = e.GetPosition(element).Y > element.ActualHeight / 2D;
            ShowLayerInsertionPreview(panel, after ? null : element, new(item.Config.ZIndex, item, after));
        }
        else if (element.DataContext is FrontedLayerGroup group && element is Border { Child: StackPanel groupContent })
        {
            ShowLayerInsertionPreview(groupContent, null, new(group.ZIndex, null, true));
        }
        else
        {
            var point = e.GetPosition(LayerPanelScrollViewer);
            if (point.Y <= LayerDropZoneEdgeSize && LayerPanelScrollViewer.VerticalOffset <= 0.1D)
                ShowLayerInsertionPreview(LayerPanelContent, LayerGroupsItemsControl, new(null, null, false, NewTop: true));
            else if (point.Y >= LayerPanelScrollViewer.ViewportHeight - LayerDropZoneEdgeSize
                     && LayerPanelScrollViewer.VerticalOffset >= LayerPanelScrollViewer.ScrollableHeight - 0.1D)
                ShowLayerInsertionPreview(LayerPanelContent, null, new(null, null, true, NewBottom: true));
            else
            {
                HideLayerInsertionPreview();
                _layerDropTarget = null;
            }
        }
    }

    private void ShowLayerInsertionPreview(Panel panel, UIElement? before, LayerDropTarget target)
    {
        if (_activeLayerDragNode is not { } source || _layerDropTarget == target && _layerInsertionPreview?.Parent == panel) return;

        HideLayerInsertionPreview();
        _layerDropTarget = target;
        var zIndex = target.ZIndex ?? (target.NewTop
            ? (_viewModel?.CurrentDocument?.Controls.Max(item => item.Config.ZIndex) ?? 0) + 1
            : (_viewModel?.CurrentDocument?.Controls.Min(item => item.Config.ZIndex) ?? 0) - 1);
        var label = new TextBlock { Text = source.DisplayName, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
        var metadata = new TextBlock
        {
            Text = $"{source.Metadata}  {I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "ZIndexShort")} {zIndex}",
            FontSize = 12,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        var content = new StackPanel();
        content.Children.Add(label);
        content.Children.Add(metadata);
        _layerInsertionPreview = new Border
        {
            Child = content,
            DataContext = null,
            Margin = new Thickness(0, 3, 0, 3),
            Padding = new Thickness(8, 6, 8, 6),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Opacity = 0.45,
            AllowDrop = true
        };
        _layerInsertionPreview.SetResourceReference(Border.BackgroundProperty, "CardBackgroundFillColorDefaultBrush");
        _layerInsertionPreview.SetResourceReference(Border.BorderBrushProperty, "AccentTextFillColorPrimaryBrush");
        _layerInsertionPreview.DragOver += UpdateLayerDragOver;
        _layerInsertionPreview.Drop += (_, e) => CommitLayerDrag(e);
        var index = before is null ? panel.Children.Count : panel.Children.IndexOf(before);
        panel.Children.Insert(index, _layerInsertionPreview);
    }

    private void HideLayerInsertionPreview()
    {
        if (_layerInsertionPreview?.Parent is Panel panel) panel.Children.Remove(_layerInsertionPreview);
        _layerInsertionPreview = null;
    }

    private void UpdateLayerDropPreviewAtPointer()
    {
        if (_activeLayerDragNode is null) return;
        var hit = LayerPanelScrollViewer.InputHitTest(Mouse.GetPosition(LayerPanelScrollViewer)) as DependencyObject;
        for (var current = hit; current is not null; current = current is Visual
                 ? VisualTreeHelper.GetParent(current) : LogicalTreeHelper.GetParent(current))
        {
            if (ReferenceEquals(current, _layerInsertionPreview)) return;
            if (current is Border { DataContext: DesignerLayerNode { Kind: DesignerLayerNodeKind.Control } } row)
            {
                var after = Mouse.GetPosition(row).Y > row.ActualHeight / 2D;
                if (row.Parent is StackPanel panel && row.DataContext is DesignerLayerNode { ControlItem: { } item }
                    && !ReferenceEquals(item, _activeLayerDragNode.ControlItem))
                    ShowLayerInsertionPreview(panel, after ? null : row, new(item.Config.ZIndex, item, after));
                else
                {
                    HideLayerInsertionPreview();
                    _layerDropTarget = null;
                }
                return;
            }
            if (current is Border { DataContext: FrontedLayerGroup group, Child: StackPanel groupContent })
            {
                ShowLayerInsertionPreview(groupContent, null, new(group.ZIndex, null, true));
                return;
            }
        }
    }

    private void CommitLayerDrag(DragEventArgs e)
    {
        var target = _layerDropTarget;
        // 先移除临时视觉；列表和文档只在 Drop 时真正更改一次。
        StopLayerDrag(e);
        if (target is null || _viewModel is not { CurrentDocument: { } document } viewModel
            || !TryGetLayerDragItem(e, out var source)) return;

        viewModel.CommitLayerDragDrop(source, target.ZIndex, target.Item, target.InsertAfter, target.NewTop, target.NewBottom);
        var selectedAtDrop = viewModel.SelectedDesignItem;
        var targetAtDrop = viewModel.SelectedTarget;
        _layerSelectionOperation?.Abort();
        _layerSelectionOperation = Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            _layerSelectionOperation = null;
            if (ReferenceEquals(viewModel.CurrentDocument, document) && document.Controls.Contains(source)
                && ReferenceEquals(viewModel.SelectedDesignItem, selectedAtDrop)
                && ReferenceEquals(viewModel.SelectedTarget, targetAtDrop))
                RunUserSelection(() => viewModel.SelectDesignItem(source));
        }));
    }
}
