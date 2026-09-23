using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Messages;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer.V3;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Parts;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.StyleTransfer;
using neo_bpsys_wpf.Core.Models.ScoreSystem;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Geometry;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Parts;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3.StyleTransfer;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Services.FrontedDesigner;
using neo_bpsys_wpf.ViewModels.FrontedDesigner;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace neo_bpsys_wpf.ViewModels.Windows;

/// <summary>
/// Fronted Designer 的几何编辑与图层投放业务逻辑。
/// </summary>
public partial class FrontedDesignerWindowViewModel
{
    /// <summary>
    /// 按逻辑增量从拖拽起点移动主选中设计项。
    /// </summary>
    /// <param name="originalLeft">拖动起始左坐标。</param>
    /// <param name="originalTop">拖动起始顶坐标。</param>
    /// <param name="deltaX">水平指针增量。</param>
    /// <param name="deltaY">垂直指针增量。</param>
    /// <param name="renderPreview">是否请求更新预览。</param>
    public void MoveSelectedDesignItem(
        double originalLeft,
        double originalTop,
        double deltaX,
        double deltaY,
        bool renderPreview)
    {
        if (CurrentDocument is null || SelectedDesignItem is null || IsRebuildingPropertyGrid)
        {
            return;
        }

        // 子控件（Part/CollectionItem）选中时，Move 通过 GeometryTarget 执行，
        // 坐标相对于父控件。GeometryTarget 内部遵守 Capabilities 约束（Resize-only 不写入）。
        if (_selectedTarget is { Kind: not FrontedV3DesignSelectionKind.Root } subTarget)
        {
            var geometry = subTarget.GeometryTarget;
            var newLeft = originalLeft + deltaX;
            var newTop = originalTop + deltaY;
            if (EffectiveSnapEnabled)
            {
                newLeft = FrontedDesignerGeometryHelper.Snap(newLeft);
                newTop = FrontedDesignerGeometryHelper.Snap(newTop);
            }

            geometry.MoveTo(newLeft, newTop);
            CurrentDocument.IsDirty = true;
            OnDesignItemGeometryChanged(renderPreview);
            return;
        }

        var selectedItems = GetMovableSelectedDesignItems();
        if (selectedItems.Count > 1)
        {
            var changedItems = new List<FrontedControlDesignItem>();
            foreach (var selectedItem in selectedItems)
            {
                FrontedDesignerGeometryHelper.Move(
                    selectedItem,
                    selectedItem.Config.Left,
                    selectedItem.Config.Top,
                    deltaX,
                    deltaY,
                    CurrentDocument,
                    EffectiveSnapEnabled,
                    SnapGridSize);
                changedItems.Add(selectedItem);
                foreach (var linkedOverlay in SyncLinkedOverlays(selectedItem))
                {
                    if (!changedItems.Contains(linkedOverlay))
                    {
                        changedItems.Add(linkedOverlay);
                    }
                }
            }

            CurrentDocument.IsDirty = true;
            ClearActiveSnapGuides();
            OnDesignItemGeometryChanged(renderPreview);
            return;
        }

        var bounds = FrontedDesignerBoundsResolver.Resolve(SelectedDesignItem.Config);
        var result = FrontedDesignerSmartSnapHelper.Move(
            SelectedDesignItem,
            CurrentDocument,
            originalLeft,
            originalTop,
            bounds.Width,
            bounds.Height,
            deltaX,
            deltaY,
            EffectiveSnapEnabled,
            SnapGridSize,
            FrontedDesignerSmartSnapHelper.CalculateLogicalTolerance(ZoomScale));

        SelectedDesignItem.Config.Left = result.Left;
        SelectedDesignItem.Config.Top = result.Top;
        CurrentDocument.IsDirty = true;
        ActiveSnapGuides = EffectiveSnapEnabled ? result.Guides : [];
        SyncLinkedOverlays(SelectedDesignItem);
        OnDesignItemGeometryChanged(renderPreview);
    }

