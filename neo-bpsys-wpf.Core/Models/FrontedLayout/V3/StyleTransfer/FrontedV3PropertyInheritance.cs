namespace neo_bpsys_wpf.Core.Models.FrontedLayout.V3.StyleTransfer;

/// <summary>
/// v3 前台控件属性的继承模式，决定子控件如何从父控件获取属性值。
/// </summary>
/// <remarks>
/// <para>
/// 支持四种继承模式：
/// <list type="bullet">
/// <item><see cref="None"/>：不继承，子控件独立持有自己的值。</item>
/// <item><see cref="ParentFallback"/>：动态读取——子控件有 override 时使用 override，
/// 没有时回退到父控件同 OptionsPath 的值。该模式<b>不</b>在创建 View 时复制 fallback 值，
/// 而是每次读取时动态判断。</item>
/// <item><see cref="CopyFromParentOnCreate"/>：创建时从父控件复制值，之后子控件独立持有。
/// 复制后与父控件不再有动态关联。</item>
/// <item><see cref="LockedToParent"/>：锁定到父控件值，拒绝 override。子控件的值始终等于父控件的值，
/// 任何对子控件的写入都会被拒绝。</item>
/// </list>
/// </para>
/// <para>
/// <see cref="ParentFallback"/> 的关键约束：<b>必须动态读取</b>。不得在创建 View 时复制一份 fallback 值，
/// 否则父控件后续修改无法反映到子控件。
/// </para>
/// </remarks>
public enum FrontedV3PropertyInheritance
{
    /// <summary>
    /// 不继承。子控件独立持有自己的值，不读取父控件。
    /// </summary>
    None,

    /// <summary>
    /// 父回退模式。子控件有 override 时使用 override，没有时动态回退到父控件同 OptionsPath 的值。
    /// </summary>
    /// <remarks>
    /// 该模式每次读取时动态判断子控件是否有 override，不缓存 fallback 值。
    /// </remarks>
    ParentFallback,

    /// <summary>
    /// 创建时从父控件复制。子控件在创建时从父控件复制值，之后独立持有，不再动态关联父控件。
    /// </summary>
    CopyFromParentOnCreate,

    /// <summary>
    /// 锁定到父控件。子控件的值始终等于父控件的值，任何对子控件的 override 写入都会被拒绝。
    /// </summary>
    LockedToParent
}
