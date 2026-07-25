using System.Windows;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
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
}
