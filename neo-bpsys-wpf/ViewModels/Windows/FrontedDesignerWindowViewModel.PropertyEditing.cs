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
/// Fronted Designer 的属性网格构建与属性提交业务逻辑。
/// </summary>
public partial class FrontedDesignerWindowViewModel
{
    public void ClearPropertyEditErrorForBufferUpdate(string propertyName)
    {
        ClearPropertyEditError(propertyName);
    }
    public void SelectPolygonVertex(int index)
    {
        if (SelectedDesignItem?.Config is not IPolygonFrontedControlConfig polygon || polygon.Points.Count == 0)
        {
            SelectedPolygonVertexIndex = -1;
            return;
        }

        SelectedPolygonVertexIndex = Math.Clamp(index, 0, polygon.Points.Count - 1);
    }

    public void MoveSelectedPolygonVertex(Point canvasPoint, bool renderPreview)
    {
        if (CurrentDocument is null
            || SelectedDesignItem?.Config is not IPolygonFrontedControlConfig polygon
            || SelectedPolygonVertexIndex < 0
            || SelectedPolygonVertexIndex >= polygon.Points.Count)
        {
            return;
        }

        var normalized = PolygonVertexGeometryHelper.ToNormalizedPoint(SelectedDesignItem.Config, canvasPoint);
        polygon.Points[SelectedPolygonVertexIndex].X = normalized.X;
        polygon.Points[SelectedPolygonVertexIndex].Y = normalized.Y;
        CurrentDocument.IsDirty = true;
        OnDesignItemGeometryChanged(renderPreview);
    }

    [RelayCommand]
    private void AddPolygonVertex()
    {
        if (CurrentDocument is null || SelectedDesignItem?.Config is not IPolygonFrontedControlConfig polygon)
        {
            return;
        }

        CaptureUndoSnapshot();
        var afterIndex = SelectedPolygonVertexIndex >= 0 && SelectedPolygonVertexIndex < polygon.Points.Count
            ? SelectedPolygonVertexIndex
            : polygon.Points.Count - 1;
        var nextIndex = polygon.Points.Count == 0 ? 0 : (afterIndex + 1) % polygon.Points.Count;
        var first = polygon.Points.Count == 0 ? new PolygonVertexConfig(0, 0) : polygon.Points[afterIndex];
        var second = polygon.Points.Count == 0 ? new PolygonVertexConfig(1, 1) : polygon.Points[nextIndex];
        polygon.Points.Insert(afterIndex + 1, new PolygonVertexConfig(
            (first.X + second.X) / 2D,
            (first.Y + second.Y) / 2D));
        SelectedPolygonVertexIndex = afterIndex + 1;
        CurrentDocument.IsDirty = true;
        FinishPolygonVertexEdit();
    }

    [RelayCommand(CanExecute = nameof(CanRemovePolygonVertex))]
    private void RemovePolygonVertex()
    {
        if (CurrentDocument is null
            || SelectedDesignItem?.Config is not IPolygonFrontedControlConfig polygon
            || !CanRemovePolygonVertex)
        {
            return;
        }

        CaptureUndoSnapshot();
        polygon.Points.RemoveAt(SelectedPolygonVertexIndex);
        SelectedPolygonVertexIndex = Math.Min(SelectedPolygonVertexIndex, polygon.Points.Count - 1);
        CurrentDocument.IsDirty = true;
        FinishPolygonVertexEdit();
    }

    private void FinishPolygonVertexEdit()
    {
        OnPropertyChanged(nameof(SelectedPolygonVertexDisplay));
        OnPropertyChanged(nameof(CanRemovePolygonVertex));
        RemovePolygonVertexCommand.NotifyCanExecuteChanged();
        RefreshDirtyState();
        ValidateCurrentDocument();
        RequestPreviewRenderCurrentDocument();
    }

