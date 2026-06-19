namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>Resolves GitHub asset URLs through the application's configured mirror chain.</summary>
public interface IGitHubDownloadUrlResolver
{
    /// <summary>Resolves a URL, returning the original URL when no mirror is applicable.</summary>
    /// <param name="url">Original GitHub URL.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The URL to download.</returns>
    Task<string> ResolveAsync(string url, CancellationToken cancellationToken = default);
    /// <summary>Clears the cached mirror probe result.</summary>
    void ResetCache();
}
