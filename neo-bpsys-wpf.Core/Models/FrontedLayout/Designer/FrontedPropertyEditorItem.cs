using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using System.Windows.Media;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

/// <summary>
/// Single editable row in Designer v3 property grid.
/// </summary>
public class FrontedPropertyEditorItem : ObservableObject
{
    private string _displayName = string.Empty;
    private string _propertyName = string.Empty;
    private string? _description;
    private Type _propertyType = typeof(string);
    private FrontedPropertyEditorKind _editorKind;
    private object? _value;
    private Color _colorValue = FrontedPropertyColorHelper.FallbackColor;
    private bool _isReadOnly;
    private bool _isRequired;
    private IReadOnlyList<string> _validationErrors = [];
    private IReadOnlyList<object>? _options;
    private string? _groupName;
    private string? _groupDisplayName;
    private bool _isGroupHeaderVisible;

    /// <summary>
    /// User-facing row label.
    /// </summary>
    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    /// <summary>
    /// Underlying design item or config property name.
    /// </summary>
    public string PropertyName
    {
        get => _propertyName;
        set => SetProperty(ref _propertyName, value);
    }

    /// <summary>
    /// Optional help text for the property.
    /// </summary>
    public string? Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    /// <summary>
    /// Underlying CLR property type.
    /// </summary>
    public Type PropertyType
    {
        get => _propertyType;
        set => SetProperty(ref _propertyType, value);
    }

    /// <summary>
    /// Editor kind selected for the property.
    /// </summary>
    public FrontedPropertyEditorKind EditorKind
    {
        get => _editorKind;
        set => SetProperty(ref _editorKind, value);
    }

    /// <summary>
    /// Current property value.
    /// </summary>
    public object? Value
    {
        get => _value;
        set
        {
            if (!SetProperty(ref _value, value))
            {
                return;
            }

            if (EditorKind == FrontedPropertyEditorKind.Color)
            {
                SetProperty(
                    ref _colorValue,
                    FrontedPropertyColorHelper.TryParseArgbColor(value as string, out var color)
                        ? color
                        : FrontedPropertyColorHelper.FallbackColor,
                    nameof(ColorValue));
            }
        }
    }

    /// <summary>
    /// ColorPicker-friendly value for color string rows.
    /// </summary>
    public Color ColorValue
    {
        get => _colorValue;
        set
        {
            if (!SetProperty(ref _colorValue, value))
            {
                return;
            }

            Value = FrontedPropertyColorHelper.ToArgbString(value);
        }
    }

    /// <summary>
    /// Whether the row is read-only.
    /// </summary>
    public bool IsReadOnly
    {
        get => _isReadOnly;
        set => SetProperty(ref _isReadOnly, value);
    }

    /// <summary>
    /// Whether the property is required.
    /// </summary>
    public bool IsRequired
    {
        get => _isRequired;
        set => SetProperty(ref _isRequired, value);
    }

    /// <summary>
    /// Validation messages attached to this property row.
    /// </summary>
    public IReadOnlyList<string> ValidationErrors
    {
        get => _validationErrors;
        set => SetProperty(ref _validationErrors, value);
    }

    /// <summary>
    /// Available values for enum-like editors.
    /// </summary>
    public IReadOnlyList<object>? Options
    {
        get => _options;
        set => SetProperty(ref _options, value);
    }

    /// <summary>
    /// Logical group name.
    /// </summary>
    public string? GroupName
    {
        get => _groupName;
        set => SetProperty(ref _groupName, value);
    }

    /// <summary>
    /// Localized group label for display.
    /// </summary>
    public string? GroupDisplayName
    {
        get => _groupDisplayName;
        set => SetProperty(ref _groupDisplayName, value);
    }

    /// <summary>
    /// Whether this row should display its group header.
    /// </summary>
    public bool IsGroupHeaderVisible
    {
        get => _isGroupHeaderVisible;
        set => SetProperty(ref _isGroupHeaderVisible, value);
    }
}
