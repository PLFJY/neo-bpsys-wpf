namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Designer.V3;

/// <summary>
/// Designer 选中目标的类别，对应 PropertyGrid 的三类 Schema 构造。
/// </summary>
/// <remarks>
/// Phase 6 Designer 去特化后，PropertyGrid 只根据 Schema 构造
/// <see cref="Root"/>/<see cref="FixedPart"/>/<see cref="CollectionItem"/> 三类 selection，
/// 不再通过 <c>if (config is BorderedImage...)</c> 等类型分支选择属性构造路径。
/// </remarks>
public enum FrontedV3DesignSelectionKind
{
    /// <summary>
    /// 根控件选中，对应根控件属性 Schema。
    /// </summary>
    Root,

    /// <summary>
    /// 固定 Part 选中，对应 Part 属性 Schema。
    /// </summary>
    FixedPart,

    /// <summary>
    /// PartCollection 集合项选中，对应集合项属性 Schema。
    /// </summary>
    CollectionItem
}
