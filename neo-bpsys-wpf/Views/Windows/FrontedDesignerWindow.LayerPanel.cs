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
/// FrontedDesignerWindow 的LayerPanel业务交互逻辑。
/// </summary>
public partial class FrontedDesignerWindow
{
    private void ControlList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0 || _viewModel is null)
        {
            return;
        }

        if (e.AddedItems[0] is FrontedControlDesignItem item)
        {
            if (sender is FrameworkElement element
                && (element.IsKeyboardFocusWithin
                    || Mouse.LeftButton == MouseButtonState.Pressed
                    || Mouse.RightButton == MouseButtonState.Pressed))
            {
                RunUserSelection(() => _viewModel.SelectDesignItem(item));
            }
            else
            {
                _viewModel.SelectDesignItem(item);
            }
        }
    }

    private void ControlListItem_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: FrontedControlDesignItem item })
        {
            RunUserSelection(() => _viewModel?.SelectDesignItem(item));
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
            UpdateLayerDropPreviewAtPointer();
        };
        return timer;
    }

    private void LayerItem_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: DesignerLayerNode node })
        {
            return;
        }

        if (e.ClickCount == 2
            && node.Kind == DesignerLayerNodeKind.Control
            && node.ControlItem is not null)
        {
            ResetPendingLayerPointer();
            BeginLayerRename(node);
            e.Handled = true;
            return;
        }

        _layerSelectionOperation?.Abort();
        _layerSelectionOperation = null;
        ResetPendingLayerPointer();
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            RunUserSelection(() => _viewModel?.ToggleLayerNodeSelection(node));
            e.Handled = true;
            return;
        }

        if (!node.CanReorder)
        {
            RunUserSelection(() => _viewModel?.SelectLayerNode(node));
            e.Handled = true;
            return;
        }

        // 拖动前不加载属性/行为面板；只有释放为点击时才执行选择。
        _pendingLayerDragNode = node;
        _pendingLayerDragElement = (FrameworkElement)sender;
        _layerDragStartPoint = e.GetPosition(this);
        _pendingLayerDragElement.CaptureMouse();
        e.Handled = true;
    }

    private void LayerItem_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var node = _pendingLayerDragNode;
        ResetPendingLayerPointer();
        if (node is not null) RunUserSelection(() => _viewModel?.SelectLayerNode(node));
        e.Handled = true;
    }

    private void LayerItem_OnLostMouseCapture(object sender, MouseEventArgs e)
    {
        if (ReferenceEquals(sender, _pendingLayerDragElement)) ResetPendingLayerPointer();
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
            _viewModel.StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.LayerPanel.ReorderBlocked");
            var blockedNode = _pendingLayerDragNode;
            ResetPendingLayerPointer();
            // 拖动阈值内的小抖动被判定为禁止重排时，仍按一次普通点击处理，
            // 否则用户会看到“点了但没选中”。
            if (blockedNode is not null)
            {
                RunUserSelection(() => _viewModel?.SelectLayerNode(blockedNode));
            }

            return;
        }

        _activeLayerDragNode = _pendingLayerDragNode;
        ResetPendingLayerPointer();
        var data = new DataObject(typeof(FrontedControlDesignItem), _activeLayerDragNode.ControlItem);
        ShowLayerDragGhost(_activeLayerDragNode!, e.GetPosition(LayerPanelHostGrid));
        try
        {
            DragDrop.DoDragDrop(dragSource, data, DragDropEffects.Move);
        }
        finally
        {
            _activeLayerDragNode = null;
            HideLayerInsertionPreview();
            _layerDropTarget = null;
            HideLayerDragGhost();
            StopLayerAutoScroll();
            HideLayerDropZones();
        }
    }

    private void LayerItem_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DesignerLayerNode node })
        {
            RunUserSelection(() => _viewModel?.SelectLayerNode(node));
        }
    }

    private void LayerGroupHeader_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel is not null
            && sender is FrameworkElement { DataContext: FrontedLayerGroup group })
        {
            _viewModel.SelectLayerGroup(group, (Keyboard.Modifiers & ModifierKeys.Control) != 0);
            e.Handled = true;
        }
    }

    private void LayerGroupDeleteMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        var contextMenu = (sender as System.Windows.Controls.MenuItem)?.Parent as ContextMenu;
        if (_viewModel is not null
            && contextMenu?.PlacementTarget is FrameworkElement { DataContext: FrontedLayerGroup group })
        {
            _viewModel.DeleteLayerGroup(group);
        }
    }

    private void BeginLayerRename(DesignerLayerNode node)
    {
        // 切换重命名目标时先提交上一个仍在编辑的草稿，避免其 LostKeyboardFocus
        // 因为 IsRenaming 已清除而直接丢弃用户输入。
        foreach (var other in LayerGroupsEnumerable())
        {
            if (!ReferenceEquals(other, node) && other.IsRenaming)
            {
                CommitLayerRename(other, cancel: false);
            }
        }

        if (!node.IsRenaming)
        {
            node.EditingName = node.DisplayName;
            node.IsRenaming = true;
        }
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            var textBox = FindVisualChild<System.Windows.Controls.TextBox>(
                LayerGroupsItemsControl,
                candidate => ReferenceEquals(candidate.DataContext, node));
            if (textBox is not null)
            {
                textBox.Focus();
                textBox.SelectAll();
            }
        }));
    }

    private IEnumerable<DesignerLayerNode> LayerGroupsEnumerable()
    {
        if (_viewModel is null)
        {
            yield break;
        }

        foreach (var group in _viewModel.LayerGroups)
        {
            foreach (var node in group.Items)
            {
                yield return node;
                foreach (var child in node.Children)
                {
                    yield return child;
                }
            }
        }
    }

    private void LayerRenameTextBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: DesignerLayerNode node })
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            CommitLayerRename(node, cancel: false);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CommitLayerRename(node, cancel: true);
            e.Handled = true;
        }
    }

    private void LayerRenameTextBox_OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DesignerLayerNode node } && node.IsRenaming)
        {
            CommitLayerRename(node, cancel: false);
        }
    }

    private void CommitLayerRename(DesignerLayerNode node, bool cancel)
    {
        if (!node.IsRenaming)
        {
            return;
        }

        node.IsRenaming = false;
        if (cancel || node.ControlItem is not { } item || _viewModel is null)
        {
            return;
        }

        if (_viewModel.TryRenameDesignItem(item, node.EditingName, out var errorMessage))
        {
            node.DisplayName = item.Name;
        }
        else if (!string.IsNullOrEmpty(errorMessage))
        {
            _viewModel.StatusMessage = errorMessage;
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent, Func<T, bool> predicate)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match && predicate(match))
            {
                return match;
            }

            var descendant = FindVisualChild(child, predicate);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
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
            RunUserSelection(() => _viewModel?.SelectDesignItem(item));
            _viewModel?.DeleteSelectedControlCommand.Execute(null);
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

        UpdateLayerDropPreview(sender, e);
        CommitLayerDrag(e);
    }

    private void LayerItem_OnDragOver(object sender, DragEventArgs e)
    {
        UpdateLayerDragOver(sender, e);
    }

    private void LayerGroup_OnDragOver(object sender, DragEventArgs e)
    {
        UpdateLayerDragOver(sender, e);
    }

    private void LayerGroup_OnDrop(object sender, DragEventArgs e)
    {
        if (_viewModel is null
            || sender is not FrameworkElement { DataContext: FrontedLayerGroup group }
            || !TryGetLayerDragItem(e, out var source))
        {
            return;
        }

        UpdateLayerDropPreview(sender, e);
        CommitLayerDrag(e);
    }

    private void LayerPanel_OnDragOver(object sender, DragEventArgs e)
    {
        UpdateLayerDragOver(sender, e);
    }

    private void LayerPanel_OnDrop(object sender, DragEventArgs e)
    {
        UpdateLayerDropPreview(sender, e);
        CommitLayerDrag(e);
    }

    private void LayerPanel_OnDragLeave(object sender, DragEventArgs e)
    {
        var point = e.GetPosition(LayerPanelHostGrid);
        if (!new Rect(LayerPanelHostGrid.RenderSize).Contains(point))
        {
            HideLayerInsertionPreview();
            _layerDropTarget = null;
            StopLayerAutoScroll();
        }
        e.Handled = true;
    }

    private void LayerTopDropZone_OnDragOver(object sender, DragEventArgs e)
    {
        UpdateLayerDragOver(sender, e);
    }

    private void LayerTopDropZone_OnDrop(object sender, DragEventArgs e)
    {
        UpdateLayerDropPreview(sender, e);
        CommitLayerDrag(e);
    }

    private void LayerBottomDropZone_OnDragOver(object sender, DragEventArgs e)
    {
        UpdateLayerDragOver(sender, e);
    }

    private void LayerBottomDropZone_OnDrop(object sender, DragEventArgs e)
    {
        UpdateLayerDropPreview(sender, e);
        CommitLayerDrag(e);
    }

    private void UpdateLayerDragOver(object sender, DragEventArgs e)
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

        UpdateLayerAutoScroll(e.GetPosition(LayerPanelScrollViewer));
        UpdateLayerDropPreview(sender, e);
        e.Effects = _layerDropTarget is null ? DragDropEffects.None : DragDropEffects.Move;
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

        if (Math.Abs(_layerAutoScrollVelocity) > 0.01D)
        {
            if (!_layerAutoScrollTimer.IsEnabled) _layerAutoScrollTimer.Start();
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
        HideLayerInsertionPreview();
        _layerDropTarget = null;
        HideLayerDragGhost();
        StopLayerAutoScroll();
        HideLayerDropZones();
        e.Handled = true;
    }

    private void ShowLayerDragGhost(DesignerLayerNode node, Point panelPosition)
    {
        LayerDragGhostNameText.Text = node.DisplayName;
        LayerDragGhostMetaText.Text = $"{node.Metadata}  {I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "ZIndexShort")} {node.ZIndex}";
        LayerDragGhost.Visibility = Visibility.Visible;
        UpdateLayerDragGhost(panelPosition);
    }

    private void UpdateLayerDragGhost(Point panelPosition)
    {
        if (LayerDragGhost.Visibility != Visibility.Visible)
        {
            return;
        }

        if (!LayerDragGhost.IsMeasureValid)
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
        var localized = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, key);
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

}
