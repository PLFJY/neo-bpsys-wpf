using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Parts;

/// <summary>
/// PartCollection 集合项的属性编辑上下文，携带集合定义、关联 Config 与当前项键，供 Designer 属性网格使用。
/// </summary>
/// <remarks>
/// <para>
/// 当用户在 Designer 中选中某个集合项时，属性网格根据该上下文构建编辑行，
/// 编辑操作通过集合项的 CLR 属性写回 <see cref="Config"/> 的现有集合字段。
/// </para>
/// <para>
/// 该上下文不缓存独立值，所有读写最终作用于 <see cref="Config"/> 的现有集合项字段。
/// </para>
/// </remarks>
public sealed class FrontedV3PartCollectionPropertyContext
{
    /// <summary>
    /// 初始化 <see cref="FrontedV3PartCollectionPropertyContext"/>。
    /// </summary>
    /// <param name="collectionDefinition">集合定义。</param>
    /// <param name="config">关联的控件配置实例。</param>
    /// <param name="itemKey">当前选中集合项的唯一键。</param>
    /// <exception cref="ArgumentNullException">当参数为 <see langword="null"/> 时抛出。</exception>
    public FrontedV3PartCollectionPropertyContext(
        FrontedV3PartCollectionDefinition collectionDefinition,
        FrontedControlConfigBase config,
        string itemKey)
    {
        ArgumentNullException.ThrowIfNull(collectionDefinition);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(itemKey);
        CollectionDefinition = collectionDefinition;
        Config = config;
        ItemKey = itemKey;
    }

    /// <summary>
    /// 获取集合定义。
    /// </summary>
    public FrontedV3PartCollectionDefinition CollectionDefinition { get; }

    /// <summary>
    /// 获取关联的控件配置实例，作为集合项字段的单一事实来源。
    /// </summary>
    public FrontedControlConfigBase Config { get; }

    /// <summary>
    /// 获取当前选中集合项的唯一键。
    /// </summary>
    public string ItemKey { get; }
}
