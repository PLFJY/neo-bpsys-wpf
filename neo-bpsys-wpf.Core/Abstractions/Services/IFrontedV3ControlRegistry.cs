using neo_bpsys_wpf.Core.Models.FrontedLayout.V3;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// v3 前台控件的统一注册表接口，按 <see cref="FrontedV3ControlRegistration.CanonicalControlType"/> 索引。
/// </summary>
/// <remarks>
/// 该注册表是前台控件的唯一正式注册表，按 <see cref="FrontedV3ControlRegistration.CanonicalControlType"/> 索引。
/// </remarks>
public interface IFrontedV3ControlRegistry
{
    /// <summary>
    /// 按 Canonical Control Type 查找注册信息。
    /// </summary>
    /// <param name="canonicalControlType">控件的 Canonical Control Type。</param>
    /// <returns>匹配的注册信息；未注册时为 <see langword="null"/>。</returns>
    FrontedV3ControlRegistration? GetRegistration(string canonicalControlType);

    /// <summary>
    /// 尝试按 Canonical Control Type 查找注册信息。
    /// </summary>
    /// <param name="canonicalControlType">控件的 Canonical Control Type。</param>
    /// <param name="registration">匹配的注册信息。</param>
    /// <returns>找到时为 <see langword="true"/>。</returns>
    bool TryGetRegistration(string canonicalControlType, out FrontedV3ControlRegistration registration);

    /// <summary>
    /// 返回所有已注册的 v3 控件注册信息。
    /// </summary>
    /// <returns>注册信息集合的只读视图。</returns>
    IReadOnlyCollection<FrontedV3ControlRegistration> GetRegistrations();
}
