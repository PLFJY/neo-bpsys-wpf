using System.Collections.Generic;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout.V3;

/// <summary>
/// v3 前台控件的统一注册表，收集 DI 中注册的 <see cref="FrontedV3ControlRegistration"/> 并按 Canonical Control Type 索引。
/// </summary>
/// <remarks>
/// <para>
/// 该注册表是前台控件的唯一正式注册表，收集通过
/// <c>AddFrontedV3Control&lt;T&gt;()</c> 或 <c>AddBuiltInFrontedV3Control&lt;TControl,TConfig&gt;()</c> 注册的所有控件。
/// </para>
/// <para>
/// 重复的 Canonical Control Type 在构造时抛出 <see cref="FrontedLayoutConfigException"/>。
/// </para>
/// </remarks>
public class FrontedV3ControlRegistry : IFrontedV3ControlRegistry
{
    private readonly Dictionary<string, FrontedV3ControlRegistration> _registrations;

    /// <summary>
    /// 初始化注册表并收集所有已注册的 <see cref="FrontedV3ControlRegistration"/>。
    /// </summary>
    /// <param name="registrations">DI 中注册的控件注册信息。</param>
    /// <exception cref="FrontedLayoutConfigException">当出现重复的 Canonical Control Type 时抛出。</exception>
    public FrontedV3ControlRegistry(IEnumerable<FrontedV3ControlRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        _registrations = new Dictionary<string, FrontedV3ControlRegistration>(StringComparer.OrdinalIgnoreCase);

        foreach (var registration in registrations)
        {
            if (!_registrations.TryAdd(registration.CanonicalControlType, registration))
            {
                throw new FrontedLayoutConfigException(
                    $"Duplicate v3 control registration for canonical type '{registration.CanonicalControlType}'.");
            }
        }
    }

    /// <inheritdoc />
    public FrontedV3ControlRegistration? GetRegistration(string canonicalControlType)
    {
        ArgumentNullException.ThrowIfNull(canonicalControlType);
        return _registrations.GetValueOrDefault(canonicalControlType);
    }

    /// <inheritdoc />
    public bool TryGetRegistration(string canonicalControlType, out FrontedV3ControlRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(canonicalControlType);
        return _registrations.TryGetValue(canonicalControlType, out registration!);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<FrontedV3ControlRegistration> GetRegistrations()
    {
        return _registrations.Values.ToArray();
    }
}
