using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout;

/// <summary>
/// 描述在前台控件内部渲染的生成可视化部件。
/// </summary>
public sealed class FrontedAnimationPartConfig
{
    /// <summary>
    /// 获取或设置稳定的用户自定义动画部件名称。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置生成的元素类型。
    /// </summary>
    public FrontedAnimationPartKind Kind { get; set; } = FrontedAnimationPartKind.Rectangle;

    /// <summary>
    /// 获取或设置部件是渲染在主内容下方还是上方。
    /// </summary>
    public FrontedAnimationPartLayer Layer { get; set; } = FrontedAnimationPartLayer.AboveContent;

    /// <summary>
    /// 获取或设置固定宽度（像素）。
    /// </summary>
    public double? Width { get; set; }

    /// <summary>
    /// 获取或设置固定高度（像素）。
    /// </summary>
    public double? Height { get; set; }

    /// <summary>
    /// 获取或设置可选的宽度表达式，例如 <c>100%</c>。
    /// </summary>
    public string? WidthText { get; set; }

    /// <summary>
    /// 获取或设置可选的高度表达式，例如 <c>100%</c>。
    /// </summary>
    public string? HeightText { get; set; }

    /// <summary>
    /// 获取或设置相对父控件的左侧偏移。
    /// </summary>
    public double Left { get; set; }

    /// <summary>
    /// 获取或设置相对父控件的顶部偏移。
    /// </summary>
    public double Top { get; set; }

    /// <summary>
    /// 获取或设置填充画刷文本。
    /// </summary>
    public string? Fill { get; set; }

    /// <summary>
    /// 获取或设置描边或边框画刷文本。
    /// </summary>
    public string? Stroke { get; set; }

    /// <summary>
    /// 获取或设置描边或边框粗细。
    /// </summary>
    public double StrokeThickness { get; set; }

    /// <summary>
    /// 获取或设置图片部件使用的图片资源路径。
    /// </summary>
    public string? ImagePath { get; set; }

    /// <summary>
    /// 获取或设置初始不透明度。
    /// </summary>
    public double Opacity { get; set; } = 1D;

    /// <summary>
    /// 获取或设置初始 WPF 可见性名称。
    /// </summary>
    public string Visibility { get; set; } = "Hidden";

    /// <summary>
    /// 获取或设置图层局部 Z 索引。
    /// </summary>
    public int ZIndex { get; set; }

    /// <summary>
    /// 获取或设置生成的部件是否参与命中测试。
    /// </summary>
    public bool IsHitTestVisible { get; set; }

    /// <summary>
    /// 获取或设置应用于生成部件的可选视觉效果。
    /// </summary>
    public FrontedVisualEffectConfig Effect { get; set; } = new();
}

/// <summary>
/// 支持的生成动画部件类型。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FrontedAnimationPartKind
{
    /// <summary>
    /// 填充且可选描边的矩形。
    /// </summary>
    Rectangle,

    /// <summary>
    /// 边框元素。
    /// </summary>
    Border,

    /// <summary>
    /// 图片元素。
    /// </summary>
    Image
}

/// <summary>
/// 生成动画部件使用的可视化层。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FrontedAnimationPartLayer
{
    /// <summary>
    /// 渲染在主控件内容后方。
    /// </summary>
    BelowContent,

    /// <summary>
    /// 渲染在主控件内容前方。
    /// </summary>
    AboveContent
}
