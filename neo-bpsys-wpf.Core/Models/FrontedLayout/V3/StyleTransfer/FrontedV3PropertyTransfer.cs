namespace neo_bpsys_wpf.Core.Models.FrontedLayout.V3.StyleTransfer;

/// <summary>
/// v3 前台控件属性的传播能力配置，描述哪些语义分类可以参与 StyleTransfer 传播。
/// </summary>
/// <remarks>
/// <para>
/// 该配置是<b>能力声明</b>（capability），描述控件<b>允许</b>哪些语义被传播。
/// 实际传播时还需配合 <see cref="FrontedV3StyleTransferProfile"/>（每次操作的传播选择），
/// 只有能力允许且 profile 选中的语义才会真正传播。
/// </para>
/// <para>
/// 不可破坏的约束：<see cref="FrontedV3PropertySemantic.DataIdentity"/> 永远不可传播，
/// 无论 profile 如何设置。传播数据身份字段会导致控件指向错误的数据源。
/// </para>
/// <para>
/// 根级保留字段（<c>Left</c>/<c>Top</c>/<c>ZIndex</c> 等）由
/// <see cref="neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties.FrontedV3ReservedFields"/> 管理，
/// 不会注册为属性，因此不需要在传播能力中排除。
/// </para>
/// </remarks>
public sealed class FrontedV3PropertyTransfer
{
    /// <summary>
    /// 初始化 <see cref="FrontedV3PropertyTransfer"/> 并使用默认能力（仅 Appearance 可传播）。
    /// </summary>
    public FrontedV3PropertyTransfer()
    {
    }

    /// <summary>
    /// 获取或设置是否允许传播 <see cref="FrontedV3PropertySemantic.Appearance"/> 语义的属性，默认 <see langword="true"/>。
    /// </summary>
    public bool CanTransferAppearance { get; init; } = true;

    /// <summary>
    /// 获取或设置是否允许传播 <see cref="FrontedV3PropertySemantic.RootSize"/> 语义的属性，默认 <see langword="false"/>。
    /// </summary>
    public bool CanTransferRootSize { get; init; }

    /// <summary>
    /// 获取或设置是否允许传播 <see cref="FrontedV3PropertySemantic.PartLayout"/> 语义的属性，默认 <see langword="false"/>。
    /// </summary>
    public bool CanTransferPartLayout { get; init; }

    /// <summary>
    /// 获取或设置是否允许传播 <see cref="FrontedV3PropertySemantic.Behaviors"/> 语义的属性，默认 <see langword="false"/>。
    /// </summary>
    public bool CanTransferBehaviors { get; init; }

    /// <summary>
    /// 获取或设置是否允许传播 <see cref="FrontedV3PropertySemantic.Effects"/> 语义的属性，默认 <see langword="false"/>。
    /// </summary>
    public bool CanTransferEffects { get; init; }

    /// <summary>
    /// 判断给定语义是否可传播。
    /// </summary>
    /// <param name="semantic">要检查的属性语义。</param>
    /// <returns>当该语义可传播时为 <see langword="true"/>；<see cref="FrontedV3PropertySemantic.DataIdentity"/> 和 <see cref="FrontedV3PropertySemantic.Other"/> 永远返回 <see langword="false"/>。</returns>
    public bool CanTransfer(FrontedV3PropertySemantic semantic)
    {
        return semantic switch
        {
            FrontedV3PropertySemantic.Appearance => CanTransferAppearance,
            FrontedV3PropertySemantic.RootSize => CanTransferRootSize,
            FrontedV3PropertySemantic.PartLayout => CanTransferPartLayout,
            FrontedV3PropertySemantic.Behaviors => CanTransferBehaviors,
            FrontedV3PropertySemantic.Effects => CanTransferEffects,
            // DataIdentity 永远不可传播；Other 不参与传播。
            _ => false
        };
    }

    /// <summary>
    /// 默认传播能力：仅 <see cref="FrontedV3PropertySemantic.Appearance"/> 可传播。
    /// </summary>
    public static FrontedV3PropertyTransfer Default { get; } = new();

    /// <summary>
    /// 仅外观传播能力：仅 <see cref="FrontedV3PropertySemantic.Appearance"/> 可传播，与 <see cref="Default"/> 等价。
    /// </summary>
    public static FrontedV3PropertyTransfer AppearanceOnly { get; } = new();

    /// <summary>
    /// 全量传播能力：除 <see cref="FrontedV3PropertySemantic.DataIdentity"/> 和 <see cref="FrontedV3PropertySemantic.Other"/> 外，
    /// 所有语义均可传播。
    /// </summary>
    public static FrontedV3PropertyTransfer All { get; } = new()
    {
        CanTransferAppearance = true,
        CanTransferRootSize = true,
        CanTransferPartLayout = true,
        CanTransferBehaviors = true,
        CanTransferEffects = true
    };
}
