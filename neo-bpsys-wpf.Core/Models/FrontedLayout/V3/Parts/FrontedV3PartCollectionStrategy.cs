namespace neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Parts;

/// <summary>
/// PartCollection 的策略，决定集合项的增删与模板补齐行为。
/// </summary>
/// <remarks>
/// <para>
/// 三种预设策略：
/// <list type="bullet">
/// <item><see cref="FixedTemplate"/>：根据业务模板补齐缺失项，拒绝任意增删；可移动、缩放、编辑。</item>
/// <item><see cref="Dynamic"/>：允许任意增删集合项。</item>
/// <item><see cref="ReadOnly"/>：只读，不允许增删或几何操作。</item>
/// </list>
/// </para>
/// <para>
/// 典型用法：
/// <list type="bullet">
/// <item>GlobalScoreRow 的 Cells：<see cref="FixedTemplate"/>（BO3/BO5 模板补齐，拒绝增删）。</item>
/// <item>动态图层列表：<see cref="Dynamic"/>。</item>
/// <item>只读装饰集合：<see cref="ReadOnly"/>。</item>
/// </list>
/// </para>
/// </remarks>
public sealed class FrontedV3PartCollectionStrategy
{
    /// <summary>
    /// 获取是否允许用户添加集合项。
    /// </summary>
    public bool CanAdd { get; init; }

    /// <summary>
    /// 获取是否允许用户删除集合项。
    /// </summary>
    public bool CanDelete { get; init; }

    /// <summary>
    /// 获取是否根据业务模板自动补齐缺失项。
    /// </summary>
    public bool IsTemplateDriven { get; init; }

    /// <summary>
    /// 固定模板策略：根据业务模板补齐缺失项，拒绝任意增删；允许移动、缩放、编辑。
    /// </summary>
    public static FrontedV3PartCollectionStrategy FixedTemplate { get; } = new()
    {
        CanAdd = false,
        CanDelete = false,
        IsTemplateDriven = true
    };

    /// <summary>
    /// 动态策略：允许任意增删集合项。
    /// </summary>
    public static FrontedV3PartCollectionStrategy Dynamic { get; } = new()
    {
        CanAdd = true,
        CanDelete = true,
        IsTemplateDriven = false
    };

    /// <summary>
    /// 只读策略：不允许增删或几何操作。
    /// </summary>
    public static FrontedV3PartCollectionStrategy ReadOnly { get; } = new()
    {
        CanAdd = false,
        CanDelete = false,
        IsTemplateDriven = false
    };
}
