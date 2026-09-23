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
/// 前台属性网格构建器的Schema逻辑。
/// </summary>
public sealed partial class FrontedPropertyGridBuilder
{
    private void AddSchemaConfigRows(
        List<FrontedPropertyEditorItem> rows,
        FrontedControlDesignItem selectedItem,
        IReadOnlyList<FrontedLayoutValidationMessage> messages,
        IReadOnlyList<FrontedV3PropertyDefinition> properties,
        IDictionary<string, FrontedV3PropertyDefinition> schemaByPath)
    {
        foreach (var property in properties)
        {
            if (!property.Metadata.IsVisible)
            {
                continue;
            }

            var row = CreateSchemaPropertyRow(selectedItem, messages, property);
            if (row is null)
            {
                continue;
            }

            schemaByPath[property.OptionsPath] = property;
            rows.Add(row);
        }
    }

    private FrontedPropertyEditorItem? CreateSchemaPropertyRow(
        FrontedControlDesignItem selectedItem,
        IReadOnlyList<FrontedLayoutValidationMessage> messages,
        FrontedV3PropertyDefinition property)
    {
        var config = selectedItem.Config;
        var propertyName = property.Metadata.DisplayNameKey ?? property.OptionsPath;

        // 类型相关动态可见性（如 Shape 渐变关闭时隐藏渐变属性）。
        if (!IsVisibleProperty(config, propertyName))
        {
            return null;
        }

        var kind = property.Metadata.EditorKind ?? ResolveEditorKindFromType(property.PropertyType, propertyName);
        var isReadOnly = !selectedItem.IsEditableInEditor
                         || property.Metadata.IsReadOnly
                         || IsEffectDetailDisabled(propertyName, config);
        var isEditingDisabled = IsOverlayStretchEditingDisabled(propertyName, config);
        var groupName = property.Metadata.GroupName;
        var validationMessages = GetPropertyValidationMessages(messages, selectedItem.Name, propertyName).ToList();
        var validationErrors = validationMessages.Select(message => message.Message).ToList();
        var value = property.GetValue(config);

        if (kind == FrontedPropertyEditorKind.Color
            && value is string color
            && !string.IsNullOrWhiteSpace(color)
            && !IsColorOverriddenByBinding(config, propertyName)
            && !FrontedPropertyColorHelper.TryParseArgbColor(color, out _))
        {
            var message = _localizationService.GetDesignerText(
                "Designer.Validation.InvalidArgbColor",
                "Invalid color. Use #RRGGBB, #AARRGGBB, or a WPF color name.");
            validationErrors.Add(message);
            validationMessages.Add(CreatePropertyError(message, selectedItem.Name, propertyName));
        }

        var canBrowseBinding = !isReadOnly && IsBindingPathProperty(propertyName);
        var canBrowseResource = !isReadOnly
                                && !canBrowseBinding
                                && IsResourcePathProperty(propertyName);
        var bindingTargetKind = canBrowseBinding
            ? ResolveBindingTargetKindByName(config, propertyName)
            : FrontedBindingTargetKind.Any;
        var requiresExplicitCommit = RequiresExplicitCommit(propertyName, kind, canBrowseBinding, canBrowseResource);

        return new FrontedPropertyEditorItem
        {
            DisplayName = _localizationService.GetPropertyDisplayName(propertyName),
            PropertyName = property.OptionsPath,
            Description = NullIfEmpty(_localizationService.GetPropertyDescription(propertyName)) ?? string.Empty,
            PropertyType = property.PropertyType,
            EditorKind = isReadOnly && !IsEffectDetailProperty(propertyName)
                ? FrontedPropertyEditorKind.ReadOnly
                : kind,
            Value = value,
            DisplayValue = GetDisplayValue(value, isReadOnly),
            EditText = GetEditTextValue(value, kind),
            IsReadOnly = isReadOnly,
            IsEditingDisabled = isEditingDisabled,
            IsRequired = propertyName is nameof(FrontedControlConfigBase.Left)
                or nameof(FrontedControlConfigBase.Top),
            Options = ResolveSchemaOptions(property, kind, propertyName),
            GroupName = groupName,
            SectionName = ResolveOverlaySectionName(propertyName, config),
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
        };
    }

