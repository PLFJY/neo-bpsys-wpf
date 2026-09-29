using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Services.FrontedDesigner;
using neo_bpsys_wpf.ViewModels.FrontedDesigner.GraphEditor;
using System.Collections.ObjectModel;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Windows;

namespace neo_bpsys_wpf.ViewModels.FrontedDesigner;

public sealed partial class TriggerFilterEditorViewModel : ObservableObject
{
    private readonly Action _markDirty;
    private readonly Action _captureUndoSnapshot;
    private readonly Func<string, string, string> _localize;

    public TriggerFilterEditorViewModel(
        TriggerFilter model,
        IReadOnlyList<BehaviorOptionViewModel> operatorOptions,
        Action markDirty,
        Func<string, string, string> localize,
        Action? captureUndoSnapshot = null)
    {
        Model = model;
        OperatorOptions = operatorOptions;
        _markDirty = markDirty;
        _captureUndoSnapshot = captureUndoSnapshot ?? (() => { });
        _localize = localize;
    }

    public TriggerFilter Model { get; }

    public IReadOnlyList<BehaviorOptionViewModel> OperatorOptions { get; }

    /// <summary>获取当前选中 payload 字段类型推荐的运算符。</summary>
    public IReadOnlyList<BehaviorOptionViewModel> DisplayedOperatorOptions =>
        IsEnumField || IsBooleanField
            ? OperatorOptions.Where(option => option.Value is TriggerFilterOperator.Equals
                or TriggerFilterOperator.NotEquals
                or TriggerFilterOperator.Exists).ToArray()
            : OperatorOptions;

    public IReadOnlyList<BehaviorPayloadFieldOptionViewModel> PayloadFieldOptions { get; private set; } = [];

    public bool IsUnknownParameter => PayloadFieldOptions.FirstOrDefault(option =>
        string.Equals(option.Path, Left, StringComparison.Ordinal))?.IsUnknown == true;

    /// <summary>获取当前选中 payload 字段是否类似枚举。</summary>
    public bool IsEnumField => SelectedPayloadField is { } selectedField
        && (selectedField.EnumValues.Count > 0
            || string.Equals(selectedField.TypeName.TrimEnd('?'), "Enum", StringComparison.OrdinalIgnoreCase));

    /// <summary>获取当前选中 payload 字段是否类似布尔值。</summary>
    public bool IsBooleanField => SelectedPayloadField is { } selectedField
        && IsBooleanTypeName(selectedField.TypeName);

    /// <summary>获取右侧值是否应使用文本编辑器。</summary>
    public bool IsTextValue => !IsEnumField && !IsBooleanField;

    /// <summary>获取右侧值编辑器可用的稳定枚举选项。</summary>
    public IReadOnlyList<BehaviorOptionViewModel> EnumValueOptions =>
        SelectedPayloadField?.EnumValues
            .Select(value => new BehaviorOptionViewModel(
                value,
                FormatEnumDisplay(value)))
            .ToArray()
        ?? [];

    /// <summary>获取右侧值编辑器可用的稳定布尔选项。</summary>
    public IReadOnlyList<BehaviorOptionViewModel> BooleanValueOptions { get; } =
    [
        new("true", "true"),
        new("false", "false")
    ];

    public string Left
    {
        get => Model.Left;
        set
        {
            if (string.Equals(Model.Left, value, StringComparison.Ordinal))
            {
                return;
            }

            _captureUndoSnapshot();
            if (SetProperty(Model.Left, value, Model, static (model, next) => model.Left = next))
            {
                _markDirty();
                OnPropertyChanged(nameof(IsUnknownParameter));
                OnPropertyChanged(nameof(HintText));
                OnPropertyChanged(nameof(HasHintText));
                OnPropertyChanged(nameof(IsEnumField));
                OnPropertyChanged(nameof(IsBooleanField));
                OnPropertyChanged(nameof(IsTextValue));
                OnPropertyChanged(nameof(EnumValueOptions));
                OnPropertyChanged(nameof(BooleanValueOptions));
                OnPropertyChanged(nameof(DisplayedOperatorOptions));
                if ((IsEnumField || IsBooleanField) && Operator is not (TriggerFilterOperator.Equals or TriggerFilterOperator.NotEquals or TriggerFilterOperator.Exists))
                {
                    Operator = TriggerFilterOperator.Equals;
                }
            }
        }
    }

    public TriggerFilterOperator Operator
    {
        get => Model.Operator;
        set
        {
            if (Model.Operator == value)
            {
                return;
            }

            _captureUndoSnapshot();
            if (SetProperty(Model.Operator, value, Model, static (model, next) => model.Operator = next))
            {
                _markDirty();
            }
        }
    }

