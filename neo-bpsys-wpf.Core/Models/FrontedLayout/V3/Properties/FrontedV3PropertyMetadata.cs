using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.StyleTransfer;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties;

/// <summary>
/// v3 前台控件属性的元数据，驱动 Designer 属性网格的显示、分组与编辑器选择。
/// </summary>
/// <remarks>
/// 元数据只描述属性的"如何展示与编辑"，不参与 JSON 序列化，也不决定值的读写位置
/// （值的读写由 <see cref="neo_bpsys_wpf.Core.Abstractions.Services.IFrontedV3StorageAccessor"/> 决定）。
/// </remarks>
public sealed class FrontedV3PropertyMetadata
{
    /// <summary>
    /// 初始化 <see cref="FrontedV3PropertyMetadata"/>。
    /// </summary>
    public FrontedV3PropertyMetadata()
    {
    }

    /// <summary>
    /// 初始化 <see cref="FrontedV3PropertyMetadata"/> 并指定显示名键。
    /// </summary>
    /// <param name="displayNameKey">属性显示名的本地化键。</param>
    public FrontedV3PropertyMetadata(string? displayNameKey)
    {
        DisplayNameKey = displayNameKey;
    }

    /// <summary>
    /// 属性显示名的本地化键；为 <see langword="null"/> 时回退到 OptionsPath 的末段。
    /// </summary>
    public string? DisplayNameKey { get; init; }

    /// <summary>
    /// 属性描述的本地化键。
    /// </summary>
    public string? DescriptionKey { get; init; }

    /// <summary>
    /// 属性在 Designer 属性网格中的分组名，默认 <c>Plugin</c>。
    /// </summary>
    public string GroupName { get; init; } = "Plugin";

    /// <summary>
    /// 属性使用的编辑器类型；为 <see langword="null"/> 时由宿主按 <c>PropertyType</c> 推断。
    /// </summary>
    public FrontedPropertyEditorKind? EditorKind { get; init; }

    /// <summary>
    /// 枚举或固定选项属性的可选值列表。
    /// </summary>
    public IReadOnlyList<FrontedPropertyEditorOption>? Options { get; init; }

    /// <summary>
    /// 属性在 Designer 中是否可见，默认 <see langword="true"/>。
    /// </summary>
    public bool IsVisible { get; init; } = true;

    /// <summary>
    /// 属性在 Designer 中是否只读，默认 <see langword="false"/>。
    /// </summary>
    public bool IsReadOnly { get; init; }

    /// <summary>
    /// 属性的语义分类，决定该属性是否参与 StyleTransfer 传播以及传播时的行为。
    /// 默认 <see cref="FrontedV3PropertySemantic.Other"/>（不参与传播）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 语义分类是 StyleTransfer 的核心驱动：
    /// <list type="bullet">
    /// <item><see cref="FrontedV3PropertySemantic.Appearance"/>：默认传播。</item>
    /// <item><see cref="FrontedV3PropertySemantic.DataIdentity"/>：永不传播。</item>
    /// <item><see cref="FrontedV3PropertySemantic.RootSize"/>/<see cref="FrontedV3PropertySemantic.PartLayout"/>/
    /// <see cref="FrontedV3PropertySemantic.Behaviors"/>/<see cref="FrontedV3PropertySemantic.Effects"/>：仅当 profile 开启时传播。</item>
    /// </list>
    /// </para>
    /// </remarks>
    public FrontedV3PropertySemantic Semantic { get; init; } = FrontedV3PropertySemantic.Other;

    /// <summary>
    /// 属性的继承模式，决定子控件如何从父控件获取该属性的值。
    /// 默认 <see cref="FrontedV3PropertyInheritance.None"/>（不继承）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 继承模式影响 <see cref="neo_bpsys_wpf.Core.Services.FrontedLayout.V3.StyleTransfer.FrontedV3StyleTransferService"/>
    /// 的读取与写入行为：
    /// <list type="bullet">
    /// <item><see cref="FrontedV3PropertyInheritance.ParentFallback"/>：动态读取，子项 override 优先，否则回退到父值。</item>
    /// <item><see cref="FrontedV3PropertyInheritance.LockedToParent"/>：锁定到父值，拒绝 override。</item>
    /// <item><see cref="FrontedV3PropertyInheritance.CopyFromParentOnCreate"/>：创建时从父复制，之后独立。</item>
    /// </list>
    /// </para>
    /// </remarks>
    public FrontedV3PropertyInheritance Inheritance { get; init; } = FrontedV3PropertyInheritance.None;

    /// <summary>
    /// 属性的样式组件分类，用于 Designer UI 分组与未来细粒度传播控制。
    /// 默认 <see cref="FrontedV3StyleComponent.None"/>（未分类）。
    /// </summary>
    public FrontedV3StyleComponent StyleComponent { get; init; } = FrontedV3StyleComponent.None;
}
