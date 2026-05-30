namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// v3 文本控件配置。
/// </summary>
public class TextFrontedControlConfig : FrontedControlConfigBase
{
    /// <summary>
    /// 初始化文本控件配置。
    /// </summary>
    public TextFrontedControlConfig()
    {
        ControlType = "Text";
    }

    /// <summary>
    /// 静态文本内容，仅在 BindingPath 为空时使用。
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// 文本块水平对齐。
    /// </summary>
    public string? HorizontalAlignment { get; set; }

    /// <summary>
    /// 文本块垂直对齐。
    /// </summary>
    public string? VerticalAlignment { get; set; }

    /// <summary>
    /// 文本对齐。
    /// </summary>
    public string? TextAlignment { get; set; }

    /// <summary>
    /// 文本换行方式。
    /// </summary>
    public string? TextWrapping { get; set; }

    /// <summary>
    /// 字体族。
    /// </summary>
    public string? FontFamily { get; set; }

    /// <summary>
    /// 字重。
    /// </summary>
    public string? FontWeight { get; set; }

    /// <summary>
    /// 文本颜色。
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// 字号。
    /// </summary>
    public double FontSize { get; set; }
}
