using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using System.Collections;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Text.RegularExpressions;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Builds Designer v3 property grid rows for the selected design item.
/// </summary>
public class FrontedPropertyGridBuilder
{
    private static readonly HashSet<string> CommonPropertyNames = new(StringComparer.Ordinal)
    {
        nameof(FrontedControlConfigBase.Left),
        nameof(FrontedControlConfigBase.Top),
        nameof(FrontedControlConfigBase.Width),
        nameof(FrontedControlConfigBase.Height),
        nameof(FrontedControlConfigBase.ZIndex),
        nameof(FrontedControlConfigBase.BindingPath)
    };

    private static readonly HashSet<string> ColorPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Color",
        "Foreground",
        "Background",
        "FillColor",
        "BorderColor"
    };

    private static readonly HashSet<string> AppearancePropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Color",
        "Foreground",
        "Background",
        "FillColor",
        "BorderColor",
        "FontFamily",
        "FontWeight",
        "FontSize",
        "HorizontalAlignment",
        "VerticalAlignment",
        "TextAlignment",
        "TextWrapping",
        "Stretch",
        "SizingMode",
        "CornerRadius",
        "ClipToBounds"
    };

    private static readonly Regex ArgbColorRegex = new(
        "^#[0-9A-Fa-f]{8}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Builds property editor rows for the selected design item.
    /// </summary>
    public ObservableCollection<FrontedPropertyEditorItem> Build(
        FrontedCanvasDesignDocument document,
        FrontedControlDesignItem selectedItem,
        FrontedLayoutValidator validator,
        FrontedLayoutReferenceScanner referenceScanner,
        FrontedLayoutRuntimeContractCatalog runtimeContractCatalog)
    {
        var messages = validator.Validate(document);
        referenceScanner.SetControls(document.Controls);
        selectedItem.IsRuntimeCritical = runtimeContractCatalog.IsRuntimeCritical(
            document.WindowTypeName,
            document.CanvasName,
            selectedItem.Name);

        var rows = new List<FrontedPropertyEditorItem>();
        AddIdentityRows(rows, selectedItem, messages);
        AddConfigRows(rows, selectedItem, messages);
        MarkGroupHeaders(rows);
        return new ObservableCollection<FrontedPropertyEditorItem>(rows);
    }

    private static void AddIdentityRows(
        ICollection<FrontedPropertyEditorItem> rows,
        FrontedControlDesignItem selectedItem,
        IReadOnlyList<FrontedLayoutValidationMessage> messages)
    {
        var nameReadOnly = selectedItem.IsRuntimeCritical
                           || !selectedItem.IsSelectableInEditor
                           || !selectedItem.IsEditableInEditor;

        rows.Add(new FrontedPropertyEditorItem
        {
            DisplayName = nameof(FrontedControlDesignItem.Name),
            PropertyName = nameof(FrontedControlDesignItem.Name),
            Description = "Runtime-critical control names are part of the runtime contract and cannot be edited.",
            PropertyType = typeof(string),
            EditorKind = nameReadOnly ? FrontedPropertyEditorKind.ReadOnly : FrontedPropertyEditorKind.Text,
            Value = selectedItem.Name,
            IsReadOnly = nameReadOnly,
            IsRequired = true,
            GroupName = "Identity",
            ValidationErrors = GetPropertyMessages(messages, selectedItem.Name, nameof(FrontedControlDesignItem.Name))
        });

        rows.Add(new FrontedPropertyEditorItem
        {
            DisplayName = nameof(FrontedControlConfigBase.ControlType),
            PropertyName = nameof(FrontedControlConfigBase.ControlType),
            PropertyType = typeof(string),
            EditorKind = FrontedPropertyEditorKind.ReadOnly,
            Value = selectedItem.Config.ControlType,
            IsReadOnly = true,
            IsRequired = true,
            GroupName = "Identity",
            ValidationErrors = GetPropertyMessages(messages, selectedItem.Name, nameof(FrontedControlConfigBase.ControlType))
        });

        rows.Add(new FrontedPropertyEditorItem
        {
            DisplayName = "RuntimeCritical",
            PropertyName = "RuntimeCritical",
            PropertyType = typeof(bool),
            EditorKind = FrontedPropertyEditorKind.ReadOnly,
            Value = selectedItem.IsRuntimeCritical,
            IsReadOnly = true,
            GroupName = "Identity"
        });

        if (!string.IsNullOrWhiteSpace(selectedItem.LinkedTargetControlName))
        {
            rows.Add(new FrontedPropertyEditorItem
            {
                DisplayName = nameof(FrontedControlDesignItem.LinkedTargetControlName),
                PropertyName = nameof(FrontedControlDesignItem.LinkedTargetControlName),
                PropertyType = typeof(string),
                EditorKind = FrontedPropertyEditorKind.ReadOnly,
                Value = selectedItem.LinkedTargetControlName,
                IsReadOnly = true,
                GroupName = "Identity"
            });
        }
    }

    private static void AddConfigRows(
        ICollection<FrontedPropertyEditorItem> rows,
        FrontedControlDesignItem selectedItem,
        IReadOnlyList<FrontedLayoutValidationMessage> messages)
    {
        var properties = selectedItem.Config.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(IsSupportedProperty)
            .OrderBy(GetPropertyOrder);

        foreach (var property in properties)
        {
            if (property.Name == nameof(FrontedControlConfigBase.ControlType))
            {
                continue;
            }

            var kind = ResolveEditorKind(property);
            var isReadOnly = !selectedItem.IsEditableInEditor || !property.CanWrite;
            var groupName = ResolveGroupName(property.Name);
            var validationErrors = GetPropertyMessages(messages, selectedItem.Name, property.Name).ToList();
            var value = property.GetValue(selectedItem.Config);

            if (kind == FrontedPropertyEditorKind.Color
                && value is string color
                && !string.IsNullOrWhiteSpace(color)
                && !ArgbColorRegex.IsMatch(color))
            {
                validationErrors.Add("Invalid color. Use #AARRGGBB.");
            }

            rows.Add(new FrontedPropertyEditorItem
            {
                DisplayName = property.Name,
                PropertyName = property.Name,
                PropertyType = property.PropertyType,
                EditorKind = isReadOnly ? FrontedPropertyEditorKind.ReadOnly : kind,
                Value = value,
                IsReadOnly = isReadOnly,
                IsRequired = property.Name is nameof(FrontedControlConfigBase.Left)
                    or nameof(FrontedControlConfigBase.Top),
                Options = kind == FrontedPropertyEditorKind.Enum
                    ? Enum.GetValues(GetCoreType(property.PropertyType)).Cast<object>().ToArray()
                    : null,
                GroupName = groupName,
                ValidationErrors = validationErrors
            });
        }
    }

    private static bool IsSupportedProperty(PropertyInfo property)
    {
        if (property.GetIndexParameters().Length > 0 || !property.CanRead)
        {
            return false;
        }

        if (!property.CanWrite && property.Name != nameof(FrontedControlConfigBase.ControlType))
        {
            return false;
        }

        var type = GetCoreType(property.PropertyType);
        if (type == typeof(string)
            || type == typeof(bool)
            || type.IsEnum
            || IsNumericType(type))
        {
            return true;
        }

        return typeof(IEnumerable).IsAssignableFrom(type) && type == typeof(string);
    }

    private static FrontedPropertyEditorKind ResolveEditorKind(PropertyInfo property)
    {
        var type = GetCoreType(property.PropertyType);
        if (property.PropertyType == typeof(string) && ColorPropertyNames.Contains(property.Name))
        {
            return FrontedPropertyEditorKind.Color;
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

    private static string ResolveGroupName(string propertyName)
    {
        if (propertyName is nameof(FrontedControlConfigBase.Left)
            or nameof(FrontedControlConfigBase.Top)
            or nameof(FrontedControlConfigBase.Width)
            or nameof(FrontedControlConfigBase.Height)
            or nameof(FrontedControlConfigBase.ZIndex))
        {
            return "Layout";
        }

        if (propertyName == nameof(FrontedControlConfigBase.BindingPath))
        {
            return "Binding";
        }

        return AppearancePropertyNames.Contains(propertyName)
            ? "Appearance"
            : "ControlSpecific";
    }

    private static int GetPropertyOrder(PropertyInfo property)
    {
        if (CommonPropertyNames.Contains(property.Name))
        {
            return property.Name switch
            {
                nameof(FrontedControlConfigBase.Left) => 10,
                nameof(FrontedControlConfigBase.Top) => 11,
                nameof(FrontedControlConfigBase.Width) => 12,
                nameof(FrontedControlConfigBase.Height) => 13,
                nameof(FrontedControlConfigBase.ZIndex) => 14,
                nameof(FrontedControlConfigBase.BindingPath) => 20,
                _ => 30
            };
        }

        return property.DeclaringType == typeof(FrontedControlConfigBase)
            ? 30
            : 100 + property.MetadataToken;
    }

    private static IReadOnlyList<string> GetPropertyMessages(
        IEnumerable<FrontedLayoutValidationMessage> messages,
        string controlName,
        string propertyName)
    {
        return messages
            .Where(message => message.ControlName == controlName && message.PropertyName == propertyName)
            .Select(message => message.Message)
            .ToArray();
    }

    private static void MarkGroupHeaders(IReadOnlyList<FrontedPropertyEditorItem> rows)
    {
        string? currentGroup = null;
        foreach (var row in rows)
        {
            row.IsGroupHeaderVisible = row.GroupName != currentGroup;
            row.GroupDisplayName = row.GroupName;
            currentGroup = row.GroupName;
        }
    }

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
