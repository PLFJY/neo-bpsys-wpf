using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Binding;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties;
using System.Collections;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 前台属性网格构建器的Common逻辑。
/// </summary>
public sealed partial class FrontedPropertyGridBuilder
{
    /// <summary>
    /// 获取字体编辑器的当前字体系列选项。
    /// </summary>
    /// <returns>字体系列选项。</returns>
    public IReadOnlyList<object> GetFontFamilyOptions()
    {
        return _fontFamilyOptionProvider.GetFontFamilyOptions().Cast<object>().ToArray();
    }

    /// <summary>
    /// 清除缓存的字体系列选项。
    /// </summary>
    public void ClearFontFamilyOptionCache()
    {
        _fontFamilyOptionProvider.ClearCache();
    }

    private void AddIdentityRows(
        ICollection<FrontedPropertyEditorItem> rows,
        FrontedControlDesignItem selectedItem,
        IReadOnlyList<FrontedLayoutValidationMessage> messages)
    {
        var nameReadOnly = !selectedItem.IsSelectableInEditor
                           || !selectedItem.IsEditableInEditor;

        rows.Add(new FrontedPropertyEditorItem
        {
            DisplayName = _localizationService.GetPropertyDisplayName(nameof(FrontedControlDesignItem.Name)),
            PropertyName = nameof(FrontedControlDesignItem.Name),
            Description = _localizationService.GetPropertyDescription(nameof(FrontedControlDesignItem.Name)),
            PropertyType = typeof(string),
            EditorKind = nameReadOnly ? FrontedPropertyEditorKind.ReadOnly : FrontedPropertyEditorKind.Text,
            Value = selectedItem.Name,
            DisplayValue = selectedItem.Name,
            EditText = selectedItem.Name,
            IsReadOnly = nameReadOnly,
            IsRequired = true,
            RequiresExplicitCommit = true,
            GroupName = "Identity",
            ValidationErrors = GetPropertyMessages(messages, selectedItem.Name, nameof(FrontedControlDesignItem.Name)),
            ValidationMessages = GetPropertyValidationMessages(messages, selectedItem.Name, nameof(FrontedControlDesignItem.Name))
        });

        rows.Add(new FrontedPropertyEditorItem
        {
            DisplayName = _localizationService.GetPropertyDisplayName(nameof(FrontedControlConfigBase.ControlType)),
            PropertyName = nameof(FrontedControlConfigBase.ControlType),
            PropertyType = typeof(string),
            EditorKind = FrontedPropertyEditorKind.ReadOnly,
            Value = selectedItem.Config.ControlType,
            DisplayValue = _localizationService.GetControlTypeDisplayName(selectedItem.Config.ControlType),
            IsReadOnly = true,
            IsRequired = true,
            GroupName = "Identity",
            ValidationErrors = GetPropertyMessages(messages, selectedItem.Name, nameof(FrontedControlConfigBase.ControlType)),
            ValidationMessages = GetPropertyValidationMessages(messages, selectedItem.Name, nameof(FrontedControlConfigBase.ControlType))
        });

    }

