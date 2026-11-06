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
/// FrontedDesignerWindow 的DesignSurface业务交互逻辑。
/// </summary>
public partial class FrontedDesignerWindow
{
    private const double DesignSurfaceOverflowPadding = 240D;
    private double _designSurfaceOffsetX;
    private double _designSurfaceOffsetY;
    private double _designSurfaceCanvasWidth;
    private double _designSurfaceCanvasHeight;
    private double _designSurfaceContentWidth;
    private double _designSurfaceContentHeight;
    private double _designSurfaceFitWidth;
    private double _designSurfaceFitHeight;

    private void ConfigureDesignSurface(double width, double height)
    {
        DesignSurfaceGrid.Width = width;
        DesignSurfaceGrid.Height = height;
        PreviewCanvas.Width = width;
        PreviewCanvas.Height = height;
        InteractionLayer.Width = width;
        InteractionLayer.Height = height;
        _viewModel?.UpdateDesignSurfaceContentSize(width, height);
        UpdateDesignSurfaceExtent();
        UpdatePreviewWorkspaceSize();
        _viewModel?.UpdateFitZoom(PreviewScrollViewer.ViewportWidth, PreviewScrollViewer.ViewportHeight);
    }

    private void RebuildInteractionLayer()
    {
        _viewModel?.ClearActiveSnapGuides();
        var controls = _viewModel?.CurrentDocument?.Controls;
        var currentItems = controls?.Where(item => item.IsSelectableInEditor).ToHashSet() ?? [];
        foreach (var item in _hitboxes.Keys.Where(item => !currentItems.Contains(item)).ToArray())
        {
            InteractionLayer.Children.Remove(_hitboxes[item]);
            _hitboxes.Remove(item);
        }

        var retainedHitboxes = _hitboxes.Values.Cast<UIElement>().ToHashSet();
        for (var index = InteractionLayer.Children.Count - 1; index >= 0; index--)
        {
            if (!retainedHitboxes.Contains(InteractionLayer.Children[index]))
                InteractionLayer.Children.RemoveAt(index);
        }
        _snapGuideLines.Clear();
        _resizeHandles.Clear();
        _multiSelectionOutlinePool.AddRange(_multiSelectionOutlines);
        _multiSelectionOutlines.Clear();
        _multiResizeHandles.Clear();
        _marqueeSelectionPreviewOutlines.Clear();
        _marqueeSelectionEntryOrders.Clear();
        _lastMarqueeEnteredItem = null;
        _marqueeSelectionEntrySequence = 0;
        _selectionOutline = null;
        _parentSelectionOutline = null;
        _selectionLabel = null;
        _marqueeSelectionOutline = null;
        _childResizeHandles.Clear();
        _childSelectionOutline = null;
        _childSelectionLabel = null;
        _currentSubTargetInfo = null;

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

            if (!_hitboxes.TryGetValue(entry.Item, out var hitbox))
            {
                hitbox = CreateHitbox(entry.Item, entry.Index);
                _hitboxes[entry.Item] = hitbox;
                InteractionLayer.Children.Add(hitbox);
            }
            else
            {
                var bounds = ResolveItemBounds(entry.Item);
                hitbox.Width = bounds.Width;
                hitbox.Height = bounds.Height;
                Canvas.SetLeft(hitbox, bounds.Left);
                Canvas.SetTop(hitbox, bounds.Top);
                Panel.SetZIndex(hitbox, FrontedDesignerEditorVisualHelper.GetHitboxZIndex(
                    entry.Item.Config.ZIndex, entry.Index, ReferenceEquals(entry.Item, _viewModel.SelectedDesignItem)));
            }
        }

        if (_viewModel.SelectedDesignItems.Count > 0)
        {
            AddSelectionAdorners();
        }

        // 子控件只能通过控件列表选中；画布仅为已选中的子控件绘制编辑装饰器。
        if (_viewModel.IsSubControlSelected)
        {
            AddSubControlSelectionAdorner();
        }

