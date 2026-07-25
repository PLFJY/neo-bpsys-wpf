using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.V3.StyleTransfer;

/// <summary>
/// Peer Style Transfer 操作的目标，携带目标控件的注册信息与配置实例。
/// </summary>
/// <remarks>
/// <para>
/// 该类型用于 <see cref="neo_bpsys_wpf.Core.Services.FrontedLayout.V3.StyleTransfer.FrontedV3StyleTransferService.TransferPeerStyle"/>，
/// 描述每个 peer 目标的注册（用于校验 CanonicalControlType 匹配）与配置（作为写入目标）。
/// </para>
/// <para>
/// <b>精确匹配约束</b>：peer 的 <see cref="Registration"/>.<see cref="FrontedV3ControlRegistration.CanonicalControlType"/>
/// 必须与源完全相同。<c>plugin:a/TeamCard</c> 不能作为 <c>plugin:b/TeamCard</c> 的 peer。
/// </para>
/// </remarks>
public sealed class PeerStyleTarget
{
    /// <summary>
    /// 初始化 <see cref="PeerStyleTarget"/>。
    /// </summary>
    public PeerStyleTarget()
    {
    }

    /// <summary>
    /// 初始化 <see cref="PeerStyleTarget"/> 并指定注册与配置。
    /// </summary>
    /// <param name="registration">目标控件的注册信息。</param>
    /// <param name="config">目标控件的配置实例。</param>
    /// <exception cref="ArgumentNullException">当参数为 <see langword="null"/> 时抛出。</exception>
    public PeerStyleTarget(FrontedV3ControlRegistration registration, FrontedControlConfigBase config)
    {
        Registration = registration ?? throw new ArgumentNullException(nameof(registration));
        Config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// 获取或设置目标控件的注册信息，用于校验 CanonicalControlType 匹配。
    /// </summary>
    public FrontedV3ControlRegistration? Registration { get; set; }

    /// <summary>
    /// 获取或设置目标控件的配置实例，作为传播写入的目标。
    /// </summary>
    public FrontedControlConfigBase? Config { get; set; }
}