    private void AddConfigRows(
        ICollection<FrontedPropertyEditorItem> rows,
        FrontedControlDesignItem selectedItem,
        IReadOnlyList<FrontedLayoutValidationMessage> messages)
    {
        var registration = _v3ControlRegistry?.GetRegistration(selectedItem.Config.ControlType);
        if (selectedItem.Config is PluginFrontedControlConfig missingPlugin && registration is null)
        {
            AddMissingPluginRows(rows, selectedItem, missingPlugin, messages);
            return;
        }

        var properties = selectedItem.Config.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(IsSupportedProperty)
            .OrderBy(property => GetGroupOrder(ResolveGroupName(property.Name, selectedItem.Config)))
            .ThenBy(GetPropertyOrder);

        foreach (var property in properties)
        {
            if (property.Name is nameof(FrontedControlConfigBase.ControlType)
                or nameof(FrontedControlConfigBase.BehaviorGuid))
            {
                continue;
            }

            if (!IsVisibleProperty(selectedItem.Config, property.Name))
            {
                continue;
            }

            var kind = ResolveEditorKind(property);
            var isReadOnly = !selectedItem.IsEditableInEditor
                             || !property.CanWrite
                             || IsEffectDetailDisabled(property.Name, selectedItem.Config);
            var isEditingDisabled = IsOverlayStretchEditingDisabled(property.Name, selectedItem.Config);
            var groupName = ResolveGroupName(property.Name, selectedItem.Config);
            var validationMessages = GetPropertyValidationMessages(messages, selectedItem.Name, property.Name).ToList();
            var validationErrors = validationMessages.Select(message => message.Message).ToList();
            var value = property.GetValue(selectedItem.Config);

            if (kind == FrontedPropertyEditorKind.Color
                && value is string color
                && !string.IsNullOrWhiteSpace(color)
                && !IsColorOverriddenByBinding(selectedItem.Config, property.Name)
                && !FrontedPropertyColorHelper.TryParseArgbColor(color, out _))
            {
                var message = _localizationService.GetDesignerText(
                    "Designer.Validation.InvalidArgbColor",
                    "Invalid color. Use #RRGGBB, #AARRGGBB, or a WPF color name.");
                validationErrors.Add(message);
                validationMessages.Add(CreatePropertyError(message, selectedItem.Name, property.Name));
            }

            var canBrowseBinding = !isReadOnly && IsBindingPathProperty(property.Name);
            var canBrowseResource = !isReadOnly
                                    && !canBrowseBinding
                                    && IsResourcePathProperty(property.Name);
            var bindingTargetKind = canBrowseBinding
                ? ResolveBindingTargetKind(selectedItem.Config, property)
                : FrontedBindingTargetKind.Any;
            var requiresExplicitCommit = RequiresExplicitCommit(property.Name, kind, canBrowseBinding, canBrowseResource);

            rows.Add(new FrontedPropertyEditorItem
            {
                DisplayName = _localizationService.GetPropertyDisplayName(property.Name),
                PropertyName = property.Name,
                Description = NullIfEmpty(_localizationService.GetPropertyDescription(property.Name)),
                PropertyType = property.PropertyType,
                EditorKind = isReadOnly && !IsEffectDetailProperty(property.Name)
                    ? FrontedPropertyEditorKind.ReadOnly
                    : kind,
                Value = value,
                DisplayValue = GetDisplayValue(value, isReadOnly),
                EditText = GetEditTextValue(value, kind),
                IsReadOnly = isReadOnly,
                IsEditingDisabled = isEditingDisabled,
                IsRequired = property.Name is nameof(FrontedControlConfigBase.Left)
                    or nameof(FrontedControlConfigBase.Top),
                Options = ResolveOptions(property, kind),
                GroupName = groupName,
                SectionName = ResolveOverlaySectionName(property.Name, selectedItem.Config),
                ValidationErrors = validationErrors,
                ValidationMessages = validationMessages,
                CanBrowseBinding = canBrowseBinding,
                CanBrowseResource = canBrowseResource,
                RequiresExplicitCommit = requiresExplicitCommit,
                BrowseButtonText = "...",
                BrowseDialogTitle = canBrowseBinding
                    ? _localizationService.GetDesignerText("Designer.Editor.BindingBrowser", "Binding Browser")
                    : canBrowseResource
                        ? _localizationService.GetDesignerText("Designer.Editor.ResourceBrowser", "Resource Browser")
                        : null,
                BindingTargetKind = bindingTargetKind,
                ExpectedBindingTypeName = _localizationService.GetBindingTypeDisplayName(ResolveBindingTargetTypeName(bindingTargetKind)),
                AllowedBindingTypeNames = ResolveAllowedBindingTypeNames(bindingTargetKind)
            });
        }
    }

    private void AddMissingPluginRows(
        ICollection<FrontedPropertyEditorItem> rows,
        FrontedControlDesignItem selectedItem,
        PluginFrontedControlConfig config,
        IReadOnlyList<FrontedLayoutValidationMessage> messages)
    {
        foreach (var propertyName in new[]
                 {
                     nameof(FrontedControlConfigBase.Left),
                     nameof(FrontedControlConfigBase.Top),
                     nameof(FrontedControlConfigBase.Width),
                     nameof(FrontedControlConfigBase.Height),
                     nameof(FrontedControlConfigBase.ZIndex),
                     nameof(FrontedControlConfigBase.BindingPath)
                 })
        {
            var property = selectedItem.Config.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property is not null && IsSupportedProperty(property))
            {
                AddPropertyRow(rows, selectedItem, messages, property);
            }
        }

        AddReadOnlyInfoRow(rows, "PackageId", config.PackageId, "Plugin");
        AddReadOnlyInfoRow(rows, "ControlTypeName", config.ControlTypeName, "Plugin");
        if (config.ExtensionData.Count > 0)
        {
            AddReadOnlyInfoRow(rows, "PluginExtensionData", string.Join(", ", config.ExtensionData.Keys.OrderBy(key => key, StringComparer.Ordinal)), "Plugin");
        }

        AddReadOnlyInfoRow(
            rows,
            "PluginInstallGuidance",
            _localizationService.GetDesignerText(
                "Designer.PluginInstallGuidance",
                "This plugin is not installed. Install guidance will be available in a future version."),
            "Plugin");

        AddEffectRows(rows, selectedItem, messages, new HashSet<string>(StringComparer.Ordinal));
    }

}
