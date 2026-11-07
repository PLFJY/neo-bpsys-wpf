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
/// FrontedDesignerWindow 的Preview业务交互逻辑。
/// </summary>
public partial class FrontedDesignerWindow
{
    private void InitializePreviewRenderDebounceTimer()
    {
        _previewRenderDebounceTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            // 属性编辑、粘贴/删除可能在同一帧内连续请求预览；合并为最后一次完整渲染。
            Interval = TimeSpan.FromMilliseconds(80)
        };
        _previewRenderDebounceTimer.Tick += (_, _) =>
        {
            _previewRenderDebounceTimer?.Stop();
            ExecutePendingPreviewRender();
        };
    }

    private void OnControlsChangedRequested(
        object? sender,
        FrontedDesignerControlsChangedRequestedEventArgs e)
    {
        if (!_isLoaded
            || (e.Added.Count == 0 && e.Removed.Count == 0)
            || e.Config is not { } config
            || _lastPreviewRenderArgs is not { Context: { } context }
            || _renderer is not IFrontedIncrementalRenderer renderer)
        {
            RequestFullPreviewFallback();
            return;
        }

        var changed = false;
        var needsFallback = false;
        try
        {
            if (e.Removed.Count > 0)
            {
                var removedNames = e.Removed.Select(item => item.Name).Distinct(StringComparer.Ordinal).ToArray();
                if (renderer.RemoveControls(PreviewCanvas, removedNames) != removedNames.Length)
                {
                    // 控件未在预览树中找到时无法确认视觉已清除，回退完整渲染。
                    needsFallback = true;
                }
                else
                {
                    changed = true;
                }
            }

            if (!needsFallback && e.Added.Count > 0)
            {
                var addedNames = e.Added.Select(item => item.Name).Distinct(StringComparer.Ordinal).ToArray();
                if (renderer.AddControls(PreviewCanvas, config, context, addedNames) != addedNames.Length)
                {
                    needsFallback = true;
                }
                else
                {
                    changed = true;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Designer preview control add/remove failed; falling back to full render.");
            needsFallback = true;
        }

        if (needsFallback)
        {
            RequestFullPreviewFallback();
            return;
        }

        if (!changed)
        {
            return;
        }

        // 新增/删除只影响对应控件 host；现有其他控件动画部件无需重新应用。
        CompleteIncrementalPreviewUpdate(applyAnimationParts: false);
    }

    private void OnControlRefreshRequested(
        object? sender,
        FrontedDesignerControlRefreshRequestedEventArgs e)
    {
        if (!_isLoaded
            || e.Controls.Count == 0
            || e.Config is not { } config
            || _lastPreviewRenderArgs is not { Context: { } context }
            || _renderer is not IFrontedIncrementalRenderer renderer)
        {
            RequestFullPreviewFallback();
            return;
        }

        var refreshFailed = false;
        try
        {
            var names = e.Controls.Select(item => item.Name).Distinct(StringComparer.Ordinal).ToArray();
            var refreshedCount = renderer.RefreshControls(PreviewCanvas, config, context, names);
            refreshFailed = refreshedCount != names.Length;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Designer preview control refresh failed; falling back to full render.");
            refreshFailed = true;
        }

        if (refreshFailed)
        {
            RequestFullPreviewFallback();
            return;
        }

        var behaviorGuids = e.Controls
            .Select(item => item.Config.BehaviorGuid)
            .Where(guid => guid != Guid.Empty)
            .ToHashSet();
        CompleteIncrementalPreviewUpdate(behaviorGuids);
    }

    private void CompleteIncrementalPreviewUpdate(
        IReadOnlySet<Guid>? behaviorGuids = null,
        bool applyAnimationParts = true)
    {
        PopulatePreviewElementRegistry();
        if (applyAnimationParts && _viewModel?.BehaviorPanel.CurrentDocument is { } behaviorDocument)
        {
            if (_animationPartRenderer is IFrontedFilteredBehaviorAnimationPartRenderer concreteRenderer)
            {
                concreteRenderer.ApplyAnimationParts(PreviewCanvas, behaviorDocument, behaviorGuids);
            }
            else
            {
                _animationPartRenderer?.ApplyAnimationParts(PreviewCanvas, behaviorDocument);
            }
        }

        PreviewCanvas.UpdateLayout();
        // 选择集合变更时 View 已重建交互层；这里只同步当前选中项的装饰器，
        // 避免每次属性刷新都重新遍历全部控件 hitbox。
        UpdateSelectedInteractionVisuals();
        UpdateSelectedPreviewElement();
        _viewModel?.UpdateBehaviorPreviewAnimationScope(PreviewCanvas);
    }

    private void RequestFullPreviewFallback()
    {
        if (_viewModel is not null)
        {
            _viewModel.RequestFullPreviewRefresh();
        }
        else if (_lastPreviewRenderArgs is not null)
        {
            OnPreviewRenderRequested(this, _lastPreviewRenderArgs);
        }
    }

    private void OnPreviewRenderRequested(
        object? sender,
        FrontedDesignerPreviewRenderRequestedEventArgs e)
    {
        if (!_isLoaded) return;
        _pendingPreviewRenderArgs = e;
        if (_previewRenderScheduled)
        {
            // 连续编辑时延后到下一次停顿再渲染，避免在输入/拖动过程中反复全量重建预览。
            if (_previewRenderDebounceTimer is not null)
            {
                _previewRenderDebounceTimer.Stop();
                _previewRenderDebounceTimer.Start();
            }

            return;
        }

        _previewRenderScheduled = true;
        if (_previewRenderDebounceTimer is not null)
        {
            _previewRenderDebounceTimer.Stop();
            _previewRenderDebounceTimer.Start();
            return;
        }

        _previewRenderOperation = Dispatcher.BeginInvoke(
            new Action(ExecutePendingPreviewRender),
            DispatcherPriority.Background);
    }

    private void ExecutePendingPreviewRender()
    {
        _previewRenderOperation = null;
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
        _lastPreviewRenderArgs = e;
        var total = StartDesignerPerfTrace();
        var isInitialPreview = !_initialPreviewReady.Task.IsCompleted;
        if (isInitialPreview)
        {
            _logger?.LogInformation("Initial preview render started.");
        }
        if (_renderer is null || e.Config is null || e.Context is null)
        {
            ClearPreviewCanvas();
            LogDesignerPerf("PreviewRender", "clear", Elapsed(total));
            return;
        }

        try
        {
            // 完整渲染会替换控件；旧动画 session 的 BaseValues/overlay 仍引用旧视觉对象。
            _viewModel?.ClearBehaviorPreviewAnimationScope();
            ConfigureDesignSurface(e.Config.CanvasWidth, e.Config.CanvasHeight, updateFit: isInitialPreview);
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
            if (isInitialPreview)
            {
                _logger?.LogInformation("Initial preview render completed.");
                _initialPreviewReady.TrySetResult();
            }
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
        _viewModel?.ClearBehaviorPreviewAnimationScope();
        FrontedRenderer.ClearGeneratedControls(PreviewCanvas);
        PreviewCanvas.Children.Clear();
        _previewElementsByControlName.Clear();
        PreviewCanvas.Background = null;
        ConfigureDesignSurface(640, 360);
        InteractionLayer.Children.Clear();
        _snapGuideLines.Clear();
        _hitboxes.Clear();
        _resizeHandles.Clear();
        _polygonVertexHandles.Clear();
        _multiSelectionOutlines.Clear();
        _multiSelectionOutlinePool.Clear();
        _multiResizeHandles.Clear();
        _childResizeHandles.Clear();
        _currentSubTargetInfo = null;
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
            foreach (var (item, index) in _viewModel.CurrentDocument.Controls.Select((item, index) => (item, index)))
            {
                if (_hitboxes.TryGetValue(item, out var hitbox))
                {
                    Panel.SetZIndex(hitbox, FrontedDesignerEditorVisualHelper.GetHitboxZIndex(
                        item.Config.ZIndex, index, ReferenceEquals(item, _viewModel.SelectedDesignItem)));
                }
            }
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
                Panel.SetZIndex(FrontedEffectHostFactory.ResolveLayoutCarrier(element), item.Config.ZIndex);
            }
        }

        return true;
    }

    private void ApplyPreviewElementGeometry(FrameworkElement element, FrontedControlDesignItem item)
    {
        var layoutCarrier = FrontedEffectHostFactory.ResolveLayoutCarrier(element);
        Canvas.SetLeft(layoutCarrier, item.Config.Left);
        Canvas.SetTop(layoutCarrier, item.Config.Top);

        element.Width = item.Config.Width ?? double.NaN;
        element.Height = item.Config.Height ?? double.NaN;

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
            .Cast<FrameworkElement>()
            .Select(FrontedEffectHostFactory.ResolveLayoutCarrier)
            .Distinct()
            .Cast<UIElement>()
            .ToList();

        for (var index = 0; index < generatedChildren.Count; index++)
        {
            var child = generatedChildren[index];
            if (PreviewCanvas.Children.IndexOf(child) == index) continue;
            PreviewCanvas.Children.Remove(child);
            PreviewCanvas.Children.Insert(index, child);
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

    private void UpdatePreviewWorkspaceSize()
    {
        // 工作区至少与视口同大；内容放大后同步扩大，避免居中布局把超出视口的部分放到负坐标而被裁剪。
        var zoom = _viewModel?.ZoomScale ?? 1D;
        var contentWidth = Math.Max(1D, _designSurfaceContentWidth) * zoom;
        var contentHeight = Math.Max(1D, _designSurfaceContentHeight) * zoom;
        PreviewWorkspace.MinWidth = Math.Max(1D, Math.Max(PreviewScrollViewer.ViewportWidth, contentWidth));
        PreviewWorkspace.MinHeight = Math.Max(1D, Math.Max(PreviewScrollViewer.ViewportHeight, contentHeight));
    }

    /// <summary>
    /// 缩小时 extent/视口会变化，保留的平移量可能把画布推到可视区外；
    /// 在布局稳定后重新按当前滚动范围夹取平移量。
    /// </summary>
    private void SchedulePreviewPanClamp()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            if (_viewModel is null)
            {
                return;
            }

            var x = ClampViewportPan(
                PreviewPanTransform.X,
                PreviewScrollViewer.ExtentWidth,
                PreviewScrollViewer.ViewportWidth,
                PreviewScrollViewer.HorizontalOffset);
            var y = ClampViewportPan(
                PreviewPanTransform.Y,
                PreviewScrollViewer.ExtentHeight,
                PreviewScrollViewer.ViewportHeight,
                PreviewScrollViewer.VerticalOffset);
            if (Math.Abs(x - PreviewPanTransform.X) < 0.1D
                && Math.Abs(y - PreviewPanTransform.Y) < 0.1D)
            {
                return;
            }

            PreviewPanTransform.X = x;
            PreviewPanTransform.Y = y;
            PreviewWorkspace.InvalidateVisual();
        }));
    }

    private void ResetPreviewScrollOffsetForFitMode()
    {
        if (_viewModel?.IsFitMode != true)
        {
            return;
        }

        if (_fitScrollResetOperation is { Status: DispatcherOperationStatus.Pending })
        {
            // 适应布局期间滚动条显隐/尺寸变化会再次请求居中；不能直接丢弃，
            // 否则最后一次布局变化后没有机会重新滚动到正确位置。
            _fitScrollResetRequested = true;
            return;
        }

        // 适应后滚动条出现/消失会再次改变视口尺寸；等到布局和滚动条都稳定后再居中，
        // 否则首次计算使用的是旧视口，必须再点击一次画布才恢复正确位置。
        _fitScrollResetOperation = Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(() =>
            {
                _fitScrollResetOperation = null;
                _fitScrollResetRequested = false;
                PreviewPanTransform.X = 0D;
                PreviewPanTransform.Y = 0D;
                UpdateDesignSurfaceExtent();
                UpdatePreviewWorkspaceSize();

                // 适应操作后把键盘焦点交回画布。点击画布会触发焦点/布局链，使
                // 延迟的 ScrollViewer 滚动范围最终生效；这里主动聚焦，避免用户
                // 必须先点一次画布才能看到正确的适应位置。
                InteractionLayer.Focus();
                PreviewScrollViewer.UpdateLayout();
                CenterPreviewContentInViewport();

                // 滚动条显隐会触发第二次布局，再补一次居中，确保最终位置稳定。
                Dispatcher.BeginInvoke(
                    DispatcherPriority.ContextIdle,
                    new Action(() =>
                    {
                        CenterPreviewContentInViewport();
                        if (_fitScrollResetRequested)
                        {
                            ResetPreviewScrollOffsetForFitMode();
                        }
                    }));
            }));
    }

    /// <summary>
    /// 滚动范围变化后重新居中适应模式下的内容。滚动条显隐、窗口尺寸变化或
    /// 设计表面重新布局都可能在首次居中之后才更新 extent，必须再对齐一次。
    /// </summary>
    private void PreviewScrollViewer_OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_viewModel?.IsFitMode != true)
        {
            return;
        }

        // 拖动控件/缩放手柄期间，内容宽高会连续变化；此时若跟着滚动居中，
        // 画布会在指针下移动，表现为宽度调整跟不上鼠标。等交互结束后再居中。
        if (_capturedElement is not null || _isPanningViewport || _suppressFitScrollCentering)
        {
            return;
        }

        if (Math.Abs(e.ExtentWidthChange) < 0.01D
            && Math.Abs(e.ExtentHeightChange) < 0.01D)
        {
            return;
        }

        CenterPreviewContentInViewport();
    }

    /// <summary>
    /// 将设计表面内容中心滚动到预览视口中心。
    /// </summary>
    private void CenterPreviewContentInViewport()
    {
        if (_viewModel?.IsFitMode != true)
        {
            return;
        }

        var viewportWidth = PreviewScrollViewer.ViewportWidth;
        var viewportHeight = PreviewScrollViewer.ViewportHeight;
        var extentWidth = PreviewScrollViewer.ExtentWidth;
        var extentHeight = PreviewScrollViewer.ExtentHeight;
        if (viewportWidth <= 0D || viewportHeight <= 0D
            || extentWidth <= 0D || extentHeight <= 0D)
        {
            return;
        }

        // 使用 ScrollViewer 实际 extent 居中，兼容滚动条出现后视口变化及
        // 设计表面为画布外控件扩展出的空白。
        PreviewScrollViewer.ScrollToHorizontalOffset(
            Math.Max(0D, (extentWidth - viewportWidth) / 2D));
        PreviewScrollViewer.ScrollToVerticalOffset(
            Math.Max(0D, (extentHeight - viewportHeight) / 2D));
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

        innerImage.Width = config.ImageWidth ?? double.NaN;
        innerImage.Height = config.ImageHeight ?? double.NaN;
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

}
