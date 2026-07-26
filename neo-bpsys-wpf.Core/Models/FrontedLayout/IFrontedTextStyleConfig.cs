namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// 可接收旧版文本样式的 v3 类文本控件配置。
/// </summary>
public interface IFrontedTextStyleConfig
{
    /// <summary>
    /// 字体族。
    /// </summary>
    string? FontFamily { get; set; }

    /// <summary>
    /// 字重。
    /// </summary>
    string? FontWeight { get; set; }

    /// <summary>
    /// 文本颜色。
    /// </summary>
    string? Color { get; set; }

    /// <summary>
    /// 字号。
    /// </summary>
    double FontSize { get; set; }
}
