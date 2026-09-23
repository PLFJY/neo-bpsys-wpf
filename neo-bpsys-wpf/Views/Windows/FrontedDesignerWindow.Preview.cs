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
    private void OnPreviewRenderRequested(
        object? sender,
        FrontedDesignerPreviewRenderRequestedEventArgs e)
    {
        _pendingPreviewRenderArgs = e;
        if (_previewRenderScheduled)
        {
            return;
        }

        _previewRenderScheduled = true;
        Dispatcher.BeginInvoke(
            new Action(ExecutePendingPreviewRender),
            DispatcherPriority.Background);
    }

    private void ExecutePendingPreviewRender()
    {
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
            ConfigureDesignSurface(e.Config.CanvasWidth, e.Config.CanvasHeight);
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
        PreviewCanvas.Children.Clear();
        _previewElementsByControlName.Clear();
        _viewModel?.ClearBehaviorPreviewAnimationScope();
        PreviewCanvas.Background = null;
        ConfigureDesignSurface(640, 360);
        InteractionLayer.Children.Clear();
        _snapGuideLines.Clear();
        _hitboxes.Clear();
        _resizeHandles.Clear();
        _polygonVertexHandles.Clear();
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

        if (item.Config.Width.HasValue)
        {
            element.Width = item.Config.Width.Value;
        }

        if (item.Config.Height.HasValue)
        {
            element.Height = item.Config.Height.Value;
        }

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

        foreach (var child in generatedChildren)
        {
            PreviewCanvas.Children.Remove(child);
        }

        foreach (var child in generatedChildren)
        {
            PreviewCanvas.Children.Add(child);
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
        PreviewWorkspace.MinWidth = Math.Max(1D, PreviewScrollViewer.ViewportWidth);
        PreviewWorkspace.MinHeight = Math.Max(1D, PreviewScrollViewer.ViewportHeight);
    }

    private void ResetPreviewScrollOffsetForFitMode()
    {
        if (_viewModel?.IsFitMode != true)
        {
            return;
        }

        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(() =>
            {
                PreviewScrollViewer.ScrollToHorizontalOffset(0D);
                PreviewScrollViewer.ScrollToVerticalOffset(0D);
            }));
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

        if (config.ImageWidth.HasValue)
        {
            innerImage.Width = config.ImageWidth.Value;
        }

        if (config.ImageHeight.HasValue)
        {
            innerImage.Height = config.ImageHeight.Value;
        }
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
