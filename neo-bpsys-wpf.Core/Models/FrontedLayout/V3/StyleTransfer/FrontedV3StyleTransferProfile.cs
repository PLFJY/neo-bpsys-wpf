namespace neo_bpsys_wpf.Core.Models.FrontedLayout.V3.StyleTransfer;

/// <summary>
/// v3 前台控件 StyleTransfer 的每次操作传播 profile，描述本次操作实际传播哪些语义。
/// </summary>
/// <remarks>
/// <para>
/// Profile 是<b>操作选择</b>（per-operation selection），描述本次 Apply Parent Style / Peer Transfer
/// 操作实际要传播哪些语义。实际传播需要同时满足两个条件：
/// <list type="bullet">
/// <item>属性语义被 <see cref="FrontedV3PropertyTransfer"/>（能力声明）允许。</item>
/// <item>属性语义被本 Profile 选中。</item>
/// </list>
/// </para>
/// <para>
/// 默认 profile 仅传播 <see cref="FrontedV3PropertySemantic.Appearance"/>。
/// <see cref="FrontedV3PropertySemantic.RootSize"/>、<see cref="FrontedV3PropertySemantic.PartLayout"/>、
/// <see cref="FrontedV3PropertySemantic.Behaviors"/>、<see cref="FrontedV3PropertySemantic.Effects"/>
/// 需要显式开启才会传播。
/// </para>
/// <para>
/// <see cref="FrontedV3PropertySemantic.DataIdentity"/> 在任何 profile 下都不会传播，
/// 因为 <see cref="FrontedV3PropertyTransfer.CanTransfer"/> 对 DataIdentity 永远返回 <see langword="false"/>。
/// </para>
/// </remarks>
public sealed class FrontedV3StyleTransferProfile
{
    /// <summary>
    /// 初始化 <see cref="FrontedV3StyleTransferProfile"/> 并使用默认选择（仅 Appearance 传播）。
    /// </summary>
    public FrontedV3StyleTransferProfile()
    {
    }

    /// <summary>
    /// 获取或设置本次操作是否传播 <see cref="FrontedV3PropertySemantic.Appearance"/> 语义的属性，默认 <see langword="true"/>。
    /// </summary>
    public bool TransferAppearance { get; init; } = true;

    /// <summary>
    /// 获取或设置本次操作是否传播 <see cref="FrontedV3PropertySemantic.RootSize"/> 语义的属性，默认 <see langword="false"/>。
    /// </summary>
    public bool TransferRootSize { get; init; }

    /// <summary>
    /// 获取或设置本次操作是否传播 <see cref="FrontedV3PropertySemantic.PartLayout"/> 语义的属性，默认 <see langword="false"/>。
    /// </summary>
    public bool TransferPartLayout { get; init; }

    /// <summary>
    /// 获取或设置本次操作是否传播 <see cref="FrontedV3PropertySemantic.Behaviors"/> 语义的属性，默认 <see langword="false"/>。
    /// </summary>
    public bool TransferBehaviors { get; init; }

    /// <summary>
    /// 获取或设置本次操作是否传播 <see cref="FrontedV3PropertySemantic.Effects"/> 语义的属性，默认 <see langword="false"/>。
    /// </summary>
    public bool TransferEffects { get; init; }

    /// <summary>
    /// 判断本次操作是否选中传播给定语义。
    /// </summary>
    /// <param name="semantic">要检查的属性语义。</param>
    /// <returns>当本次操作选中该语义时为 <see langword="true"/>；<see cref="FrontedV3PropertySemantic.DataIdentity"/> 和 <see cref="FrontedV3PropertySemantic.Other"/> 永远返回 <see langword="false"/>。</returns>
    public bool ShouldTransfer(FrontedV3PropertySemantic semantic)
    {
        return semantic switch
        {
            FrontedV3PropertySemantic.Appearance => TransferAppearance,
            FrontedV3PropertySemantic.RootSize => TransferRootSize,
            FrontedV3PropertySemantic.PartLayout => TransferPartLayout,
            FrontedV3PropertySemantic.Behaviors => TransferBehaviors,
            FrontedV3PropertySemantic.Effects => TransferEffects,
            // DataIdentity 和 Other 永远不选中。
            _ => false
        };
    }

    /// <summary>
    /// 默认 profile：仅传播 <see cref="FrontedV3PropertySemantic.Appearance"/>。
    /// </summary>
    public static FrontedV3StyleTransferProfile Default { get; } = new();

    /// <summary>
    /// 创建一个传播所有可传播语义的 profile（Appearance + RootSize + PartLayout + Behaviors + Effects）。
    /// </summary>
    /// <returns>全量传播 profile。</returns>
    /// <remarks>
    /// 即使使用全量 profile，<see cref="FrontedV3PropertySemantic.DataIdentity"/> 仍然不会传播，
    /// 因为 <see cref="FrontedV3PropertyTransfer.CanTransfer"/> 对 DataIdentity 永远返回 <see langword="false"/>。
    /// </remarks>
    public static FrontedV3StyleTransferProfile TransferAll()
    {
        return new FrontedV3StyleTransferProfile
        {
            TransferAppearance = true,
            TransferRootSize = true,
            TransferPartLayout = true,
            TransferBehaviors = true,
            TransferEffects = true
        };
    }
}
