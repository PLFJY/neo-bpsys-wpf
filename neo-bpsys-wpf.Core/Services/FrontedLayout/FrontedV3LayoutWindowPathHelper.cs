using System.Text.RegularExpressions;
using System.IO;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 将 v3 <c>Canonical ID</c> 标识转换为文件系统安全的布局路径。
/// </summary>
/// <remarks>
/// 内置标识直接映射，例如 <c>BpWindow</c> 映射到 <c>FrontedLayouts/BpWindow.json</c>。
/// 插件标识从 <c>plugin:{PackageId}/{LocalWindowId}</c> 映射到
/// <c>FrontedLayouts/plugin/{PackageId}/{LocalWindowId}.json</c>。
/// </remarks>
public static partial class FrontedV3LayoutWindowPathHelper
{
    /// <summary>
    /// 插件前台窗口布局标识使用的前缀。
    /// </summary>
    public const string PluginPrefix = "plugin:";

    /// <summary>
    /// 获取 Canonical ID 相对于前台布局根目录的安全文件夹路径。
    /// </summary>
    /// <param name="canonicalWindowId">内置窗口 LocalWindowId 或插件 Canonical ID。</param>
    /// <returns>不含布局 JSON 文件名的安全相对文件夹路径。</returns>
    /// <exception cref="ArgumentException">当 <paramref name="canonicalWindowId"/> 不是路径安全时抛出。</exception>
    public static string GetLayoutFolderRelativePath(string canonicalWindowId)
    {
        if (TryParsePluginCanonicalWindowId(canonicalWindowId, out var packageId, out var localWindowId))
        {
            EnsureSafePathSegment(packageId, nameof(packageId));
            EnsureSafePathSegment(localWindowId, nameof(localWindowId));
            return Path.Combine("plugin", packageId, localWindowId);
        }

        EnsureSafePathSegment(canonicalWindowId, nameof(canonicalWindowId));
        return canonicalWindowId;
    }

    /// <summary>
    /// 获取相对于前台布局根目录的安全窗口布局 JSON 路径。
    /// </summary>
    /// <param name="canonicalWindowId">内置窗口 LocalWindowId 或插件 Canonical ID。</param>
    /// <returns>安全的相对布局 JSON 路径。</returns>
    /// <exception cref="ArgumentException">当 <paramref name="canonicalWindowId"/> 不是路径安全时抛出。</exception>
    public static string GetLayoutRelativePath(string canonicalWindowId)
    {
        if (TryParsePluginCanonicalWindowId(canonicalWindowId, out var packageId, out var localWindowId))
        {
            EnsureSafePathSegment(packageId, nameof(packageId));
            EnsureSafePathSegment(localWindowId, nameof(localWindowId));
            return Path.Combine("plugin", packageId, $"{localWindowId}.json");
        }

        EnsureSafePathSegment(canonicalWindowId, nameof(canonicalWindowId));
        return $"{canonicalWindowId}.json";
    }

    /// <summary>
    /// 获取相对于前台布局根目录的安全窗口选项 JSON 路径。
    /// </summary>
    /// <param name="canonicalWindowId">内置窗口 LocalWindowId 或插件 Canonical ID。</param>
    /// <returns>安全的相对窗口选项 JSON 路径。</returns>
    /// <exception cref="ArgumentException">当 <paramref name="canonicalWindowId"/> 不是路径安全时抛出。</exception>
    public static string GetWindowOptionsRelativePath(string canonicalWindowId)
    {
        return Path.Combine(GetLayoutFolderRelativePath(canonicalWindowId), "window.json");
    }

    /// <summary>
    /// 将安全的相对布局文件夹转换回对应的 Canonical ID。
    /// </summary>
    /// <param name="relativeFolder">由 <see cref="GetLayoutFolderRelativePath"/> 创建的相对文件夹。</param>
    /// <returns>该文件夹表示的 Canonical ID。</returns>
    /// <exception cref="ArgumentException">当 <paramref name="relativeFolder"/> 不是有效的布局文件夹时抛出。</exception>
    public static string ToCanonicalWindowIdFromRelativeFolder(string relativeFolder)
    {
        var parts = relativeFolder
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 3 && string.Equals(parts[0], "plugin", StringComparison.OrdinalIgnoreCase))
        {
            EnsureSafePathSegment(parts[1], "packageId");
            EnsureSafePathSegment(parts[2], "localWindowId");
            return $"{PluginPrefix}{parts[1]}/{parts[2]}";
        }

        if (parts.Length == 1)
        {
            EnsureSafePathSegment(parts[0], "localWindowId");
            return parts[0];
        }

