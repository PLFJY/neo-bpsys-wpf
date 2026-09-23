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
/// 前台属性网格构建器的Effects逻辑。
/// </summary>
public sealed partial class FrontedPropertyGridBuilder
{
    private void AddEffectRows(
        ICollection<FrontedPropertyEditorItem> rows,
        FrontedControlDesignItem selectedItem,
        IReadOnlyList<FrontedLayoutValidationMessage> messages,
        ISet<string> added)
    {
        foreach (var propertyName in new[]
                 {
                     nameof(FrontedControlConfigBase.IsGaussianBlurEnabled),
                     nameof(FrontedControlConfigBase.GaussianBlurRadius),
                     nameof(FrontedControlConfigBase.IsShadowEnabled),
                     nameof(FrontedControlConfigBase.ShadowColor),
                     nameof(FrontedControlConfigBase.ShadowRadius),
                     nameof(FrontedControlConfigBase.ShadowDepth),
                     nameof(FrontedControlConfigBase.ShadowDirection),
                     nameof(FrontedControlConfigBase.ShadowOpacity),
                     nameof(FrontedControlConfigBase.IsGlowEnabled),
                     nameof(FrontedControlConfigBase.GlowColor),
                     nameof(FrontedControlConfigBase.GlowRadius),
                     nameof(FrontedControlConfigBase.GlowOpacity)
                 })
        {
            if (!added.Add(propertyName))
            {
                continue;
            }

            var property = selectedItem.Config.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property is not null && IsSupportedProperty(property))
            {
                AddPropertyRow(rows, selectedItem, messages, property);
            }
        }
    }

    private void AddReadOnlyInfoRow(
        ICollection<FrontedPropertyEditorItem> rows,
        string propertyName,
        object? value,
        string groupName)
    {
        rows.Add(new FrontedPropertyEditorItem
        {
            DisplayName = _localizationService.GetPropertyDisplayName(propertyName),
            PropertyName = propertyName,
            PropertyType = typeof(string),
            EditorKind = FrontedPropertyEditorKind.ReadOnly,
            Value = value,
            DisplayValue = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            IsReadOnly = true,
            GroupName = groupName
        });
    }

    private void AddPropertyRow(
        ICollection<FrontedPropertyEditorItem> rows,
        FrontedControlDesignItem selectedItem,
        IReadOnlyList<FrontedLayoutValidationMessage> messages,
        PropertyInfo property)
    {
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
            Description = NullIfEmpty(_localizationService.GetPropertyDescription(property.Name)) ?? string.Empty,
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

    private static bool RequiresExplicitCommit(
        string propertyName,
        FrontedPropertyEditorKind editorKind,
        bool canBrowseBinding,
        bool canBrowseResource)
    {
        if (canBrowseBinding
            || canBrowseResource
            || editorKind is FrontedPropertyEditorKind.FontFamily
                or FrontedPropertyEditorKind.TextBinding
                or FrontedPropertyEditorKind.ReadOnly)
        {
            return true;
        }

        return propertyName.EndsWith("Name", StringComparison.Ordinal)
               || propertyName.EndsWith("Id", StringComparison.Ordinal)
               || propertyName.EndsWith("Key", StringComparison.Ordinal)
               || propertyName.Contains("Filter", StringComparison.OrdinalIgnoreCase)
               || propertyName.Contains("Guid", StringComparison.OrdinalIgnoreCase);
    }

    private string ResolveMetadataText(string? key, string fallback) =>
        string.IsNullOrWhiteSpace(key) ? fallback : _localizationService.GetDesignerText(key, fallback);

}