    public bool ApplyPropertyEdit(FrontedPropertyEditorItem item, object? newValue, bool refreshPropertyGrid = true)
    {
        if (CurrentDocument is null || SelectedDesignItem is null)
        {
            return false;
        }

        if (item.IsReadOnly || item.EditorKind == FrontedPropertyEditorKind.ReadOnly)
        {
            return false;
        }

        if (!item.IsMultiSelectionBatchEditable)
        {
            return false;
        }

        if (item.IsMultiSelectionMixedValue && IsEmptyMultiSelectionPlaceholderValue(newValue))
        {
            return true;
        }

        if (item.CanBrowseResource
            && newValue is string text
            && IsAbsoluteFilePath(text))
        {
            return ApplyPropertyResourceSelection(item, text);
        }

        _propertyEditErrors.Remove(item.PropertyName);
        _propertyEditBuffers.Remove(item.PropertyName);

        // Schema 驱动路径：子控件选中时，属性编辑通过 PropertyDefinition.Storage 写入，
        // 不通过 propertyName 字符串反射写入。
        if (_schemaPropertiesByPath.TryGetValue(item.PropertyName, out var schemaProperty)
            && _selectedTarget is not null)
        {
            return ApplySchemaPropertyEdit(item, schemaProperty, newValue, refreshPropertyGrid);
        }

        if (item.PropertyName == nameof(FrontedControlDesignItem.Name))
        {
            return ApplyNameEdit(item, newValue);
        }

        var property = SelectedDesignItem.Config.GetType().GetProperty(
            item.PropertyName,
            BindingFlags.Instance | BindingFlags.Public);
        if (property is null || !property.CanWrite)
        {
            return false;
        }

        var commitValue = ClampEditorPropertyValue(item.PropertyName, SelectedDesignItem.Config.ControlType, newValue, out var wasClamped);
        if (!TryConvertPropertyValue(property, commitValue, out var convertedValue, out var errorMessage))
        {
            SetPropertyEditError(item, errorMessage, newValue);
            return false;
        }

        var editTargets = GetPropertyEditTargets(item.PropertyName);
        var changedTargets = editTargets
            .Where(target =>
            {
                var targetProperty = target.Config.GetType().GetProperty(
                    item.PropertyName,
                    BindingFlags.Instance | BindingFlags.Public);
                return targetProperty is not null
                       && targetProperty.CanWrite
                       && !ValuesEqual(targetProperty.GetValue(target.Config), convertedValue);
            })
            .ToList();

        if (changedTargets.Count == 0)
        {
            ClearPropertyEditError(item.PropertyName);
            item.Value = convertedValue;
            item.EditText = GetCommittedEditText(item, convertedValue);
            return true;
        }

        CaptureUndoSnapshot();
        foreach (var target in changedTargets)
        {
            var targetProperty = target.Config.GetType().GetProperty(
                item.PropertyName,
                BindingFlags.Instance | BindingFlags.Public);
            targetProperty?.SetValue(target.Config, convertedValue);
        }

        item.Value = convertedValue;
        item.EditText = GetCommittedEditText(item, convertedValue);
        CurrentDocument.IsDirty = true;

        if (IsGeometryProperty(item.PropertyName))
        {
            foreach (var target in changedTargets)
            {
                SyncLinkedOverlays(target);
            }
        }

        FinishPropertyEdit(item.PropertyName, refreshPropertyGrid);
        if (wasClamped)
        {
            StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "InputTruncated");
        }

