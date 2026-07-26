namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// 背景染色矩形控件配置。
/// </summary>
public class BackgroundTintRectangleFrontedControlConfig : BackgroundTintFrontedControlConfigBase
{
    /// <summary>
    /// 初始化背景染色矩形控件配置。
    /// </summary>
    public BackgroundTintRectangleFrontedControlConfig()
    {
        ControlType = "BackgroundTintRectangle";
    }

    /// <summary>
    /// 圆角 X 轴半径。
    /// </summary>
    public double RadiusX { get; set; }

    /// <summary>
    /// 圆角 Y 轴半径。
    /// </summary>
    public double RadiusY { get; set; }
}
