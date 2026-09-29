using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// .bpui 布局依赖某插件的原因。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FrontedPluginDependencyReason
{
    /// <summary>
    /// 依赖来源未知，或包未提供。
    /// </summary>
    Unknown,

    /// <summary>
    /// 包含一个或多个 <c>ControlType</c> 属于该插件的控件。
    /// </summary>
    FrontedControl,

    /// <summary>
    /// 包含一个或多个该插件的插件窗口布局。
    /// </summary>
    FrontedWindow,

    /// <summary>
    /// 包同时因插件控件和插件窗口布局而需要该插件。
    /// </summary>
    Both,

    /// <summary>
    /// 行为文档引用了该插件注册的语义事件。
    /// </summary>
    BehaviorEvent,

    /// <summary>
    /// 依赖来自三个来源中的至少两个，或包含无法再用 <see cref="Both"/> 精确表达的组合。
    /// </summary>
    Multiple
}
