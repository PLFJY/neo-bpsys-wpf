using System.Text.RegularExpressions;

namespace neo_bpsys_wpf.WebRenderer.Host;

/// <summary>验证已部署 Web client 的入口页与本地静态引用。</summary>
internal static partial class StaticClientVerifier
{
    private const string BuildIdMetaName = "web-renderer-client-build-id";

    /// <summary>读取并验证 sidecar 最终运行目录中的 Web client。</summary>
    /// <param name="staticRoot">最终 <c>wwwroot</c> 目录。</param>
    /// <returns>已验证的入口页路径、client build id 与本地资源 URL。</returns>
    /// <exception cref="InvalidOperationException">入口页、build id 或本地资源引用无效时引发。</exception>
    public static VerifiedStaticClient Verify(string staticRoot)
    {
        var fullRoot = Path.GetFullPath(staticRoot);
        var indexPath = Path.Combine(fullRoot, "index.html");
        if (!File.Exists(indexPath))
            throw new InvalidOperationException($"Web Renderer static client index.html is missing: {indexPath}");

        var html = File.ReadAllText(indexPath);
        var buildId = BuildIdRegex().Match(html).Groups["value"].Value;
        if (string.IsNullOrWhiteSpace(buildId))
            throw new InvalidOperationException($"Web Renderer static client index.html is missing meta '{BuildIdMetaName}': {indexPath}");

        var urls = ScriptUrlRegex().Matches(html).Select(match => match.Groups["url"].Value)
            .Concat(LinkUrlRegex().Matches(html).Select(match => match.Groups["url"].Value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (urls.Length == 0)
            throw new InvalidOperationException($"Web Renderer static client index.html has no local script or link references: {indexPath}");

        foreach (var url in urls)
            ValidateLocalReference(fullRoot, url, indexPath);

        return new(indexPath, buildId, urls);
    }

    private static void ValidateLocalReference(string staticRoot, string url, string indexPath)
    {
        if (string.IsNullOrWhiteSpace(url) || url.StartsWith("//", StringComparison.Ordinal) ||
            Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException($"Web Renderer static client index.html contains a non-local reference '{url}': {indexPath}");
        }

        var localPath = url.Split(['?', '#'], 2)[0];
        if (string.IsNullOrWhiteSpace(localPath))
            throw new InvalidOperationException($"Web Renderer static client index.html contains an empty local reference: {indexPath}");

        var decodedPath = Uri.UnescapeDataString(localPath).Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var rootWithSeparator = staticRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var filePath = Path.GetFullPath(Path.Combine(staticRoot, decodedPath));
        if (!filePath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) || !File.Exists(filePath))
        {
            throw new InvalidOperationException($"Web Renderer static client reference is missing or escapes wwwroot: '{url}' resolved to '{filePath}' from '{indexPath}'.");
        }
    }

    [GeneratedRegex("<meta\\s+[^>]*name=[\\\"']web-renderer-client-build-id[\\\"'][^>]*content=[\\\"'](?<value>[^\\\"']+)[\\\"']", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BuildIdRegex();

    [GeneratedRegex("<script\\b[^>]*\\bsrc=[\\\"'](?<url>[^\\\"']+)[\\\"'][^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ScriptUrlRegex();

    [GeneratedRegex("<link\\b[^>]*\\bhref=[\\\"'](?<url>[^\\\"']+)[\\\"'][^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LinkUrlRegex();
}

/// <summary>已验证 Web client 的部署信息。</summary>
/// <param name="IndexPath">最终入口页的物理路径。</param>
/// <param name="BuildId">入口页和客户端共享的构建标识。</param>
/// <param name="LocalResourceUrls">入口页中所有本地 script/link 资源 URL。</param>
internal sealed record VerifiedStaticClient(string IndexPath, string BuildId, IReadOnlyList<string> LocalResourceUrls);
