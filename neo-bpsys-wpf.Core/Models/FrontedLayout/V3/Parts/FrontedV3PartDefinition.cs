using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Parts;

/// <summary>
/// 固定 Part 的已解析定义，描述 Part 的标识、存储位置与操作能力。
/// </summary>
/// <remarks>
/// <para>
/// Part 是控件内部固定区域的抽象（如 BorderedImage 的内层 Image、MapV2 的 TeamName 部件）。
/// 每个 Part 通过 <see cref="Id"/> 标识，几何值通过 <see cref="XStorage"/>/<see cref="YStorage"/>/
/// <see cref="WidthStorage"/>/<see cref="HeightStorage"/> 读写到 Config 的现有字段。
/// </para>
/// <para>
/// 存储访问器为 <see langword="null"/> 时表示该维度不可持久化（如 BorderedImage 内层 Image 的 X/Y
/// 由对齐方式决定，不存储到 Config）。
/// </para>
/// <para>
/// <see cref="Capabilities"/> 决定 Designer 中允许对该 Part 执行的操作类型；
/// <see cref="IFrontedV3GeometryTarget"/> 实现必须遵守能力约束。
/// </para>
/// </remarks>
public sealed class FrontedV3PartDefinition
{
    /// <summary>
    /// 初始化 <see cref="FrontedV3PartDefinition"/>。
    /// </summary>
    public FrontedV3PartDefinition()
    {
    }

    /// <summary>
    /// 初始化 <see cref="FrontedV3PartDefinition"/> 并指定全部属性。
    /// </summary>
    /// <param name="id">Part 标识，在同一控件内必须唯一。</param>
    /// <param name="capabilities">Part 的操作能力。</param>
    /// <param name="widthStorage">宽度存储访问器；为 <see langword="null"/> 时宽度不可持久化。</param>
    /// <param name="heightStorage">高度存储访问器；为 <see langword="null"/> 时高度不可持久化。</param>
    /// <param name="xStorage">X 坐标存储访问器；为 <see langword="null"/> 时 X 不可持久化。</param>
    /// <param name="yStorage">Y 坐标存储访问器；为 <see langword="null"/> 时 Y 不可持久化。</param>
    public FrontedV3PartDefinition(
        string id,
        FrontedV3PartCapabilities capabilities,
        IFrontedV3StorageAccessor? widthStorage = null,
        IFrontedV3StorageAccessor? heightStorage = null,
        IFrontedV3StorageAccessor? xStorage = null,
        IFrontedV3StorageAccessor? yStorage = null)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
        WidthStorage = widthStorage;
        HeightStorage = heightStorage;
        XStorage = xStorage;
        YStorage = yStorage;
    }

    /// <summary>
    /// 获取或设置 Part 标识，在同一控件内必须唯一（例如 <c>Image</c>、<c>Logo</c>）。
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置 Part 的操作能力，决定 Designer 中允许的几何操作类型。
    /// </summary>
    public FrontedV3PartCapabilities Capabilities { get; set; } = FrontedV3PartCapabilities.None;

    /// <summary>
    /// 获取或设置宽度存储访问器；为 <see langword="null"/> 时宽度不可持久化。
    /// </summary>
    public IFrontedV3StorageAccessor? WidthStorage { get; set; }

    /// <summary>
    /// 获取或设置高度存储访问器；为 <see langword="null"/> 时高度不可持久化。
    /// </summary>
    public IFrontedV3StorageAccessor? HeightStorage { get; set; }

    /// <summary>
    /// 获取或设置 X 坐标存储访问器；为 <see langword="null"/> 时 X 不可持久化。
    /// </summary>
    public IFrontedV3StorageAccessor? XStorage { get; set; }

    /// <summary>
    /// 获取或设置 Y 坐标存储访问器；为 <see langword="null"/> 时 Y 不可持久化。
    /// </summary>
    public IFrontedV3StorageAccessor? YStorage { get; set; }

    /// <summary>
    /// 获取或设置 Part 的外观属性定义列表；为 <see langword="null"/> 时表示该 Part 仅支持几何编辑。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 当 Part 在 Designer 中被选中时，<see cref="FrontedV3DesignSelectionBuilder.BuildFixedPartSelection"/>
    /// 会将几何属性（<see cref="XStorage"/>/<see cref="YStorage"/>/<see cref="WidthStorage"/>/<see cref="HeightStorage"/>
    /// 对应的属性）与该列表中的外观属性合并，作为选中目标的 <c>Properties</c> 返回。
    /// </para>
    /// <para>
    /// 该列表中的 <see cref="FrontedV3PropertyDefinition"/> 必须已配置好
    /// <see cref="FrontedV3PropertyDefinition.Storage"/>（通常使用
    /// <see cref="FrontedV3Storage.ClrProperty"/> 读写父 Config 的 CLR 属性，
    /// 或使用 <see cref="FrontedV3Storage.CollectionItemProperty"/> 读写 Part 在父 Config 列表中的项属性），
    /// 因为固定 Part 不需要 itemKey 参数。
    /// </para>
    /// <para>
    /// 默认 <see langword="null"/> 表示该 Part 只支持几何编辑（如 MapV2 的内部部件、BorderedImage 的 Image）。
    /// </para>
    /// </remarks>
    public IReadOnlyList<FrontedV3PropertyDefinition>? Properties { get; set; }
}
