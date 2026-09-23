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

public sealed class BehaviorOptionViewModel : ObservableObject
{
    private readonly string _displayNameKey;
    private readonly string _displayNameFallback;
    private readonly Func<string, string, string> _localize;
    private string _displayName;

    public BehaviorOptionViewModel(object value, string displayNameKey, string displayNameFallback, Func<string, string, string> localize)
    {
        Value = value;
        _displayNameKey = displayNameKey;
        _displayNameFallback = displayNameFallback;
        _localize = localize;
        _displayName = localize(displayNameKey, displayNameFallback);
    }

    /// <summary>
    /// 用于非本地化值（例如仅显示符号的运算符）。
    /// </summary>
    public BehaviorOptionViewModel(object value, string displayName)
    {
        Value = value;
        _displayNameKey = string.Empty;
        _displayNameFallback = displayName;
        _localize = static (_, fallback) => fallback;
        _displayName = displayName;
    }

    public object Value { get; }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    /// <summary>
    /// 通过本地化函数重新解析 <see cref="DisplayName"/>。
    /// </summary>
    public void Refresh()
    {
        if (!string.IsNullOrEmpty(_displayNameKey))
        {
            DisplayName = _localize(_displayNameKey, _displayNameFallback);
        }
    }
}

public sealed class BehaviorEventOptionViewModel : ObservableObject
{
    private readonly string _displayNameKey;
    private readonly string _categoryDisplayNameKey;
    private readonly string _descriptionKey;
    private readonly string _eventTypeFallback;
    private readonly string _categoryFallback;
    private readonly string _descriptionFallback;
    private readonly Func<string, string, string> _localize;
    private string _displayName;
    private string _categoryDisplayName;
    private string _description;

    /// <summary>
    /// 使用 3.0/3.1 版本的构造函数签名初始化事件选项。
    /// </summary>
    /// <param name="eventType">规范事件类型。</param>
    /// <param name="displayNameKey">显示名称本地化键。</param>
    /// <param name="categoryDisplayNameKey">分类显示名称本地化键。</param>
    /// <param name="descriptionKey">描述本地化键。</param>
    /// <param name="eventTypeFallback">事件显示名称 fallback。</param>
    /// <param name="categoryFallback">分类显示名称 fallback。</param>
    /// <param name="payloadFields">事件负载字段选项。</param>
    /// <param name="localize">本地化函数。</param>
    public BehaviorEventOptionViewModel(
        string eventType,
        string displayNameKey,
        string categoryDisplayNameKey,
        string descriptionKey,
        string eventTypeFallback,
        string categoryFallback,
        IReadOnlyList<BehaviorPayloadFieldOptionViewModel> payloadFields,
        Func<string, string, string> localize)
        : this(
            eventType,
            displayNameKey,
            categoryDisplayNameKey,
            descriptionKey,
            eventTypeFallback,
            categoryFallback,
            eventTypeFallback,
            FrontedBehaviorEventUsage.All,
            payloadFields,
            localize)
    {
    }

    public BehaviorEventOptionViewModel(
        string eventType,
        string displayNameKey,
        string categoryDisplayNameKey,
        string descriptionKey,
        string eventTypeFallback,
        string categoryFallback,
        string descriptionFallback,
        FrontedBehaviorEventUsage supportedUsages,
        IReadOnlyList<BehaviorPayloadFieldOptionViewModel> payloadFields,
        Func<string, string, string> localize,
        bool isMissing = false)
    {
        EventType = eventType;
        _displayNameKey = displayNameKey;
        _categoryDisplayNameKey = categoryDisplayNameKey;
        _descriptionKey = descriptionKey;
        _eventTypeFallback = eventTypeFallback;
        _categoryFallback = categoryFallback;
        _descriptionFallback = descriptionFallback;
        _localize = localize;
        PayloadFields = payloadFields;
        SupportedUsages = supportedUsages;
        IsMissing = isMissing;

        var category = localize(categoryDisplayNameKey, categoryFallback);
        _categoryDisplayName = category;
        _displayName = $"{category} / {localize(displayNameKey, eventTypeFallback)}";
        _description = localize(descriptionKey, descriptionFallback);
    }

    /// <summary>
    /// 获取规范事件类型。
    /// </summary>
    public string EventType { get; }

    /// <summary>
    /// 获取事件所属的运行时触发链路。
    /// </summary>
    public FrontedBehaviorEventUsage SupportedUsages { get; }