    /// <summary>
    /// 按逻辑增量从原始拖拽起点边界移动选中控件。
    /// </summary>
    /// <param name="originalBounds">拖动开始时捕获的原始边界。</param>
    /// <param name="deltaX">水平指针增量。</param>
    /// <param name="deltaY">垂直指针增量。</param>
    /// <param name="renderPreview">是否立即渲染完整预览。</param>
    public void MoveSelectedDesignItems(
        IReadOnlyDictionary<FrontedControlDesignItem, FrontedDesignerResolvedBounds> originalBounds,
        double deltaX,
        double deltaY,
        bool renderPreview)
    {
        if (CurrentDocument is null || originalBounds.Count == 0)
        {
            return;
        }

        var selectedItems = GetMovableSelectedDesignItems();
        if (selectedItems.Count <= 1)
        {
            var bounds = originalBounds.Values.FirstOrDefault();
            MoveSelectedDesignItem(
                originalBounds.Count > 0 ? bounds.Left : SelectedDesignItem?.Config.Left ?? 0D,
                originalBounds.Count > 0 ? bounds.Top : SelectedDesignItem?.Config.Top ?? 0D,
                deltaX,
                deltaY,
                renderPreview);
            return;
        }

        var primaryItem = SelectedDesignItem is { } primarySelectedItem && selectedItems.Contains(primarySelectedItem)
            ? primarySelectedItem
            : selectedItems[0];
        if (!originalBounds.TryGetValue(primaryItem, out var primaryBounds))
        {
            return;
        }

        var appliedDeltaX = EffectiveSnapEnabled
            ? FrontedDesignerGeometryHelper.NormalizeCoordinate(
                primaryBounds.Left + deltaX,
                effectiveSnapEnabled: true,
                SnapGridSize) - primaryBounds.Left
            : FrontedDesignerGeometryHelper.Snap(primaryBounds.Left + deltaX) - primaryBounds.Left;
        var appliedDeltaY = EffectiveSnapEnabled
            ? FrontedDesignerGeometryHelper.NormalizeCoordinate(
                primaryBounds.Top + deltaY,
                effectiveSnapEnabled: true,
                SnapGridSize) - primaryBounds.Top
            : FrontedDesignerGeometryHelper.Snap(primaryBounds.Top + deltaY) - primaryBounds.Top;

        foreach (var selectedItem in selectedItems)
        {
            if (!originalBounds.TryGetValue(selectedItem, out var bounds))
            {
                continue;
            }

            // 多选控件必须使用主选中项计算出的同一吸附增量，
            // 不能分别对每个绝对坐标取整，否则会破坏相对位置并造成拖动抖动。
            selectedItem.Config.Left = bounds.Left + appliedDeltaX;
            selectedItem.Config.Top = bounds.Top + appliedDeltaY;
            SyncLinkedOverlays(selectedItem);
        }

        CurrentDocument.IsDirty = true;
        ClearActiveSnapGuides();
        OnDesignItemGeometryChanged(renderPreview);
    }

    /// <summary>
    /// 按增量移动当前选择，通常用于键盘微调。
    /// </summary>
    /// <param name="deltaX">水平增量。</param>
    /// <param name="deltaY">垂直增量。</param>
    public void MoveSelectedDesignItemBy(double deltaX, double deltaY)
    {
        if (CurrentDocument is null || SelectedDesignItem is null)
        {
            return;
        }

        CaptureUndoSnapshot();
        ClearActiveSnapGuides();

        // 子控件选中时，键盘微调通过 GeometryTarget 执行。
        if (_selectedTarget is { Kind: not FrontedV3DesignSelectionKind.Root } subTarget)
        {
            var geometry = subTarget.GeometryTarget;
            geometry.MoveTo(geometry.Left + deltaX, geometry.Top + deltaY);
            CurrentDocument.IsDirty = true;
            OnDesignItemGeometryChanged(renderPreview: false);
            RequestDesignerGeometryPatch([SelectedDesignItem], updateSelection: true);
            return;
        }

        var selectedItems = GetMovableSelectedDesignItems();
        if (selectedItems.Count > 1)
        {
            var primaryItem = SelectedDesignItem is { } primarySelectedItem && selectedItems.Contains(primarySelectedItem)
                ? primarySelectedItem
                : selectedItems[0];
            var appliedDeltaX = EffectiveSnapEnabled
                ? FrontedDesignerGeometryHelper.NormalizeCoordinate(
                    primaryItem.Config.Left + deltaX,
                    effectiveSnapEnabled: true,
                    SnapGridSize) - primaryItem.Config.Left
                : FrontedDesignerGeometryHelper.Snap(primaryItem.Config.Left + deltaX) - primaryItem.Config.Left;
            var appliedDeltaY = EffectiveSnapEnabled
                ? FrontedDesignerGeometryHelper.NormalizeCoordinate(
                    primaryItem.Config.Top + deltaY,
                    effectiveSnapEnabled: true,
                    SnapGridSize) - primaryItem.Config.Top
                : FrontedDesignerGeometryHelper.Snap(primaryItem.Config.Top + deltaY) - primaryItem.Config.Top;
            var batchChangedItems = new List<FrontedControlDesignItem>();
            foreach (var selectedItem in selectedItems)
            {
                selectedItem.Config.Left += appliedDeltaX;
                selectedItem.Config.Top += appliedDeltaY;
                batchChangedItems.Add(selectedItem);
                foreach (var linkedOverlay in SyncLinkedOverlays(selectedItem))
                {
                    if (!batchChangedItems.Contains(linkedOverlay))
                    {
                        batchChangedItems.Add(linkedOverlay);
                    }
                }
            }

            CurrentDocument.IsDirty = true;
            OnDesignItemGeometryChanged(renderPreview: false);
            RequestDesignerGeometryPatch(batchChangedItems, updateSelection: true);
            return;
        }

        FrontedDesignerGeometryHelper.MoveBy(
            SelectedDesignItem,
            deltaX,
            deltaY,
            CurrentDocument,
            EffectiveSnapEnabled,
            SnapGridSize);
        var changedItems = new List<FrontedControlDesignItem> { SelectedDesignItem };
        foreach (var linkedOverlay in SyncLinkedOverlays(SelectedDesignItem))
        {
            if (!changedItems.Contains(linkedOverlay))
            {
                changedItems.Add(linkedOverlay);
            }
        }

        OnDesignItemGeometryChanged(renderPreview: false);
        RequestDesignerGeometryPatch(changedItems, updateSelection: true);
    }

