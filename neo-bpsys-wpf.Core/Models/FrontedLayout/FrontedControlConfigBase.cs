namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// v3 前台控件配置基类。
/// </summary>
public class FrontedControlConfigBase
{
    /// <summary>
    /// 控件类型。
    /// </summary>
    public string ControlType { get; set; } = string.Empty;

    /// <summary>
    /// Canvas 左侧坐标。
    /// </summary>
    public double Left { get; set; }

    /// <summary>
    /// Canvas 顶部坐标。
    /// </summary>
    public double Top { get; set; }

    /// <summary>
    /// 控件宽度。
    /// </summary>
    public double? Width { get; set; }

    /// <summary>
    /// 控件高度。
    /// </summary>
    public double? Height { get; set; }

    /// <summary>
    /// Canvas 层级。
    /// </summary>
    public int ZIndex { get; set; }

    /// <summary>
    /// 绑定到共享数据服务的属性路径。
    /// </summary>
    public string? BindingPath { get; set; }
}
