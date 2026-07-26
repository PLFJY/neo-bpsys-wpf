namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// 形状控件配置基类。
/// </summary>
public abstract class ShapeFrontedControlConfigBase : FrontedControlConfigBase
{
    /// <summary>
    /// 填充模式。
    /// </summary>
    public ShapeFillMode FillMode { get; set; } = ShapeFillMode.Solid;

    /// <summary>
    /// 是否启用渐变填充。启用时 FillColor 作为渐变起始色，GradientEndColor 可见。
    /// </summary>
    public bool UseGradient { get; set; }

    /// <summary>
    /// 是否使用填充颜色绑定。
    /// </summary>
    public bool UseFillBinding { get; set; }

    /// <summary>
    /// 填充颜色。
    /// </summary>
    public string? FillColor { get; set; } = "#FFFFFFFF";

    /// <summary>
    /// 填充颜色绑定的属性路径。
    /// </summary>
    public string? FillBindingPath { get; set; }

    /// <summary>
    /// 是否使用渐变起始色绑定。
    /// </summary>
    public bool UseGradientStartBinding { get; set; }

    /// <summary>
    /// 渐变起始颜色。
    /// </summary>
    public string? GradientStartColor { get; set; } = "#FFFFFFFF";

    /// <summary>
    /// 渐变起始颜色绑定的属性路径。
    /// </summary>
    public string? GradientStartBindingPath { get; set; }

    /// <summary>
    /// 是否使用渐变结束色绑定。
    /// </summary>
    public bool UseGradientEndBinding { get; set; }

    /// <summary>
    /// 渐变结束颜色。
    /// </summary>
    public string? GradientEndColor { get; set; } = "#00FFFFFF";

    /// <summary>
    /// 渐变结束颜色绑定的属性路径。
    /// </summary>
    public string? GradientEndBindingPath { get; set; }

    private double _gradientAngle;

    /// <summary>
    /// 渐变角度（0-360 度）。
    /// </summary>
    public double GradientAngle
    {
        get => _gradientAngle;
        set
        {
            if (!double.IsFinite(value))
            {
                _gradientAngle = 0;
                return;
            }

            var normalized = value % 360D;
            _gradientAngle = normalized < 0 ? normalized + 360D : normalized;
        }
    }

    /// <summary>
    /// 描边颜色。
    /// </summary>
    public string? StrokeColor { get; set; }

    /// <summary>
    /// 描边粗细。
    /// </summary>
    public double StrokeThickness { get; set; }
}
