namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// v3 本地化静态文本控件配置。
/// </summary>
public class LocalizedTextControlConfig : FrontedControlConfigBase
{
    /// <summary>
    /// 初始化本地化静态文本控件配置。
    /// </summary>
    public LocalizedTextControlConfig()
    {
        ControlType = "LocalizedText";
    }

    /// <summary>
    /// 本地化资源 key。
    /// </summary>
    public string LocalizationKey { get; set; } = string.Empty;

    /// <summary>
    /// 资源 key 缺失时显示的备用文本。
    /// </summary>
    public string? FallbackText { get; set; }

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

    /// <summary>
    /// 文本对齐。
    /// </summary>
    public string? TextAlignment { get; set; }

    /// <summary>
    /// 文本块水平对齐。
    /// </summary>
    public string? HorizontalAlignment { get; set; }

    /// <summary>
    /// 文本块垂直对齐。
    /// </summary>
    public string? VerticalAlignment { get; set; }

    /// <summary>
    /// 文本换行方式。
    /// </summary>
    public string? TextWrapping { get; set; }
}
