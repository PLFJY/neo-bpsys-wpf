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
/// Fronted Designer 的控件操作、样式传递与布局模板业务逻辑。
/// </summary>
public partial class FrontedDesignerWindowViewModel
{
    /// <summary>
    /// 添加由“添加控件”目录项描述的新控件。
    /// </summary>
    /// <param name="parameter">应为 <see cref="FrontedAddControlCatalogItem"/>。</param>
    [RelayCommand]
    private void AddControl(object? parameter)
    {
        if (CurrentDocument is null)
        {
            StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "CannotAddControl");
            return;
        }

        var request = parameter as FrontedAddControlRequest;
        var controlType = request?.ControlType ?? Convert.ToString(parameter, CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(controlType) || !_defaultConfigFactory.CanCreate(controlType))
        {
            StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "UnsupportedControlType");
            return;
        }

        if (CurrentDocument.Controls.Count >= FrontedLayoutLimits.MaxControlsPerCanvas)
        {
            StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "ControlCountLimitReached");
            return;
        }

        CaptureUndoSnapshot();
        var config = _defaultConfigFactory.Create(
            controlType,
            CurrentDocument,
            request?.CenterX,
            request?.CenterY);
        var item = new FrontedControlDesignItem
        {
            Name = _controlNameGenerator.Generate(GetNameSeed(controlType), CurrentDocument),
            Config = config,
            IsSelectableInEditor = true,
            IsEditableInEditor = true
        };

        CurrentDocument.Controls.Add(item);
        CurrentDocument.IsDirty = true;
        RefreshDirtyState();
        ControlFilterText = string.Empty;
        RebuildFilteredDesignItems();
        SelectDesignItem(item);
        ValidateCurrentDocument();
        RequestPreviewRenderCurrentDocument();
        StatusMessage = $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "AddedControl")}: {item.Name}";
    }

    /// <summary>
    /// 将选中的可编辑控件复制到 Designer 控件剪贴板。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCopySelectedControl))]
    private void CopySelectedControl()
    {
        var selected = SelectedDesignItem;
        if (selected is null || !CanCopyControl(selected))
        {
            StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "CannotCopyControl");
            return;
        }

        _copiedControl = FrontedDesignerClipboardPayload.Create(selected);
        PasteControlCommand.NotifyCanExecuteChanged();
        StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "CopyControl");
    }

    /// <summary>
    /// 将复制的控件粘贴到当前文档，并分配不冲突的名称。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanPasteControl))]
    private void PasteControl()
    {
        if (CurrentDocument is null || _copiedControl is null)
        {
            StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "CannotPasteControl");
            return;
        }

        if (CurrentDocument.Controls.Count + 1 > FrontedLayoutLimits.MaxControlsPerCanvas)
        {
            StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "ControlCountLimitReached");
            return;
        }

        var copiedControl = _copiedControl;
        if (copiedControl is null)
        {
            StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "CannotPasteControl");
            return;
        }

        var total = StartDesignerPerfTrace();
        LogDesignerPerf("Paste", "start");
        var clonedConfig = copiedControl.CreateConfig();
        clonedConfig.BehaviorGuid = FrontedBehaviorGuidHelper.NewGuid();
        LogDesignerPerf("Paste", "clone config", Elapsed(total));
        clonedConfig.Left += 10D;
        clonedConfig.Top += 10D;
        clonedConfig.ZIndex = CurrentDocument.Controls.Count == 0
            ? clonedConfig.ZIndex
            : CurrentDocument.Controls.Max(control => control.Config.ZIndex) + 1;

        var item = new FrontedControlDesignItem
        {
            Name = GeneratePasteName(copiedControl.SourceName, copiedControl.ControlType, CurrentDocument),
            Config = clonedConfig,
            IsSelectableInEditor = true,
            IsEditableInEditor = true
        };
        LogDesignerPerf("Paste", "name/z-index preparation", Elapsed(total));

        CaptureUndoSnapshot();
        LogDesignerPerf("Paste", "undo snapshot capture", Elapsed(total));
        CurrentDocument.Controls.Add(item);
        LogDesignerPerf("Paste", "add control", Elapsed(total));
        CurrentDocument.IsDirty = true;
        RefreshDirtyState();
        AddFilteredDesignItemIfVisible(item);
        LogDesignerPerf("Paste", "filtered list update", Elapsed(total));
        SelectDesignItem(item);
        LogDesignerPerf("Paste", "selection update", Elapsed(total));
        ScheduleValidationAndPreviewRender("Paste");
        LogDesignerPerf("Paste", "validation scheduling", Elapsed(total));
        LogDesignerPerf("Paste", "preview render scheduling", Elapsed(total));
        StatusMessage = $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "PasteControl")}: {item.Name}";
        LogDesignerPerf("Paste", "total", Elapsed(total));
    }

    /// <summary>
    /// 在完成引用和运行时关键检查后删除选中的可编辑控件。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDeleteSelectedControl))]
    private void DeleteSelectedControl()
    {
        if (CurrentDocument is null || SelectedDesignItem is null)
        {
            return;
        }

        if (!SelectedDesignItem.IsEditableInEditor || !SelectedDesignItem.IsSelectableInEditor)
        {
            StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "CannotDeleteReferencedControl");
            return;
        }

        _referenceScanner.SetControls(CurrentDocument.Controls);
        if (_referenceScanner.GetIncomingReferences(SelectedDesignItem.Name).Count > 0)
        {
            StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "CannotDeleteReferencedControl");
            return;
        }

        var total = StartDesignerPerfTrace();
        LogDesignerPerf("Delete", "start");
        CaptureUndoSnapshot();
        LogDesignerPerf("Delete", "undo snapshot capture", Elapsed(total));
        var deletedName = SelectedDesignItem.Name;
        var deletedItem = SelectedDesignItem;
        var deletedBehaviorGuid = deletedItem.Config.BehaviorGuid;
        CurrentDocument.Controls.Remove(SelectedDesignItem);
        if (deletedBehaviorGuid != Guid.Empty)
        {
            BehaviorPanel.RemoveBehaviors(deletedBehaviorGuid);
            _behaviorService.RemoveBehaviors(deletedBehaviorGuid);
        }

        LogDesignerPerf("Delete", "remove control", Elapsed(total));
        CurrentDocument.IsDirty = true;
        RefreshDirtyState();
        SelectDesignItem(null);
        RemoveFilteredDesignItem(deletedItem);
        LogDesignerPerf("Delete", "filtered list update", Elapsed(total));
        RebuildPropertyEditorItems();
        LogDesignerPerf("Delete", "selection/property update", Elapsed(total));
        ScheduleValidationAndPreviewRender("Delete");
        LogDesignerPerf("Delete", "validation scheduling", Elapsed(total));
        LogDesignerPerf("Delete", "preview render scheduling", Elapsed(total));
        StatusMessage = $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "DeleteSelectedControl")}: {deletedName}";
        LogDesignerPerf("Delete", "total", Elapsed(total));
    }

    [RelayCommand]
    private void FillMissingGlobalScoreCells()
    {
        if (CurrentDocument is null
            || SelectedDesignItem?.Config is not { } config
            || !GlobalScoreRowCellLayoutHelper.SupportsTemplateOperations(config))
        {
            return;
        }

        CaptureUndoSnapshot();
        GlobalScoreRowCellLayoutHelper.EnsureCompleteCells(
            config,
            CurrentDocument.EditingBoModeState == FrontedCanvasBoModeState.Bo3);
        FinishGlobalScoreRowAction();
    }

    [RelayCommand]
    private void AutoArrangeGlobalScoreCellsBySpacing()
    {
        if (CurrentDocument is null
            || SelectedDesignItem?.Config is not { } config
            || !GlobalScoreRowCellLayoutHelper.SupportsTemplateOperations(config))
        {
            return;
        }

        CaptureUndoSnapshot();
        GlobalScoreRowCellLayoutHelper.AutoArrangeBySpacing(
            config,
            CurrentDocument.EditingBoModeState == FrontedCanvasBoModeState.Bo3);
        FinishGlobalScoreRowAction();
    }

    [RelayCommand]
    private void ApplyBo3GlobalScoreVisibilityTemplate()
    {
        if (CurrentDocument is null
            || SelectedDesignItem?.Config is not { } config
            || !GlobalScoreRowCellLayoutHelper.SupportsTemplateOperations(config))
        {
            return;
        }

        CaptureUndoSnapshot();
        GlobalScoreRowCellLayoutHelper.ApplyBo3VisibilityTemplate(config);
        FinishGlobalScoreRowAction();
    }

    [RelayCommand]
    private void ApplyBo5GlobalScoreVisibilityTemplate()
    {
        if (CurrentDocument is null
            || SelectedDesignItem?.Config is not { } config
            || !GlobalScoreRowCellLayoutHelper.SupportsTemplateOperations(config))
        {
            return;
        }

        CaptureUndoSnapshot();
        GlobalScoreRowCellLayoutHelper.ApplyBo5VisibilityTemplate(config);
        FinishGlobalScoreRowAction();
    }

    private void FinishGlobalScoreRowAction()
    {
        if (CurrentDocument is null)
        {
            return;
        }

        CurrentDocument.IsDirty = true;
        RebuildPropertyEditorItems();
        RefreshDirtyState();
        RequestPreviewRenderCurrentDocument();
    }

    /// <summary>
    /// 判断"应用到同类型控件"命令是否可执行：需要存在当前文档、选中根控件、
    /// 源控件 Registration 声明了 <see cref="FrontedV3ControlRegistration.SupportsPeerStyleTransfer"/>、
    /// 且文档中存在与源控件 <see cref="FrontedControlConfigBase.ControlType"/> 相同的其他控件。
    /// </summary>
    /// <returns>当可执行同类型样式传播时返回 <see langword="true"/>。</returns>
    private bool CanApplyAppearanceToSameType()
    {
        if (CurrentDocument is null)
        {
            return false;
        }

        // 子控件选中时禁用，仅根控件选中可传播同类型样式。
        if (_selectedTarget is { Kind: not FrontedV3DesignSelectionKind.Root })
        {
            return false;
        }

        var sourceConfig = SelectedDesignItem?.Config;
        if (sourceConfig is null)
        {
            return false;
        }

        // 仅当控件显式声明 SupportsPeerStyleTransfer 时才允许同类型样式传播。
        if (_selectionBuilder.ResolveRegistration(sourceConfig)?.SupportsPeerStyleTransfer != true)
        {
            return false;
        }

        return TryGetSameTypePeerDesignItems().Count > 0;
    }

    /// <summary>
    /// 将当前选中根控件的外观属性（按 <see cref="FrontedV3StyleTransferProfile.Default"/>）
    /// 传播到 <see cref="CurrentDocument"/> 中所有相同 <see cref="FrontedControlConfigBase.ControlType"/>
    /// 的其他控件上。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 传播范围由 <see cref="FrontedV3StyleTransferProfile.Default"/> 控制，仅传播
    /// <see cref="FrontedV3PropertySemantic.Appearance"/> 语义的属性；
    /// <see cref="FrontedV3PropertySemantic.DataIdentity"/> 与位置/尺寸/行为/效果等语义不会被传播。
    /// </para>
    /// <para>
    /// 完成传播后触发：Undo 快照已先于传播捕获、属性面板重建、预览刷新、文档标记为脏。
    /// </para>
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanApplyAppearanceToSameType))]
    private void ApplyAppearanceToSameType()
    {
        if (CurrentDocument is null)
        {
            return;
        }

        // 子控件选中时禁用，仅根控件选中可传播同类型样式。
        if (_selectedTarget is { Kind: not FrontedV3DesignSelectionKind.Root })
        {
            return;
        }

        var sourceDesignItem = SelectedDesignItem;
        if (sourceDesignItem?.Config is not { } sourceConfig)
        {
            return;
        }

        var sourceRegistration = _selectionBuilder.ResolveRegistration(sourceConfig);
        if (sourceRegistration is null)
        {
            return;
        }

        // 仅当控件显式声明 SupportsPeerStyleTransfer 时才允许同类型样式传播。
        // 该检查与 CanApplyAppearanceToSameType 保持一致，防止 Execute 被绕过 CanExecute 直接调用时
        // 对未声明该能力的控件类型意外传播样式。
        if (!sourceRegistration.SupportsPeerStyleTransfer)
        {
            return;
        }

        var peerDesignItems = TryGetSameTypePeerDesignItems();
        if (peerDesignItems.Count == 0)
        {
            return;
        }

        var peers = new List<PeerStyleTarget>(peerDesignItems.Count);
        foreach (var peerDesignItem in peerDesignItems)
        {
            if (peerDesignItem.Config is null)
            {
                continue;
            }

            var peerRegistration = _selectionBuilder.ResolveRegistration(peerDesignItem.Config);
            if (peerRegistration is null)
            {
                continue;
            }

            peers.Add(new PeerStyleTarget(peerRegistration, peerDesignItem.Config));
        }

        if (peers.Count == 0)
        {
            return;
        }

        CaptureUndoSnapshot();
        StyleTransferService.TransferPeerStyle(
            sourceRegistration,
            sourceConfig,
            peers,
            FrontedV3StyleTransferProfile.Default);

        CurrentDocument.IsDirty = true;
        RebuildPropertyEditorItems();
        RefreshDirtyState();
        RequestPreviewRenderCurrentDocument();
        ApplyAppearanceToSameTypeCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// 返回当前文档中与选中根控件 <see cref="FrontedControlConfigBase.ControlType"/> 相同、
    /// 但 Config 引用不同的设计项列表。仅根控件选中时返回非空列表。
    /// </summary>
    /// <returns>同类型 peer 设计项列表；无选中或无 peer 时返回空列表。</returns>
    private List<FrontedControlDesignItem> TryGetSameTypePeerDesignItems()
    {
        if (CurrentDocument is null)
        {
            return [];
        }

        // 子控件选中时不参与同类型传播，避免对 Part/CollectionItem 应用外观传播。
        if (_selectedTarget is { Kind: not FrontedV3DesignSelectionKind.Root })
        {
            return [];
        }

        var sourceDesignItem = SelectedDesignItem;
        if (sourceDesignItem?.Config is not { } sourceConfig)
        {
            return [];
        }

        var sourceControlType = sourceConfig.ControlType;
        var peers = new List<FrontedControlDesignItem>();
        foreach (var item in CurrentDocument.Controls)
        {
            if (item is null
                || ReferenceEquals(item, sourceDesignItem)
                || item.Config is null
                || !string.Equals(item.Config.ControlType, sourceControlType, StringComparison.Ordinal))
            {
                continue;
            }

            peers.Add(item);
        }

        return peers;
    }

    /// <summary>
    /// 判断"应用到所有子控件"命令是否可执行：需要选中根控件且其 Config 存在
    /// <see cref="FrontedV3PartCollectionDefinition.ItemPropertiesFactory"/> 非 <see langword="null"/>
    /// 的 PartCollection 定义。
    /// </summary>
    /// <returns>当可执行父到子外观派发时返回 <see langword="true"/>。</returns>
    private bool CanApplyParentStyleToChildren() => HasChildAppearanceProperties;

    /// <summary>
    /// 将当前选中根控件的外观属性（按 <see cref="FrontedV3StyleTransferProfile.Default"/>）
    /// 派发到所有子控件集合项（如 GlobalScoreRow 的 Cells）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 实现要点：
    /// <list type="bullet">
    /// <item>通过 <see cref="BuiltInPartCollectionDefinitionResolver"/> 查找选中控件 Config 上
    /// <see cref="FrontedV3PartCollectionDefinition.ItemPropertiesFactory"/> 非 <see langword="null"/> 的集合定义。</item>
    /// <item>对每个集合项，使用 <see cref="FrontedV3PartCollectionDefinition.ItemPropertiesFactory"/>
    /// 构建绑定到该项 <c>itemKey</c> 的子属性列表（<see cref="FrontedV3Storage.CollectionItemProperty"/> 存储）。</item>
    /// <item>调用 <see cref="FrontedV3StyleTransferService.ApplyParentStyle"/>，按 OptionsPath 匹配父子属性，
    /// 仅传播 <see cref="FrontedV3PropertySemantic.Appearance"/> 语义属性。</item>
    /// <item>由于 <see cref="FrontedV3Storage.CollectionItemProperty"/> 存储以父 Config 为载体按 itemKey 定位子项，
    /// 此处将父 Config 同时作为 parentConfig 与 childConfigs 元素传入。</item>
    /// </list>
    /// </para>
    /// <para>
    /// 完成派发后触发：Undo 快照已先于派发捕获、属性面板重建、预览刷新、文档标记为脏。
    /// </para>
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanApplyParentStyleToChildren))]
    private void ApplyParentStyleToChildren()
    {
        if (CurrentDocument is null)
        {
            return;
        }

        // 子控件选中时禁用，仅根控件选中可派发外观到子控件。
        if (_selectedTarget is { Kind: not FrontedV3DesignSelectionKind.Root })
        {
            return;
        }

        var sourceConfig = SelectedDesignItem?.Config;
        if (sourceConfig is null)
        {
            return;
        }

        var registration = _selectionBuilder.ResolveRegistration(sourceConfig);
        if (registration is null)
        {
            return;
        }

        var collection = ResolveChildAppearanceCollection(sourceConfig);
        if (collection?.ItemPropertiesFactory is null || collection.CollectionGetter is null)
        {
            return;
        }

        var childItems = collection.CollectionGetter(sourceConfig);
        if (childItems is null || childItems.Count == 0)
        {
            return;
        }

        CaptureUndoSnapshot();

        // 对每个子项构建绑定到其 itemKey 的子属性列表，并以父 Config 作为 ApplyParentStyle 的 childConfig 载体
        // （CollectionItemProperty 存储通过 itemKey 在父 Config 的集合中定位实际子项）。
        foreach (var childItem in childItems)
        {
            if (childItem is null)
            {
                continue;
            }

            var itemKey = collection.ItemKeySelector(childItem);
            var childProperties = collection.ItemPropertiesFactory(itemKey);
            if (childProperties.Count == 0)
            {
                continue;
            }

            StyleTransferService.ApplyParentStyle(
                registration.Properties,
                sourceConfig,
                childProperties,
                [sourceConfig],
                FrontedV3StyleTransferProfile.Default);
        }

        CurrentDocument.IsDirty = true;
        RebuildPropertyEditorItems();
        RefreshDirtyState();
        RequestPreviewRenderCurrentDocument();
    }

    /// <summary>
    /// 判断"清除子控件外观覆盖"命令是否可执行：需要选中根控件且其 Config 存在
    /// <see cref="FrontedV3PartCollectionDefinition.ItemPropertiesFactory"/> 非 <see langword="null"/>
    /// 的 PartCollection 定义。
    /// </summary>
    /// <returns>当可清除子控件外观覆盖时返回 <see langword="true"/>。</returns>
    private bool CanClearChildStyleOverrides() => HasChildAppearanceProperties;

    /// <summary>
    /// 清除所有子控件集合项的外观属性 override（<see cref="FrontedV3PropertyInheritance.ParentFallback"/>
    /// 与 <see cref="FrontedV3PropertyInheritance.LockedToParent"/> 模式属性），
    /// 使子控件回退到父值。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 实现要点与 <see cref="ApplyParentStyleToChildren"/> 类似，通过
    /// <see cref="BuiltInPartCollectionDefinitionResolver"/> 查找集合定义，对每个子项构建子属性列表，
    /// 调用 <see cref="FrontedV3StyleTransferService.ClearChildOverrides"/> 将可清空属性写 <see langword="null"/>。
    /// </para>
    /// <para>
    /// 完成清除后触发：Undo 快照已先于清除捕获、属性面板重建、预览刷新、文档标记为脏。
    /// </para>
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanClearChildStyleOverrides))]
    private void ClearChildStyleOverrides()
    {
        if (CurrentDocument is null)
        {
            return;
        }

        // 子控件选中时禁用，仅根控件选中可清除子控件外观覆盖。
        if (_selectedTarget is { Kind: not FrontedV3DesignSelectionKind.Root })
        {
            return;
        }

        var sourceConfig = SelectedDesignItem?.Config;
        if (sourceConfig is null)
        {
            return;
        }

        var collection = ResolveChildAppearanceCollection(sourceConfig);
        if (collection?.ItemPropertiesFactory is null || collection.CollectionGetter is null)
        {
            return;
        }

        var childItems = collection.CollectionGetter(sourceConfig);
        if (childItems is null || childItems.Count == 0)
        {
            return;
        }

        CaptureUndoSnapshot();

        foreach (var childItem in childItems)
        {
            if (childItem is null)
            {
                continue;
            }

            var itemKey = collection.ItemKeySelector(childItem);
            var childProperties = collection.ItemPropertiesFactory(itemKey);
            if (childProperties.Count == 0)
            {
                continue;
            }

            StyleTransferService.ClearChildOverrides(
                childProperties,
                [sourceConfig],
                FrontedV3StyleTransferProfile.Default);
        }

        CurrentDocument.IsDirty = true;
        RebuildPropertyEditorItems();
        RefreshDirtyState();
        RequestPreviewRenderCurrentDocument();
    }

    /// <summary>
    /// 切换子控件继承属性的"跟随父控件 / 独立设定"状态。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 当 <paramref name="propertyName"/> 对应的属性为 <see cref="FrontedV3PropertyInheritance.ParentFallback"/> 继承属性时：
    /// <list type="bullet">
    /// <item>若当前为跟随父控件（<c>IsInheritedFromParent=true</c>），切换为独立设定：将当前显示值写入子控件作为 override。</item>
    /// <item>若当前为独立设定（<c>IsInheritedFromParent=false</c>），切换为跟随父控件：清除子控件 override（写 null）。</item>
    /// </list>
    /// </para>
    /// <para>
    /// 非继承属性调用此命令为 no-op。
    /// </para>
    /// </remarks>
    /// <param name="propertyName">属性行的 OptionsPath（<see cref="FrontedPropertyEditorItem.PropertyName"/>）。</param>
    [RelayCommand]
    private void TogglePropertyInheritance(string? propertyName)
    {
        if (string.IsNullOrEmpty(propertyName)
            || CurrentDocument is null
            || _selectedTarget is null
            || _selectedTarget.DesignItem is not { } designItem)
        {
            return;
        }

        if (!_schemaPropertiesByPath.TryGetValue(propertyName, out var schemaProperty))
        {
            return;
        }

        if (schemaProperty.Metadata.Inheritance != FrontedV3PropertyInheritance.ParentFallback)
        {
            return;
        }

        var config = designItem.Config;
        var childValue = schemaProperty.GetValue(config);
        var wasMissing = FrontedV3StyleTransferService.IsOverrideMissing(childValue);

        CaptureUndoSnapshot();

        if (wasMissing)
        {
            // 跟随父控件 → 独立设定：将当前继承显示值（父值）写入子控件作为 override。
            var parentProperties = ResolveParentPropertiesForInheritance(_selectedTarget);
            var parentProperty = parentProperties is not null
                ? FindPropertyByOptionsPath(parentProperties, schemaProperty.OptionsPath)
                : null;
            var inheritedValue = parentProperty is not null
                ? parentProperty.GetValue(config)
                : childValue;
            StyleTransferService.TrySetChildValue(schemaProperty, config, inheritedValue);
        }
        else
        {
            // 独立设定 → 跟随父控件：清除子控件 override（写 null）。
            schemaProperty.SetValue(config, null);
        }

        CurrentDocument.IsDirty = true;
        RebuildPropertyEditorItems();
        RefreshDirtyState();
        RequestPreviewRenderCurrentDocument();
    }

    /// <summary>
    /// 判断"按模板重新分配"命令是否可执行：需要选中根控件且其 Config 存在
    /// <see cref="FrontedV3PartCollectionDefinition.ApplyTemplate"/> 非 <see langword="null"/> 的 PartCollection 定义。
    /// </summary>
    /// <returns>当可执行模板分配时返回 <see langword="true"/>。</returns>
    private bool CanApplyLayoutTemplate()
    {
        if (_selectedTarget is { Kind: not FrontedV3DesignSelectionKind.Root })
        {
            return false;
        }

        if (SelectedDesignItem?.Config is not { } sourceConfig)
        {
            return false;
        }

        foreach (var collection in _selectionBuilder.GetCollections(sourceConfig))
        {
            if (collection.ApplyTemplate is not null)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 对当前选中根控件的子控件集合应用布局模板（如 GlobalScoreRow 的 BO3/BO5 模板），
    /// 重新分配子控件的位置与可见性。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 实现要点：通过 <see cref="FrontedV3DesignSelectionBuilder"/> 查找选中控件 Config 上
    /// <see cref="FrontedV3PartCollectionDefinition.ApplyTemplate"/> 非 <see langword="null"/> 的集合定义，
    /// 调用其 <see cref="FrontedV3PartCollectionDefinition.ApplyTemplate"/> 回调。
    /// 该回调由控件自身实现，决定如何按模板分配位置与可见性（不修改外观属性）。
    /// </para>
    /// <para>
    /// 通用按钮路径：<see cref="FrontedV3TemplateContext.TemplateId"/> 为 <see langword="null"/>，
    /// 控件应回退到基于 <see cref="FrontedV3TemplateContext.CurrentBoModeState"/> 的默认模板。
    /// 具名模板按钮路径：通过 <see cref="ApplyLayoutTemplateByName"/> 调用，
    /// <see cref="FrontedV3TemplateContext.TemplateId"/> 为被点击模板的 Id。
    /// </para>
    /// <para>
    /// 完成分配后触发：Undo 快照已先于分配捕获、属性面板重建、预览刷新、文档标记为脏。
    /// </para>
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanApplyLayoutTemplate))]
    private void ApplyLayoutTemplate()
        => ApplyLayoutTemplateCore(templateId: null);

    /// <summary>
    /// 对当前选中根控件的子控件集合应用指定具名布局模板（如 <c>BO3</c>、<c>BO5</c>），
    /// 重新分配子控件的位置与可见性。
    /// </summary>
    /// <param name="templateId">被点击的具名模板 Id；为 <see langword="null"/> 或空字符串时回退到通用按钮行为。</param>
    /// <remarks>
    /// 该命令由 Designer 中具名模板按钮（<see cref="LayoutTemplates"/>）触发，
    /// 将 <paramref name="templateId"/> 作为 <see cref="FrontedV3TemplateContext.TemplateId"/>
    /// 传递给 <see cref="FrontedV3PartCollectionDefinition.ApplyTemplate"/> 回调。
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanApplyLayoutTemplate))]
    private void ApplyLayoutTemplateByName(string? templateId)
        => ApplyLayoutTemplateCore(templateId);

    /// <summary>
    /// "按模板重新分配"通用/具名模板共享实现。查找首个支持 <c>ApplyTemplate</c> 的 PartCollection，
    /// 构建 <see cref="FrontedV3TemplateContext"/>（携带当前 BO 状态与 <paramref name="templateId"/>），
    /// 调用回调并触发属性面板/预览刷新。
    /// </summary>
    /// <param name="templateId">具名模板 Id；为 <see langword="null"/> 时表示通用按钮路径。</param>
    private void ApplyLayoutTemplateCore(string? templateId)
    {
        if (CurrentDocument is null)
        {
            return;
        }

        if (_selectedTarget is { Kind: not FrontedV3DesignSelectionKind.Root })
        {
            return;
        }

        var sourceConfig = SelectedDesignItem?.Config;
        if (sourceConfig is null)
        {
            return;
        }

        FrontedV3PartCollectionDefinition? targetCollection = null;
        foreach (var collection in _selectionBuilder.GetCollections(sourceConfig))
        {
            if (collection.ApplyTemplate is not null)
            {
                targetCollection = collection;
                break;
            }
        }

        if (targetCollection?.ApplyTemplate is not { } applyTemplate)
        {
            return;
        }

        var context = BuildTemplateContext(templateId);

        // CaptureUndoSnapshot 在 applyTemplate 之前调用，捕获修改前快照，
        // 这样 Undo 时能回到调用前状态。若回调返回 false（无变更），
        // 需丢弃刚捕获的快照，避免无变化操作污染 Undo 与 dirty 状态。
        var undoCountBefore = _undoStack.Count;
        CaptureUndoSnapshot();
        var pushedNewSnapshot = _undoStack.Count > undoCountBefore;

        var modified = applyTemplate(sourceConfig, context);
        if (!modified)
        {
            if (pushedNewSnapshot)
            {
                _undoStack.Pop();
                NotifyUndoRedoCommands();
            }

            return;
        }

        CurrentDocument.IsDirty = true;
        RebuildPropertyEditorItems();
        RefreshDirtyState();
        RequestPreviewRenderCurrentDocument();
    }

    /// <summary>
    /// 构建调用 <see cref="FrontedV3PartCollectionDefinition.ApplyTemplate"/> 时使用的
    /// <see cref="FrontedV3TemplateContext"/>，携带当前编辑的 BO 状态、窗口/Canvas 信息、
    /// Designer 文档实例、DI 服务提供器与可选的具名模板 Id。
    /// </summary>
    /// <param name="templateId">具名模板 Id；为 <see langword="null"/> 时表示通用按钮路径。</param>
    /// <returns>用于调用模板分配回调的上下文实例。</returns>
    private FrontedV3TemplateContext BuildTemplateContext(string? templateId)
    {
        var document = CurrentDocument;
        var boModeState = document?.EditingBoModeState ?? FrontedCanvasBoModeState.Bo5;
        var windowTypeName = document?.WindowTypeName ?? string.Empty;
        var canvasName = document?.CanvasName ?? string.Empty;
        var services = IAppHost.Host?.Services ?? (IServiceProvider)EmptyServiceProvider.Instance;

        return new FrontedV3TemplateContext(
            services,
            boModeState,
            windowTypeName,
            canvasName,
            document,
            templateId);
    }

    /// <summary>
    /// 不解析任何服务的空 <see cref="IServiceProvider"/>，作为 Designer 默认服务提供器。
    /// </summary>
    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public static readonly EmptyServiceProvider Instance = new();

        public object? GetService(Type serviceType) => null;
    }

    /// <summary>
    /// 返回给定 Config 上首个 <see cref="FrontedV3PartCollectionDefinition.ItemPropertiesFactory"/>
    /// 非 <see langword="null"/> 的 PartCollection 定义；无则返回 <see langword="null"/>。
    /// </summary>
    /// <param name="config">根控件配置实例。</param>
    /// <returns>支持外观属性派发的集合定义；无则 <see langword="null"/>。</returns>
    private FrontedV3PartCollectionDefinition? ResolveChildAppearanceCollection(FrontedControlConfigBase config)
    {
        ArgumentNullException.ThrowIfNull(config);

        foreach (var candidate in _selectionBuilder.GetCollections(config))
        {
            if (candidate.ItemPropertiesFactory is not null)
            {
                return candidate;
            }
        }

        return null;
    }
}
