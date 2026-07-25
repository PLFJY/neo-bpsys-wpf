namespace neo_bpsys_wpf.Core.Models.FrontedLayout.V3.StyleTransfer;

/// <summary>
/// v3 前台控件属性的语义分类，驱动 StyleTransfer 的传播范围与 PropertyGrid 的分组。
/// </summary>
/// <remarks>
/// <para>
/// 语义分类决定属性在 StyleTransfer 操作中的行为：
/// <list type="bullet">
/// <item><see cref="Appearance"/>：视觉样式属性（颜色、字体、边框等），默认参与传播。</item>
/// <item><see cref="DataIdentity"/>：数据身份字段（MapKey、TeamType、BindingPath、ControlName 等），永不传播。</item>
/// <item><see cref="RootSize"/>：根控件尺寸（Width/Height），仅当 profile 显式开启时传播。</item>
/// <item><see cref="PartLayout"/>：固定内部部件几何（X/Y/Width/Height），仅当 profile 显式开启时传播。</item>
/// <item><see cref="Behaviors"/>：行为设置，仅当 profile 显式开启时传播。</item>
/// <item><see cref="Effects"/>：视觉效果（模糊等），仅当 profile 显式开启时传播。</item>
/// <item><see cref="Other"/>：未分类属性，不参与传播。</item>
/// </list>
/// </para>
/// <para>
/// 根级保留字段（<c>Left</c>/<c>Top</c>/<c>ZIndex</c> 等）由
/// <see cref="neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties.FrontedV3ReservedFields"/> 管理，
/// 不会注册为属性，因此不需要单独的语义分类。
/// </para>
/// </remarks>
public enum FrontedV3PropertySemantic
{
    /// <summary>
    /// 视觉样式属性（颜色、字体、边框、图片路径等），默认参与 StyleTransfer 传播。
    /// </summary>
    Appearance,

    /// <summary>
    /// 数据身份字段（MapKey、TeamType、BindingPath、ControlName 等），永不参与传播。
    /// </summary>
    /// <remarks>
    /// 传播数据身份字段会导致控件指向错误的数据源，因此该语义的属性在任何 profile 下都不会被传播。
    /// </remarks>
    DataIdentity,

    /// <summary>
    /// 根控件尺寸（Width/Height），仅当 <see cref="FrontedV3StyleTransferProfile"/> 显式开启时传播。
    /// </summary>
    RootSize,

    /// <summary>
    /// 固定内部部件几何（Part 的 X/Y/Width/Height），仅当 profile 显式开启时传播。
    /// </summary>
    PartLayout,

    /// <summary>
    /// 行为设置（BehaviorGuid 等），仅当 profile 显式开启时传播。
    /// </summary>
    Behaviors,

    /// <summary>
    /// 视觉效果（高斯模糊等），仅当 profile 显式开启时传播。
    /// </summary>
    Effects,

    /// <summary>
    /// 未分类属性，不参与传播。
    /// </summary>
    Other
}
