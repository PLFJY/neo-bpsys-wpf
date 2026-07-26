using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Designer.V3;

/// <summary>
/// Designer 的统一选中目标，可表示根控件、固定 Part 或 PartCollection 集合项三类选中。
/// </summary>
/// <remarks>
/// <para>
/// Phase 6 Designer 去特化的核心契约：Designer 只维护
/// <c>FrontedV3DesignSelection? SelectedTarget</c>，所有 Move/Resize/Snap/Clamp/Undo
/// 只调用 <see cref="GeometryTarget"/>，所有属性编辑只读写 <see cref="Properties"/>。
/// </para>
/// <para>
/// 三类 <see cref="FrontedV3DesignSelectionKind"/> 对应三种 selection：
/// <list type="bullet">
/// <item><see cref="FrontedV3DesignSelectionKind.Root"/>：根控件属性，由 <c>RootControlGeometryTarget</c> 驱动几何。</item>
/// <item><see cref="FrontedV3DesignSelectionKind.FixedPart"/>：固定 Part 属性，由 <c>FixedPartGeometryTarget</c> 驱动几何。</item>
/// <item><see cref="FrontedV3DesignSelectionKind.CollectionItem"/>：PartCollection 集合项属性，由 <c>CollectionItemGeometryTarget</c> 驱动几何。</item>
/// </list>
/// </para>
/// <para>
/// 该类型不缓存独立属性值；属性读写都通过 <see cref="Properties"/> 中的
/// <see cref="FrontedV3PropertyDefinition.Storage"/> 完成以保持单一事实来源。
/// </para>
/// </remarks>
public sealed class FrontedV3DesignSelection
{
    /// <summary>
    /// 初始化 <see cref="FrontedV3DesignSelection"/>。
    /// </summary>
    /// <param name="kind">选中目标的类别。</param>
    /// <param name="designItem">所属设计项（根选中时为当前控件，Part/CollectionItem 选中时为父控件）。</param>
    /// <param name="geometryTarget">几何操作目标，所有 Move/Resize/Snap/Clamp/Undo 通过该目标执行。</param>
    /// <param name="properties">该选中目标可编辑的属性定义列表；为空表示无 Schema 可编辑属性。</param>
    /// <param name="subTarget">可选的子目标描述（Part Id 或 Collection Item Key）。</param>
    /// <exception cref="ArgumentNullException">
    /// 当 <paramref name="designItem"/>、<paramref name="geometryTarget"/> 或 <paramref name="properties"/> 为 <see langword="null"/> 时抛出。
    /// </exception>
    public FrontedV3DesignSelection(
        FrontedV3DesignSelectionKind kind,
        FrontedControlDesignItem designItem,
        IFrontedV3GeometryTarget geometryTarget,
        IReadOnlyList<FrontedV3PropertyDefinition> properties,
        FrontedV3DesignSubTarget? subTarget = null)
    {
        ArgumentNullException.ThrowIfNull(designItem);
        ArgumentNullException.ThrowIfNull(geometryTarget);
        ArgumentNullException.ThrowIfNull(properties);

        Kind = kind;
        DesignItem = designItem;
        GeometryTarget = geometryTarget;
        Properties = properties;
        SubTarget = subTarget;
    }

    /// <summary>
    /// 获取选中目标的类别。
    /// </summary>
    public FrontedV3DesignSelectionKind Kind { get; }

    /// <summary>
    /// 获取所属设计项。根选中时为当前控件；Part/CollectionItem 选中时为父控件。
    /// </summary>
    public FrontedControlDesignItem DesignItem { get; }

    /// <summary>
    /// 获取几何操作目标。所有 Move/Resize/Snap/Clamp/Undo 通过该目标执行，
    /// Designer 不通过 <c>if (config is BorderedImage...)</c> 等类型分支选择几何实现。
    /// </summary>
    public IFrontedV3GeometryTarget GeometryTarget { get; }

    /// <summary>
    /// 获取该选中目标可编辑的属性定义列表。属性编辑直接调用
    /// <see cref="FrontedV3PropertyDefinition.SetValue"/>，
    /// 不通过 propertyName 字符串反射写入。
    /// </summary>
    public IReadOnlyList<FrontedV3PropertyDefinition> Properties { get; }

    /// <summary>
    /// 获取可选的子目标描述。Part 选中时为 Part Id；CollectionItem 选中时为 Item Key；根选中时为 <see langword="null"/>。
    /// </summary>
    public FrontedV3DesignSubTarget? SubTarget { get; }

    /// <summary>
    /// 获取是否具有可编辑的 Schema 属性。无 Schema 属性时 Designer 应显示只读诊断视图。
    /// </summary>
    public bool HasEditableSchema => Properties.Count > 0;

    /// <summary>
    /// 创建根控件选中目标。
    /// </summary>
    /// <param name="designItem">选中的设计项。</param>
    /// <param name="geometryTarget">根控件几何目标。</param>
    /// <param name="properties">根控件属性定义列表。</param>
    /// <returns>根控件选中目标实例。</returns>
    /// <exception cref="ArgumentNullException">当任一参数为 <see langword="null"/> 时抛出。</exception>
    public static FrontedV3DesignSelection ForRoot(
        FrontedControlDesignItem designItem,
        IFrontedV3GeometryTarget geometryTarget,
        IReadOnlyList<FrontedV3PropertyDefinition> properties)
        => new(FrontedV3DesignSelectionKind.Root, designItem, geometryTarget, properties);

    /// <summary>
    /// 创建固定 Part 选中目标。
    /// </summary>
    /// <param name="designItem">父控件设计项。</param>
    /// <param name="geometryTarget">Part 几何目标。</param>
    /// <param name="properties">Part 属性定义列表。</param>
    /// <param name="partId">Part 标识。</param>
    /// <returns>固定 Part 选中目标实例。</returns>
    /// <exception cref="ArgumentNullException">当任一参数为 <see langword="null"/> 时抛出。</exception>
    public static FrontedV3DesignSelection ForFixedPart(
        FrontedControlDesignItem designItem,
        IFrontedV3GeometryTarget geometryTarget,
        IReadOnlyList<FrontedV3PropertyDefinition> properties,
        string partId)
    {
        ArgumentNullException.ThrowIfNull(partId);
        return new FrontedV3DesignSelection(
            FrontedV3DesignSelectionKind.FixedPart,
            designItem,
            geometryTarget,
            properties,
            new FrontedV3FixedPartTarget(partId));
    }

    /// <summary>
    /// 创建 PartCollection 集合项选中目标。
    /// </summary>
    /// <param name="designItem">父控件设计项。</param>
    /// <param name="geometryTarget">集合项几何目标。</param>
    /// <param name="properties">集合项属性定义列表。</param>
    /// <param name="collectionId">集合标识。</param>
    /// <param name="itemKey">集合项唯一键。</param>
    /// <returns>集合项选中目标实例。</returns>
    /// <exception cref="ArgumentNullException">当任一参数为 <see langword="null"/> 时抛出。</exception>
    public static FrontedV3DesignSelection ForCollectionItem(
        FrontedControlDesignItem designItem,
        IFrontedV3GeometryTarget geometryTarget,
        IReadOnlyList<FrontedV3PropertyDefinition> properties,
        string collectionId,
        string itemKey)
    {
        ArgumentNullException.ThrowIfNull(collectionId);
        ArgumentNullException.ThrowIfNull(itemKey);
        return new FrontedV3DesignSelection(
            FrontedV3DesignSelectionKind.CollectionItem,
            designItem,
            geometryTarget,
            properties,
            new FrontedV3CollectionItemTarget(collectionId, itemKey));
    }
}