    /// <summary>
    /// 通过拖拽手柄缩放主选中设计项。
    /// </summary>
    /// <param name="handle">正在拖动的调整大小句柄。</param>
    /// <param name="originalLeft">拖动起始左坐标。</param>
    /// <param name="originalTop">拖动起始顶坐标。</param>
    /// <param name="originalWidth">拖动起始宽度。</param>
    /// <param name="originalHeight">拖动起始高度。</param>
    /// <param name="deltaX">水平拖动增量。</param>
    /// <param name="deltaY">垂直拖动增量。</param>
    /// <param name="renderPreview">是否请求更新预览。</param>
    public void ResizeSelectedDesignItem(
        FrontedDesignerResizeHandleKind handle,
        double originalLeft,
        double originalTop,
        double originalWidth,
        double originalHeight,
        double deltaX,
        double deltaY,
        bool renderPreview)
    {
        if (CurrentDocument is null || SelectedDesignItem is null)
        {
            return;
        }

        // 子控件（Part/CollectionItem）选中时，Resize 通过 GeometryTarget 执行。
        // 几何值通过 GeometryTarget.ResizeTo 写入，坐标相对于父控件。
        // GeometryTarget 内部遵守 Capabilities 约束（Move-only 不写入尺寸）。
        if (_selectedTarget is { Kind: not FrontedV3DesignSelectionKind.Root } subTarget)
        {
            var geometry = subTarget.GeometryTarget;
            FrontedDesignerGeometryHelper.ComputeResizedBounds(
                handle,
                originalLeft,
                originalTop,
                originalWidth,
                originalHeight,
                deltaX,
                deltaY,
                EffectiveSnapEnabled,
                SnapGridSize,
                out var newLeft,
                out var newTop,
                out var newWidth,
                out var newHeight);

            geometry.ResizeTo(newLeft, newTop, newWidth, newHeight);
            CurrentDocument.IsDirty = true;
            OnDesignItemGeometryChanged(renderPreview);
            return;
        }

        var selectedItems = GetMovableSelectedDesignItems();
        if (selectedItems.Count > 1)
        {
            var changedItems = new List<FrontedControlDesignItem>();
            foreach (var selectedItem in selectedItems)
            {
                var bounds = FrontedDesignerBoundsResolver.Resolve(selectedItem.Config);
                FrontedDesignerGeometryHelper.Resize(
                    selectedItem,
                    handle,
                    selectedItem.Config.Left,
                    selectedItem.Config.Top,
                    bounds.Width,
                    bounds.Height,
                    deltaX,
                    deltaY,
                    CurrentDocument,
                    EffectiveSnapEnabled,
                    SnapGridSize);
                changedItems.Add(selectedItem);
                foreach (var linkedOverlay in SyncLinkedOverlays(selectedItem))
                {
                    if (!changedItems.Contains(linkedOverlay))
                    {
                        changedItems.Add(linkedOverlay);
                    }
                }
            }

            CurrentDocument.IsDirty = true;
            ClearActiveSnapGuides();
            OnDesignItemGeometryChanged(renderPreview);
            return;
        }

        var result = FrontedDesignerSmartSnapHelper.Resize(
            SelectedDesignItem,
            CurrentDocument,
            handle,
            originalLeft,
            originalTop,
            originalWidth,
            originalHeight,
            deltaX,
            deltaY,
            EffectiveSnapEnabled,
            SnapGridSize,
            FrontedDesignerSmartSnapHelper.CalculateLogicalTolerance(ZoomScale));

        SelectedDesignItem.Config.Left = result.Left;
        SelectedDesignItem.Config.Top = result.Top;
        SelectedDesignItem.Config.Width = result.Width;
        SelectedDesignItem.Config.Height = result.Height;
        CurrentDocument.IsDirty = true;
        ActiveSnapGuides = EffectiveSnapEnabled ? result.Guides : [];
        SyncLinkedOverlays(SelectedDesignItem);
        OnDesignItemGeometryChanged(renderPreview);
    }

