namespace neo_bpsys_wpf.Core.Models.FrontedLayout.V3.StyleTransfer;

/// <summary>
/// v3 前台控件样式组件的分类，用于对 StyleTransfer 传播范围进行更细粒度的分组。
/// </summary>
/// <remarks>
/// <para>
/// 该分类是 <see cref="FrontedV3PropertySemantic"/> 的补充：Semantic 决定属性是否参与传播，
/// 而 Component 决定属性属于哪个样式组件（字体、颜色、边框等），便于未来实现基于组件的细粒度传播控制。
/// </para>
/// <para>
/// Phase 5 的核心传播逻辑基于 <see cref="FrontedV3PropertySemantic"/>，
/// <see cref="FrontedV3StyleComponent"/> 作为元数据附加到属性上，供 Designer UI 分组和未来扩展使用。
/// </para>
/// </remarks>
public enum FrontedV3StyleComponent
{
    /// <summary>
    /// 未分类，默认值。
    /// </summary>
    None,

    /// <summary>
    /// 字体相关属性（FontFamily、FontSize、FontWeight 等）。
    /// </summary>
    Font,

    /// <summary>
    /// 颜色相关属性（TextColor、BackgroundColor、BorderColor 等）。
    /// </summary>
    Color,

    /// <summary>
    /// 边框相关属性（BorderBrush、BorderThickness、CornerRadius 等）。
    /// </summary>
    Border,

    /// <summary>
    /// 图片相关属性（ImagePath、ImageWidth、ImageHeight 等）。
    /// </summary>
    Image,

    /// <summary>
    /// 几何相关属性（内部部件的 X/Y/Width/Height）。
    /// </summary>
    Geometry,

    /// <summary>
    /// 文本内容属性（TeamName、MapName 等）。
    /// </summary>
    Text,

    /// <summary>
    /// 行为相关属性（BehaviorGuid 等）。
    /// </summary>
    Behavior,

    /// <summary>
    /// 其他属性。
    /// </summary>
    Other
}
