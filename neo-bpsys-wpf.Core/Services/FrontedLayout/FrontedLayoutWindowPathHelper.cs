using System.Text.RegularExpressions;
using System.IO;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 将 v3 <c>FullWindowType</c> 标识转换为文件系统安全的布局路径。
/// </summary>
/// <remarks>
/// 内置标识直接映射，例如 <c>BpWindow</c> 映射到 <c>FrontedLayouts/BpWindow.json</c>。
/// 插件标识从 <c>plugin:{PackageId}/{WindowTypeName}</c> 映射到
/// <c>FrontedLayouts/plugin/{PackageId}/{WindowTypeName}.json</c>。
/// </remarks>
public static partial class FrontedLayoutWindowPathHelper
{
    /// <summary>
    /// 插件前台窗口布局标识使用的前缀。
    /// </summary>
    public const string PluginPrefix = "plugin:";

    /// <summary>
    /// 获取完整窗口类型相对于前台布局根目录的安全文件夹路径。
    /// </summary>
    /// <param name="fullWindowType">内置窗口类型名称或插件完整窗口类型。</param>
    /// <returns>不含布局 JSON 文件名的安全相对文件夹路径。</returns>
    /// <exception cref="ArgumentException">当 <paramref name="fullWindowType"/> 不是路径安全时抛出。</exception>
    public static string GetLayoutFolderRelativePath(string fullWindowType)
    {
        if (TryParsePluginFullWindowType(fullWindowType, out var packageId, out var windowTypeName))
        {
            EnsureSafePathSegment(packageId, nameof(packageId));
            EnsureSafePathSegment(windowTypeName, nameof(windowTypeName));
            return Path.Combine("plugin", packageId, windowTypeName);
        }

        EnsureSafePathSegment(fullWindowType, nameof(fullWindowType));
        return fullWindowType;
    }

    /// <summary>
    /// 获取相对于前台布局根目录的安全窗口布局 JSON 路径。
    /// </summary>
    /// <param name="fullWindowType">内置窗口类型名称或插件完整窗口类型。</param>
    /// <returns>安全的相对布局 JSON 路径。</returns>
    /// <exception cref="ArgumentException">当 <paramref name="fullWindowType"/> 不是路径安全时抛出。</exception>
    public static string GetLayoutRelativePath(string fullWindowType)
    {
        if (TryParsePluginFullWindowType(fullWindowType, out var packageId, out var windowTypeName))
        {
            EnsureSafePathSegment(packageId, nameof(packageId));
            EnsureSafePathSegment(windowTypeName, nameof(windowTypeName));
            return Path.Combine("plugin", packageId, $"{windowTypeName}.json");
        }

        EnsureSafePathSegment(fullWindowType, nameof(fullWindowType));
        return $"{fullWindowType}.json";
    }

    /// <summary>
    /// 获取相对于前台布局根目录的安全窗口选项 JSON 路径。
    /// </summary>
    /// <param name="fullWindowType">内置窗口类型名称或插件完整窗口类型。</param>
    /// <returns>安全的相对窗口选项 JSON 路径。</returns>
    /// <exception cref="ArgumentException">当 <paramref name="fullWindowType"/> 不是路径安全时抛出。</exception>
    public static string GetWindowOptionsRelativePath(string fullWindowType)
    {
        return Path.Combine(GetLayoutFolderRelativePath(fullWindowType), "window.json");
    }

    /// <summary>
    /// 将安全的相对布局文件夹转换回对应的完整窗口类型。
    /// </summary>
    /// <param name="relativeFolder">由 <see cref="GetLayoutFolderRelativePath"/> 创建的相对文件夹。</param>
    /// <returns>该文件夹表示的完整窗口类型。</returns>
    /// <exception cref="ArgumentException">当 <paramref name="relativeFolder"/> 不是有效的布局文件夹时抛出。</exception>
    public static string ToFullWindowTypeFromRelativeFolder(string relativeFolder)
    {
        var parts = relativeFolder
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 3 && string.Equals(parts[0], "plugin", StringComparison.OrdinalIgnoreCase))
        {
            EnsureSafePathSegment(parts[1], "packageId");
            EnsureSafePathSegment(parts[2], "windowTypeName");
            return $"{PluginPrefix}{parts[1]}/{parts[2]}";
        }

        if (parts.Length == 1)
        {
            EnsureSafePathSegment(parts[0], "windowTypeName");
            return parts[0];
        }

        throw new ArgumentException("Layout folder is not a valid FullWindowType path.", nameof(relativeFolder));
    }

    /// <summary>
    /// 返回完整窗口类型是否可以安全映射到布局路径。
    /// </summary>
    /// <param name="fullWindowType">内置窗口类型名称或插件完整窗口类型。</param>
    /// <returns>当值可以映射到安全布局路径时为 <see langword="true"/>。</returns>
    public static bool IsSafeFullWindowType(string fullWindowType)
    {
        try
        {
            _ = GetLayoutFolderRelativePath(fullWindowType);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 解析格式为 <c>plugin:{PackageId}/{WindowTypeName}</c> 的插件完整窗口类型。
    /// </summary>
    /// <param name="fullWindowType">要解析的完整窗口类型。</param>
    /// <param name="packageId">解析成功时得到的包标识。</param>
    /// <param name="windowTypeName">解析成功时得到的插件窗口类型名称。</param>
    /// <returns>当 <paramref name="fullWindowType"/> 是有效的插件完整窗口类型时为 <see langword="true"/>。</returns>
    public static bool TryParsePluginFullWindowType(
        string fullWindowType,
        out string packageId,
        out string windowTypeName)
    {
        packageId = string.Empty;
        windowTypeName = string.Empty;
        if (string.IsNullOrWhiteSpace(fullWindowType)
            || !fullWindowType.StartsWith(PluginPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var rest = fullWindowType[PluginPrefix.Length..];
        var slash = rest.IndexOf('/');
        if (slash <= 0 || slash == rest.Length - 1 || rest.IndexOf('/', slash + 1) >= 0)
        {
            return false;
        }

        packageId = rest[..slash];
        windowTypeName = rest[(slash + 1)..];
        return IsSafePathSegment(packageId) && IsSafePathSegment(windowTypeName);
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
