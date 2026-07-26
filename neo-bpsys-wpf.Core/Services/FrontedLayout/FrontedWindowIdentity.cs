namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 生成前台窗口的 Canonical ID。
/// </summary>
/// <remarks>
/// 内置窗口或未关联插件包的窗口直接使用局部窗口标识；
/// 插件窗口使用 <c>plugin:{PackageId}/{LocalId}</c> 形式，与
/// <see cref="FrontedV3LayoutWindowPathHelper"/> 的 <c>CanonicalWindowId</c> 约定一致。
/// </remarks>
public static class FrontedWindowIdentity
{
    /// <summary>
    /// 根据局部窗口标识、插件包 ID 与是否内置，构造 Canonical ID。
    /// </summary>
    /// <param name="localWindowId">提供方内部的局部窗口标识。</param>
    /// <param name="packageId">插件包 ID；非插件时为 <see langword="null"/>。</param>
    /// <param name="isBuiltIn">是否为宿主内置窗口。</param>
    /// <returns>当 <paramref name="isBuiltIn"/> 为 <see langword="true"/> 或
    /// <paramref name="packageId"/> 为 <see langword="null"/> 时返回 <paramref name="localWindowId"/>；
    /// 否则返回 <c>plugin:{PackageId}/{LocalId}</c>。</returns>
    public static string BuildCanonicalId(string localWindowId, string? packageId, bool isBuiltIn)
    {
        if (isBuiltIn || packageId is null)
        {
            return localWindowId;
        }

        return $"plugin:{packageId}/{localWindowId}";
    }

    /// <summary>
    /// 校验窗口局部 ID 是否可作为 Canonical ID 的安全片段。
    /// </summary>
    /// <param name="id">待校验的窗口局部 ID。</param>
    /// <exception cref="ArgumentException">
    /// 当 ID 为空/空白、含前后空白、含路径分隔符（<c>/</c>、<c>\</c>）、含冒号（<c>:</c>）或含控制字符时抛出。
    /// </exception>
    /// <remarks>
    /// 该校验不要求 GUID；允许任意不会破坏 Canonical ID 结构、路径解析或比较语义的稳定字符串。
    /// 插件 XAML 窗口的 Canonical ID 为 <c>plugin:{PackageId}/{LocalId}</c>，
    /// 其中 <c>/</c> 是结构分隔符，因此 LocalId 自身不得包含 <c>/</c>。
    /// </remarks>
    public static void EnsureValidWindowLocalId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Window local ID must not be null, empty, or whitespace.", nameof(id));
        }
        if (!string.Equals(id, id.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException($"Window local ID must not have leading or trailing whitespace: '{id}'.", nameof(id));
        }
        if (id.Contains('/', StringComparison.Ordinal)
            || id.Contains('\\', StringComparison.Ordinal)
            || id.Contains(':', StringComparison.Ordinal)
            || id.Any(char.IsControl))
        {
            throw new ArgumentException(
                $"Window local ID must not contain path separators ('/', '\\'), colons (':'), or control characters: '{id}'.",
                nameof(id));
        }
    }
}
