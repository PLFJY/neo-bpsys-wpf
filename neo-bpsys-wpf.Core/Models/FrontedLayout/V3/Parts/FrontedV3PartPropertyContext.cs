using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Parts;

/// <summary>
/// 固定 Part 的属性编辑上下文，携带 Part 定义与关联 Config，供 Designer 属性网格使用。
/// </summary>
/// <remarks>
/// <para>
/// 当用户在 Designer 中选中某个固定 Part 时，属性网格根据该上下文构建编辑行，
/// 编辑操作通过 <see cref="PartDefinition"/> 的存储访问器写回 <see cref="Config"/>。
/// </para>
/// <para>
/// 该上下文不缓存独立值，所有读写最终作用于 <see cref="Config"/> 的现有字段。
/// </para>
/// </remarks>
public sealed class FrontedV3PartPropertyContext
{
    /// <summary>
    /// 初始化 <see cref="FrontedV3PartPropertyContext"/>。
    /// </summary>
    /// <param name="partDefinition">Part 定义。</param>
    /// <param name="config">关联的控件配置实例。</param>
    /// <exception cref="ArgumentNullException">当参数为 <see langword="null"/> 时抛出。</exception>
    public FrontedV3PartPropertyContext(
        FrontedV3PartDefinition partDefinition,
        FrontedControlConfigBase config)
    {
        ArgumentNullException.ThrowIfNull(partDefinition);
        ArgumentNullException.ThrowIfNull(config);
        PartDefinition = partDefinition;
        Config = config;
    }

    /// <summary>
    /// 获取 Part 定义。
    /// </summary>
    public FrontedV3PartDefinition PartDefinition { get; }

    /// <summary>
    /// 获取关联的控件配置实例，作为 Part 几何字段的单一事实来源。
    /// </summary>
    public FrontedControlConfigBase Config { get; }
}
