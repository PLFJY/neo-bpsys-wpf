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
        _resizeHandles.Clear();
        _multiSelectionOutlines.Clear();
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

            var hitbox = CreateHitbox(entry.Item, entry.Index);
            _hitboxes[entry.Item] = hitbox;
            InteractionLayer.Children.Add(hitbox);
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
            Child = new System.Windows.Controls.TextBlock
            {
                Text = item.Name,
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
            Child = new System.Windows.Controls.TextBlock
            {
                Text = target.IsCollectionItem ? $"{target.Id} [{target.ItemKey}]" : target.Id,
                FontSize = FrontedDesignerEditorVisualHelper.SelectionLabelBaseFontSize,
                Foreground = Brushes.White
            },
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

            var topOffset = FrontedDesignerEditorVisualHelper.GetEffectiveSelectionLabelTopOffset(zoomScale);
            Canvas.SetLeft(_childSelectionLabel, bounds.Left);
            Canvas.SetTop(_childSelectionLabel, Math.Max(0, bounds.Top - topOffset));
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

}
