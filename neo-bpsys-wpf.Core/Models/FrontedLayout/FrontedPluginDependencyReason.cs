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
    Both
}
