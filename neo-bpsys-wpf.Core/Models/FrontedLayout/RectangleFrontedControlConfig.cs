namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// 矩形控件配置。
/// </summary>
public class RectangleFrontedControlConfig : ShapeFrontedControlConfigBase
{
    /// <summary>
    /// 初始化矩形控件配置。
    /// </summary>
    public RectangleFrontedControlConfig()
    {
        ControlType = "Rectangle";
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
