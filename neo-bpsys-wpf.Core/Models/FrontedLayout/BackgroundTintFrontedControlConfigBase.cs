namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// 背景染色控件配置基类。
/// </summary>
public abstract class BackgroundTintFrontedControlConfigBase : FrontedControlConfigBase
{
    /// <summary>
    /// 染色颜色值。
    /// </summary>
    public string? TintColor { get; set; } = "#FFFFFFFF";

    /// <summary>
    /// 染色绑定的属性路径。
    /// </summary>
    public string? TintBindingPath { get; set; }

    /// <summary>
    /// 染色模式。
    /// </summary>
    public BackgroundTintMode TintMode { get; set; } = BackgroundTintMode.LuminanceColorize;

    /// <summary>
    /// 染色强度。
    /// </summary>
    public double TintStrength { get; set; } = 1D;

    /// <summary>
    /// 纹理强度。
    /// </summary>
    public double TextureStrength { get; set; } = 0.45D;

    /// <summary>
    /// 是否在缺少背景时显示占位符。
    /// </summary>
    public bool ShowMissingBackgroundPlaceholder { get; set; } = true;
}
