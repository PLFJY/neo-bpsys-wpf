using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// 控制图片覆盖层何时可见。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FrontedOverlayVisibilityMode
{
    /// <summary>
    /// 绑定值为 true 时可见。
    /// </summary>
    VisibleWhenTrue,

    /// <summary>
    /// 绑定值为 false 时可见。
    /// </summary>
    VisibleWhenFalse,

    /// <summary>
    /// 始终可见。
    /// </summary>
    Always
}
