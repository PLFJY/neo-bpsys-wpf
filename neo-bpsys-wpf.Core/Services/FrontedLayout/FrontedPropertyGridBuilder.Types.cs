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
/// 前台属性网格构建器的Types逻辑。
/// </summary>
public sealed partial class FrontedPropertyGridBuilder
{

    private IReadOnlyList<string> GetPropertyMessages(
        IEnumerable<FrontedLayoutValidationMessage> messages,
        string controlName,
        string propertyName)
    {
        return GetPropertyValidationMessages(messages, controlName, propertyName)
            .Select(message => message.Message)
            .ToArray();
    }

    private IReadOnlyList<FrontedLayoutValidationMessage> GetPropertyValidationMessages(
        IEnumerable<FrontedLayoutValidationMessage> messages,
        string controlName,
        string propertyName)
    {
        return messages
            .Where(message => message.ControlName == controlName && message.PropertyName == propertyName)
            .Select(LocalizePropertyValidationMessage)
            .ToArray();
    }

    private FrontedLayoutValidationMessage LocalizePropertyValidationMessage(FrontedLayoutValidationMessage message)
    {
        var localizedMessage = (message.Code, message.PropertyName) switch
        {
            ("StaticTextIgnored", nameof(TextFrontedControlConfig.Text)) =>
                _localizationService.GetDesignerText(
                    "Designer.Validation.TextStaticIgnored",
                    "Static Text is ignored while TextBinding has active sources."),
            ("StaticTextIgnored", nameof(LocalizedTextControlConfig.LocalizationKey)) =>
                _localizationService.GetDesignerText(
                    "Designer.Validation.LocalizationKeyIgnored",
                    "LocalizationKey is ignored while TextBinding has active sources."),
            ("ImagePathIgnored", nameof(ImageFrontedControlConfig.ImagePath)) =>
                _localizationService.GetDesignerText(
                    "Designer.Validation.ImagePathIgnored",
                    "ImagePath is ignored while BindingPath is set."),
            ("FillColorIgnored", nameof(ShapeFrontedControlConfigBase.FillColor)) =>
                _localizationService.GetDesignerText(
                    "Designer.Validation.FillColorIgnored",
                    "Static fill color is ignored while binding is active. The bound color value is used instead."),
            ("GradientEndColorIgnored", nameof(ShapeFrontedControlConfigBase.GradientEndColor)) =>
                _localizationService.GetDesignerText(
                    "Designer.Validation.GradientEndColorIgnored",
                    "Static gradient end color is ignored while binding is active. The bound color value is used instead."),
            ("TextColorIgnored", nameof(TextFrontedControlConfig.Color))
                or ("TextColorIgnored", nameof(LocalizedTextControlConfig.Color))
                or ("TextColorIgnored", nameof(GameProgressTextControlConfig.Color))
                or ("TextColorIgnored", nameof(MapNameTextControlConfig.Color)) =>
                _localizationService.GetDesignerText(
                    "Designer.Validation.TextColorIgnored",
                    "Static text color is ignored while binding is active. The bound color value is used instead."),
            _ => message.Message
        };

        return new FrontedLayoutValidationMessage
        {
            Severity = message.Severity,
            Code = message.Code,
            Message = localizedMessage,
            ControlName = message.ControlName,
            PropertyName = message.PropertyName
        };
    }

    private static FrontedLayoutValidationMessage CreatePropertyError(
        string message,
        string controlName,
        string propertyName) =>
        new()
        {
            Severity = FrontedLayoutValidationSeverity.Error,
            Code = "PropertyValidationError",
            Message = message,
            ControlName = controlName,
            PropertyName = propertyName
        };

    private void MarkGroupHeaders(IReadOnlyList<FrontedPropertyEditorItem> rows)
    {
        string? currentGroup = null;
        string? currentSection = null;
        foreach (var row in rows)
        {
            var isGroupChanged = row.GroupName != currentGroup;
            row.IsGroupHeaderVisible = isGroupChanged;
            row.GroupDisplayName = string.IsNullOrWhiteSpace(row.GroupName)
                ? row.GroupName
                : _localizationService.GetGroupDisplayName(row.GroupName);
            row.IsSectionHeaderVisible = !string.IsNullOrWhiteSpace(row.SectionName)
                && (isGroupChanged || row.SectionName != currentSection);
            row.SectionDisplayName = string.IsNullOrWhiteSpace(row.SectionName)
                ? row.SectionName
                : _localizationService.GetDesignerText(
                    $"Designer.PropertySection.{row.SectionName}",
                    row.SectionName);
            currentGroup = row.GroupName;
            currentSection = row.SectionName;
        }
    }

    private FrontedPropertyEditorOption CreateOption(string propertyName, object? value) =>
        new()
        {
            Value = value,
            DisplayName = _localizationService.GetOptionDisplayName(propertyName, value)
        };

    private FrontedPropertyEditorOption CreateBooleanOption(bool value) =>
        new()
        {
            Value = value,
            DisplayName = _localizationService.GetDesignerText(
                value ? "Designer.Value.True" : "Designer.Value.False",
                value ? "true" : "false")
        };

    private string GetDisplayValue(object? value, bool isReadOnly)
    {
        if (value is FrontedTextBindingExpression expression)
        {
            var sources = expression.GetActiveSources();
            return sources.Count == 0
                ? _localizationService.GetDesignerText("Designer.TextBinding.None", "No binding sources")
                : string.Format(
                    System.Globalization.CultureInfo.CurrentCulture,
                    _localizationService.GetDesignerText(
                        "Designer.TextBinding.SourceSummary",
                        "{0} source(s)"),
                    sources.Count,
                    string.Join(", ", sources.Select(s => s.DisplayName ?? s.Path)));
        }

        if (isReadOnly && value is bool boolValue)
        {
            return _localizationService.GetDesignerText(
                boolValue ? "Designer.Value.True" : "Designer.Value.False",
                boolValue ? "True" : "False");
        }

        return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private string GetEditTextValue(object? value, FrontedPropertyEditorKind kind)
    {
        if (kind == FrontedPropertyEditorKind.FontFamily)
        {
            return _fontFamilyOptionProvider.GetDisplayName(value as string);
        }

        return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static Type GetCoreType(Type type)
    {
        return Nullable.GetUnderlyingType(type) ?? type;
    }

    private static bool IsNumericType(Type type)
    {
        return type == typeof(byte)
               || type == typeof(short)
               || type == typeof(int)
               || type == typeof(long)
               || type == typeof(float)
               || type == typeof(double)
               || type == typeof(decimal);
    }
}
