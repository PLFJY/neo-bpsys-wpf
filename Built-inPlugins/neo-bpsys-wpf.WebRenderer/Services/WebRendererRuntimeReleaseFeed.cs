using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace neo_bpsys_wpf.WebRenderer.Services;

/// <summary>
/// 表示从官方 release metadata 解析出的 ASP.NET Core Runtime 下载信息。
/// </summary>
/// <param name="Version">语义化版本号，例如 "10.0.10"。</param>
/// <param name="DownloadUrl">win-x64 installer 的直链 URL。</param>
/// <param name="Sha512">官方提供的 SHA-512 校验值；若 release metadata 未提供则为 <c>null</c>。</param>
public sealed record WebRendererRuntimeReleaseInfo(string Version, string DownloadUrl, string? Sha512);

/// <summary>
/// 查询 Microsoft 官方 release metadata，解析最新 ASP.NET Core 10.0 x64 Runtime 的下载信息。
/// </summary>
public sealed class WebRendererRuntimeReleaseFeed
{
    private const string ReleaseMetadataUrl = "https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json";

    /// <summary>
    /// 当在线查询失败时使用的已知最新版本号。发布新版本后应同步更新此常量。
    /// </summary>
    public const string KnownFallbackVersion = "10.0.10";

    /// <summary>
    /// 当在线查询失败时使用的 fallback 下载 URL。该 URL pattern 由 Microsoft 官方维护，跨版本稳定。
    /// </summary>
    public static readonly string KnownFallbackUrl =
        $"https://builds.dotnet.microsoft.com/dotnet/aspnetcore/Runtime/{KnownFallbackVersion}/aspnetcore-runtime-{KnownFallbackVersion}-win-x64.exe";

    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private readonly ILogger<WebRendererRuntimeReleaseFeed> _logger;

    /// <summary>
    /// 初始化 <see cref="WebRendererRuntimeReleaseFeed"/>。
    /// </summary>
    /// <param name="logger">日志记录器。</param>
    public WebRendererRuntimeReleaseFeed(ILogger<WebRendererRuntimeReleaseFeed> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 获取 fallback 下载信息（不发起网络请求）。
    /// </summary>
    /// <returns>基于 <see cref="KnownFallbackVersion"/> 与 <see cref="KnownFallbackUrl"/> 的下载信息，无 hash 校验。</returns>
    public WebRendererRuntimeReleaseInfo GetFallback()
    {
        _logger.LogWarning("Using fallback ASP.NET Core runtime release info: {Version}", KnownFallbackVersion);
        return new WebRendererRuntimeReleaseInfo(KnownFallbackVersion, KnownFallbackUrl, null);
    }

    /// <summary>
    /// 在线查询最新 ASP.NET Core 10.0 x64 Runtime 下载信息。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>解析成功则返回下载信息；网络或解析失败则返回 <c>null</c>，由调用方 fallback。</returns>
    public async Task<WebRendererRuntimeReleaseInfo?> FetchLatestAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await SharedHttpClient.GetAsync(ReleaseMetadataUrl, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            return TryParseLatest(doc.RootElement);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to fetch ASP.NET Core runtime release metadata from {Url}", ReleaseMetadataUrl);
            return null;
        }
    }

    private static WebRendererRuntimeReleaseInfo? TryParseLatest(JsonElement root)
    {
        if (!root.TryGetProperty("releases", out var releases) || releases.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var release in releases.EnumerateArray())
        {
            if (!release.TryGetProperty("aspnetcore-runtime", out var aspnet) || aspnet.ValueKind != JsonValueKind.Object)
                continue;
            if (!aspnet.TryGetProperty("files", out var files) || files.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var file in files.EnumerateArray())
            {
                if (!file.TryGetProperty("rid", out var rid) || rid.ValueKind != JsonValueKind.String)
                    continue;
                if (!string.Equals(rid.GetString(), "win-x64", StringComparison.Ordinal))
                    continue;
                if (!file.TryGetProperty("url", out var url) || url.ValueKind != JsonValueKind.String)
                    continue;
                if (!file.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String)
                    continue;
                var nameStr = name.GetString();
                if (nameStr is null || !nameStr.EndsWith("-win-x64.exe", StringComparison.Ordinal))
                    continue;

                var version = aspnet.TryGetProperty("version", out var ver) && ver.ValueKind == JsonValueKind.String
                    ? ver.GetString()
                    : null;
                if (string.IsNullOrWhiteSpace(version))
                    continue;

                var downloadUrl = url.GetString();
                if (string.IsNullOrWhiteSpace(downloadUrl))
                    continue;

                string? sha512 = null;
                if (file.TryGetProperty("hash", out var hash) && hash.ValueKind == JsonValueKind.String)
                {
                    var hashStr = hash.GetString();
                    if (!string.IsNullOrWhiteSpace(hashStr))
                        sha512 = hashStr;
                }

                return new WebRendererRuntimeReleaseInfo(version, downloadUrl, sha512);
            }
        }

        return null;
    }
}
