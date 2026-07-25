using System.Windows;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Parts;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.StyleTransfer;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.V3;

/// <summary>
/// v3 前台控件的注册信息，由统一 Registry 按 <see cref="CanonicalControlType"/> 索引。
/// </summary>
/// <remarks>
/// <para>
/// 该类型统一了内置控件与插件控件的身份与元数据。
/// Registry 只维护 <see cref="CanonicalControlType"/> → <see cref="FrontedV3ControlRegistration"/>。
/// </para>
/// <para>
/// 身份规则：
/// <list type="bullet">
/// <item>内置控件：<see cref="CanonicalControlType"/> 直接使用 <see cref="LocalControlId"/>，例如 <c>Text</c>。</item>
/// <item>插件控件：<see cref="CanonicalControlType"/> 为 <c>plugin:{PackageId}/{LocalControlId}</c>，例如 <c>plugin:plfjy.ExamplePlugin/TeamCard</c>。</item>
/// </list>
/// </para>
/// </remarks>
public sealed class FrontedV3ControlRegistration
{
    /// <summary>
    /// 控件的 Canonical Control Type，作为 Registry 的唯一键。
    /// </summary>
    public required string CanonicalControlType { get; init; }

    /// <summary>
    /// 控件局部标识，例如 <c>TeamCard</c> 或内置的 <c>Text</c>。
    /// </summary>
    public required string LocalControlId { get; init; }

    /// <summary>
    /// 所属插件包 ID；内置控件为 <see langword="null"/>。
    /// </summary>
    public string? PackageId { get; init; }

    /// <summary>
    /// 是否为宿主内置控件。
    /// </summary>
    public bool IsBuiltIn { get; init; }

    /// <summary>
    /// 是否在 Designer 中显示"应用到同类型控件"按钮并允许同类型 peer 之间传播外观样式。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 默认为 <see langword="false"/>。该字段由 Registry 在注册时从
    /// <c>FrontedV3ControlAttribute.SupportsPeerStyleTransfer</c>
    /// 复制而来，用于在 Designer 中门控同类型 peer 样式传播入口的可见性与可用性。
    /// </para>
    /// <para>
    /// 仅当该字段为 <see langword="true"/> 时，Designer 才会显示"应用到同类型控件"按钮；
    /// 按钮的启用还需满足存在同类型 peer 等其他条件。
    /// </para>
    /// </remarks>
    public bool SupportsPeerStyleTransfer { get; init; }

    /// <summary>
    /// 控件类型，必须继承 <see cref="FrameworkElement"/>（通常是 <c>FrontedV3ControlBase</c>）。
    /// </summary>
    public required Type ControlType { get; init; }

    /// <summary>
    /// 控件配置类型，必须继承 <see cref="FrontedControlConfigBase"/>。
    /// 插件控件为 <see cref="PluginFrontedControlConfig"/>；内置控件为各自的强类型 Config。
    /// </summary>
    public required Type ConfigType { get; init; }

    /// <summary>
    /// 控件的属性定义列表。
    /// </summary>
    public required IReadOnlyList<Properties.FrontedV3PropertyDefinition> Properties { get; init; }

    /// <summary>
    /// 创建默认配置实例的工厂。框架根据 <see cref="IsBuiltIn"/> 与 <see cref="ConfigType"/> 提供默认实现。
    /// </summary>
    public required Func<FrontedControlConfigBase> CreateDefaultConfig { get; init; }

    /// <summary>
    /// 控件的 StyleTransfer 能力声明，描述哪些语义分类可以参与传播。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 默认为 <see cref="FrontedV3PropertyTransfer.Default"/>（仅 Appearance 可传播）。
    /// 实际传播时还需配合 <see cref="FrontedV3StyleTransferProfile"/>（每次操作的选择），
    /// 只有能力允许且 profile 选中的语义才会真正传播。
    /// </para>
    /// <para>
    /// <see cref="FrontedV3PropertySemantic.DataIdentity"/> 永远不可传播，无论能力声明如何设置。
    /// </para>
    /// </remarks>
    public FrontedV3PropertyTransfer StyleTransfer { get; init; } = FrontedV3PropertyTransfer.Default;

    /// <summary>
    /// 控件声明的固定 Part 定义列表，由控件类上的 <c>public static readonly FrontedV3Part</c> 字段发现，
    /// 或由内置控件的 <c>BuiltInPartDefinitionResolver</c> 提供。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 插件控件通过 <c>FrontedV3Part.Register&lt;TControl&gt;</c> 声明固定 Part，
    /// 框架在注册时通过 <see cref="neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Parts.FrontedV3Part.Discover"/>
    /// 反射发现并转换为 <see cref="FrontedV3PartDefinition"/>。
    /// </para>
    /// <para>
    /// 内置控件（如 BorderedImage、MapV2Display）通过 <c>BuiltInPartDefinitionResolver</c>
    /// 在注册时填充，无需在控件类上声明静态字段。
    /// </para>
    /// <para>
    /// Designer 选择 Part 时优先从该字段查找，确保插件 Part 与内置 Part 走统一链路。
    /// 默认为空列表，表示控件无可编辑的固定 Part。
    /// </para>
    /// </remarks>
    public IReadOnlyList<FrontedV3PartDefinition> FixedParts { get; init; } = Array.Empty<FrontedV3PartDefinition>();

    /// <summary>
    /// 控件声明的 PartCollection 定义列表，由控件类上的 <c>public static readonly FrontedV3Parts</c> 字段发现，
    /// 或由内置控件的 <c>BuiltInPartCollectionDefinitionResolver</c> 提供。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 插件控件通过 <c>FrontedV3Parts.RegisterCollection&lt;TControl&gt;</c> 声明 PartCollection，
    /// 框架在注册时通过 <see cref="neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Parts.FrontedV3Parts.Discover"/>
    /// 反射发现并转换为 <see cref="FrontedV3PartCollectionDefinition"/>。
    /// </para>
    /// <para>
    /// 内置控件（如 GlobalScoreRow）通过 <c>BuiltInPartCollectionDefinitionResolver</c>
    /// 在注册时填充，无需在控件类上声明静态字段。
    /// </para>
    /// <para>
    /// Designer 选择集合项时优先从该字段查找，确保插件与内置走统一链路。
    /// 默认为空列表，表示控件无可编辑的 PartCollection。
    /// </para>
    /// </remarks>
    public IReadOnlyList<FrontedV3PartCollectionDefinition> PartCollections { get; init; } = Array.Empty<FrontedV3PartCollectionDefinition>();

    /// <summary>
    /// 控件在 Designer 与运行时的元数据，由 <see cref="FrontedV3ControlAttribute"/> 推导。
    /// </summary>
    /// <remarks>
    /// 默认为空 <see cref="FrontedV3ControlMetadata"/>，调用方按字段 null 情况回退到合理默认值。
    /// </remarks>
    public FrontedV3ControlMetadata Metadata { get; init; } = new();
}
