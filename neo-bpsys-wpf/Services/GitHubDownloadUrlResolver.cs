using Microsoft.Extensions.Logging;
using System.Net.Http;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Helpers;

namespace neo_bpsys_wpf.Services;

/// <summary>Shared GitHub release download mirror resolver.</summary>
public sealed class GitHubDownloadUrlResolver(
    ISettingsHostService settingsHostService,
    ILogger<GitHubDownloadUrlResolver> logger) : IGitHubDownloadUrlResolver
{
    private readonly Dictionary<string, string> _cache = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public async Task<string> ResolveAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (!uri.Host.Contains("github.com", StringComparison.OrdinalIgnoreCase) &&
             !uri.Host.Contains("githubusercontent.com", StringComparison.OrdinalIgnoreCase)) ||
            !settingsHostService.Settings.CultureInfo.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            return url;

        var preferred = settingsHostService.Settings.GhProxyMirror;
        if (string.IsNullOrWhiteSpace(preferred)) return url;
        lock (_cache) if (_cache.TryGetValue(preferred, out var cached)) return string.IsNullOrEmpty(cached) ? url : cached + url;

        var candidates = new[] { preferred }.Concat(DownloadMirrorPresets.GhProxyMirrorList)
            .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var mirror in candidates)
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                using var response = await client.GetAsync(mirror + url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (!response.IsSuccessStatusCode) continue;
                lock (_cache) _cache[preferred] = mirror;
                return mirror + url;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                logger.LogDebug(ex, "GitHub mirror unavailable: {Mirror}", mirror);
            }
        }
        lock (_cache) _cache[preferred] = string.Empty;
        return url;
    }

    /// <inheritdoc />
    public void ResetCache() { lock (_cache) _cache.Clear(); }
}
