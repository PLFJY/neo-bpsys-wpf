namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// 背景染色模式。
/// </summary>
public enum BackgroundTintMode
{
    /// <summary>
    /// 基于亮度的颜色化。
    /// </summary>
    LuminanceColorize,

    /// <summary>
    /// 正片叠底。
    /// </summary>
    Multiply,

    /// <summary>
    /// 基色叠加纹理。
    /// </summary>
    BaseColorWithTexture
}
