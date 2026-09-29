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
/// FrontedDesignerWindow 的Interaction业务交互逻辑。
/// </summary>
public partial class FrontedDesignerWindow
{
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
            RunUserSelection(() => _viewModel.ToggleDesignItemSelection(item));
            e.Handled = true;
            return;
        }

        // 子控件选中时，点击父控件 hitbox 回退到根控件选中。
        if (_viewModel.IsSubControlSelected
            && ReferenceEquals(_viewModel.SelectedDesignItem, item))
        {
            RunUserSelection(() => _viewModel.EscapeToRootSelection());
            e.Handled = true;
            return;
        }

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
            if (_viewModel?.IsSubControlSelected == true)
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
                UpdateSubControlSelectionVisuals();
                UpdateSelectedPreviewElement();
            }
            else if (_originalSelectedBounds.Count > 1)
            {
                _viewModel?.ResizeSelectedDesignItems(handle, _originalSelectedBounds, deltaX, deltaY, renderPreview: false);
                UpdateSelectedInteractionVisuals();
                UpdateSelectedPreviewElement();
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
                UpdateSelectedInteractionVisuals();
                UpdateSelectedPreviewElement();
            }
        }
        else if (_interactionMode == InteractionMode.SubControlMove)
        {
            _viewModel?.MoveSelectedDesignItem(
                _originalLeft,
                _originalTop,
                deltaX,
                deltaY,
                renderPreview: false);
            UpdateSubControlSelectionVisuals();
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
                RunUserSelection(() => _viewModel?.SelectDesignItem(_pendingHitCandidate));
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
        else if (_interactionMode == InteractionMode.SubControlMove)
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
            PreviewScrollViewer.ScrollToHorizontalOffset(
                (oldHorizontalOffset + cursorPosition.X - PreviewPanTransform.X) * ratio
                + PreviewPanTransform.X
                - cursorPosition.X);
            PreviewScrollViewer.ScrollToVerticalOffset(
                (oldVerticalOffset + cursorPosition.Y - PreviewPanTransform.Y) * ratio
                + PreviewPanTransform.Y
                - cursorPosition.Y);
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
        if (_viewModel is null || ShouldIgnoreKeyboardInput())
        {
            return;
        }

        // Esc 键：子控件选中时回退到根控件选中。
        if (e.Key == Key.Escape && _viewModel.IsSubControlSelected)
        {
            RunUserSelection(() => _viewModel.EscapeToRootSelection());
            e.Handled = true;
            return;
        }

        if (_viewModel.SelectedDesignItem is null)
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
                UpdateSubControlSelectionVisuals();
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
        _originalLeft = item.Config.Left;
        _originalTop = item.Config.Top;
        var bounds = ResolveItemBounds(item);
        if (!item.Config.Width.HasValue)
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

    /// <summary>
    /// 启动子控件（Part/CollectionItem）的交互（Move/Resize）。
    /// 与 <see cref="BeginInteraction"/> 不同，原始几何值由调用方设置（来自子控件相对坐标），
    /// 不从根控件 Config 读取，也不捕获根级 SelectedBounds。
    /// </summary>
    /// <param name="mode">交互模式。</param>
    /// <param name="startMousePosition">起始鼠标位置（画布坐标）。</param>
    /// <param name="element">捕获鼠标的元素。</param>
    private void BeginSubControlInteraction(InteractionMode mode, Point startMousePosition, FrameworkElement element)
    {
        if (_viewModel?.SelectedDesignItem is null)
        {
            return;
        }

        _viewModel?.CaptureUndoSnapshot();
        _interactionMode = mode;
        _startMousePosition = startMousePosition;
        _originalSelectedBounds.Clear();
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
        _panStartTranslationX = PreviewPanTransform.X;
        _panStartTranslationY = PreviewPanTransform.Y;
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
        PreviewPanTransform.X = _panStartTranslationX + deltaX;
        PreviewPanTransform.Y = _panStartTranslationY + deltaY;

        // RenderTransform 不参与 ScrollViewer 的布局失效范围计算。
        // 显式重绘工作区，避免画布边框在旧位置留下残影。
        PreviewWorkspace.InvalidateVisual();
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
        _originalLeft = item.Config.Left;
        _originalTop = item.Config.Top;
        var bounds = ResolveItemBounds(item);
        _originalWidth = bounds.Width;
        _originalHeight = bounds.Height;
        CaptureOriginalSelectedBounds();
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
        UpdateMarqueeSelectionPreview(new Rect(left, top, width, height), currentPosition);
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
        var selectedItems = GetMarqueeSelectedItems(marqueeBounds);
        var primaryItem = _lastMarqueeEnteredItem is not null && selectedItems.Contains(_lastMarqueeEnteredItem)
            ? _lastMarqueeEnteredItem
            : selectedItems
                .OrderBy(item => _marqueeSelectionEntryOrders.GetValueOrDefault(item))
                .LastOrDefault();
        RunUserSelection(() => _viewModel.SelectDesignItems(selectedItems, primaryItem));
    }

    /// <summary>
    /// 获取当前框选区域命中的可选控件。
    /// </summary>
    /// <param name="marqueeBounds">框选区域的逻辑画布坐标。</param>
    /// <returns>命中控件。</returns>
    private IReadOnlyList<FrontedControlDesignItem> GetMarqueeSelectedItems(Rect marqueeBounds)
    {
        if (_viewModel?.CurrentDocument is null)
        {
            return [];
        }

        return _viewModel.CurrentDocument.Controls
            .Where(item => item.IsSelectableInEditor && _hitboxes.TryGetValue(item, out var hitbox)
                && marqueeBounds.IntersectsWith(new Rect(
                    Canvas.GetLeft(hitbox),
                    Canvas.GetTop(hitbox),
                    hitbox.Width,
                    hitbox.Height)))
            .ToList();
    }

    /// <summary>
    /// 实时同步框选区域内控件的临时边框。此预览不改变 ViewModel 选择状态，
    /// 因而不会在鼠标移动期间触发属性面板或渲染器更新。
    /// </summary>
    /// <param name="marqueeBounds">框选区域的逻辑画布坐标。</param>
    /// <param name="currentPosition">当前鼠标逻辑画布坐标。</param>
    private void UpdateMarqueeSelectionPreview(Rect marqueeBounds, Point currentPosition)
    {
        var selectedItems = GetMarqueeSelectedItems(marqueeBounds);
        var selectedSet = selectedItems.ToHashSet();

        var newlySelectedItems = selectedItems
            .Where(item => !_marqueeSelectionPreviewOutlines.ContainsKey(item))
            .OrderBy(item => GetMarqueeEntryProgress(item, currentPosition))
            .ToList();
        foreach (var item in newlySelectedItems)
        {
            _marqueeSelectionEntryOrders[item] = ++_marqueeSelectionEntrySequence;
            _lastMarqueeEnteredItem = item;
        }

        foreach (var item in _marqueeSelectionPreviewOutlines.Keys
                     .Where(item => !selectedSet.Contains(item))
                     .ToList())
        {
            InteractionLayer.Children.Remove(_marqueeSelectionPreviewOutlines[item]);
            _marqueeSelectionPreviewOutlines.Remove(item);
            _marqueeSelectionEntryOrders.Remove(item);
            if (ReferenceEquals(_lastMarqueeEnteredItem, item))
            {
                _lastMarqueeEnteredItem = selectedItems
                    .OrderBy(candidate => _marqueeSelectionEntryOrders.GetValueOrDefault(candidate))
                    .LastOrDefault();
            }
        }

        foreach (var item in selectedItems)
        {
            if (!_marqueeSelectionPreviewOutlines.TryGetValue(item, out var outline))
            {
                outline = new Border
                {
                    BorderBrush = TryFindResource("AccentFillColorDefaultBrush") as Brush ?? Brushes.DeepSkyBlue,
                    BorderThickness = new Thickness(FrontedDesignerEditorVisualHelper.SelectionBorderThickness),
                    Opacity = 0.65D,
                    IsHitTestVisible = false
                };
                Panel.SetZIndex(outline, FrontedDesignerEditorVisualHelper.SelectedOutlineZIndex + 1);
                _marqueeSelectionPreviewOutlines.Add(item, outline);
                InteractionLayer.Children.Add(outline);
            }

            var bounds = ResolveItemBounds(item);
            outline.Width = bounds.Width;
            outline.Height = bounds.Height;
            Canvas.SetLeft(outline, bounds.Left);
            Canvas.SetTop(outline, bounds.Top);
        }
    }

    /// <summary>
    /// 计算控件在当前拖拽方向上首次与框选区域相交的相对进度。
    /// 数值越大，表示控件越晚被框进；用于解决一次鼠标移动同时命中多个控件的顺序。
    /// </summary>
    /// <param name="item">要计算的控件。</param>
    /// <param name="currentPosition">当前鼠标逻辑画布坐标。</param>
    /// <returns>从框选起点到当前指针位置的相对进入进度。</returns>
    private double GetMarqueeEntryProgress(FrontedControlDesignItem item, Point currentPosition)
    {
        var bounds = ResolveItemBounds(item);
        return Math.Max(
            GetMarqueeAxisEntryProgress(_startMousePosition.X, currentPosition.X, bounds.Left, bounds.Left + bounds.Width),
            GetMarqueeAxisEntryProgress(_startMousePosition.Y, currentPosition.Y, bounds.Top, bounds.Top + bounds.Height));
    }

    /// <summary>
    /// 计算沿单一坐标轴扩展框选时，与目标区间首次相交的相对进度。
    /// </summary>
    /// <param name="start">框选起点坐标。</param>
    /// <param name="current">当前指针坐标。</param>
    /// <param name="minimum">目标区间最小坐标。</param>
    /// <param name="maximum">目标区间最大坐标。</param>
    /// <returns>相对进入进度。</returns>
    private static double GetMarqueeAxisEntryProgress(
        double start,
        double current,
        double minimum,
        double maximum)
    {
        var delta = current - start;
        if (Math.Abs(delta) < double.Epsilon)
        {
            return 0D;
        }

        var edge = delta > 0D ? minimum : maximum;
        return Math.Max(0D, (edge - start) / delta);
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

        foreach (var outline in _marqueeSelectionPreviewOutlines.Values)
        {
            InteractionLayer.Children.Remove(outline);
        }

        _marqueeSelectionPreviewOutlines.Clear();
        _marqueeSelectionEntryOrders.Clear();
        _lastMarqueeEnteredItem = null;
        _marqueeSelectionEntrySequence = 0;
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
                UpdateSubControlSelectionVisuals();
                RenderSnapGuides();
            }),
            DispatcherPriority.Loaded);
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

        var value = (PreviewScrollViewer.HorizontalOffset
                     + PreviewScrollViewer.ViewportWidth / 2D
                     - PreviewPanTransform.X)
                    / _viewModel.ZoomScale;
        return double.IsFinite(value) ? value : null;
    }

    private double? GetViewportCenterY()
    {
        if (_viewModel?.CurrentDocument is null || _viewModel.ZoomScale <= 0D)
        {
            return null;
        }

        var value = (PreviewScrollViewer.VerticalOffset
                     + PreviewScrollViewer.ViewportHeight / 2D
                     - PreviewPanTransform.Y)
                    / _viewModel.ZoomScale;
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
        Marquee,
        SubControlMove
    }
}
