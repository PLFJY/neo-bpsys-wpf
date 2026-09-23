using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Tutorial;

namespace neo_bpsys_wpf.ViewModels.FrontedDesigner.GraphEditor;

public sealed partial class FrontedNodePropertyEditorViewModel : ObservableValidator
{
    private readonly FrontedNode _node;
    private readonly Action _markDirty;
    private readonly Action _validate;
    private readonly IReadOnlyList<FrontedNodeTargetOptionViewModel> _targetOptions;
    private readonly IReadOnlyList<FrontedGraphConditionFieldOptionViewModel> _conditionFieldOptions;
    private readonly Func<string, string, string> _localize;
    private readonly Func<FrontedNode, string, bool> _hasIncomingConnection;
    private readonly IReadOnlyList<FrontedNodePropertyOptionViewModel> _localizedOptions;
    private readonly IReadOnlyList<FrontedNodePropertyOptionViewModel> _booleanOptions;
    private readonly IReadOnlyList<FrontedNodePropertyOptionViewModel> _visibilityOptions;
    private Action? _refreshRelatedProperties;
    private string? _validationError;
    private Color _colorValue = Colors.White;
    private double? _numberValue;

    public FrontedNodePropertyEditorViewModel(
        FrontedNode node,
        FrontedNodePropertyDescriptor descriptor,
        Action markDirty,
        Action validate,
        Func<string, string, string> localize,
        IReadOnlyList<FrontedNodeTargetOptionViewModel> targetOptions,
        IReadOnlyList<FrontedGraphConditionFieldOptionViewModel>? conditionFieldOptions = null,
        Func<FrontedNode, string, bool>? hasIncomingConnection = null)
    {
        _node = node;
        Descriptor = descriptor;
        _markDirty = markDirty;
        _validate = validate;
        _targetOptions = targetOptions;
        _conditionFieldOptions = conditionFieldOptions ?? [];
        _localize = localize;
        _hasIncomingConnection = hasIncomingConnection ?? ((_, _) => false);
        DisplayName = localize(descriptor.DisplayNameKey, descriptor.Name);
        Description = localize($"{descriptor.DisplayNameKey}.Description", descriptor.Name);
        _localizedOptions = descriptor.Options
            .Select(option => new FrontedNodePropertyOptionViewModel(option, LocalizeOption(option)))
            .ToArray();
        _booleanOptions =
        [
            new FrontedNodePropertyOptionViewModel("true", "true"),
            new FrontedNodePropertyOptionViewModel("false", "false")
        ];
        _visibilityOptions = FrontedBehaviorPropertyMetadata.VisibilityOptions
            .Select(option => new FrontedNodePropertyOptionViewModel(option, localize($"Designer.Option.Visibility.{option}", option)))
            .ToArray();
        if (ColorHelper.TryParseColor(TextValue, out var color))
        {
            _colorValue = color;
        }
        _numberValue = ParseNumberValue();
        ErrorsChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(NumberValue))
            {
                OnPropertyChanged(nameof(ValidationError));
                OnPropertyChanged(nameof(HasValidationError));
            }
        };
        ValidateProperty(NumberValue, nameof(NumberValue));
    }

    public FrontedNodePropertyDescriptor Descriptor { get; }
    public string DisplayName { get; }
    public string Description { get; }
    /// <summary>获取当前属性是否应在属性面板中显示。</summary>
    public bool IsVisible => !IsNumericInputUnitSelector || HasConnectedNumericInput;
    /// <summary>获取指示当前手动值是否可编辑的值。</summary>
    public bool IsManualValueEnabled => !HasExternalValueInput;
    /// <summary>获取指示当前属性是否由外部数值输入提供的值。</summary>
    public bool HasExternalValueInput => ResolveExternalInputPort() is { } port && _hasIncomingConnection(_node, port);
    /// <summary>获取数值输入端口是否已连接。</summary>
    public bool HasConnectedNumericInput => ResolveNumericInputPort() is { } port && _hasIncomingConnection(_node, port);
    /// <summary>获取外部数值输入说明。</summary>
    public string ExternalValueInputNotice => _localize("Designer.Graph.ExternalValueInput", "An external numeric input is connected; the manual value is disabled.");
    /// <summary>获取当前属性的上下文输入提示。</summary>
    public string Placeholder => DynamicMetadata?.Placeholder ?? string.Empty;
    /// <summary>获取当前属性的上下文帮助。</summary>
    public string HelpText => DynamicMetadata is null
        ? Description
        : _localize(DynamicMetadata.DescriptionKey, $"{DynamicMetadata.Placeholder}; example: {DynamicMetadata.Example}");
    public bool IsBoolean => Descriptor.EditorKind == FrontedNodePropertyEditorKind.Boolean;
    public bool IsEnum => Descriptor.EditorKind == FrontedNodePropertyEditorKind.Enum && !IsConditionOperator;
    public bool IsNumber => Descriptor.EditorKind == FrontedNodePropertyEditorKind.Number || IsNumericDynamicValue || IsNumericConditionValue;
    /// <summary>获取该数值属性是否可在不丢失百分比表达式的情况下使用 NumberBox。</summary>
    public bool IsNumberBox => IsNumber && !FrontedBehaviorPropertyMetadata.SupportsPercentage(EffectivePropertyNameForValidation());
    /// <summary>获取该数值属性是否必须保留文本编辑以支持百分比表达式。</summary>
    public bool IsPercentageNumberText => IsNumber && !IsNumberBox;
    public bool IsColor => Descriptor.EditorKind == FrontedNodePropertyEditorKind.Color || IsColorDynamicValue;
    public bool IsControlReference => Descriptor.EditorKind == FrontedNodePropertyEditorKind.ControlReference;
    public bool IsPropertyName => Descriptor.EditorKind == FrontedNodePropertyEditorKind.PropertyName;
    public bool IsVisibilityValue => IsDynamicValue && FrontedBehaviorPropertyMetadata.IsVisibilityProperty(CurrentBehaviorPropertyName);
    public bool HasTextSuggestions => !IsConditionProperty && !IsBoolean && !IsEnum && !IsNumber && !IsColor && !IsControlReference && !IsPropertyName && Descriptor.Options.Count > 0;
    public bool IsText => !IsConditionProperty && !IsBoolean && !IsEnum && !IsNumber && !IsColor && !IsControlReference && !IsPropertyName && !HasTextSuggestions && !IsVisibilityValue;
    /// <summary>获取该属性是否选择左侧条件字段。</summary>
    public bool IsConditionField => _node.NodeType == "flow.if" && Descriptor.Name == "Left";
    /// <summary>获取该属性是否选择数值事件上下文字段。</summary>
    public bool IsEventContextField => _node.NodeType == "value.eventContext" && Descriptor.Name == "Path";
    /// <summary>获取该属性是否选择条件运算符。</summary>
    public bool IsConditionOperator => _node.NodeType == "flow.if" && Descriptor.Name == "Operator";
    /// <summary>获取该属性是否编辑右侧条件值。</summary>
    public bool IsConditionValue => _node.NodeType == "flow.if" && Descriptor.Name == "Right";
    /// <summary>获取选中的条件值是否类似布尔值。</summary>
    public bool IsBooleanConditionValue => IsConditionValue && IsBooleanType(SelectedConditionField?.TypeName);
    /// <summary>获取选中的条件值是否类似枚举。</summary>
    public bool IsEnumConditionValue => IsConditionValue && SelectedConditionField?.EnumValues.Count > 0;
    /// <summary>获取选中的条件值是否为数值。</summary>
    public bool IsNumericConditionValue => IsConditionValue && IsNumericType(SelectedConditionField?.TypeName);
    /// <summary>获取条件值是否应使用自由文本。</summary>
    public bool IsTextConditionValue => IsConditionValue && !IsBooleanConditionValue && !IsEnumConditionValue && !IsNumericConditionValue;
    /// <summary>获取该条件可用的上下文感知事件字段。</summary>
    public IReadOnlyList<FrontedGraphConditionFieldOptionViewModel> ConditionFieldOptions => EnsureCurrentConditionFieldOption();
    /// <summary>获取当前阶段可用于数值计算的事件上下文字段。</summary>
    public IReadOnlyList<FrontedGraphConditionFieldOptionViewModel> EventContextFieldOptions =>
        EnsureCurrentConditionFieldOption();
    /// <summary>获取选中字段类型可用的上下文感知运算符。</summary>
    public IReadOnlyList<FrontedNodePropertyOptionViewModel> ConditionOperatorOptions => ResolveConditionOperatorOptions();
    /// <summary>获取选中字段可用的稳定布尔值或枚举值。</summary>
    public IReadOnlyList<FrontedNodePropertyOptionViewModel> ConditionValueOptions => ResolveConditionValueOptions();
    public IReadOnlyList<string> Options => Descriptor.Options;
    public IReadOnlyList<FrontedNodePropertyOptionViewModel> LocalizedOptions => _localizedOptions;
    /// <summary>获取布尔属性编辑器可用的稳定布尔选项。</summary>
    public IReadOnlyList<FrontedNodePropertyOptionViewModel> BooleanOptions => _booleanOptions;
    public IReadOnlyList<FrontedNodePropertyOptionViewModel> DisplayedOptions => ResolveDisplayedOptions();
    public IReadOnlyList<FrontedNodePropertyOptionViewModel> VisibilityOptions => _visibilityOptions;
    public IReadOnlyList<FrontedNodeTargetOptionViewModel> TargetOptions => EnsureCurrentTargetOption();
    public string? Unit => IsRotation ? "°" : Descriptor.Unit;
    public bool HasUnit => !string.IsNullOrWhiteSpace(Unit);
    public bool HasValidationError => !string.IsNullOrWhiteSpace(ValidationError);
    public string? ValidationError
    {
        get => _validationError ?? GetErrors(nameof(NumberValue)).Cast<object>().FirstOrDefault()?.ToString();
        private set
        {
            if (SetProperty(ref _validationError, value))
            {
                OnPropertyChanged(nameof(HasValidationError));
            }
        }
    }
    public Color ColorValue
    {
        get => _colorValue;
        set
        {
            if (!SetProperty(ref _colorValue, value))
            {
                return;
            }

            TextValue = value.ToArgbHexString();
        }
    }

    public string TextValue
    {
        get => Read().ValueKind == JsonValueKind.String ? Read().GetString() ?? string.Empty : Read().ToString();
        set
        {
            if (IsNumberBox)
            {
                NumberValue = double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                    && double.IsFinite(number)
                    ? number
                    : null;
                return;
            }

            if (!ValidateTextValue(value, out var normalized))
            {
                OnPropertyChanged(nameof(TextValue));
                return;
            }

            Write(Descriptor.PropertyType == FrontedNodePropertyType.Number && double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedNumber)
                ? JsonSerializer.SerializeToElement(parsedNumber)
                : JsonSerializer.SerializeToElement(normalized));
        }
    }

    public bool BooleanValue
    {
        get => Read().ValueKind == JsonValueKind.True;
        set => Write(JsonSerializer.SerializeToElement(value));
    }

    /// <summary>获取或设置由 WPF-UI NumberBox 编辑的数值。</summary>
    [CustomValidation(typeof(FrontedNodePropertyEditorViewModel), nameof(ValidateNumberBoxValue))]
    public double? NumberValue
    {
        get => _numberValue;
        set
        {
            if (!SetProperty(ref _numberValue, value, true))
            {
                return;
            }

            if (!GetErrors(nameof(NumberValue)).Cast<object>().Any() && value is { } number)
            {
                Write(JsonSerializer.SerializeToElement(number));
            }
        }
    }

    /// <summary>获取 NumberBox 显示的最小值。</summary>
    public double NumberMinimum => _node.NodeType == "flow.parallel" && Descriptor.Name == "BranchCount"
        ? FrontedParallelNodePorts.MinBranchCount
        : Descriptor.Name == "DurationMs"
            ? 0D
            : DynamicMetadata?.Min ?? double.MinValue;

    /// <summary>获取 NumberBox 显示的最大值。</summary>
    public double NumberMaximum => _node.NodeType == "flow.parallel" && Descriptor.Name == "BranchCount"
        ? FrontedParallelNodePorts.MaxBranchCount
        : DynamicMetadata?.Max ?? double.MaxValue;

    /// <summary>获取 NumberBox 的小数位限制。</summary>
    public int NumberMaxDecimalPlaces => RequiresIntegerNumber ? 0 : 6;

    /// <summary>获取或设置用于 ComboBox 编辑的稳定小写布尔字符串。</summary>
    public string BooleanTextValue
    {
        get => BooleanValue ? "true" : "false";
        set => BooleanValue = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    public string EnumValue
    {
        get => TextValue;
        set => Write(JsonSerializer.SerializeToElement(value));
    }

    /// <summary>获取或设置稳定的条件字段路径。</summary>
    public string ConditionFieldValue
    {
        get => TextValue;
        set
        {
            TextValue = value;
            var allowed = ResolveConditionOperatorOptions().Select(option => option.Value).ToHashSet(StringComparer.Ordinal);
            var currentOperator = ReadNodeString("Operator");
            if (!allowed.Contains(currentOperator))
            {
                _node.Properties["Operator"] = JsonSerializer.SerializeToElement(TriggerFilterOperator.Equals.ToString());
                _markDirty();
                _validate();
                _refreshRelatedProperties?.Invoke();
            }
        }
    }

    /// <summary>获取或设置数值事件上下文的稳定字段路径。</summary>
    public string EventContextFieldValue
    {
        get => TextValue;
        set => TextValue = value;
    }

    /// <summary>获取或设置稳定的条件运算符名称。</summary>
    public string ConditionOperatorValue
    {
        get => TextValue;
        set => TextValue = value;
    }

    /// <summary>获取或设置稳定的类型化条件选项。</summary>
    public string ConditionChoiceValue
    {
        get => TextValue;
        set => TextValue = value;
    }

    public string TargetValue
    {
        get => TextValue;
        set => TextValue = value;
    }

    public string PropertyNameValue
    {
        get => TextValue;
        set => TextValue = value;
    }

    public string PropertyNameText
    {
        get => DisplayForValue(TextValue);
        set => TextValue = ValueForDisplay(value);
    }

    public string SuggestionText
    {
        get => DisplayForValue(TextValue);
        set => TextValue = ValueForDisplay(value);
    }

    public string VisibilityValue
    {
        get => TextValue;
        set => TextValue = value;
    }

    public void SetRefreshRelatedProperties(Action refreshRelatedProperties)
    {
        _refreshRelatedProperties = refreshRelatedProperties;
    }

    public void RefreshEditorState()
    {
        OnPropertyChanged(nameof(IsNumber));
        OnPropertyChanged(nameof(IsNumberBox));
        OnPropertyChanged(nameof(IsPercentageNumberText));
        OnPropertyChanged(nameof(IsColor));
        OnPropertyChanged(nameof(IsVisibilityValue));
        OnPropertyChanged(nameof(IsText));
        OnPropertyChanged(nameof(HasTextSuggestions));
        OnPropertyChanged(nameof(Unit));
        OnPropertyChanged(nameof(HasUnit));
        OnPropertyChanged(nameof(DisplayedOptions));
        if (_node.NodeType == "flow.if")
        {
            OnPropertyChanged(nameof(IsConditionField));
            OnPropertyChanged(nameof(IsConditionOperator));
            OnPropertyChanged(nameof(IsConditionValue));
            OnPropertyChanged(nameof(IsBooleanConditionValue));
            OnPropertyChanged(nameof(IsEnumConditionValue));
            OnPropertyChanged(nameof(IsNumericConditionValue));
            OnPropertyChanged(nameof(IsTextConditionValue));
            OnPropertyChanged(nameof(ConditionFieldOptions));
            OnPropertyChanged(nameof(ConditionOperatorOptions));
            OnPropertyChanged(nameof(ConditionValueOptions));
            OnPropertyChanged(nameof(ConditionFieldValue));
            OnPropertyChanged(nameof(ConditionOperatorValue));
            OnPropertyChanged(nameof(ConditionChoiceValue));
        }
        if (_node.NodeType == "value.eventContext")
        {
            OnPropertyChanged(nameof(IsEventContextField));
            OnPropertyChanged(nameof(EventContextFieldOptions));
            OnPropertyChanged(nameof(EventContextFieldValue));
        }
        OnPropertyChanged(nameof(PropertyNameText));
        OnPropertyChanged(nameof(Placeholder));
        OnPropertyChanged(nameof(HelpText));
        if (IsNumber)
        {
            OnPropertyChanged(nameof(NumberMinimum));
            OnPropertyChanged(nameof(NumberMaximum));
            OnPropertyChanged(nameof(NumberMaxDecimalPlaces));
        }
        _numberValue = ParseNumberValue();
        OnPropertyChanged(nameof(NumberValue));
        ValidateProperty(NumberValue, nameof(NumberValue));
    }

    private JsonElement Read() => _node.Properties.TryGetValue(Descriptor.Name, out var value) ? value : Descriptor.DefaultValue;

    private void Write(JsonElement value)
    {
        if (JsonElement.DeepEquals(Read(), value))
        {
            return;
        }

        _node.Properties[Descriptor.Name] = value;
        _markDirty();
        _validate();
        ValidationError = null;
        if (IsColor && ColorHelper.TryParseColor(TextValue, out var color))
        {
            SetProperty(ref _colorValue, color, nameof(ColorValue));
        }
        OnPropertyChanged(nameof(TextValue));
        _numberValue = ParseNumberValue();
        OnPropertyChanged(nameof(NumberValue));
        OnPropertyChanged(nameof(BooleanValue));
        OnPropertyChanged(nameof(BooleanTextValue));
        OnPropertyChanged(nameof(EnumValue));
        OnPropertyChanged(nameof(TargetValue));
        OnPropertyChanged(nameof(PropertyNameValue));
        OnPropertyChanged(nameof(PropertyNameText));
        OnPropertyChanged(nameof(SuggestionText));
        OnPropertyChanged(nameof(VisibilityValue));
        OnPropertyChanged(nameof(DisplayedOptions));
        if (_node.NodeType == "flow.if")
        {
            OnPropertyChanged(nameof(ConditionFieldValue));
            OnPropertyChanged(nameof(ConditionOperatorValue));
            OnPropertyChanged(nameof(ConditionChoiceValue));
        }
        if (_node.NodeType == "value.eventContext")
        {
            OnPropertyChanged(nameof(EventContextFieldValue));
        }
        _refreshRelatedProperties?.Invoke();
    }

    private bool ValidateTextValue(string? value, out string normalized)
    {
        normalized = value ?? string.Empty;
        if (IsColor)
        {
            if (!ColorHelper.TryNormalizeHex(value, out normalized))
            {
                ValidationError = "Invalid color. Use #RRGGBB, #AARRGGBB, or a WPF color name.";
                return false;
            }

            return true;
        }

        if (IsNumber)
        {
            var propertyName = EffectivePropertyNameForValidation();
            if (!FrontedBehaviorPropertyMetadata.TryValidateValue(propertyName, value, out var message))
            {
                ValidationError = message;
                return false;
            }

            var trimmed = value?.Trim() ?? string.Empty;
            var isPercentage = FrontedBehaviorPropertyMetadata.SupportsPercentage(propertyName)
                               && trimmed.EndsWith('%');
            var numericText = isPercentage ? trimmed[..^1] : trimmed;
            if (!double.TryParse(numericText, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number))
            {
                ValidationError = "Value must be a finite number.";
                return false;
            }

            if (isPercentage)
            {
                normalized = $"{number.ToString(CultureInfo.InvariantCulture)}%";
                return true;
            }

            normalized = number.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        if (IsVisibilityValue
            && !FrontedBehaviorPropertyMetadata.VisibilityOptions.Any(option => string.Equals(option, value, StringComparison.OrdinalIgnoreCase)))
        {
            ValidationError = "Visibility must be Visible, Hidden, or Collapsed.";
            return false;
        }

        ValidationError = null;
        return true;
    }

    private IReadOnlyList<FrontedNodeTargetOptionViewModel> EnsureCurrentTargetOption()
    {
        if (!IsControlReference || string.IsNullOrWhiteSpace(TextValue)
            || _targetOptions.Any(option => string.Equals(option.Value, TextValue, StringComparison.Ordinal)))
        {
            return _targetOptions;
        }

        return [.. _targetOptions, new FrontedNodeTargetOptionViewModel(TextValue, $"Unknown target ({TextValue})")];
    }

    private IReadOnlyList<FrontedGraphConditionFieldOptionViewModel> EnsureCurrentConditionFieldOption()
    {
        var current = IsConditionField || IsEventContextField ? TextValue : ReadNodeString("Left");
        if (string.IsNullOrWhiteSpace(current)
            || _conditionFieldOptions.Any(option => string.Equals(option.ValuePath, current, StringComparison.Ordinal)))
        {
            return _conditionFieldOptions;
        }

        return [.. _conditionFieldOptions, new FrontedGraphConditionFieldOptionViewModel(current, current, current, "string", [], null, current)];
    }

    private IReadOnlyList<FrontedNodePropertyOptionViewModel> ResolveConditionOperatorOptions()
    {
        var allowed = IsBooleanType(SelectedConditionField?.TypeName) || SelectedConditionField?.EnumValues.Count > 0
            ? new[] { TriggerFilterOperator.Equals, TriggerFilterOperator.NotEquals, TriggerFilterOperator.Exists }
            : IsNumericType(SelectedConditionField?.TypeName)
                ? new[]
                {
                    TriggerFilterOperator.Equals, TriggerFilterOperator.NotEquals, TriggerFilterOperator.GreaterThan,
                    TriggerFilterOperator.GreaterThanOrEqual, TriggerFilterOperator.LessThan,
                    TriggerFilterOperator.LessThanOrEqual, TriggerFilterOperator.Exists
                }
                : Enum.GetValues<TriggerFilterOperator>();
        return allowed.Select(value => new FrontedNodePropertyOptionViewModel(value.ToString(), LocalizeOption(value.ToString()))).ToArray();
    }

    private IReadOnlyList<FrontedNodePropertyOptionViewModel> ResolveConditionValueOptions()
    {
        if (IsBooleanType(SelectedConditionField?.TypeName))
        {
            return _booleanOptions;
        }

        return SelectedConditionField?.EnumValues
            .Select(value => new FrontedNodePropertyOptionViewModel(value, value))
            .ToArray() ?? [];
    }

    private string LocalizeOption(string value)
    {
        if (IsPropertyName)
        {
            return _localize(
                string.Equals(value, "All", StringComparison.OrdinalIgnoreCase)
                    ? "Designer.Graph.PropertyName.All"
                    : $"Designer.Property.{value}",
                value);
        }

        return _localize($"Designer.Option.{Descriptor.Name}.{value}", value);
    }

    private string DisplayForValue(string value) =>
        DisplayedOptions.FirstOrDefault(option => string.Equals(option.Value, value, StringComparison.Ordinal))?.DisplayName
        ?? value;

    private string ValueForDisplay(string? display)
    {
        var value = display ?? string.Empty;
        return DisplayedOptions.FirstOrDefault(option =>
                   string.Equals(option.DisplayName, value, StringComparison.Ordinal)
                   || string.Equals(option.Value, value, StringComparison.Ordinal))?.Value
               ?? value;
    }

    private IReadOnlyList<FrontedNodePropertyOptionViewModel> ResolveDisplayedOptions()
    {
        if (!IsPropertyName)
        {
            return _localizedOptions;
        }

        var names = FrontedBehaviorPropertyMetadata.GetPropertyNamesForLayer(
            CurrentTargetLayer,
            Descriptor.Options.Any(option => string.Equals(option, "All", StringComparison.OrdinalIgnoreCase)));
        return names
            .Select(option => new FrontedNodePropertyOptionViewModel(option, LocalizeOption(option)))
            .ToArray();
    }

    private string? CurrentBehaviorPropertyName =>
        _node.Properties.TryGetValue("PropertyName", out var property)
            ? property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString()
            : null;

    private FrontedGraphConditionFieldOptionViewModel? SelectedConditionField =>
        EnsureCurrentConditionFieldOption().FirstOrDefault(option =>
            string.Equals(option.ValuePath, ReadNodeString("Left"), StringComparison.Ordinal));

    private bool IsConditionProperty => IsConditionField || IsEventContextField || IsConditionOperator || IsConditionValue;

    /// <summary>刷新外部值输入连接导致的可编辑状态。</summary>
    public void RefreshExternalInputState()
    {
        OnPropertyChanged(nameof(HasExternalValueInput));
        OnPropertyChanged(nameof(HasConnectedNumericInput));
        OnPropertyChanged(nameof(IsManualValueEnabled));
        OnPropertyChanged(nameof(ExternalValueInputNotice));
        OnPropertyChanged(nameof(IsVisible));
    }

    private bool IsNumericInputUnitSelector => Descriptor.Name is "ValueInputUnit" or "FromInputUnit" or "ToInputUnit";

    private string? ResolveExternalInputPort() => (_node.NodeType, Descriptor.Name) switch
    {
        ("action.setProperty", "Value") => "ValueInput",
        ("action.animateProperty", "From") => "FromInput",
        ("action.animateProperty", "To") => "ToInput",
        _ => null
    };

    private string? ResolveNumericInputPort() => (_node.NodeType, Descriptor.Name) switch
    {
        ("action.setProperty", "Value") or ("action.setProperty", "ValueInputUnit") => "ValueInput",
        ("action.animateProperty", "From") or ("action.animateProperty", "FromInputUnit") => "FromInput",
        ("action.animateProperty", "To") or ("action.animateProperty", "ToInputUnit") => "ToInput",
        _ => null
    };

    private string ReadNodeString(string name) =>
        _node.Properties.TryGetValue(name, out var value)
            ? value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString()
            : string.Empty;

    private static bool IsBooleanType(string? typeName) =>
        string.Equals(typeName?.TrimEnd('?'), "bool", StringComparison.OrdinalIgnoreCase)
        || string.Equals(typeName?.TrimEnd('?'), "Boolean", StringComparison.OrdinalIgnoreCase);

    private static bool IsNumericType(string? typeName) =>
        typeName?.TrimEnd('?').ToLowerInvariant() is "byte" or "short" or "int" or "long" or "float" or "double" or "decimal";

    private FrontedAnimationTargetLayer CurrentTargetLayer =>
        _node.Properties.TryGetValue("TargetLayer", out var value)
        && Enum.TryParse<FrontedAnimationTargetLayer>(
            value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString(),
            true,
            out var layer)
            ? layer
            : FrontedAnimationTargetLayer.Auto;

    private bool IsDynamicValue => Descriptor.Name is "Value" or "From" or "To";
    private bool IsColorDynamicValue => IsDynamicValue && FrontedBehaviorPropertyMetadata.IsColorProperty(CurrentBehaviorPropertyName);
    private bool IsNumericDynamicValue => IsDynamicValue && FrontedBehaviorPropertyMetadata.IsNumericProperty(CurrentBehaviorPropertyName);
    private bool IsRotation => string.Equals(Descriptor.Name, "Rotation", StringComparison.OrdinalIgnoreCase)
        || string.Equals(CurrentBehaviorPropertyName, "Rotation", StringComparison.OrdinalIgnoreCase);
    private FrontedAnimatablePropertyMetadata? DynamicMetadata =>
        IsDynamicValue ? FrontedBehaviorPropertyMetadata.Find(CurrentBehaviorPropertyName) : FrontedBehaviorPropertyMetadata.Find(Descriptor.Name);
    private string? EffectivePropertyNameForValidation() =>
        IsDynamicValue ? CurrentBehaviorPropertyName : Descriptor.Name;

    private bool RequiresIntegerNumber =>
        _node.NodeType == "flow.parallel" && Descriptor.Name == "BranchCount"
        || Descriptor.Name == "DurationMs";

    private double? ParseNumberValue()
    {
        var value = TextValue;
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number)
            ? number
            : null;
    }

    /// <summary>
    /// 根据图属性元数据校验 NumberBox 值。
    /// </summary>
    /// <param name="value">候选数值。</param>
    /// <param name="context">包含属性编辑器的校验上下文。</param>
    /// <returns>校验结果。</returns>
    public static ValidationResult? ValidateNumberBoxValue(double? value, ValidationContext context)
    {
        var editor = (FrontedNodePropertyEditorViewModel)context.ObjectInstance;
        if (!editor.IsNumberBox)
        {
            return ValidationResult.Success;
        }

        if (value is null || !double.IsFinite(value.Value))
        {
            return new ValidationResult(editor._localize("Designer.Graph.Validation.NumberFinite", "Value must be a finite number."));
        }

        if (editor.RequiresIntegerNumber && value.Value != Math.Truncate(value.Value))
        {
            return new ValidationResult(editor._localize("Designer.Graph.Validation.NumberInteger", "Value must be an integer."));
        }

        if (value.Value < editor.NumberMinimum || value.Value > editor.NumberMaximum)
        {
            return new ValidationResult(string.Format(
                editor._localize("Designer.Graph.Validation.NumberRange", "Value must be between {0} and {1}."),
                editor.NumberMinimum,
                editor.NumberMaximum));
        }

        var text = value.Value.ToString(CultureInfo.InvariantCulture);
        return FrontedBehaviorPropertyMetadata.TryValidateValue(editor.EffectivePropertyNameForValidation(), text, out var message)
            ? ValidationResult.Success
            : new ValidationResult(message);
    }
}

/// <summary>
/// 行为图目标编辑器显示的目标选项。
/// </summary>
/// <param name="Value">已持久化的目标引用值。</param>
/// <param name="DisplayName">面向用户的目标显示名称。</param>
