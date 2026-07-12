namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;

/// <summary>
/// 设计器 v3 绑定目标的预期值类别。
/// </summary>
public enum FrontedBindingTargetKind
{
    /// <summary>
    /// 接受任何可选的绑定值。
    /// </summary>
    Any,

    /// <summary>
    /// 接受适合文本显示的字符串和数值。
    /// </summary>
    Text,

    /// <summary>
    /// 接受图像源值。
    /// </summary>
    Image,

    /// <summary>
    /// 接受对局进度枚举值。
    /// </summary>
    GameProgress,

    /// <summary>
    /// 接受地图枚举值。
    /// </summary>
    Map,

    /// <summary>
    /// 接受布尔值。
    /// </summary>
    Boolean,

    /// <summary>
    /// 接受数值。
    /// </summary>
    Number,

    /// <summary>
    /// 接受字符串值。
    /// </summary>
    String,

    /// <summary>
    /// 接受天赋模型值。
    /// </summary>
    Talent,

    /// <summary>
    /// 接受特质模型值。
    /// </summary>
    Trait
}