    private static FrontedPropertyEditorKind ResolveEditorKindFromType(Type propertyType, string propertyName)
    {
        var type = GetCoreType(propertyType);

        if (type == typeof(FrontedTextBindingExpression))
        {
            return FrontedPropertyEditorKind.TextBinding;
        }

        if (propertyType == typeof(string) && IsColorProperty(propertyName))
        {
            return FrontedPropertyEditorKind.Color;
        }

        if (propertyType == typeof(string) && IsFontFamilyProperty(propertyName))
        {
            return FrontedPropertyEditorKind.FontFamily;
        }

        if (propertyType == typeof(string) && TryGetStringOptions(propertyName, out _))
        {
            return FrontedPropertyEditorKind.Enum;
        }

        if (propertyName is nameof(ImageFrontedControlConfig.Lockable)
            or nameof(ImageFrontedControlConfig.PickingBorderAvailable)
            or nameof(ImageFrontedControlConfig.UseIndependentLockStretch)
            or nameof(ImageFrontedControlConfig.UseIndependentPickingBorderStretch))
        {
            return FrontedPropertyEditorKind.ToggleSwitch;
        }

        if (propertyName == nameof(FrontedControlConfigBase.IsGaussianBlurEnabled))
        {
            return FrontedPropertyEditorKind.ToggleSwitch;
        }

        if (propertyName == nameof(FrontedControlConfigBase.IsShadowEnabled))
        {
            return FrontedPropertyEditorKind.ToggleSwitch;
        }

        if (propertyName == nameof(FrontedControlConfigBase.IsGlowEnabled))
        {
            return FrontedPropertyEditorKind.ToggleSwitch;
        }

        if (type == typeof(bool))
        {
            return FrontedPropertyEditorKind.Boolean;
        }

        if (type.IsEnum)
        {
            return FrontedPropertyEditorKind.Enum;
        }

        if (IsNumericType(type))
        {
            return FrontedPropertyEditorKind.Number;
        }

        return type == typeof(string)
            ? FrontedPropertyEditorKind.Text
            : FrontedPropertyEditorKind.ReadOnly;
    }

    private IReadOnlyList<object>? ResolveSchemaOptions(
        FrontedV3PropertyDefinition property,
        FrontedPropertyEditorKind kind,
        string propertyName)
    {
        // 显式声明的 Options 优先（插件控件常用）。
        if (property.Metadata.Options is { } metadataOptions)
        {
            return metadataOptions.Cast<object>().ToArray();
        }

        if (kind == FrontedPropertyEditorKind.FontFamily)
        {
            return GetFontFamilyOptions();
        }

        if (kind == FrontedPropertyEditorKind.Boolean)
        {
            return [CreateBooleanOption(true), CreateBooleanOption(false)];
        }

        if (kind != FrontedPropertyEditorKind.Enum)
        {
            return null;
        }

        // 字符串枚举（HorizontalAlignment 等）：按属性名查找预定义选项。
        if (property.PropertyType == typeof(string)
            && TryGetStringOptions(propertyName, out var stringOptions))
        {
            return stringOptions
                .Select(value => CreateOption(GetOptionPropertyName(propertyName), value))
                .Cast<object>()
                .ToArray();
        }

        var enumType = GetCoreType(property.PropertyType);
        if (!enumType.IsEnum)
        {
            return null;
        }

        var values = Enum.GetValues(enumType).Cast<object>();

        if (property.PropertyType == typeof(GameProgressTextDisplayMode))
        {
            values = GameProgressDisplayModeOptions.Cast<object>();
        }

        // DisplayLanguage 使用共用的 LanguageKey 枚举，但排除全局的 System 值。
        if (propertyName == nameof(GameProgressTextControlConfig.DisplayLanguage))
        {
            values = values.Where(v => v is not LanguageKey.System);
        }

        return values
            .Select(value => CreateOption(propertyName, value))
            .Cast<object>()
            .ToArray();
    }

    private static FrontedBindingTargetKind ResolveBindingTargetKindByName(
        FrontedControlConfigBase config,
        string propertyName)
    {
        if (!IsBindingPathProperty(propertyName))
        {
            return FrontedBindingTargetKind.Any;
        }

        if (propertyName == nameof(ImageFrontedControlConfig.LockVisibilityBindingPath))
        {
            return FrontedBindingTargetKind.Boolean;
        }

        if (propertyName.EndsWith("ColorBindingPath", StringComparison.Ordinal))
        {
            return FrontedBindingTargetKind.String;
        }

        if (config is ShapeFrontedControlConfigBase)
        {
            return FrontedBindingTargetKind.String;
        }

        if (config is BackgroundTintFrontedControlConfigBase)
        {
            return FrontedBindingTargetKind.String;
        }

        return config switch
        {
            TextFrontedControlConfig => FrontedBindingTargetKind.Text,
            LocalizedTextControlConfig => FrontedBindingTargetKind.Text,
            ImageFrontedControlConfig => FrontedBindingTargetKind.Image,
            GameProgressTextControlConfig => FrontedBindingTargetKind.GameProgress,
            MapNameTextControlConfig => FrontedBindingTargetKind.Map,
            _ => FrontedBindingTargetKind.Any
        };
    }

}
