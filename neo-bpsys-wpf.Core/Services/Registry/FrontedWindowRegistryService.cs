using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Registrations;
using neo_bpsys_wpf.Core.Services.FrontedLayout;

namespace neo_bpsys_wpf.Core.Services.Registry;

/// <summary>
/// 前台窗口注册表服务，从 DI 接收 <see cref="FrontedWindowRegistration"/> 集合并提供查询。
/// </summary>
/// <remarks>
/// 该实现维护唯一的 Canonical ID 索引。Canonical ID 比较使用
/// <see cref="StringComparer.OrdinalIgnoreCase"/>，与 Windows 文件系统、路径和符号
/// 大小写不敏感语义一致。重复 Canonical ID 在构造时抛出
/// <see cref="InvalidOperationException"/>，保证 fail-fast；空 ID 同样 fail-fast。
/// </remarks>
public sealed class FrontedWindowRegistryService : IFrontedWindowRegistry
{
    private readonly object _syncRoot = new();
    private readonly IReadOnlyList<FrontedWindowRegistration> _staticWindows;
    private IReadOnlyList<FrontedWindowRegistration> _windows;
    private IReadOnlyList<FrontedV3LayoutWindowRegistration> _v3LayoutWindows;
    private IReadOnlyDictionary<string, FrontedWindowRegistration> _byCanonicalId;

    /// <summary>
    /// 从 DI 接收的 registration 集合初始化注册表。
    /// </summary>
    /// <param name="registrations">由 DI 注册的所有前台窗口 registration。</param>
    /// <exception cref="InvalidOperationException">
    /// 当出现重复 Canonical ID 或空 Canonical ID 时抛出。
    /// 重复 ID 的异常信息含 ID、PackageId、IsBuiltIn、Kind、XAML WindowType（若存在）。
    /// 空 ID 的异常信息明确说明 ID 为空。
    /// </exception>
    public FrontedWindowRegistryService(
        IEnumerable<FrontedWindowRegistration> registrations)
    {
        var registrationList = registrations as IReadOnlyList<FrontedWindowRegistration>
                               ?? registrations.ToArray();
        // 使用 OrdinalIgnoreCase 与 Windows 路径/符号大小写不敏感语义保持一致。
        var byCanonicalId = new Dictionary<string, FrontedWindowRegistration>(StringComparer.OrdinalIgnoreCase);

        foreach (var registration in registrationList)
        {
            if (string.IsNullOrWhiteSpace(registration.Id))
            {
                // 空 ID 视为配置错误，fail-fast 而不是静默跳过。
                throw new InvalidOperationException(
                    $"Fronted window registration has an empty Canonical ID. "
                    + $"PackageId={registration.PackageId ?? "(null)"}, "
                    + $"IsBuiltIn={registration.IsBuiltIn}, Kind={registration.Kind}, "
                    + $"LocalId={registration.LocalId ?? "(null)"}. "
                    + $"Canonical ID must not be null, empty, or whitespace.");
            }

            if (byCanonicalId.TryGetValue(registration.Id, out var existing))
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

            byCanonicalId[registration.Id] = registration;
        }

        _byCanonicalId = byCanonicalId;
        _staticWindows = byCanonicalId.Values.ToArray();
        _windows = _staticWindows;
        _v3LayoutWindows = _windows.OfType<FrontedV3LayoutWindowRegistration>().ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyList<FrontedWindowRegistration> GetWindows()
    {
        lock (_syncRoot)
        {
            return _windows;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<FrontedV3LayoutWindowRegistration> GetV3LayoutWindows()
    {
        lock (_syncRoot)
        {
            return _v3LayoutWindows;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<FrontedWindowRegistration> GetManageableWindows() => GetWindows();

    /// <inheritdoc />
    public bool TryGet(string canonicalId, out FrontedWindowRegistration registration)
    {
        lock (_syncRoot)
        {
            return _byCanonicalId.TryGetValue(canonicalId, out registration!);
        }
    }

    /// <inheritdoc />
    public void ReplaceCustomWindows(IEnumerable<FrontedCustomV3LayoutWindowRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        var next = _staticWindows.ToList();
        foreach (var registration in registrations)
        {
            if (string.IsNullOrWhiteSpace(registration.Id)
                || !FrontedV3LayoutWindowPathHelper.TryParseCustomCanonicalWindowId(
                    registration.Id, out var packageId, out var localId)
                || !string.Equals(packageId, registration.PackageScopeId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(localId, registration.LocalId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Invalid custom v3 window registration: {registration.Id}");
            }

            if (next.Any(existing => string.Equals(existing.Id, registration.Id, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Duplicate fronted window Canonical ID '{registration.Id}'.");
            }

            next.Add(registration);
        }

        var nextByCanonicalId = new Dictionary<string, FrontedWindowRegistration>(StringComparer.OrdinalIgnoreCase);
        foreach (var registration in next)
        {
            nextByCanonicalId.Add(registration.Id, registration);
        }

        var nextWindows = next.ToArray();
        var nextV3LayoutWindows = next.OfType<FrontedV3LayoutWindowRegistration>().ToArray();
        lock (_syncRoot)
        {
            _byCanonicalId = nextByCanonicalId;
            _windows = nextWindows;
            _v3LayoutWindows = nextV3LayoutWindows;
        }
    }
}