    /// <summary>
    /// 按逻辑增量从原始拖拽起点边界缩放选中控件。
    /// </summary>
    /// <param name="handle">正在拖拽的缩放手柄。</param>
    /// <param name="originalBounds">调整大小开始时捕获的原始边界。</param>
    /// <param name="deltaX">水平指针增量。</param>
    /// <param name="deltaY">垂直指针增量。</param>
    /// <param name="renderPreview">是否立即渲染完整预览。</param>
    public void ResizeSelectedDesignItems(
        FrontedDesignerResizeHandleKind handle,
        IReadOnlyDictionary<FrontedControlDesignItem, FrontedDesignerResolvedBounds> originalBounds,
        double deltaX,
        double deltaY,
        bool renderPreview)
    {
        if (CurrentDocument is null || originalBounds.Count == 0)
        {
            return;
        }

        var selectedItems = GetMovableSelectedDesignItems();
        if (selectedItems.Count <= 1)
        {
            var bounds = originalBounds.Values.FirstOrDefault();
            ResizeSelectedDesignItem(
                handle,
                originalBounds.Count > 0 ? bounds.Left : SelectedDesignItem?.Config.Left ?? 0D,
                originalBounds.Count > 0 ? bounds.Top : SelectedDesignItem?.Config.Top ?? 0D,
                originalBounds.Count > 0 ? bounds.Width : SelectedDesignItem?.Config.Width ?? FrontedDesignerGeometryHelper.MinHitWidth,
                originalBounds.Count > 0 ? bounds.Height : SelectedDesignItem?.Config.Height ?? FrontedDesignerGeometryHelper.MinHitHeight,
                deltaX,
                deltaY,
                renderPreview);
            return;
        }

        foreach (var selectedItem in selectedItems)
        {
            if (!originalBounds.TryGetValue(selectedItem, out var bounds))
            {
                continue;
            }

            FrontedDesignerGeometryHelper.Resize(
                selectedItem,
                handle,
                bounds.Left,
                bounds.Top,
                bounds.Width,
                bounds.Height,
                deltaX,
                deltaY,
                CurrentDocument,
                EffectiveSnapEnabled,
                SnapGridSize);
            SyncLinkedOverlays(selectedItem);
        }

        CurrentDocument.IsDirty = true;
        ClearActiveSnapGuides();
        OnDesignItemGeometryChanged(renderPreview);
    }

    public void ClearActiveSnapGuides()
    {
        if (ActiveSnapGuides.Count > 0)
        {
            ActiveSnapGuides = [];
        }
    }

    public IReadOnlyList<FrontedControlDesignItem> SyncLinkedOverlays(
        FrontedControlDesignItem changedTarget,
        FrontedDesignerResolvedBounds? targetBounds = null)
    {
        return [];
    }

    public bool IsLayerReorderable(FrontedControlDesignItem? item)
    {
        return item is
        {
            IsSelectableInEditor: true,
            IsEditableInEditor: true,
            IsLinkedOverlay: false
        };
    }