    /// <summary>
    /// 获取该选项是否代表当前未注册的插件事件。
    /// </summary>
    public bool IsMissing { get; }

    /// <summary>
    /// 获取可用于过滤器的负载字段。
    /// </summary>
    public IReadOnlyList<BehaviorPayloadFieldOptionViewModel> PayloadFields { get; }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public string CategoryDisplayName
    {
        get => _categoryDisplayName;
        set => SetProperty(ref _categoryDisplayName, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    /// <summary>
    /// 通过本地化函数重新解析所有本地化显示字符串。
    /// </summary>
    public void Refresh()
    {
        CategoryDisplayName = _localize(_categoryDisplayNameKey, _categoryFallback);
        DisplayName = $"{CategoryDisplayName} / {_localize(_displayNameKey, _eventTypeFallback)}";
        Description = _localize(_descriptionKey, _descriptionFallback);
        foreach (var field in PayloadFields)
        {
            field.Refresh();
        }
    }
}

public sealed class BehaviorPayloadFieldOptionViewModel : ObservableObject
{
    private readonly string _displayNameKey;
    private readonly string _descriptionKey;
    private readonly string _pathOrTypeFallback;
    private readonly string _displayNameFallback;
    private readonly string _descriptionFallback;
    private readonly Func<string, string, string> _localize;
    private string _displayName;
    private string _description;

    /// <summary>
    /// 使用 3.0/3.1 版本的构造函数签名初始化负载字段选项。
    /// </summary>
    /// <param name="path">字段路径。</param>
    /// <param name="displayNameKey">显示名称本地化键。</param>
    /// <param name="descriptionKey">描述本地化键。</param>
    /// <param name="typeName">字段类型名称。</param>
    /// <param name="enumValues">稳定枚举名称。</param>
    /// <param name="isUnknown">是否为保留的未知字段。</param>
    /// <param name="isCommonFilterTarget">是否为常用过滤字段。</param>
    /// <param name="localize">本地化函数。</param>
    public BehaviorPayloadFieldOptionViewModel(
        string path,
        string displayNameKey,
        string descriptionKey,
        string typeName,
        IReadOnlyList<string>? enumValues,
        bool isUnknown,
        bool isCommonFilterTarget,
        Func<string, string, string> localize)
        : this(
            path,
            displayNameKey,
            descriptionKey,
            typeName,
            path,
            path,
            enumValues,
            isUnknown,
            isCommonFilterTarget,
            localize)
    {
    }

    public BehaviorPayloadFieldOptionViewModel(
        string path,
        string displayNameKey,
        string descriptionKey,
        string typeName,
        string displayNameFallback,
        string descriptionFallback,
        IReadOnlyList<string>? enumValues,
        bool isUnknown,
        bool isCommonFilterTarget,
        Func<string, string, string> localize)
    {
        Path = path;
        _displayNameKey = displayNameKey;
        _descriptionKey = descriptionKey;
        _pathOrTypeFallback = typeName;
        _displayNameFallback = displayNameFallback;
        _descriptionFallback = descriptionFallback;
        _localize = localize;
        TypeName = typeName;
        EnumValues = enumValues ?? [];
        IsUnknown = isUnknown;
        IsCommonFilterTarget = isCommonFilterTarget;
        if (isUnknown)
        {
            _displayName = string.Format(
                localize(displayNameKey, "Unknown parameter: {0}"),
                path);
        }
        else
        {
            _displayName = localize(displayNameKey, displayNameFallback);
        }
        _description = localize(descriptionKey, descriptionFallback);
    }

    public string Path { get; }
    public string TypeName { get; }
    /// <summary>获取该 payload 字段可用的稳定枚举名称。</summary>
    public IReadOnlyList<string> EnumValues { get; }
    public bool IsUnknown { get; }
    public bool IsCommonFilterTarget { get; }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    /// <summary>
    /// 通过本地化函数重新解析所有本地化显示字符串。
    /// </summary>
    public void Refresh()
    {
        if (IsUnknown)
        {
            DisplayName = string.Format(
                _localize(_displayNameKey, "Unknown parameter: {0}"),
                Path);
        }
        else
        {
            DisplayName = _localize(_displayNameKey, _displayNameFallback);
            Description = _localize(_descriptionKey, _descriptionFallback);
        }
    }
}