        throw new ArgumentException("Layout folder is not a valid CanonicalWindowId path.", nameof(relativeFolder));
    }

    /// <summary>
    /// 将相对于前台布局根目录的布局 JSON 文件路径转换回对应的 Canonical ID。
    /// </summary>
    /// <param name="relativePath">由 <see cref="GetLayoutRelativePath"/> 创建的相对 JSON 文件路径。</param>
    /// <returns>该文件表示的 Canonical ID。</returns>
    /// <exception cref="ArgumentException">当 <paramref name="relativePath"/> 不是有效的布局文件路径时抛出。</exception>
    public static string ToCanonicalWindowIdFromLayoutRelativePath(string relativePath)
    {
        var parts = relativePath
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 3
            && string.Equals(parts[0], "plugin", StringComparison.OrdinalIgnoreCase)
            && parts[2].EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            var packageId = parts[1];
            var localWindowId = Path.GetFileNameWithoutExtension(parts[2]);
            EnsureSafePathSegment(packageId, nameof(packageId));
            EnsureSafePathSegment(localWindowId, nameof(localWindowId));
            return $"{PluginPrefix}{packageId}/{localWindowId}";
        }

        if (parts.Length == 1 && parts[0].EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            var localWindowId = Path.GetFileNameWithoutExtension(parts[0]);
            EnsureSafePathSegment(localWindowId, nameof(localWindowId));
            return localWindowId;
        }

        throw new ArgumentException("Layout path is not a valid window-centric layout path.", nameof(relativePath));
    }

    /// <summary>
    /// 将相对于前台布局根目录的布局 JSON 文件路径转换回对应的 Canonical ID。
    /// 转换失败时返回 <see langword="false"/> 而不抛出异常。
    /// </summary>
    /// <param name="relativePath">由 <see cref="GetLayoutRelativePath"/> 创建的相对 JSON 文件路径。</param>
    /// <param name="canonicalWindowId">转换成功时输出的 Canonical ID。</param>
    /// <returns>当 <paramref name="relativePath"/> 是有效的布局文件路径时为 <see langword="true"/>。</returns>
    public static bool TryToCanonicalWindowIdFromLayoutRelativePath(
        string relativePath,
        out string canonicalWindowId)
    {
        try
        {
            canonicalWindowId = ToCanonicalWindowIdFromLayoutRelativePath(relativePath);
            return true;
        }
        catch
        {
            canonicalWindowId = string.Empty;
            return false;
        }
    }

    /// <summary>
    /// 返回 Canonical ID 是否可以安全映射到布局路径。
    /// </summary>
    /// <param name="canonicalWindowId">内置窗口 LocalWindowId 或插件 Canonical ID。</param>
    /// <returns>当值可以映射到安全布局路径时为 <see langword="true"/>。</returns>
    public static bool IsSafeCanonicalWindowId(string canonicalWindowId)
    {
        try
        {
            _ = GetLayoutFolderRelativePath(canonicalWindowId);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 解析格式为 <c>plugin:{PackageId}/{LocalWindowId}</c> 的插件 Canonical ID。
    /// </summary>
    /// <param name="canonicalWindowId">要解析的 Canonical ID。</param>
    /// <param name="packageId">解析成功时得到的包标识。</param>
    /// <param name="localWindowId">解析成功时得到的插件窗口 LocalWindowId。</param>
    /// <returns>当 <paramref name="canonicalWindowId"/> 是有效的插件 Canonical ID 时为 <see langword="true"/>。</returns>
    public static bool TryParsePluginCanonicalWindowId(
        string canonicalWindowId,
        out string packageId,
        out string localWindowId)
    {
        packageId = string.Empty;
        localWindowId = string.Empty;
        if (string.IsNullOrWhiteSpace(canonicalWindowId)
            || !canonicalWindowId.StartsWith(PluginPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var rest = canonicalWindowId[PluginPrefix.Length..];
        var slash = rest.IndexOf('/');
        if (slash <= 0 || slash == rest.Length - 1 || rest.IndexOf('/', slash + 1) >= 0)
        {
            return false;
        }

        packageId = rest[..slash];
        localWindowId = rest[(slash + 1)..];
        return IsSafePathSegment(packageId) && IsSafePathSegment(localWindowId);
    }

    /// <summary>
    /// 返回值对于一个布局路径段是否安全。
    /// </summary>
    /// <param name="value">要验证的路径段值。</param>
    /// <returns>当值对于单个布局路径段安全时为 <see langword="true"/>。</returns>
    public static bool IsSafePathSegment(string value)
    {
        return !string.IsNullOrWhiteSpace(value)
               && SafeSegmentRegex().IsMatch(value)
               && !value.Contains("..", StringComparison.Ordinal);
    }

    private static void EnsureSafePathSegment(string value, string name)
    {
        if (!IsSafePathSegment(value))
        {
            throw new ArgumentException($"{name} is not a safe layout path segment: {value}", name);
        }
    }

    [GeneratedRegex("^[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeSegmentRegex();
}