    public string? Right
    {
        get => Model.Right;
        set
        {
            if (string.Equals(Model.Right, value, StringComparison.Ordinal))
            {
                return;
            }

            _captureUndoSnapshot();
            if (SetProperty(Model.Right, value, Model, static (model, next) => model.Right = next))
            {
                _markDirty();
            }
        }
    }

    /// <summary>
    /// 获取过滤值的上下文提示。
    /// </summary>
    public string HintText => Left switch
    {
        "Event.Action" or "Event.PreviousAction" =>
            _localize("Designer.Behaviors.FilterHint.Action", "Use enum values such as PickSur, PickHun, BanSur."),
        "Event.Indexes" or "Event.PreviousIndexes" =>
            _localize("Designer.Behaviors.FilterHint.Indexes", "For string contains filters, prefer IndexesText / PreviousIndexesText."),
        "Event.IndexesText" or "Event.PreviousIndexesText" =>
            _localize("Designer.Behaviors.FilterHint.IndexesText", "Formatted as [0] or [1, 2]. Use Contains 0 to match index 0."),
        _ => string.Empty
    };

    /// <summary>
    /// 获取 <see cref="HintText" /> 是否有内容。
    /// </summary>
    public bool HasHintText => !string.IsNullOrWhiteSpace(HintText);

    public void SetPayloadFieldOptions(IReadOnlyList<BehaviorPayloadFieldOptionViewModel> options)
    {
        PayloadFieldOptions = options;
        OnPropertyChanged(nameof(PayloadFieldOptions));
        OnPropertyChanged(nameof(IsUnknownParameter));
        OnPropertyChanged(nameof(HintText));
        OnPropertyChanged(nameof(HasHintText));
        OnPropertyChanged(nameof(IsEnumField));
        OnPropertyChanged(nameof(IsBooleanField));
        OnPropertyChanged(nameof(IsTextValue));
        OnPropertyChanged(nameof(EnumValueOptions));
        OnPropertyChanged(nameof(BooleanValueOptions));
        OnPropertyChanged(nameof(DisplayedOperatorOptions));
    }

    /// <summary>
    /// 刷新 payload 字段选项显示字符串以支持热切换语言。
    /// </summary>
    public void RefreshLocalization()
    {
        foreach (var option in PayloadFieldOptions)
        {
            option.Refresh();
        }
        OnPropertyChanged(nameof(IsUnknownParameter));
        OnPropertyChanged(nameof(HintText));
        OnPropertyChanged(nameof(HasHintText));
    }

    private BehaviorPayloadFieldOptionViewModel? SelectedPayloadField =>
        PayloadFieldOptions.FirstOrDefault(option => string.Equals(option.Path, Left, StringComparison.Ordinal));

    private static bool IsBooleanTypeName(string? typeName)
    {
        var normalized = typeName?.TrimEnd('?');
        return string.Equals(normalized, "bool", StringComparison.OrdinalIgnoreCase)
               || string.Equals(normalized, "Boolean", StringComparison.OrdinalIgnoreCase);
    }

    private string FormatEnumDisplay(string value)
    {
        var enumType = SelectedPayloadField?.TypeName.TrimEnd('?');
        if (string.Equals(enumType, "GameAction", StringComparison.Ordinal))
        {
            // GameAction 枚举值对应的本地化键分散在 Bp 字典（BanMap/PickMap 等）
            // 与 Game 字典（DistributeCharacters），不能走 Designer 字典的 _localize 通道。
            var key = GameActionLocalizationKey(value);
            var dictionary = string.Equals(key, "DistributeCharacters", StringComparison.Ordinal)
                ? AppI18nDictionaries.Game
                : AppI18nDictionaries.Bp;
            var localized = I18nHelper.GetLocalizedString(dictionary, key);
            return string.Equals(localized, value, StringComparison.Ordinal) ? value : $"{value} — {localized}";
        }

        var designerLocalized = _localize($"Designer.Enum.{enumType}.{value}", value);
        return string.Equals(designerLocalized, value, StringComparison.Ordinal) ? value : $"{value} — {designerLocalized}";
    }

    private static string GameActionLocalizationKey(string value) => value switch
    {
        "BanMap" => "BanMap",
        "PickMap" => "PickMap",
        "PickCamp" => "PickCamp",
        "BanSur" => "BanSurvivor",
        "BanHun" => "BanHunter",
        "PickSur" => "PickSurvivor",
        "PickHun" => "PickHunter",
        "PickSurTalent" => "PickSurTalent",
        "PickHunTalent" => "PickHunTalent",
        "DistributeChara" => "DistributeCharacters",
        _ => $"Designer.Enum.GameAction.{value}"
    };
}
