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
/// Fronted Designer 的设计目标选择、筛选与图层树业务逻辑。
/// </summary>
public partial class FrontedDesignerWindowViewModel
{
    partial void OnControlFilterTextChanged(string value)
    {
        var clamped = FrontedTextLimitHelper.Clamp(value, FrontedLayoutLimits.MaxSearchTextLength);
        if (!string.Equals(value, clamped, StringComparison.Ordinal))
        {
            ControlFilterText = clamped;
            StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "InputTruncated");
            return;
        }

        RebuildFilteredDesignItems();
        OnPropertyChanged(nameof(CanReorderLayers));
        OnPropertyChanged(nameof(LayerReorderHint));
    }
    /// <summary>
    /// 选择一个设计项，并刷新属性、行为和图层选择状态。
    /// </summary>
    /// <param name="item">要选择的设计项，或 <see langword="null"/> 表示清除选择。</param>
    public void SelectDesignItem(FrontedControlDesignItem? item)
    {
        if (item?.IsSelectableInEditor == false)
        {
            item = null;
        }

        SetSelectedDesignItems(item is null ? [] : [item], item);
    }

    /// <summary>
    /// 选中根控件，构建 Root selection 并同步 <see cref="SelectedDesignItem"/>。
    /// </summary>
    /// <param name="designItem">要选中的设计项。</param>
    /// <remarks>
    /// 当 <paramref name="designItem"/> 无可用 Schema 属性时，<see cref="SelectedTarget"/> 会被清除。
    /// </remarks>
    public void SelectRoot(FrontedControlDesignItem designItem)
    {
        ArgumentNullException.ThrowIfNull(designItem);

        _isApplyingSelectedTarget = true;
        try
        {
            if (!ReferenceEquals(SelectedDesignItem, designItem))
            {
                SetSelectedDesignItems([designItem], designItem);
            }

            SelectedTarget = _selectionBuilder.BuildRootSelection(designItem);
            RebuildPropertyEditorItems();
            RefreshLayerNodeSelection();
        }
        finally
        {
            _isApplyingSelectedTarget = false;
        }
    }

    /// <summary>
    /// 选中控件内部的固定 Part，构建 FixedPart selection。
    /// </summary>
    /// <param name="designItem">Part 所属的父控件设计项。</param>
    /// <param name="partId">Part 标识。</param>
    /// <remarks>
    /// 当 Part 不存在或无可用 Schema 时，<see cref="SelectedTarget"/> 保持不变。
    /// 该方法会同步 <see cref="SelectedDesignItem"/> 到 <paramref name="designItem"/>，以便属性网格与画布定位到父控件。
    /// </remarks>
    public void SelectFixedPart(FrontedControlDesignItem designItem, string partId)
    {
        ArgumentNullException.ThrowIfNull(designItem);
        ArgumentNullException.ThrowIfNull(partId);

        _isApplyingSelectedTarget = true;
        try
        {
            if (!ReferenceEquals(SelectedDesignItem, designItem))
            {
                SetSelectedDesignItems([designItem], designItem);
            }

            var selection = _selectionBuilder.BuildFixedPartSelection(designItem, partId);
            if (selection is null)
            {
                return;
            }

            SelectedTarget = selection;
            RebuildPropertyEditorItems();
            RefreshLayerNodeSelection();
        }
        finally
        {
            _isApplyingSelectedTarget = false;
        }
    }

    /// <summary>
    /// 选中控件内部 PartCollection 的一个集合项，构建 CollectionItem selection。
    /// </summary>
    /// <param name="designItem">集合所属的父控件设计项。</param>
    /// <param name="collectionId">集合标识。</param>
    /// <param name="itemKey">集合项唯一键。</param>
    /// <remarks>
    /// 当集合或项不存在时，<see cref="SelectedTarget"/> 保持不变。
    /// </remarks>
    public void SelectCollectionItem(FrontedControlDesignItem designItem, string collectionId, string itemKey)
    {
        ArgumentNullException.ThrowIfNull(designItem);
        ArgumentNullException.ThrowIfNull(collectionId);
        ArgumentNullException.ThrowIfNull(itemKey);

        _isApplyingSelectedTarget = true;
        try
        {
            if (!ReferenceEquals(SelectedDesignItem, designItem))
            {
                SetSelectedDesignItems([designItem], designItem);
            }

            var selection = _selectionBuilder.BuildCollectionItemSelection(designItem, collectionId, itemKey);
            if (selection is null)
            {
                return;
            }

            SelectedTarget = selection;
            RebuildPropertyEditorItems();
            RefreshLayerNodeSelection();
        }
        finally
        {
            _isApplyingSelectedTarget = false;
        }
    }

    /// <summary>
    /// 当子控件（Part/CollectionItem）被选中时，按 Esc 回退到根控件选中。
    /// 根控件选中时调用此方法无效果。
    /// </summary>
    /// <returns>是否执行了回退（即调用前为子控件选中）。</returns>
    public bool EscapeToRootSelection()
    {
        if (_selectedTarget is not { Kind: not FrontedV3DesignSelectionKind.Root } target
            || target.DesignItem is null)
        {
            return false;
        }

        SelectRoot(target.DesignItem);
        return true;
    }

    /// <summary>
    /// 返回当前选中根控件可编辑子控件（Part/CollectionItem）的命中框与装饰器信息列表。
    /// 供 View 创建透明 hitbox；仅在根控件选中时返回非空列表，子控件选中或无选中时返回空列表。
    /// </summary>
    /// <returns>子控件目标信息列表；无可用子控件时返回空列表。</returns>
    /// <remarks>
    /// 几何值相对于父控件，View 需要叠加父控件的画布坐标得到绝对位置。
    /// 当<see cref="SelectedTarget"/> 为子控件选中时也返回空列表（不再显示同级 hitbox）。
    /// </remarks>
    public IReadOnlyList<DesignerChildTargetInfo> GetChildTargetInfos()
    {
        if (SelectedDesignItem is not { } designItem
            || _selectedTarget is not { Kind: FrontedV3DesignSelectionKind.Root })
        {
            return Array.Empty<DesignerChildTargetInfo>();
        }

        var config = designItem.Config;
        var parentBounds = FrontedDesignerBoundsResolver.Resolve(config);
        var parentWidth = parentBounds.Width;
        var parentHeight = parentBounds.Height;
        var result = new List<DesignerChildTargetInfo>();

        foreach (var part in _selectionBuilder.GetAvailableParts(designItem))
        {
            var geometry = new FixedPartGeometryTarget(part, config);
            var width = geometry.Width ?? parentWidth;
            var height = geometry.Height ?? parentHeight;
            result.Add(new DesignerChildTargetInfo
            {
                ParentItem = designItem,
                Id = part.Id,
                ItemKey = null,
                IsCollectionItem = false,
                Left = geometry.Left,
                Top = geometry.Top,
                Width = width,
                Height = height,
                CanMove = part.Capabilities.CanMove,
                CanResize = part.Capabilities.CanResize
            });
        }

        foreach (var collection in _selectionBuilder.GetAvailableCollections(designItem))
        {
            var items = collection.CollectionGetter(config);
            foreach (var item in items)
            {
                var itemKey = collection.ItemKeySelector(item);
                var geometry = new CollectionItemGeometryTarget(collection, config, itemKey);
                var width = geometry.Width ?? parentWidth;
                var height = geometry.Height ?? parentHeight;
                result.Add(new DesignerChildTargetInfo
                {
                    ParentItem = designItem,
                    Id = collection.Id,
                    ItemKey = itemKey,
                    IsCollectionItem = true,
                    Left = geometry.Left,
                    Top = geometry.Top,
                    Width = width,
                    Height = height,
                    CanMove = collection.ItemCapabilities.CanMove,
                    CanResize = collection.ItemCapabilities.CanResize
                });
            }
        }

        return result;
    }

    /// <summary>
    /// 返回当前选中子控件（Part/CollectionItem）的目标信息，供 View 绘制 selection adorner 与 resize handles。
    /// 根控件选中或无选中时返回 <see langword="null"/>。
    /// </summary>
    /// <returns>当前子控件目标信息；非子控件选中时为 <see langword="null"/>。</returns>
    public DesignerChildTargetInfo? GetCurrentSubTargetInfo()
    {
        if (_selectedTarget is not { Kind: not FrontedV3DesignSelectionKind.Root } target
            || target.DesignItem is not { } designItem)
        {
            return null;
        }

        var config = designItem.Config;
        var parentBounds = FrontedDesignerBoundsResolver.Resolve(config);
        var parentWidth = parentBounds.Width;
        var parentHeight = parentBounds.Height;
        var geometry = target.GeometryTarget;
        var width = geometry.Width ?? parentWidth;
        var height = geometry.Height ?? parentHeight;

        return target.Kind switch
        {
            FrontedV3DesignSelectionKind.FixedPart when target.SubTarget is FrontedV3FixedPartTarget partTarget
                => new DesignerChildTargetInfo
                {
                    ParentItem = designItem,
                    Id = partTarget.PartId,
                    ItemKey = null,
                    IsCollectionItem = false,
                    Left = geometry.Left,
                    Top = geometry.Top,
                    Width = width,
                    Height = height,
                    CanMove = ResolvePartCapabilities(designItem, partTarget.PartId).CanMove,
                    CanResize = ResolvePartCapabilities(designItem, partTarget.PartId).CanResize
                },
            FrontedV3DesignSelectionKind.CollectionItem
                when target.SubTarget is FrontedV3CollectionItemTarget collectionTarget
                => new DesignerChildTargetInfo
                {
                    ParentItem = designItem,
                    Id = collectionTarget.CollectionId,
                    ItemKey = collectionTarget.ItemKey,
                    IsCollectionItem = true,
                    Left = geometry.Left,
                    Top = geometry.Top,
                    Width = width,
                    Height = height,
                    CanMove = ResolveCollectionItemCapabilities(designItem, collectionTarget.CollectionId).CanMove,
                    CanResize = ResolveCollectionItemCapabilities(designItem, collectionTarget.CollectionId).CanResize
                },
            _ => null
        };
    }

    private FrontedV3PartCapabilities ResolvePartCapabilities(
        FrontedControlDesignItem designItem,
        string partId)
    {
        var part = _selectionBuilder.FindPart(designItem.Config, partId);
        return part?.Capabilities ?? FrontedV3PartCapabilities.None;
    }

    private FrontedV3PartCapabilities ResolveCollectionItemCapabilities(
        FrontedControlDesignItem designItem,
        string collectionId)
    {
        var collection = _selectionBuilder.FindCollection(designItem.Config, collectionId);
        return collection?.ItemCapabilities ?? FrontedV3PartCapabilities.None;
    }

    /// <summary>
    /// 选择多个设计控件，并将其中一个设为属性网格的主目标。
    /// </summary>
    /// <param name="items">要选中的控件。</param>
    /// <param name="primaryItem">主选中控件；省略时使用第一个选中控件。</param>
    /// <summary>
    /// 在一次选择事务中选择多个设计项。
    /// </summary>
    /// <param name="items">要选中的条目。</param>
    /// <param name="primaryItem">用于属性编辑的主条目。</param>
    public void SelectDesignItems(
        IEnumerable<FrontedControlDesignItem> items,
        FrontedControlDesignItem? primaryItem = null)
    {
        if (CurrentDocument is null)
        {
            SelectDesignItem(null);
            return;
        }

        var selected = items
            .Where(item => item.IsSelectableInEditor && CurrentDocument.Controls.Contains(item))
            .Distinct()
            .ToList();
        var primary = primaryItem is not null && selected.Contains(primaryItem)
            ? primaryItem
            : selected.FirstOrDefault();

        SetSelectedDesignItems(selected, primary);
    }

    /// <summary>
    /// 从当前多选中添加或移除一个控件。
    /// </summary>
    /// <param name="item">要切换选中状态的控件。</param>
    /// <summary>
    /// 切换多选集合中的一个项目。
    /// </summary>
    /// <param name="item">要切换的条目。</param>
    public void ToggleDesignItemSelection(FrontedControlDesignItem item)
    {
        if (CurrentDocument is null || !item.IsSelectableInEditor || !CurrentDocument.Controls.Contains(item))
        {
            return;
        }

        var selected = SelectedDesignItems.ToList();
        if (selected.Contains(item))
        {
            selected.Remove(item);
            SetSelectedDesignItems(selected, ReferenceEquals(SelectedDesignItem, item) ? selected.LastOrDefault() : SelectedDesignItem);
        }
        else
        {
            selected.Add(item);
            SetSelectedDesignItems(selected, item);
        }
    }

    /// <summary>
    /// 选择图层树节点，并在可能时将该选择同步到设计画布。
    /// </summary>
    /// <param name="node">要选中的图层节点。</param>
    public void SelectLayerNode(DesignerLayerNode? node)
    {
        if (node is null || !node.CanSelect)
        {
            ClearSelection();
            return;
        }

        switch (node.Kind)
        {
            case DesignerLayerNodeKind.Control when node.ControlItem is not null:
                SelectDesignItem(node.ControlItem);
                break;
            case DesignerLayerNodeKind.Part when node.ControlItem is not null && node.PartId is not null:
                SelectFixedPart(node.ControlItem, node.PartId);
                break;
            case DesignerLayerNodeKind.CollectionItem
                when node.ControlItem is not null
                && node.CollectionId is not null
                && node.ItemKey is not null:
                SelectCollectionItem(node.ControlItem, node.CollectionId, node.ItemKey);
                break;
            default:
                ClearSelection();
                break;
        }
    }

    /// <summary>
    /// 切换图层树节点的展开状态。
    /// </summary>
    /// <param name="node">要展开或折叠的图层节点。</param>
    public void ToggleLayerNodeExpansion(DesignerLayerNode node)
    {
    }

    /// <summary>
    /// 清除设计画布、图层树、属性和行为选择状态。
    /// </summary>
    public void ClearSelection()
    {
        SelectDesignItem(null);
    }

    /// <summary>
    /// 应用完整选择集合，同时保留用于属性编辑的主项目。
    /// </summary>
    /// <param name="items">应被选中的条目。</param>
    /// <param name="primaryItem">主选中条目。</param>
    private void SetSelectedDesignItems(
        IReadOnlyCollection<FrontedControlDesignItem> items,
        FrontedControlDesignItem? primaryItem)
    {
        var selected = items
            .Where(item => item.IsSelectableInEditor)
            .Distinct()
            .ToList();
        var primary = primaryItem is not null && selected.Contains(primaryItem)
            ? primaryItem
            : selected.FirstOrDefault();

        _isApplyingDesignSelection = true;
        try
        {
            SelectedDesignItems.Clear();
            foreach (var item in selected)
            {
                SelectedDesignItems.Add(item);
            }

            SelectedDesignItem = primary;
            if (ReferenceEquals(SelectedDesignItem, primary))
            {
                ApplyDesignSelectionFlags();
            }

            OnPropertyChanged(nameof(SelectedDesignItems));
        }
        finally
        {
            _isApplyingDesignSelection = false;
        }

        RefreshLayerNodeSelection();
    }

    /// <summary>
    /// 获取可参与移动或缩放操作的选中设计项。
    /// </summary>
    /// <returns>可移动的选中条目。</returns>
    private IReadOnlyList<FrontedControlDesignItem> GetMovableSelectedDesignItems()
    {
        if (SelectedDesignItems.Count == 0)
        {
            return SelectedDesignItem is { IsSelectableInEditor: true, IsEditableInEditor: true } item
                ? [item]
                : [];
        }

        return SelectedDesignItems
            .Where(item => item.IsSelectableInEditor && item.IsEditableInEditor)
            .ToList();
    }
    private void NotifyLayoutCommandState()
    {
        OnPropertyChanged(nameof(CanSaveLayout));
        OnPropertyChanged(nameof(CanResetToBuiltIn));
        SaveLayoutCommand.NotifyCanExecuteChanged();
        ResetToBuiltInCommand.NotifyCanExecuteChanged();
    }

    private void RequestPreviewRenderCurrentDocument()
    {
        if (CurrentDocument is null)
        {
            RequestPreviewRender(null, _selectedCatalogEntry);
            return;
        }

        RequestPreviewRender(_designConverter.ToConfig(CurrentDocument), _selectedCatalogEntry);
    }

    private int ResolveDropTargetZIndex(
        int? targetZIndex,
        bool moveToNewTopLayer,
        bool moveToNewBottomLayer)
    {
        if (CurrentDocument is null || CurrentDocument.Controls.Count == 0)
        {
            return targetZIndex ?? 0;
        }

        if (moveToNewTopLayer)
        {
            return CurrentDocument.Controls.Max(control => control.Config.ZIndex) + 1;
        }

        if (moveToNewBottomLayer)
        {
            return CurrentDocument.Controls.Min(control => control.Config.ZIndex) - 1;
        }

        return targetZIndex ?? 0;
    }

    private void RebuildDocumentControlOrder(IReadOnlyList<FrontedControlDesignItem> desiredReorderable)
    {
        if (CurrentDocument is null)
        {
            return;
        }

        var originalControls = CurrentDocument.Controls.ToList();
        var added = new HashSet<FrontedControlDesignItem>();
        var rebuilt = new List<FrontedControlDesignItem>(originalControls.Count);

        foreach (var item in desiredReorderable)
        {
            AddControlAndLinkedOverlays(item, originalControls, rebuilt, added);
        }

        foreach (var item in originalControls)
        {
            if (added.Contains(item))
            {
                continue;
            }

            rebuilt.Add(item);
            added.Add(item);
        }

        CurrentDocument.Controls.Clear();
        foreach (var item in rebuilt)
        {
            CurrentDocument.Controls.Add(item);
        }
    }

    private static void AddControlAndLinkedOverlays(
        FrontedControlDesignItem item,
        IReadOnlyList<FrontedControlDesignItem> originalControls,
        ICollection<FrontedControlDesignItem> rebuilt,
        ISet<FrontedControlDesignItem> added)
    {
        if (!added.Add(item))
        {
            return;
        }

        rebuilt.Add(item);
    }

    private void RebuildFilteredDesignItems()
    {
        FilteredDesignItems.Clear();
        LayerGroups.Clear();

        if (CurrentDocument is null)
        {
            return;
        }

        var filter = ControlFilterText?.Trim();
        var controls = CurrentDocument.Controls
            .Select((item, index) => new { Item = item, Index = index })
            .Where(entry => entry.Item.IsSelectableInEditor && MatchesControlFilter(entry.Item, filter))
            .OrderByDescending(entry => entry.Item.Config.ZIndex)
            .ThenBy(entry => entry.Index)
            .Select(entry => entry.Item);

        foreach (var item in controls)
        {
            FilteredDesignItems.Add(item);
        }

        RebuildLayerGroups();
    }

    private void AddFilteredDesignItemIfVisible(FrontedControlDesignItem item)
    {
        var filter = ControlFilterText?.Trim();
        if (!item.IsSelectableInEditor || !MatchesControlFilter(item, filter))
        {
            return;
        }

        var insertIndex = GetFilteredInsertIndex(item);
        FilteredDesignItems.Insert(insertIndex, item);
        RebuildLayerGroups();
    }

    private void RemoveFilteredDesignItem(FrontedControlDesignItem item)
    {
        var index = FilteredDesignItems.IndexOf(item);
        if (index >= 0)
        {
            FilteredDesignItems.RemoveAt(index);
            RebuildLayerGroups();
        }
    }

    private void RefreshFilteredDesignItemPosition(FrontedControlDesignItem item)
    {
        RemoveFilteredDesignItem(item);
        AddFilteredDesignItemIfVisible(item);
    }

    private void RebuildLayerGroups()
    {
        LayerGroups.Clear();
        foreach (var group in FilteredDesignItems
                     .GroupBy(item => item.Config.ZIndex)
                     .OrderByDescending(group => group.Key))
        {
            var layerGroup = new FrontedLayerGroup
            {
                ZIndex = group.Key,
                DisplayName = $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.LayerPanel.Layer")} {group.Key}"
            };

            foreach (var item in group.OrderBy(item => CurrentDocument?.Controls.IndexOf(item) ?? 0))
            {
                layerGroup.Items.Add(CreateControlLayerNode(item));
            }

            LayerGroups.Add(layerGroup);
        }

        RefreshLayerNodeSelection();
    }

    private DesignerLayerNode CreateControlLayerNode(FrontedControlDesignItem item)
    {
        var node = new DesignerLayerNode
        {
            Kind = DesignerLayerNodeKind.Control,
            ControlItem = item,
            CanSelect = item.IsSelectableInEditor,
            CanReorder = IsLayerReorderable(item),
            DisplayName = item.Name,
            Metadata = _localizationService.GetControlTypeDisplayName(item.Config.ControlType),
            ZIndex = item.Config.ZIndex
        };

        AppendChildLayerNodes(node, item);
        return node;
    }

    /// <summary>
    /// 为控件图层节点追加 Part/CollectionItem 子节点，使图层树可展开选中子控件。
    /// </summary>
    /// <param name="parent">父控件节点。</param>
    /// <param name="item">父控件设计项。</param>
    private void AppendChildLayerNodes(DesignerLayerNode parent, FrontedControlDesignItem item)
    {
        foreach (var part in _selectionBuilder.GetAvailableParts(item))
        {
            parent.Children.Add(new DesignerLayerNode
            {
                Kind = DesignerLayerNodeKind.Part,
                ControlItem = item,
                CanSelect = part.Capabilities.CanMove || part.Capabilities.CanResize,
                CanReorder = false,
                DisplayName = part.Id,
                Metadata = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.LayerPanel.Part"),
                ZIndex = item.Config.ZIndex,
                PartId = part.Id
            });
        }

        foreach (var collection in _selectionBuilder.GetAvailableCollections(item))
        {
            var items = collection.CollectionGetter(item.Config);
            foreach (var collectionItem in items)
            {
                var itemKey = collection.ItemKeySelector(collectionItem);
                parent.Children.Add(new DesignerLayerNode
                {
                    Kind = DesignerLayerNodeKind.CollectionItem,
                    ControlItem = item,
                    CanSelect = collection.ItemCapabilities.CanMove || collection.ItemCapabilities.CanResize,
                    CanReorder = false,
                    DisplayName = $"{collection.Id} [{itemKey}]",
                    Metadata = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "Designer.LayerPanel.CollectionItem"),
                    ZIndex = item.Config.ZIndex,
                    CollectionId = collection.Id,
                    ItemKey = itemKey
                });
            }
        }
    }

    private void RefreshLayerNodeSelection()
    {
        DesignerLayerNode? selectedNode = null;
        foreach (var node in LayerGroups.SelectMany(group => group.Items))
        {
            var isSelected = IsSelectedLayerNode(node);
            node.IsSelected = isSelected;
            if (isSelected && selectedNode is null)
            {
                selectedNode = node;
            }

            foreach (var child in node.Children)
            {
                var childSelected = IsSelectedLayerNode(child);
                child.IsSelected = childSelected;
                if (childSelected && selectedNode is null)
                {
                    selectedNode = child;
                }
            }
        }

        SelectedLayerNode = selectedNode;
    }

    private bool IsSelectedLayerNode(DesignerLayerNode node)
    {
        return node.Kind switch
        {
            DesignerLayerNodeKind.Control => node.ControlItem is not null && SelectedDesignItems.Contains(node.ControlItem),
            DesignerLayerNodeKind.Part => _selectedTarget is { Kind: FrontedV3DesignSelectionKind.FixedPart } target
                && ReferenceEquals(target.DesignItem, node.ControlItem)
                && target.SubTarget is FrontedV3FixedPartTarget partTarget
                && string.Equals(partTarget.PartId, node.PartId, StringComparison.Ordinal),
            DesignerLayerNodeKind.CollectionItem => _selectedTarget is { Kind: FrontedV3DesignSelectionKind.CollectionItem } target
                && ReferenceEquals(target.DesignItem, node.ControlItem)
                && target.SubTarget is FrontedV3CollectionItemTarget collectionTarget
                && string.Equals(collectionTarget.CollectionId, node.CollectionId, StringComparison.Ordinal)
                && string.Equals(collectionTarget.ItemKey, node.ItemKey, StringComparison.Ordinal),
            _ => false
        };
    }

    private static string GetLayerNodeExpansionKey(FrontedControlDesignItem item) => item.Name;

    private int GetFilteredInsertIndex(FrontedControlDesignItem item)
    {
        if (CurrentDocument is null)
        {
            return FilteredDesignItems.Count;
        }

        for (var index = 0; index < FilteredDesignItems.Count; index++)
        {
            if (CompareFilteredOrder(item, FilteredDesignItems[index]) < 0)
            {
                return index;
            }
        }

        return FilteredDesignItems.Count;
    }

    private int CompareFilteredOrder(FrontedControlDesignItem left, FrontedControlDesignItem right)
    {
        var zIndexCompare = right.Config.ZIndex.CompareTo(left.Config.ZIndex);
        if (zIndexCompare != 0)
        {
            return zIndexCompare;
        }

        if (CurrentDocument is null)
        {
            return 0;
        }

        return CurrentDocument.Controls.IndexOf(left).CompareTo(CurrentDocument.Controls.IndexOf(right));
    }

    private void NormalizeSelectionState()
    {
        _lastSelectedDesignItem = null;
        if (CurrentDocument is null)
        {
            SelectedDesignItems.Clear();
            SelectedDesignItem = null;
            return;
        }

        if (SelectedDesignItem is not null && !CurrentDocument.Controls.Contains(SelectedDesignItem))
        {
            SelectedDesignItem = null;
        }

        var retainedSelection = SelectedDesignItems
            .Where(item => CurrentDocument.Controls.Contains(item))
            .Distinct()
            .ToList();
        if (SelectedDesignItem is not null && !retainedSelection.Contains(SelectedDesignItem))
        {
            retainedSelection.Add(SelectedDesignItem);
        }

        SelectedDesignItems.Clear();
        foreach (var item in retainedSelection)
        {
            SelectedDesignItems.Add(item);
        }

        foreach (var control in CurrentDocument.Controls)
        {
            control.IsSelected = SelectedDesignItems.Contains(control);
            if (control.IsSelected)
            {
                _lastSelectedDesignItem = control;
            }
        }
    }

    private void ApplyDesignSelectionFlags()
    {
        var selected = SelectedDesignItems.ToHashSet();
        if (CurrentDocument is not null)
        {
            foreach (var control in CurrentDocument.Controls)
            {
                control.IsSelected = selected.Contains(control);
            }
        }

        _lastSelectedDesignItem = SelectedDesignItem;
    }

    private static string GetCommittedEditText(FrontedPropertyEditorItem item, object? value) =>
        GetCommittedEditText(item.EditorKind, value, item.Options);

    private static string GetCommittedEditText(
        FrontedPropertyEditorKind editorKind,
        object? value,
        IReadOnlyList<object>? options)
    {
        if (editorKind == FrontedPropertyEditorKind.FontFamily)
        {
            var storedValue = Convert.ToString(value, CultureInfo.InvariantCulture);
            return options?.OfType<FrontedFontFamilyOption>()
                       .FirstOrDefault(option => string.Equals(option.Value, storedValue, StringComparison.Ordinal))?.DisplayName
                   ?? FrontedFontResourceHelper.ExtractFontName(storedValue);
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private Stopwatch? StartDesignerPerfTrace()
    {
        return _logger.IsEnabled(LogLevel.Debug) ? Stopwatch.StartNew() : null;
    }

    private static TimeSpan Elapsed(Stopwatch? stopwatch)
    {
        return stopwatch?.Elapsed ?? TimeSpan.Zero;
    }

    [Conditional("DEBUG")]
    private void LogDesignerPerf(string operation, string stage)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("FrontedDesigner perf {Operation}: {Stage}", operation, stage);
        }
    }

    [Conditional("DEBUG")]
    private void LogDesignerPerf(string operation, string stage, TimeSpan elapsed)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "FrontedDesigner perf {Operation}: {Stage} at {ElapsedMilliseconds:F2} ms",
                operation,
                stage,
                elapsed.TotalMilliseconds);
        }
    }

    public static bool MatchesControlFilter(FrontedControlDesignItem item, string? filter)
    {
        if (!item.IsSelectableInEditor)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        return item.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
               || item.Config.ControlType.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshSelectedControlDisplay()
    {
        if (SelectedDesignItem is null)
        {
            SelectedControlDisplay = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "NoControlSelected");
            SelectedControlTypeDisplay = string.Empty;
            SelectedControlGeometryDisplay = string.Empty;
            SelectedControlValidationMessageCount = 0;
            return;
        }

        var config = SelectedDesignItem.Config;

        SelectedControlDisplay = SelectedDesignItem.Name;
        SelectedControlTypeDisplay = _localizationService.GetControlTypeDisplayName(config.ControlType);
        SelectedControlGeometryDisplay =
            $"L {config.Left:0.##}  T {config.Top:0.##}  "
            + $"W {(config.Width?.ToString("0.##") ?? "-")}  "
            + $"H {(config.Height?.ToString("0.##") ?? "-")}";
        SelectedControlValidationMessageCount = SelectedDesignItem.ValidationMessages.Count;
    }
}