        return true;
    }

    /// <summary>
    /// 应用属性编辑，并在资源属性输入超限本地图片时提供压缩选项。
    /// </summary>
    /// <param name="item">要编辑的属性行。</param>
    /// <param name="newValue">待应用的属性值。</param>
    /// <param name="refreshPropertyGrid">是否在提交后重建属性网格；自动提交场景应传 <see langword="false"/> 以避免输入框失焦。</param>
    /// <returns>属性已更新时返回 <see langword="true"/>。</returns>
    public async Task<bool> ApplyPropertyEditAsync(FrontedPropertyEditorItem item, object? newValue, bool refreshPropertyGrid = true)
    {
        if (item.CanBrowseResource
            && newValue is string text
            && IsAbsoluteFilePath(text))
        {
            return await ApplyPropertyResourceSelectionAsync(item, text);
        }

        return ApplyPropertyEdit(item, newValue, refreshPropertyGrid);
    }

    private IReadOnlyList<FrontedControlDesignItem> GetPropertyEditTargets(string propertyName)
    {
        if (propertyName == nameof(FrontedControlDesignItem.Name)
            || SelectedDesignItem is null
            || SelectedDesignItems.Count <= 1)
        {
            return SelectedDesignItem is null ? [] : [SelectedDesignItem];
        }

        var selectedType = SelectedDesignItem.Config.ControlType;
        var sameTypeTargets = SelectedDesignItems
            .Where(target => target.IsEditableInEditor
                             && target.IsSelectableInEditor
                             && string.Equals(target.Config.ControlType, selectedType, StringComparison.Ordinal))
            .ToList();

        return sameTypeTargets.Count == SelectedDesignItems.Count
            ? sameTypeTargets
            : [SelectedDesignItem];
    }

    /// <summary>
    /// 通过 <see cref="FrontedV3PropertyDefinition.Storage"/> 应用 Schema 属性编辑，
    /// 不通过 propertyName 字符串反射写入。
    /// </summary>
    /// <param name="item">属性行。</param>
    /// <param name="schemaProperty">属性定义。</param>
    /// <param name="newValue">用户输入的新值。</param>
    /// <returns>是否成功提交。</returns>
    private bool ApplySchemaPropertyEdit(
        FrontedPropertyEditorItem item,
        FrontedV3PropertyDefinition schemaProperty,
        object? newValue,
        bool refreshPropertyGrid = true)
    {
        if (_selectedTarget is null || _selectedTarget.DesignItem is not { } designItem)
        {
            return false;
        }

        var config = designItem.Config;
        object? convertedValue;
        if (schemaProperty.PropertyType == typeof(double)
            || schemaProperty.PropertyType == typeof(double?))
        {
            if (schemaProperty.PropertyType == typeof(double?)
                && newValue is string sizeText
                && string.IsNullOrWhiteSpace(sizeText))
            {
                convertedValue = null;
            }
            else if (!TryConvertSchemaDoubleValue(newValue, out var doubleValue, out var errorMessage))
            {
                SetPropertyEditError(item, errorMessage, newValue);
                return false;
            }
            else
            {
                convertedValue = NormalizeSchemaGeometryValue(item.PropertyName, doubleValue);
            }
        }
        else
        {
            // 复用 FrontedV3ValueConverter 统一转换规则，覆盖 enum、nullable、Color、Brush、
            // JsonElement 与已正确类型的对象；Convert.ChangeType 仅处理 IConvertible 兜底。
            if (!FrontedV3ValueConverter.TryConvert(newValue, schemaProperty.PropertyType, out convertedValue))
            {
                var errorMessage = I18nHelper.GetLocalizedString(
                    AppI18nDictionaries.Designer,
                    "PropertyValidationErrors");
                SetPropertyEditError(item, errorMessage, newValue);
                return false;
            }
        }

        // 多选编辑：通过相同 OptionsPath 和 Schema Storage 写入所有同类型选中控件。
        var editTargets = GetPropertyEditTargets(item.PropertyName);
        var changedTargets = editTargets
            .Where(target => !ValuesEqual(schemaProperty.GetValue(target.Config), convertedValue))
            .ToList();

        if (changedTargets.Count == 0)
        {
            ClearPropertyEditError(item.PropertyName);
            item.Value = convertedValue;
            item.EditText = GetCommittedEditText(item, convertedValue);
            return true;
        }

        CaptureUndoSnapshot();
        foreach (var target in changedTargets)
        {
            schemaProperty.SetValue(target.Config, convertedValue);
        }

        CurrentDocument.IsDirty = true;

        var refreshedValue = schemaProperty.GetValue(config);
        item.Value = refreshedValue;
        item.EditText = GetCommittedEditText(item, refreshedValue);

        ClearPropertyEditError(item.PropertyName);

        if (IsGeometryProperty(item.PropertyName))
        {
            foreach (var target in changedTargets)
            {
                SyncLinkedOverlays(target);
            }
        }

        FinishPropertyEdit(item.PropertyName, refreshPropertyGrid);
        OnDesignItemGeometryChanged(renderPreview: true, refreshPropertyGrid: refreshPropertyGrid);
        return true;
    }

    /// <summary>
    /// 尝试将用户输入转换为 Schema 几何属性所需的 <see cref="double"/> 值。
    /// </summary>
    /// <param name="newValue">用户输入。</param>
    /// <param name="value">转换后的值。</param>
    /// <param name="errorMessage">转换失败时的错误消息。</param>
    /// <returns>是否转换成功。</returns>
    private static bool TryConvertSchemaDoubleValue(object? newValue, out double value, out string errorMessage)
    {
        value = 0D;
        errorMessage = string.Empty;

        switch (newValue)
        {
            case double d:
                value = d;
                break;
            case IConvertible convertible:
                try
                {
                    value = Convert.ToDouble(convertible, CultureInfo.InvariantCulture);
                }
                catch
                {
                    errorMessage = I18nHelper.GetLocalizedString(
                        AppI18nDictionaries.Designer,
                        "PropertyValidationErrors");
                    return false;
                }

                break;
            default:
                if (!double.TryParse(
                        Convert.ToString(newValue, CultureInfo.InvariantCulture),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out value))
                {
                    errorMessage = I18nHelper.GetLocalizedString(
                        AppI18nDictionaries.Designer,
                        "PropertyValidationErrors");
                    return false;
                }

                break;
        }

        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            errorMessage = I18nHelper.GetLocalizedString(
                AppI18nDictionaries.Designer,
                "PropertyValidationErrors");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 对 Schema 几何属性值进行归一化（对齐网格、最小尺寸约束）。
    /// </summary>
    /// <param name="propertyName">属性名（OptionsPath 末段）。</param>
    /// <param name="value">原始值。</param>
    /// <returns>归一化后的值。</returns>
    private static double NormalizeSchemaGeometryValue(string propertyName, double value)
    {
        if (propertyName is "Width" or "Height")
        {
            return Math.Max(
                FrontedDesignerGeometryHelper.MinResizeWidth,
                FrontedDesignerGeometryHelper.Snap(value));
        }

        if (propertyName is "X" or "Y" or "Left" or "Top")
        {
            return FrontedDesignerGeometryHelper.Snap(value);
        }

        return value;
    }

    private bool ApplyNameEdit(FrontedPropertyEditorItem item, object? newValue)
    {
        if (CurrentDocument is null || SelectedDesignItem is null)
        {
            return false;
        }

        if (!SelectedDesignItem.IsSelectableInEditor
            || !SelectedDesignItem.IsEditableInEditor)
        {
            SetPropertyEditError(
                item,
                I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "InvalidControlName"),
                newValue);
            return false;
        }

        var oldName = SelectedDesignItem.Name;
        var rawName = Convert.ToString(newValue, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
        var newName = FrontedTextLimitHelper.Clamp(rawName, FrontedLayoutLimits.MaxControlNameLength);
        var wasClamped = !string.Equals(rawName, newName, StringComparison.Ordinal);
        if (oldName == newName)
        {
            ClearPropertyEditError(item.PropertyName);
            item.Value = oldName;
            item.EditText = oldName;
            return true;
        }

        if (string.IsNullOrWhiteSpace(newName) || !ValidControlNameRegex.IsMatch(newName))
        {
            SetPropertyEditError(
                item,
                I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "InvalidControlName"),
                newValue);
            return false;
        }

        var existingNames = CurrentDocument.Controls
            .Where(control => !ReferenceEquals(control, SelectedDesignItem))
            .Select(control => control.Name)
            .ToHashSet(StringComparer.Ordinal);

        if (existingNames.Contains(newName))
        {
            SetPropertyEditError(
                item,
                I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "DuplicateControlName"),
                newValue);
            return false;
        }

        _referenceScanner.SetControls(CurrentDocument.Controls);
        if (_referenceScanner.GetIncomingReferences(oldName).Count > 0)
        {
            SetPropertyEditError(
                item,
                I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "ReferencedControlRenameBlocked"),
                newValue);
            return false;
        }

        CaptureUndoSnapshot();
        SelectedDesignItem.Name = newName;
        item.Value = newName;
        item.EditText = newName;
        CurrentDocument.IsDirty = true;
        FinishPropertyEdit(item.PropertyName);
        if (wasClamped)
        {
            StatusMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "InputTruncated");
        }

        return true;
    }
    private void RebuildPropertyEditorItems()
    {
        _isRebuildingPropertyGrid = true;
        OnPropertyChanged(nameof(IsRebuildingPropertyGrid));
        try
        {
            PropertyEditorItems.Clear();
            _schemaPropertiesByPath.Clear();

            if (CurrentDocument is null || SelectedDesignItem is null)
            {
                return;
            }

            // 子控件（Part/CollectionItem）选中时走 Schema 驱动路径：
            // 属性行由 SelectedTarget.Properties 构造，属性编辑通过 Storage 写入。
            if (_selectedTarget is { Kind: not FrontedV3DesignSelectionKind.Root } subTarget)
            {
                foreach (var row in BuildSchemaPropertyEditorItems(subTarget))
                {
                    PropertyEditorItems.Add(row);
                }

                return;
            }

            // Root 选中时走 Schema 驱动路径：属性网格由 FrontedV3PropertyDefinition 列表构造，
            // 属性值通过 Storage 读写，不通过反射扫描 Config。
            // SelectedTarget 为 null 时（Missing Plugin：Registry 中未注册）同样走 BuildFromSchema，
            // 由 AddMissingPluginRows 显示诊断行，不再回退到旧反射路径扫描 Config 公共属性。
            IEnumerable<FrontedPropertyEditorItem> rows;
            if (_selectedTarget is { Kind: FrontedV3DesignSelectionKind.Root } rootTarget)
            {
                rows = _propertyGridBuilder.BuildFromSchema(
                    CurrentDocument,
                    SelectedDesignItem,
                    _validator,
                    _referenceScanner,
                    rootTarget.Properties,
                    _schemaPropertiesByPath);
            }
            else
            {
                // Missing Plugin：控件未在 Registry 中注册，SelectedTarget 为 null。
                // 仍调用 BuildFromSchema，传入空 Schema，由 AddMissingPluginRows 显示诊断行。
                rows = _propertyGridBuilder.BuildFromSchema(
                    CurrentDocument,
                    SelectedDesignItem,
                    _validator,
                    _referenceScanner,
                    Array.Empty<FrontedV3PropertyDefinition>(),
                    _schemaPropertiesByPath);
            }

            foreach (var row in rows)
            {
                ApplyMultiSelectionPropertyRowState(row);
                if (_propertyEditErrors.TryGetValue(row.PropertyName, out var editError))
                {
                    if (_propertyEditBuffers.TryGetValue(row.PropertyName, out var editBuffer))
                    {
                        row.EditText = editBuffer;
                    }

                    row.SetEditError(editError);
                    row.ValidationErrors = row.ValidationErrors
                        .Concat([editError])
                        .Distinct(StringComparer.Ordinal)
                        .ToArray();
                    row.ValidationMessages = row.ValidationMessages
                        .Concat([CreatePropertyEditValidationMessage(editError, row.PropertyName)])
                        .ToArray();
                }

                PropertyEditorItems.Add(row);
            }
        }
        finally
        {
            _isRebuildingPropertyGrid = false;
            OnPropertyChanged(nameof(IsRebuildingPropertyGrid));
        }
    }

    /// <summary>
    /// 为子控件选中（Part/CollectionItem）构造 Schema 驱动的属性行。
    /// 属性值通过 <see cref="FrontedV3PropertyDefinition.GetValue"/> 读取，
    /// 编辑时通过 <see cref="FrontedV3PropertyDefinition.SetValue"/> 写入，不通过 propertyName 字符串反射。
    /// </summary>
    /// <param name="selection">子控件选中目标。</param>
    /// <returns>属性行列表。</returns>
    private IEnumerable<FrontedPropertyEditorItem> BuildSchemaPropertyEditorItems(
        FrontedV3DesignSelection selection)
    {
        var config = selection.DesignItem.Config;
        var parentProperties = ResolveParentPropertiesForInheritance(selection);
        var inheritance = selection.Kind == FrontedV3DesignSelectionKind.CollectionItem
            ? ResolveCollectionInheritance(selection)
            : null;

        foreach (var property in selection.Properties)
        {
            _schemaPropertiesByPath[property.OptionsPath] = property;

            var value = ResolvePropertyValueWithInheritance(property, config, parentProperties, inheritance, out var isMissingOverride);
            var displayText = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            var groupName = property.Metadata.GroupName ?? "Layout";
            var editorKind = property.Metadata.EditorKind ?? FrontedPropertyEditorKind.Text;

            var row = new FrontedPropertyEditorItem
            {
                DisplayName = ResolveSchemaPropertyDisplayName(property),
                PropertyName = property.OptionsPath,
                PropertyType = property.PropertyType,
                EditorKind = editorKind,
                Value = value,
                DisplayValue = displayText,
                EditText = displayText,
                GroupName = groupName,
                GroupDisplayName = _localizationService.GetGroupDisplayName(groupName),
                IsGroupHeaderVisible = true,
                // 跟随父控件（无 override）时只读且禁用编辑器，切换为独立设定后允许编辑。
                IsReadOnly = isMissingOverride == true,
                IsEditingDisabled = isMissingOverride == true,
                Options = ResolveSchemaPropertyOptions(property, editorKind),
                // 仅 ParentFallback 继承属性支持"跟随父控件 / 独立设定"切换。
                CanToggleInheritance = isMissingOverride is not null,
                // 选中（true）= 跟随父控件（无 override）；未选中（false）= 独立设定（有 override）。
                IsInheritedFromParent = isMissingOverride == true
            };

            yield return row;
        }
    }

    /// <summary>
    /// 读取子控件属性值，对 ParentFallback 继承属性通过 StyleTransferService 动态回退到父控件值。
    /// </summary>
    private object? ResolvePropertyValueWithInheritance(
        FrontedV3PropertyDefinition property,
        FrontedControlConfigBase config,
        IReadOnlyList<FrontedV3PropertyDefinition>? parentProperties,
        FrontedV3PartCollectionDefinition? inheritance,
        out bool? isMissingOverride)
    {
        var isInherited = property.Metadata.Inheritance == FrontedV3PropertyInheritance.ParentFallback
                          && inheritance is not null;
        var parentProperty = isInherited && parentProperties is not null
            ? FindPropertyByOptionsPath(parentProperties, property.OptionsPath)
            : null;

        if (isInherited && parentProperty is not null)
        {
            var childValue = property.GetValue(config);
            var missing = FrontedV3StyleTransferService.IsOverrideMissing(childValue);
            isMissingOverride = missing;
            return StyleTransferService.ReadValueWithInheritance(
                property, config, config, parentProperty);
        }

        isMissingOverride = null;
        return property.GetValue(config);
    }

    private IReadOnlyList<FrontedV3PropertyDefinition>? ResolveParentPropertiesForInheritance(
        FrontedV3DesignSelection selection)
    {
        if (selection.DesignItem is not { } designItem)
        {
            return null;
        }

        return _selectionBuilder.ResolveRegistration(designItem.Config)?.Properties;
    }

    private FrontedV3PartCollectionDefinition? ResolveCollectionInheritance(
        FrontedV3DesignSelection selection)
    {
        if (selection.SubTarget is not FrontedV3CollectionItemTarget { CollectionId: { } collectionId }
            || selection.DesignItem is null)
        {
            return null;
        }

        return _selectionBuilder.FindCollection(selection.DesignItem.Config, collectionId);
    }

    private static FrontedV3PropertyDefinition? FindPropertyByOptionsPath(
        IReadOnlyList<FrontedV3PropertyDefinition> properties,
        string optionsPath)
    {
        foreach (var property in properties)
        {
            if (string.Equals(property.OptionsPath, optionsPath, StringComparison.Ordinal))
            {
                return property;
            }
        }

        return null;
    }

    /// <summary>
    /// 为 Schema 驱动的子控件属性解析下拉框选项。
    /// 优先使用属性元数据中显式声明的 <see cref="FrontedV3PropertyMetadata.Options"/>；
    /// 否则按 <paramref name="editorKind"/> 生成：
    /// <see cref="FrontedPropertyEditorKind.FontFamily"/> 复用 <see cref="FrontedPropertyGridBuilder"/> 的字体列表；
    /// <see cref="FrontedPropertyEditorKind.Boolean"/> 生成 true/false 选项；
    /// <see cref="FrontedPropertyEditorKind.Enum"/> 从属性类型（解包 <see cref="Nullable{T}"/>）生成枚举值列表。
    /// 其余编辑器类型返回 <see langword="null"/>。
    /// </summary>
    /// <param name="property">属性定义。</param>
    /// <param name="editorKind">属性使用的编辑器类型。</param>
    /// <returns>选项列表；无需选项的编辑器返回 <see langword="null"/>。</returns>
    private IReadOnlyList<object>? ResolveSchemaPropertyOptions(
        FrontedV3PropertyDefinition property,
        FrontedPropertyEditorKind editorKind)
    {
        if (property.Metadata.Options is { } metadataOptions)
        {
            return metadataOptions.Cast<object>().ToArray();
        }

        if (editorKind == FrontedPropertyEditorKind.FontFamily)
        {
            return _propertyGridBuilder.GetFontFamilyOptions();
        }

        if (editorKind == FrontedPropertyEditorKind.Boolean)
        {
            return [CreateSchemaBooleanOption(true), CreateSchemaBooleanOption(false)];
        }

        if (editorKind == FrontedPropertyEditorKind.Enum)
        {
            var enumType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            if (!enumType.IsEnum)
            {
                return null;
            }

            return Enum.GetValues(enumType)
                .Cast<object>()
                .Select(value => CreateSchemaEnumOption(property.OptionsPath, value))
                .Cast<object>()
                .ToArray();
        }

        return null;
    }

    private FrontedPropertyEditorOption CreateSchemaBooleanOption(bool value) =>
        new()
        {
            Value = value,
            DisplayName = _localizationService.GetDesignerText(
                value ? "Designer.Value.True" : "Designer.Value.False",
                value ? "true" : "false")
        };

    private FrontedPropertyEditorOption CreateSchemaEnumOption(string propertyName, object? value) =>
        new()
        {
            Value = value,
            DisplayName = _localizationService.GetOptionDisplayName(propertyName, value)
        };

    /// <summary>
    /// 解析 Schema 属性的显示名称。优先使用本地化键，回退到 OptionsPath 末段。
    /// </summary>
    /// <param name="property">属性定义。</param>
    /// <returns>显示名称。</returns>
    private string ResolveSchemaPropertyDisplayName(FrontedV3PropertyDefinition property)
    {
        var key = property.Metadata.DisplayNameKey;
        if (!string.IsNullOrWhiteSpace(key))
        {
            var localized = _localizationService.GetPropertyDisplayName(key);
            if (!string.IsNullOrWhiteSpace(localized))
            {
                return localized;
            }
        }

        var optionsPath = property.OptionsPath;
        var lastDot = optionsPath.LastIndexOf('.');
        return lastDot >= 0 && lastDot < optionsPath.Length - 1
            ? optionsPath[(lastDot + 1)..]
            : optionsPath;
    }

    private void ApplyMultiSelectionPropertyRowState(FrontedPropertyEditorItem row)
    {
        if (CurrentDocument is null
            || SelectedDesignItem is null
            || SelectedDesignItems.Count <= 1)
        {
            return;
        }

        if (!CanBatchEditSelectedProperty(row))
        {
            row.IsMultiSelectionBatchEditable = false;
            ClearMultiSelectionPropertyRow(row, makeReadOnly: true);
            return;
        }

        row.IsMultiSelectionBatchEditable = true;
        if (!TryGetCommonSelectedPropertyValue(row.PropertyName, out var commonValue))
        {
            row.IsMultiSelectionMixedValue = true;
            ClearMultiSelectionPropertyRow(row, makeReadOnly: false);
            return;
        }

        row.IsMultiSelectionMixedValue = false;
        row.Value = commonValue;
        row.DisplayValue = GetPropertyEditorDisplayValue(commonValue);
        row.EditText = GetCommittedEditText(row, commonValue);
    }

    private bool CanBatchEditSelectedProperty(FrontedPropertyEditorItem row)
    {
        if (SelectedDesignItem is null || SelectedDesignItems.Count <= 1)
        {
            return true;
        }

        if (row.IsReadOnly
            || row.EditorKind == FrontedPropertyEditorKind.ReadOnly
            || row.PropertyName == nameof(FrontedControlDesignItem.Name)
            || row.CanBrowseBinding
            || row.CanBrowseResource
            || row.EditorKind == FrontedPropertyEditorKind.TextBinding
            || IsMultiSelectionIsolatedProperty(row.PropertyName))
        {
            return false;
        }

        var controlType = SelectedDesignItem.Config.ControlType;

        // Schema 驱动路径：通过 Schema Storage 检查属性是否可批量编辑，不通过反射。
        if (_schemaPropertiesByPath.TryGetValue(row.PropertyName, out var schemaProperty))
        {
            return SelectedDesignItems.All(item =>
            {
                if (!item.IsEditableInEditor
                    || !item.IsSelectableInEditor
                    || !string.Equals(item.Config.ControlType, controlType, StringComparison.Ordinal))
                {
                    return false;
                }

                return !schemaProperty.Metadata.IsReadOnly
                       && IsBatchEditablePropertyType(schemaProperty.PropertyType);
            });
        }

        // 反射回退（无 Schema 的边缘情况）。
        return SelectedDesignItems.All(item =>
        {
            if (!item.IsEditableInEditor
                || !item.IsSelectableInEditor
                || !string.Equals(item.Config.ControlType, controlType, StringComparison.Ordinal))
            {
                return false;
            }

            var property = item.Config.GetType().GetProperty(
                row.PropertyName,
                BindingFlags.Instance | BindingFlags.Public);
            return property is not null
                   && property.CanRead
                   && property.CanWrite
                   && IsBatchEditablePropertyType(property.PropertyType);
        });
    }

    private bool TryGetCommonSelectedPropertyValue(string propertyName, out object? commonValue)
    {
        commonValue = null;
        var hasValue = false;

        // Schema 驱动路径：通过 Schema Storage 读取值，不通过反射。
        if (_schemaPropertiesByPath.TryGetValue(propertyName, out var schemaProperty))
        {
            foreach (var item in SelectedDesignItems)
            {
                var value = schemaProperty.GetValue(item.Config);
                if (!hasValue)
                {
                    commonValue = value;
                    hasValue = true;
                    continue;
                }

                if (!ValuesEqual(commonValue, value))
                {
                    commonValue = null;
                    return false;
                }
            }

            return hasValue;
        }

        // 反射回退（无 Schema 的边缘情况）。
        foreach (var item in SelectedDesignItems)
        {
            var property = item.Config.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            if (property is null || !property.CanRead)
            {
                return false;
            }

            var value = property.GetValue(item.Config);
            if (!hasValue)
            {
                commonValue = value;
                hasValue = true;
                continue;
            }

            if (!ValuesEqual(commonValue, value))
            {
                commonValue = null;
                return false;
            }
        }

        return hasValue;
    }

    private static void ClearMultiSelectionPropertyRow(FrontedPropertyEditorItem row, bool makeReadOnly)
    {
        row.Value = null;
        row.DisplayValue = string.Empty;
        row.EditText = string.Empty;
        if (makeReadOnly)
        {
            row.EditorKind = FrontedPropertyEditorKind.ReadOnly;
            row.IsReadOnly = true;
            row.CanBrowseBinding = false;
            row.CanBrowseResource = false;
        }
    }

    private static bool IsMultiSelectionIsolatedProperty(string propertyName)
    {
        return propertyName.Contains("Binding", StringComparison.OrdinalIgnoreCase)
               || propertyName.Contains("Behavior", StringComparison.OrdinalIgnoreCase)
               || propertyName.Contains("Trigger", StringComparison.OrdinalIgnoreCase)
               || propertyName.Contains("Filter", StringComparison.OrdinalIgnoreCase)
               || propertyName.Contains("Guid", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBatchEditablePropertyType(Type propertyType)
    {
        var type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        return type.IsPrimitive
               || type.IsEnum
               || type == typeof(string)
               || type == typeof(decimal)
               || type == typeof(DateTime)
               || type == typeof(TimeSpan);
    }

    private static bool IsEmptyMultiSelectionPlaceholderValue(object? value)
    {
        if (value is null)
        {
            return true;
        }

        return value is string text && string.IsNullOrEmpty(text);
    }

    private static string GetPropertyEditorDisplayValue(object? value) =>
        Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

    private void SetPropertyEditError(FrontedPropertyEditorItem item, string message, object? attemptedValue)
    {
        var attemptedText = Convert.ToString(attemptedValue, CultureInfo.InvariantCulture) ?? string.Empty;
        _propertyEditErrors[item.PropertyName] = message;
        _propertyEditBuffers[item.PropertyName] = attemptedText;
        item.EditText = attemptedText;
        item.SetEditError(message);
        item.ValidationErrors = item.ValidationErrors
            .Concat([message])
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        item.ValidationMessages = item.ValidationMessages
            .Concat([CreatePropertyEditValidationMessage(message, item.PropertyName)])
            .ToArray();
        StatusMessage = message;
    }

    private FrontedLayoutValidationMessage CreatePropertyEditValidationMessage(string message, string propertyName) =>
        new()
        {
            Severity = FrontedLayoutValidationSeverity.Error,
            Code = "PropertyEditError",
            Message = message,
            ControlName = SelectedDesignItem?.Name,
            PropertyName = propertyName
        };

    private void ClearPropertyEditError(string propertyName)
    {
        _propertyEditErrors.Remove(propertyName);
        _propertyEditBuffers.Remove(propertyName);
    }

    private void FinishPropertyEdit(string propertyName, bool refreshPropertyGrid = true)
    {
        ClearPropertyEditError(propertyName);
        RefreshDirtyState();
        RebuildFilteredDesignItems();
        ValidateCurrentDocument(refreshPropertyGrid);
        RequestPreviewRenderCurrentDocument();
        DeleteSelectedControlCommand.NotifyCanExecuteChanged();
    }

    private void FinishCanvasConfigEdit(string statusMessage)
    {
        CanvasPropertiesStatus = statusMessage;
        RefreshDirtyState();
        ValidateCurrentDocument();
        RequestPreviewRenderCurrentDocument();
        UpdateFitZoomFromCurrentDocument();
    }

    private void RefreshCanvasPropertyBuffers()
    {
        if (CurrentDocument is null)
        {
            CanvasWidthEditText = string.Empty;
            CanvasHeightEditText = string.Empty;
            BackgroundImageEditText = string.Empty;
            SetBoModeStateUi(enableBoModeStates: false, FrontedCanvasBoModeState.Bo5);
            return;
        }

        CanvasWidthEditText = CurrentDocument.CanvasConfig.CanvasWidth.ToString("0.##", CultureInfo.InvariantCulture);
        CanvasHeightEditText = CurrentDocument.CanvasConfig.CanvasHeight.ToString("0.##", CultureInfo.InvariantCulture);
        BackgroundImageEditText = GetEditingStateBackground(CurrentDocument) ?? string.Empty;
        _designerPreviewSharedDataService.IsBo3Mode = CurrentDocument.EditingBoModeState == FrontedCanvasBoModeState.Bo3;
        SetBoModeStateUi(CurrentDocument.CanvasConfig.EnableBoModeStates, CurrentDocument.EditingBoModeState);
    }

    private void SetBoModeStateUi(bool enableBoModeStates, FrontedCanvasBoModeState editingState)
    {
        _isUpdatingBoModeStateUi = true;
        try
        {
            EnableBoModeStates = enableBoModeStates;
            SelectedBoModeStateOption = BoModeStateOptions.FirstOrDefault(option => option.State == editingState)
                                        ?? BoModeStateOptions.FirstOrDefault();
        }
        finally
        {
            _isUpdatingBoModeStateUi = false;
        }

        OnPropertyChanged(nameof(IsBoModeStateSelectorVisible));
        OnPropertyChanged(nameof(CanCopyBo5ToBo3));
        CopyBo5ToBo3Command.NotifyCanExecuteChanged();
    }

    private static string? GetEditingStateBackground(FrontedCanvasDesignDocument document)
    {
        if (document.EditingBoModeState == FrontedCanvasBoModeState.Bo3
            && document.CanvasConfig.BoModeStates.TryGetValue(
                FrontedCanvasRuntimeStateResolver.Bo3StateKey,
                out var bo3State))
        {
            return bo3State.BackgroundImage;
        }

        return document.CanvasConfig.BackgroundImage;
    }

    private static void SetEditingStateBackground(FrontedCanvasDesignDocument document, string? value)
    {
        if (document.EditingBoModeState == FrontedCanvasBoModeState.Bo3)
        {
            EnsureBo3State(document.CanvasConfig).BackgroundImage = value;
            return;
        }

        document.CanvasConfig.BackgroundImage = value;
    }

    private static FrontedCanvasStateConfig EnsureBo3State(FrontedCanvasConfig config)
    {
        if (!config.BoModeStates.TryGetValue(FrontedCanvasRuntimeStateResolver.Bo3StateKey, out var state))
        {
            state = new FrontedCanvasStateConfig();
            config.BoModeStates[FrontedCanvasRuntimeStateResolver.Bo3StateKey] = state;
        }

        return state;
    }
}
