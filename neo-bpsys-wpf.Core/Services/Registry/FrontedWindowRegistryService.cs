using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Registrations;

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
    private readonly IReadOnlyList<FrontedWindowRegistration> _windows;
    private readonly IReadOnlyList<FrontedV3LayoutWindowRegistration> _v3LayoutWindows;
    private readonly Dictionary<string, FrontedWindowRegistration> _byCanonicalId;

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
        _byCanonicalId = new Dictionary<string, FrontedWindowRegistration>(StringComparer.OrdinalIgnoreCase);

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
            .OrderBy(registration => registration.LocalId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <inheritdoc />
    public bool TryGet(string canonicalId, out FrontedWindowRegistration registration) =>
        _byCanonicalId.TryGetValue(canonicalId, out registration!);
}