        UpdateDesignSurfaceExtent();
    }

    /// <summary>
    /// 根据画布和控件实际占用范围动态扩展设计表面，使拖到画布外的控件仍可见、可继续操作。
    /// </summary>
    private void UpdateDesignSurfaceExtent()
    {
        if (_viewModel?.CurrentDocument is null)
        {
            // 清空/渲染失败后即使没有文档，也要把上一次文档留下的平移与尺寸重置，
            // 否则空画布会被旧的 offset/工作区尺寸带偏。
            var width = DesignSurfaceGrid.Width;
            var height = DesignSurfaceGrid.Height;
            if (!double.IsFinite(width) || width <= 0D) width = 1D;
            if (!double.IsFinite(height) || height <= 0D) height = 1D;
            _designSurfaceOffsetX = 0D;
            _designSurfaceOffsetY = 0D;
            _designSurfaceContentWidth = width;
            _designSurfaceContentHeight = height;
            _designSurfaceFitWidth = width;
            _designSurfaceFitHeight = height;
            PreviewCanvasOffsetTransform.X = 0D;
            PreviewCanvasOffsetTransform.Y = 0D;
            InteractionLayerOffsetTransform.X = 0D;
            InteractionLayerOffsetTransform.Y = 0D;
            CanvasFrameOffsetTransform.X = 0D;
            CanvasFrameOffsetTransform.Y = 0D;
            _viewModel?.UpdateDesignSurfaceContentSize(width, height);
            _viewModel?.UpdateDesignSurfaceFitSize(width, height);
            UpdatePreviewWorkspaceSize();
            return;
        }

        var canvasWidth = Math.Max(1D, _viewModel.CurrentDocument.CanvasConfig.CanvasWidth);
        var canvasHeight = Math.Max(1D, _viewModel.CurrentDocument.CanvasConfig.CanvasHeight);
        _designSurfaceCanvasWidth = canvasWidth;
        _designSurfaceCanvasHeight = canvasHeight;

        var minX = 0D;
        var minY = 0D;
        var maxX = canvasWidth;
        var maxY = canvasHeight;
        foreach (var item in _viewModel.CurrentDocument.Controls)
        {
            if (!item.IsSelectableInEditor)
            {
                continue;
            }

            var bounds = ResolveItemBounds(item);
            if (bounds.Width <= 0D || bounds.Height <= 0D)
            {
                continue;
            }

            minX = Math.Min(minX, bounds.Left);
            minY = Math.Min(minY, bounds.Top);
            maxX = Math.Max(maxX, bounds.Left + bounds.Width);
            maxY = Math.Max(maxY, bounds.Top + bounds.Height);
        }

        var hasOverflow = minX < 0D || minY < 0D || maxX > canvasWidth || maxY > canvasHeight;

        // 适应模式下不能为拖拽预留空白，否则空白会被计入 ScrollViewer extent，
        // 导致适应后内容比视口略大、出现滚动条并偏离居中位置。退出适应模式后再恢复预留。
        var padding = hasOverflow && _viewModel?.IsFitMode != true
            ? DesignSurfaceOverflowPadding
            : 0D;
        var contentLeft = minX - padding;
        var contentTop = minY - padding;
        var contentWidth = Math.Max(1D, maxX + padding - contentLeft);
        var contentHeight = Math.Max(1D, maxY + padding - contentTop);

        var contentSizeChanged = !contentWidth.Equals(_designSurfaceContentWidth)
            || !contentHeight.Equals(_designSurfaceContentHeight);
        var fitWidth = Math.Max(1D, maxX - minX);
        var fitHeight = Math.Max(1D, maxY - minY);
        var fitSizeChanged = !fitWidth.Equals(_designSurfaceFitWidth)
            || !fitHeight.Equals(_designSurfaceFitHeight);
        _designSurfaceOffsetX = -contentLeft;
        _designSurfaceOffsetY = -contentTop;
        _designSurfaceContentWidth = contentWidth;
        _designSurfaceContentHeight = contentHeight;
        _designSurfaceFitWidth = fitWidth;
        _designSurfaceFitHeight = fitHeight;
        DesignSurfaceGrid.Width = contentWidth;
        DesignSurfaceGrid.Height = contentHeight;
        // 两层画布整体平移到正坐标区域。画布尺寸必须覆盖完整设计表面，不能减去平移量：
        // Canvas 的背景/子元素会被布局边界裁剪，减去平移量会让画布背景右侧/底部缺一块。
        PreviewCanvas.Width = Math.Max(1D, contentWidth);
        PreviewCanvas.Height = Math.Max(1D, contentHeight);
        InteractionLayer.Width = PreviewCanvas.Width;
        InteractionLayer.Height = PreviewCanvas.Height;
        PreviewCanvasOffsetTransform.X = _designSurfaceOffsetX;
        PreviewCanvasOffsetTransform.Y = _designSurfaceOffsetY;
        InteractionLayerOffsetTransform.X = _designSurfaceOffsetX;
        InteractionLayerOffsetTransform.Y = _designSurfaceOffsetY;
        CanvasFrameOffsetTransform.X = _designSurfaceOffsetX;
        CanvasFrameOffsetTransform.Y = _designSurfaceOffsetY;

        // 适应模式依赖设计表面内容范围；内容变化后立即重算，避免新增画布外控件后仍按旧范围缩放。
        _viewModel?.UpdateDesignSurfaceContentSize(contentWidth, contentHeight);
        _viewModel?.UpdateDesignSurfaceFitSize(fitWidth, fitHeight);
        UpdatePreviewWorkspaceSize();
        if ((contentSizeChanged || fitSizeChanged) && _viewModel?.IsFitMode == true)
        {
            _viewModel.UpdateFitZoom(PreviewScrollViewer.ViewportWidth, PreviewScrollViewer.ViewportHeight);
        }
    }

    private void AddSelectionAdorners()
    {
        if (_viewModel is null)
        {
            return;
        }

        var multiSelectionBrush = TryFindResource("AccentFillColorDefaultBrush") as Brush ?? Brushes.DeepSkyBlue;
        foreach (var item in _viewModel.SelectedDesignItems)
        {
            if (ReferenceEquals(item, _viewModel.SelectedDesignItem))
            {
                continue;
            }

            AddMultiSelectionOutline(item, multiSelectionBrush);
        }

        if (_viewModel.SelectedDesignItem is not null)
        {
            AddSelectionAdorner(_viewModel.SelectedDesignItem);
        }
    }

    private void AddMultiSelectionOutline(FrontedControlDesignItem item, Brush borderBrush)
    {
        var bounds = ResolveItemBounds(item);
        Border outline;
        if (_multiSelectionOutlinePool.Count > 0)
        {
            outline = _multiSelectionOutlinePool[^1];
            _multiSelectionOutlinePool.RemoveAt(_multiSelectionOutlinePool.Count - 1);
            outline.BorderBrush = borderBrush;
        }
        else
        {
            outline = new Border
            {
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(FrontedDesignerEditorVisualHelper.SelectionBorderThickness),
                Opacity = 0.65D,
                IsHitTestVisible = false
            };
        }

        outline.Width = bounds.Width;
        outline.Height = bounds.Height;
        Canvas.SetLeft(outline, bounds.Left);
        Canvas.SetTop(outline, bounds.Top);
        Panel.SetZIndex(outline, FrontedDesignerEditorVisualHelper.SelectedOutlineZIndex - 1);
        _multiSelectionOutlines.Add(outline);
        InteractionLayer.Children.Add(outline);

        // 多选时每个控件都提供独立缩放手柄，点击任意一个手柄都会缩放整个选择集合。
        var handles = new Dictionary<FrontedDesignerResizeHandleKind, Border>();
        foreach (var handle in Enum.GetValues<FrontedDesignerResizeHandleKind>())
        {
            var handleElement = CreateResizeHandle(handle);
            handles[handle] = handleElement;
            InteractionLayer.Children.Add(handleElement);
        }

        _multiResizeHandles[item] = handles;
        SetMultiSelectionHandlePositions(bounds, handles);
    }

    private void SetMultiSelectionHandlePositions(
        FrontedDesignerResolvedBounds bounds,
        IReadOnlyDictionary<FrontedDesignerResizeHandleKind, Border> handles)
    {
        if (handles.TryGetValue(FrontedDesignerResizeHandleKind.TopLeft, out var topLeft))
            SetHandlePosition(topLeft, bounds.Left, bounds.Top);
        if (handles.TryGetValue(FrontedDesignerResizeHandleKind.Top, out var top))
            SetHandlePosition(top, bounds.Left + bounds.Width / 2D, bounds.Top);
        if (handles.TryGetValue(FrontedDesignerResizeHandleKind.TopRight, out var topRight))
            SetHandlePosition(topRight, bounds.Left + bounds.Width, bounds.Top);
        if (handles.TryGetValue(FrontedDesignerResizeHandleKind.Left, out var left))
            SetHandlePosition(left, bounds.Left, bounds.Top + bounds.Height / 2D);
        if (handles.TryGetValue(FrontedDesignerResizeHandleKind.Right, out var right))
            SetHandlePosition(right, bounds.Left + bounds.Width, bounds.Top + bounds.Height / 2D);
        if (handles.TryGetValue(FrontedDesignerResizeHandleKind.BottomLeft, out var bottomLeft))
            SetHandlePosition(bottomLeft, bounds.Left, bounds.Top + bounds.Height);
        if (handles.TryGetValue(FrontedDesignerResizeHandleKind.Bottom, out var bottom))
            SetHandlePosition(bottom, bounds.Left + bounds.Width / 2D, bounds.Top + bounds.Height);
        if (handles.TryGetValue(FrontedDesignerResizeHandleKind.BottomRight, out var bottomRight))
            SetHandlePosition(bottomRight, bounds.Left + bounds.Width, bounds.Top + bounds.Height);
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

    /// <summary>
    /// 在指定位置查找按 <see cref="FrontedControlConfigBase.ZIndex"/> 排序的最上层可选控件。
    /// 点击选择时使用此方法优先选中视觉上更靠上的控件，不受选中态 hitbox ZIndex 提升影响。
    /// </summary>
    /// <param name="position">相对于 <see cref="InteractionLayer"/> 的逻辑画布坐标。</param>
    /// <returns>命中位置上 ZIndex 最高的可选控件设计项；无命中时返回 <see langword="null"/>。</returns>
    private FrontedControlDesignItem? FindTopmostSelectableItemAt(Point position)
    {
        if (_viewModel?.CurrentDocument is null)
        {
            return null;
        }

        FrontedControlDesignItem? best = null;
        var bestZIndex = int.MinValue;
        var bestLayoutOrder = -1;
        var controls = _viewModel.CurrentDocument.Controls;

        for (var index = 0; index < controls.Count; index++)
        {
            var item = controls[index];
            if (!item.IsSelectableInEditor)
            {
                continue;
            }

            if (!_hitboxes.TryGetValue(item, out var hitbox))
            {
                continue;
            }

            var left = Canvas.GetLeft(hitbox);
            var top = Canvas.GetTop(hitbox);
            var width = hitbox.Width;
            var height = hitbox.Height;

            if (double.IsNaN(left) || double.IsNaN(top)
                || double.IsNaN(width) || double.IsNaN(height)
                || width <= 0 || height <= 0)
            {
                continue;
            }

            if (position.X < left || position.X > left + width
                || position.Y < top || position.Y > top + height)
            {
                continue;
            }

            var zIndex = item.Config.ZIndex;
            // 更高 ZIndex 优先；ZIndex 相同时文档顺序靠后的优先，与 WPF 同 ZIndex 默认渲染顺序一致。
            if (zIndex > bestZIndex
                || (zIndex == bestZIndex && index > bestLayoutOrder))
            {
                best = item;
                bestZIndex = zIndex;
                bestLayoutOrder = index;
            }
        }

        return best;
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
            Child = CreateSelectionLabelText(item.Name),
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

    /// <summary>
    /// 子控件选中轮廓的 ZIndex，高于根缩放手柄。
    /// </summary>
    private const int ChildSelectionOutlineZIndex = 20_300;

    /// <summary>
    /// 子控件缩放手柄的 ZIndex。
    /// </summary>
    private const int ChildSelectionHandleZIndex = 20_310;

    /// <summary>
    /// 为当前选中的子控件（Part/CollectionItem）绘制 selection adorner 与 resize handles。
    /// 装饰器坐标由子控件相对几何叠加父控件画布坐标得到。
    /// </summary>
    private void AddSubControlSelectionAdorner()
    {
        if (_viewModel?.GetCurrentSubTargetInfo() is not { } target)
        {
            return;
        }

        _currentSubTargetInfo = target;
        var parentBounds = ResolveItemBounds(target.ParentItem);
        var bounds = new FrontedDesignerResolvedBounds(
            parentBounds.Left + target.Left,
            parentBounds.Top + target.Top,
            target.Width,
            target.Height);

        _childSelectionOutline = new Border
        {
            Width = bounds.Width,
            Height = bounds.Height,
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Orange,
            BorderThickness = new Thickness(FrontedDesignerEditorVisualHelper.SelectionBorderThickness),
            IsHitTestVisible = target.CanMove,
            Cursor = target.CanMove ? Cursors.SizeAll : Cursors.Arrow,
            Tag = target
        };
        _childSelectionOutline.MouseLeftButtonDown += SubControlSelectionOutline_OnMouseLeftButtonDown;
        Canvas.SetLeft(_childSelectionOutline, bounds.Left);
        Canvas.SetTop(_childSelectionOutline, bounds.Top);
        Panel.SetZIndex(_childSelectionOutline, ChildSelectionOutlineZIndex);
        InteractionLayer.Children.Add(_childSelectionOutline);

        _childSelectionLabel = new Border
        {
            Background = Brushes.Orange,
            Padding = new Thickness(4, 1, 4, 1),
            CornerRadius = new CornerRadius(2),
            Child = CreateSelectionLabelText(
                target.IsCollectionItem ? $"{target.Id} [{target.ItemKey}]" : target.Id),
            IsHitTestVisible = false
        };
        Panel.SetZIndex(_childSelectionLabel, ChildSelectionOutlineZIndex + 1);
        InteractionLayer.Children.Add(_childSelectionLabel);

        if (target.CanResize)
        {
            foreach (var handle in Enum.GetValues<FrontedDesignerResizeHandleKind>())
            {
                var handleElement = CreateChildResizeHandle(handle, target);
                _childResizeHandles[handle] = handleElement;
                InteractionLayer.Children.Add(handleElement);
            }
        }

        UpdateSubControlSelectionVisuals();
    }

    /// <summary>
    /// 创建子控件缩放手柄。手柄 Tag 携带手柄类别与子目标信息。
    /// </summary>
    /// <param name="handle">手柄方位。</param>
    /// <param name="target">子控件目标信息。</param>
    /// <returns>缩放手柄 Border。</returns>
    private Border CreateChildResizeHandle(FrontedDesignerResizeHandleKind handle, DesignerChildTargetInfo target)
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
                Background = Brushes.Orange,
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(FrontedDesignerEditorVisualHelper.HandleBorderThickness)
            },
            Cursor = GetCursor(handle),
            Tag = (handle, target)
        };

        Panel.SetZIndex(element, ChildSelectionHandleZIndex);
        element.MouseLeftButtonDown += ChildResizeHandle_OnMouseLeftButtonDown;
        return element;
    }

    /// <summary>
    /// 更新子控件选中装饰器与缩放手柄的位置。在几何变更或缩放比例变化时调用。
    /// </summary>
    private void UpdateSubControlSelectionVisuals()
    {
        if (_viewModel is null || _currentSubTargetInfo is not { } target)
        {
            return;
        }

        // 子控件几何可能已变更，重新从 ViewModel 获取最新信息。
        if (_viewModel.GetCurrentSubTargetInfo() is { } latestTarget)
        {
            _currentSubTargetInfo = latestTarget;
        }

        var currentTarget = _currentSubTargetInfo;
        if (currentTarget is null)
        {
            return;
        }

        var parentBounds = ResolveItemBounds(currentTarget.ParentItem);
        var bounds = new FrontedDesignerResolvedBounds(
            parentBounds.Left + currentTarget.Left,
            parentBounds.Top + currentTarget.Top,
            currentTarget.Width,
            currentTarget.Height);

        if (_childSelectionOutline is not null)
        {
            _childSelectionOutline.Width = bounds.Width;
            _childSelectionOutline.Height = bounds.Height;
            ApplySelectionOutlineZoomMetrics(_childSelectionOutline);
            Canvas.SetLeft(_childSelectionOutline, bounds.Left);
            Canvas.SetTop(_childSelectionOutline, bounds.Top);
        }

        if (_childSelectionLabel is not null)
        {
            var zoomScale = _viewModel.ZoomScale;
            if (_childSelectionLabel.Child is System.Windows.Controls.TextBlock textBlock)
            {
                textBlock.FontSize = FrontedDesignerEditorVisualHelper.GetEffectiveSelectionLabelFontSize(zoomScale);
            }

            var normalizedZoom = FrontedDesignerEditorVisualHelper.NormalizeZoomScale(zoomScale);
            ApplySelectionLabelChromeZoomMetrics(_childSelectionLabel, normalizedZoom);
            _childSelectionLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(_childSelectionLabel, bounds.Left);
            Canvas.SetTop(
                _childSelectionLabel,
                bounds.Top - _childSelectionLabel.DesiredSize.Height - 4D / normalizedZoom);
        }

        if (currentTarget.CanResize)
        {
            SetChildHandlePosition(FrontedDesignerResizeHandleKind.TopLeft, bounds.Left, bounds.Top);
            SetChildHandlePosition(FrontedDesignerResizeHandleKind.Top, bounds.Left + bounds.Width / 2, bounds.Top);
            SetChildHandlePosition(FrontedDesignerResizeHandleKind.TopRight, bounds.Left + bounds.Width, bounds.Top);
            SetChildHandlePosition(FrontedDesignerResizeHandleKind.Left, bounds.Left, bounds.Top + bounds.Height / 2);
            SetChildHandlePosition(FrontedDesignerResizeHandleKind.Right, bounds.Left + bounds.Width, bounds.Top + bounds.Height / 2);
            SetChildHandlePosition(FrontedDesignerResizeHandleKind.BottomLeft, bounds.Left, bounds.Top + bounds.Height);
            SetChildHandlePosition(FrontedDesignerResizeHandleKind.Bottom, bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height);
            SetChildHandlePosition(FrontedDesignerResizeHandleKind.BottomRight, bounds.Left + bounds.Width, bounds.Top + bounds.Height);
        }
    }

    /// <summary>
    /// 设置子控件缩放手柄位置。
    /// </summary>
    /// <param name="handle">手柄方位。</param>
    /// <param name="x">画布 X 坐标。</param>
    /// <param name="y">画布 Y 坐标。</param>
    private void SetChildHandlePosition(FrontedDesignerResizeHandleKind handle, double x, double y)
    {
        if (!_childResizeHandles.TryGetValue(handle, out var element))
        {
            return;
        }

        Canvas.SetLeft(element, x - element.Width / 2);
        Canvas.SetTop(element, y - element.Height / 2);
    }

    /// <summary>
    /// 已通过控件列表选中的子控件在画布上开始拖动。
    /// </summary>
    private void SubControlSelectionOutline_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: DesignerChildTargetInfo target } outline
            || _viewModel is null
            || !target.CanMove)
        {
            return;
        }

        FocusDesignSurface();
        e.Handled = true;
        _originalLeft = target.Left;
        _originalTop = target.Top;
        _originalWidth = target.Width;
        _originalHeight = target.Height;
        BeginSubControlInteraction(InteractionMode.SubControlMove, e.GetPosition(InteractionLayer), outline);
    }

    /// <summary>
    /// 子控件缩放手柄鼠标左键按下处理：启动缩放交互。
    /// </summary>
    private void ChildResizeHandle_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: (FrontedDesignerResizeHandleKind handle, DesignerChildTargetInfo target) } element
            || _viewModel is null
            || !target.CanResize)
        {
            return;
        }

        FocusDesignSurface();
        _activeResizeHandle = handle;
        _originalLeft = target.Left;
        _originalTop = target.Top;
        _originalWidth = target.Width;
        _originalHeight = target.Height;
        BeginSubControlInteraction(InteractionMode.Resize, e.GetPosition(InteractionLayer), element);
        e.Handled = true;
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
        var bounds = ResolveItemBounds(item);
        if (_parentSelectionOutline is not null)
        {
            var parentBounds = ResolveItemBounds(item);
            _parentSelectionOutline.Width = parentBounds.Width;
            _parentSelectionOutline.Height = parentBounds.Height;
            ApplySelectionOutlineZoomMetrics(_parentSelectionOutline);
            Canvas.SetLeft(_parentSelectionOutline, parentBounds.Left);
            Canvas.SetTop(_parentSelectionOutline, parentBounds.Top);
        }

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
            ApplySelectionOutlineZoomMetrics(_selectionOutline);
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
            ApplySelectionOutlineZoomMetrics(outline);
            Canvas.SetLeft(outline, bounds.Left);
            Canvas.SetTop(outline, bounds.Top);

            if (_hitboxes.TryGetValue(selectedItem, out var hitbox))
            {
                hitbox.Width = bounds.Width;
                hitbox.Height = bounds.Height;
                Canvas.SetLeft(hitbox, bounds.Left);
                Canvas.SetTop(hitbox, bounds.Top);
            }

            if (_multiResizeHandles.TryGetValue(selectedItem, out var handles))
            {
                SetMultiSelectionHandlePositions(bounds, handles);
            }

            outlineIndex++;
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

    private static System.Windows.Controls.TextBlock CreateSelectionLabelText(string text)
    {
        var textBlock = new System.Windows.Controls.TextBlock
        {
            Text = text,
            FontSize = FrontedDesignerEditorVisualHelper.SelectionLabelBaseFontSize
        };
        // 使用主题前景色：亮色主题下黑字、暗色主题下白字，避免白字叠在浅色标签上看不清。
        textBlock.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
        return textBlock;
    }

    private void ApplySelectionLabelZoomMetrics(FrontedDesignerResolvedBounds bounds)
    {
        if (_selectionLabel is null)
        {
            return;
        }

        var zoomScale = FrontedDesignerEditorVisualHelper.NormalizeZoomScale(_viewModel?.ZoomScale ?? 1D);
        if (_selectionLabel.Child is System.Windows.Controls.TextBlock textBlock)
        {
            textBlock.FontSize = FrontedDesignerEditorVisualHelper.GetEffectiveSelectionLabelFontSize(zoomScale);
        }

        ApplySelectionLabelChromeZoomMetrics(_selectionLabel, zoomScale);
        _selectionLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(_selectionLabel, bounds.Left);
        // 标签底边始终位于控件上边框之外，避免高缩放下遮住顶部三个手柄。
        Canvas.SetTop(
            _selectionLabel,
            bounds.Top - _selectionLabel.DesiredSize.Height - 4D / zoomScale);
    }

    private void ApplySelectionLabelChromeZoomMetrics(Border label, double zoomScale)
    {
        label.Padding = new Thickness(4D / zoomScale, 1D / zoomScale, 4D / zoomScale, 1D / zoomScale);
        label.CornerRadius = new CornerRadius(2D / zoomScale);
    }

    private void ApplySelectionOutlineZoomMetrics(Border outline)
    {
        var zoomScale = FrontedDesignerEditorVisualHelper.NormalizeZoomScale(_viewModel?.ZoomScale ?? 1D);
        // 高缩放下 BorderThickness 小于 1 个屏幕像素会被 WPF 抗锯齿“吃掉”；
        // 限制最小画布线宽，保证任何缩放下至少约 1 个屏幕像素可见。
        var thickness = Math.Max(1D / zoomScale, 0.1D);
        outline.BorderThickness = new Thickness(thickness);
        outline.SnapsToDevicePixels = true;
    }

    private void SetHandlePosition(FrontedDesignerResizeHandleKind handle, double x, double y)
    {
        if (!_resizeHandles.TryGetValue(handle, out var element))
        {
            return;
        }

        SetHandlePosition(element, x, y);
    }

    private void SetHandlePosition(Border element, double x, double y)
    {
        ApplyHandleZoomCompensation(element);
        Canvas.SetLeft(element, x - element.Width / 2);
        Canvas.SetTop(element, y - element.Height / 2);
    }

    /// <summary>
    /// 手柄位于缩放宿主内部，布局尺寸会随 ZoomScale 一起缩放；
    /// 反向补偿以保持屏幕上约 12px 命中区域、6px 可见方块。
    /// </summary>
    private void ApplyHandleZoomCompensation(Border handle)
    {
        var zoom = Math.Clamp(_viewModel?.ZoomScale ?? 1D, 0.05D, 4D);
        handle.Width = FrontedDesignerEditorVisualHelper.HandleHitTargetSize / zoom;
        handle.Height = FrontedDesignerEditorVisualHelper.HandleHitTargetSize / zoom;
        if (handle.Child is Border visual)
        {
            visual.Width = FrontedDesignerEditorVisualHelper.HandleVisualSize / zoom;
            visual.Height = FrontedDesignerEditorVisualHelper.HandleVisualSize / zoom;
            visual.BorderThickness = new Thickness(FrontedDesignerEditorVisualHelper.HandleBorderThickness / zoom);
        }
    }

}
