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
    private string? _displayValue;
    private string? _editText;
    private Color _colorValue = FrontedPropertyColorHelper.FallbackColor;
    private bool _isReadOnly;
    private bool _isRequired;
    private bool _hasEditError;
    private string? _editError;
    private IReadOnlyList<string> _validationErrors = [];
    private IReadOnlyList<object>? _options;
    private string? _groupName;
    private string? _groupDisplayName;
    private bool _isGroupHeaderVisible;
    private bool _canBrowseBinding;
    private bool _canBrowseResource;
    private string? _browseButtonText;
    private string? _browseDialogTitle;
    private FrontedBindingTargetKind _bindingTargetKind = FrontedBindingTargetKind.Any;
    private string? _expectedBindingTypeName;
    private IReadOnlyList<string> _allowedBindingTypeNames = [];

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
    /// Optional user-facing display text for read-only values.
    /// </summary>
    public string? DisplayValue
    {
        get => _displayValue;
        set => SetProperty(ref _displayValue, value);
    }

    /// <summary>
    /// User edit buffer for explicit-commit text-like rows.
    /// </summary>
    public string? EditText
    {
        get => _editText;
        set
        {
            if (!SetProperty(ref _editText, value))
            {
                return;
            }

            ClearEditError();
            if (EditorKind == FrontedPropertyEditorKind.Color
                && FrontedPropertyColorHelper.TryParseArgbColor(value, out var color))
            {
                SetProperty(ref _colorValue, color, nameof(ColorValue));
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

            var colorText = FrontedPropertyColorHelper.ToArgbString(value);
            Value = colorText;
            EditText = colorText;
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
    /// Whether the latest explicit text commit failed validation.
    /// </summary>
    public bool HasEditError
    {
        get => _hasEditError;
        set => SetProperty(ref _hasEditError, value);
    }

    /// <summary>
    /// Validation message for the latest failed explicit text commit.
    /// </summary>
    public string? EditError
    {
        get => _editError;
        set => SetProperty(ref _editError, value);
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

    /// <summary>
    /// Whether this text-like row can open the Binding Browser.
    /// </summary>
    public bool CanBrowseBinding
    {
        get => _canBrowseBinding;
        set => SetProperty(ref _canBrowseBinding, value);
    }

    /// <summary>
    /// Whether this text-like row can open the Resource Browser.
    /// </summary>
    public bool CanBrowseResource
    {
        get => _canBrowseResource;
        set => SetProperty(ref _canBrowseResource, value);
    }

    /// <summary>
    /// Optional short browse button text.
    /// </summary>
    public string? BrowseButtonText
    {
        get => _browseButtonText;
        set => SetProperty(ref _browseButtonText, value);
    }

    /// <summary>
    /// Optional browse dialog title key.
    /// </summary>
    public string? BrowseDialogTitle
    {
        get => _browseDialogTitle;
        set => SetProperty(ref _browseDialogTitle, value);
    }

    /// <summary>
    /// Expected binding target kind used by Binding Browser filtering.
    /// </summary>
    public FrontedBindingTargetKind BindingTargetKind
    {
        get => _bindingTargetKind;
        set => SetProperty(ref _bindingTargetKind, value);
    }

    /// <summary>
    /// Short display name for the expected binding type.
    /// </summary>
    public string? ExpectedBindingTypeName
    {
        get => _expectedBindingTypeName;
        set => SetProperty(ref _expectedBindingTypeName, value);
    }

    /// <summary>
    /// Type names accepted by the Binding Browser for this row.
    /// </summary>
    public IReadOnlyList<string> AllowedBindingTypeNames
    {
        get => _allowedBindingTypeNames;
        set => SetProperty(ref _allowedBindingTypeNames, value);
    }

    /// <summary>
    /// Applies a failed edit state without discarding the user's edit buffer.
    /// </summary>
    public void SetEditError(string message)
    {
        HasEditError = true;
        EditError = message;
    }

    private void ClearEditError()
    {
        if (!HasEditError && string.IsNullOrEmpty(EditError))
        {
            return;
        }

        HasEditError = false;
        EditError = null;
    }
}
