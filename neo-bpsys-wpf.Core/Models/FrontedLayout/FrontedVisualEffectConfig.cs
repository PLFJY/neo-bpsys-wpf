using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// 描述可应用于生成的可视化元素的可选视觉效果。
/// </summary>
public sealed class FrontedVisualEffectConfig
{
    /// <summary>
    /// 获取或设置效果类型。
    /// </summary>
    public FrontedVisualEffectKind Kind { get; set; } = FrontedVisualEffectKind.None;

    /// <summary>
    /// 获取或设置效果颜色文本。
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// 获取或设置效果不透明度。
    /// </summary>
    public double Opacity { get; set; } = 1D;

    /// <summary>
    /// 获取或设置效果模糊半径。
    /// </summary>
    public double BlurRadius { get; set; }

    /// <summary>
    /// 获取或设置投影深度。
    /// </summary>
    public double ShadowDepth { get; set; }

    /// <summary>
    /// 获取或设置投影方向。
    /// </summary>
    public double Direction { get; set; }
}

/// <summary>
/// 支持的视觉效果类型。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FrontedVisualEffectKind
{
    /// <summary>
    /// 无视觉效果。
    /// </summary>
    None,

    /// <summary>
    /// 使用零深度投影实现的发光效果。
    /// </summary>
    Glow,

    /// <summary>
    /// WPF 投影效果。
    /// </summary>
    DropShadow
}