    public bool CommitLayerDrop(
        FrontedControlDesignItem source,
        int? targetZIndex,
        FrontedControlDesignItem? targetItem,
        bool insertAfter,
        bool moveToNewTopLayer = false,
        bool moveToNewBottomLayer = false)
    {
        if (CurrentDocument is null || !CanReorderLayers || !IsLayerReorderable(source))
        {
            StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.LayerPanel.ReorderBlocked");
            return false;
        }

        if (targetItem is not null && !IsLayerReorderable(targetItem))
        {
            StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.LayerPanel.ReorderBlocked");
            return false;
        }

        var reorderableItems = CurrentDocument.Controls
            .Where(IsLayerReorderable)
            .ToList();
        if (!reorderableItems.Contains(source))
        {
            StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.LayerPanel.ReorderBlocked");
            return false;
        }

        var oldSnapshot = CreateSnapshot();
        var targetLayer = ResolveDropTargetZIndex(targetZIndex, moveToNewTopLayer, moveToNewBottomLayer);
        var desiredGroups = reorderableItems
            .Where(item => !ReferenceEquals(item, source))
            .GroupBy(item => item.Config.ZIndex)
            .ToDictionary(
                group => group.Key,
                group => group.ToList());

        if (!desiredGroups.TryGetValue(targetLayer, out var targetGroupItems))
        {
            targetGroupItems = [];
            desiredGroups[targetLayer] = targetGroupItems;
        }

        source.Config.ZIndex = targetLayer;
        var insertIndex = targetGroupItems.Count;
        if (targetItem is not null)
        {
            var targetIndex = targetGroupItems.IndexOf(targetItem);
            if (targetIndex >= 0)
            {
                insertIndex = targetIndex + (insertAfter ? 1 : 0);
            }
        }

        targetGroupItems.Insert(Math.Clamp(insertIndex, 0, targetGroupItems.Count), source);

        var desiredReorderable = desiredGroups
            .OrderByDescending(group => group.Key)
            .SelectMany(group => group.Value)
            .ToList();
        RebuildDocumentControlOrder(desiredReorderable);

        var newSnapshot = CreateSnapshot();
        if (oldSnapshot == newSnapshot)
        {
            RebuildFilteredDesignItems();
            SelectDesignItem(source);
            StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.LayerPanel.ReorderBlocked");
            return false;
        }

        if (oldSnapshot is not null)
        {
            if (!_undoStack.TryPeek(out var previous) || previous != oldSnapshot)
            {
                PushUndoSnapshot(oldSnapshot);
            }

            _redoStack.Clear();
            NotifyUndoRedoCommands();
        }

        CurrentDocument.IsDirty = true;
        RefreshDirtyState();
        RebuildFilteredDesignItems();
        SelectDesignItem(source);
        ScheduleValidationAndPreviewRender("LayerReorder");
        StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.LayerPanel.Reordered");
        return true;
    }

    public bool CommitLayerNodeDrop(
        DesignerLayerNode sourceNode,
        int? targetZIndex,
        DesignerLayerNode? targetNode,
        bool insertAfter,
        bool moveToNewTopLayer = false,
        bool moveToNewBottomLayer = false)
    {
        if (sourceNode.Kind != DesignerLayerNodeKind.Control
            || !sourceNode.CanReorder
            || sourceNode.ControlItem is null
            || targetNode is not null && (targetNode.Kind != DesignerLayerNodeKind.Control || targetNode.ControlItem is null))
        {
            StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.LayerPanel.ReorderBlocked");
            return false;
        }

        return CommitLayerDrop(
            sourceNode.ControlItem,
            targetZIndex,
            targetNode?.ControlItem,
            insertAfter,
            moveToNewTopLayer,
            moveToNewBottomLayer);
    }
    public void CommitDesignItemGeometryEdit()
    {
        if (CurrentDocument is null)
        {
            return;
        }

        ValidateCurrentDocument();
        RequestPreviewRenderCurrentDocument();
    }
    private void OnDesignItemGeometryChanged(bool renderPreview, bool refreshPropertyGrid = true)
    {
        RefreshDirtyState();
        RefreshSelectedControlDisplay();

        if (renderPreview)
        {
            ValidateCurrentDocument(refreshPropertyGrid);
            RequestPreviewRenderCurrentDocument();
        }
    }

    private void RequestDesignerGeometryPatch(
        IReadOnlyList<FrontedControlDesignItem> changedItems,
        bool updateSelection)
    {
        if (changedItems.Count == 0)
        {
            return;
        }

        var args = new FrontedDesignerGeometryPatchRequestedEventArgs(
            changedItems,
            rebuildLayerPanel: false,
            rebuildInteractionLayer: false,
            updateSelection,
            zIndexChanged: false);
        DesignerGeometryPatchRequested?.Invoke(this, args);

        if (!args.Applied)
        {
            ValidateCurrentDocument();
            RequestPreviewRenderCurrentDocument();
        }
    }
}
