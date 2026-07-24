using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Registrations;

namespace neo_bpsys_wpf.Core.Services.Registry;

/// <summary>
/// 前台窗口注册表服务，从 DI 接收 <see cref="FrontedWindowRegistration"/> 集合并提供查询。
/// </summary>
/// <remarks>
/// 该实现维护唯一的 Canonical ID 索引。重复 Canonical ID 在构造时抛出
/// <see cref="InvalidOperationException"/>，保证 fail-fast。
/// </remarks>
public sealed class FrontedWindowRegistryService : IFrontedWindowRegistry
{
    private readonly IReadOnlyList<FrontedWindowRegistration> _windows;
    private readonly IReadOnlyList<FrontedV3LayoutWindowRegistration> _v3LayoutWindows;
    private readonly Dictionary<string, FrontedWindowRegistration> _byCanonicalId;

    /// <summary>
    /// 使用默认空集合初始化注册表（主要用于测试回退）。
    /// </summary>
    public FrontedWindowRegistryService()
        : this([], NullLogger<FrontedWindowRegistryService>.Instance)
    {
    }

    /// <summary>
    /// 从 DI 接收的 registration 集合初始化注册表。
    /// </summary>
    /// <param name="registrations">由 DI 注册的所有前台窗口 registration。</param>
    /// <param name="logger">日志记录器。</param>
    /// <exception cref="InvalidOperationException">当出现重复 Canonical ID 时抛出，异常信息含 ID、PackageId、IsBuiltIn、Kind、XAML WindowType（若存在）。</exception>
    public FrontedWindowRegistryService(
        IEnumerable<FrontedWindowRegistration> registrations,
        ILogger<FrontedWindowRegistryService>? logger = null)
    {
        logger ??= NullLogger<FrontedWindowRegistryService>.Instance;

        var registrationList = registrations as IReadOnlyList<FrontedWindowRegistration>
                               ?? registrations.ToArray();
        _byCanonicalId = new Dictionary<string, FrontedWindowRegistration>(StringComparer.Ordinal);

        foreach (var registration in registrationList)
        {
            if (string.IsNullOrWhiteSpace(registration.Id))
            {
                logger.LogWarning("Rejected fronted window registration with empty Canonical ID.");
                continue;
            }

            if (_byCanonicalId.TryGetValue(registration.Id, out var existing))
            {
                throw new InvalidOperationException(
                    $"Duplicate fronted window Canonical ID '{registration.Id}'. "
                    + $"Existing: Id={existing.Id}, PackageId={existing.PackageId ?? "(null)"}, "
                    + $"IsBuiltIn={existing.IsBuiltIn}, Kind={existing.Kind}, "
                    + $"XamlWindowType={(existing is FrontedXamlWindowRegistration xaml ? xaml.WindowType.FullName ?? "(null)" : "(none)")}. "
                    + $"Duplicate: Id={registration.Id}, PackageId={registration.PackageId ?? "(null)"}, "
                    + $"IsBuiltIn={registration.IsBuiltIn}, Kind={registration.Kind}, "
                    + $"XamlWindowType={(registration is FrontedXamlWindowRegistration dupXaml ? dupXaml.WindowType.FullName ?? "(null)" : "(none)")}.");
            }

            _byCanonicalId[registration.Id] = registration;
        }

        _windows = _byCanonicalId.Values.ToArray();
        _v3LayoutWindows = _windows.OfType<FrontedV3LayoutWindowRegistration>().ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyList<FrontedWindowRegistration> GetWindows() => _windows;

    /// <inheritdoc />
    public IReadOnlyList<FrontedV3LayoutWindowRegistration> GetV3LayoutWindows() => _v3LayoutWindows;

    /// <inheritdoc />
    public IReadOnlyList<FrontedWindowRegistration> GetManageableWindows()
    {
        return _windows
            .OrderBy(registration => string.IsNullOrWhiteSpace(registration.GroupKey)
                ? registration.IsBuiltIn ? "BuiltIn" : "Plugin"
                : registration.GroupKey,
                StringComparer.Ordinal)
            .ThenBy(registration => registration.DisplayOrder ?? int.MaxValue)
            .ThenBy(registration => string.IsNullOrWhiteSpace(registration.DisplayName)
                ? registration.LocalId
                : registration.DisplayName,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <inheritdoc />
    public bool TryGet(string canonicalId, out FrontedWindowRegistration registration) =>
        _byCanonicalId.TryGetValue(canonicalId, out registration!);
}
