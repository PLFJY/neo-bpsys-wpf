using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Models.RemoteAnnouncements;
using neo_bpsys_wpf.Services.Abstractions;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using WPFLocalizeExtension.Engine;

namespace neo_bpsys_wpf.Services;

/// <summary>远程公告的增量同步与本机缓存服务。</summary>
public sealed partial class RemoteAnnouncementService(
    IHttpClientFactory httpClientFactory,
    ILogger<RemoteAnnouncementService> logger) : IRemoteAnnouncementService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly SemaphoreSlim _gate = new(1, 1);
    private RemoteAnnouncementManifest? _manifest;
    private HashSet<string> _seenIds = new(StringComparer.Ordinal);
    private IReadOnlyList<RemoteAnnouncement> _announcements = [];
    private int _refreshStarted;

    /// <inheritdoc />
    public IReadOnlyList<RemoteAnnouncement> Announcements => _announcements;

    /// <inheritdoc />
    public IReadOnlyCollection<string> SeenAnnouncementIds => _seenIds.ToArray();

    /// <inheritdoc />
    public int UnseenCount => _announcements.Count(a => !_seenIds.Contains(a.Id));

    /// <inheritdoc />
    public bool IsStartupCheckCompleted { get; private set; }

    /// <inheritdoc />
    public event EventHandler? AnnouncementsChanged;

    /// <inheritdoc />
    public async Task InitializeFromCacheAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            try
            {
                if (File.Exists(AppConstants.RemoteAnnouncementsStatePath))
                {
                    var state = await ReadJsonAsync<RemoteAnnouncementState>(AppConstants.RemoteAnnouncementsStatePath, cancellationToken);
                    _seenIds = state?.SeenAnnouncementIds is { } ids
                        ? new HashSet<string>(ids, StringComparer.Ordinal)
                        : new HashSet<string>(StringComparer.Ordinal);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                logger.LogWarning(ex, "Remote announcement state is damaged; using empty seen state.");
                _seenIds = new HashSet<string>(StringComparer.Ordinal);
            }

            try
            {
                if (File.Exists(AppConstants.RemoteAnnouncementsManifestPath))
                {
                    var manifest = await ReadJsonAsync<RemoteAnnouncementManifest>(AppConstants.RemoteAnnouncementsManifestPath, cancellationToken);
                    if (manifest?.SchemaVersion == 1 && manifest.Announcements is not null)
                    {
                        _manifest = manifest;
                        _announcements = await LoadAnnouncementsAsync(manifest, cancellationToken);
                        logger.LogInformation("Using local announcement cache: {Count} announcements.", _announcements.Count);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                logger.LogWarning(ex, "Remote announcement manifest cache is damaged.");
            }

            AnnouncementsChanged?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _refreshStarted, 1) != 0)
            return Task.CompletedTask;

        return RefreshCoreAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task RefreshOnDemandAsync(CancellationToken cancellationToken = default) =>
        RefreshCoreAsync(cancellationToken);

    private async Task RefreshCoreAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            using var client = httpClientFactory.CreateClient("RemoteAnnouncements");
            var preferGitee = IsSimplifiedChinese(LocalizeDictionary.CurrentCulture);
            var sources = preferGitee
                ? new[] { AppConstants.GiteeAnnouncementRawBaseUrl, AppConstants.GitHubAnnouncementRawBaseUrl }
                : new[] { AppConstants.GitHubAnnouncementRawBaseUrl, AppConstants.GiteeAnnouncementRawBaseUrl };
            foreach (var source in sources)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (await TrySyncFromSourceAsync(client, new Uri(source), cancellationToken))
                        return;
                }
                catch (Exception ex) when (ex is HttpRequestException or JsonException
                    || ex is OperationCanceledException && !cancellationToken.IsCancellationRequested)
                {
                    logger.LogWarning(ex, "Remote announcement source failed: {Source}.", source);
                }
                logger.LogWarning("Remote announcement sync from {Source} failed; trying next source if available.", source);
            }
            logger.LogWarning("All remote announcement sources failed; using local cache.");
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException or JsonException or TaskCanceledException or UriFormatException)
        {
            logger.LogWarning(ex, "Remote announcement sync failed; using local cache.");
        }
        finally
        {
            IsStartupCheckCompleted = true;
            _gate.Release();
            AnnouncementsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private static bool IsSimplifiedChinese(CultureInfo culture)
    {
        for (var current = culture; !string.IsNullOrEmpty(current.Name); current = current.Parent)
        {
            if (current.Name.Equals("zh-Hans", StringComparison.OrdinalIgnoreCase)
                || current.Name.Equals("zh-CHS", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private async Task<bool> TrySyncFromSourceAsync(HttpClient client, Uri rawBaseUri, CancellationToken cancellationToken)
    {
        logger.LogInformation("Remote announcement manifest fetch started.");
        var bytes = await client.GetByteArrayAsync(new Uri(rawBaseUri, "manifest.json"), cancellationToken);
        var remoteManifest = JsonSerializer.Deserialize<RemoteAnnouncementManifest>(bytes, JsonOptions);
        if (remoteManifest?.SchemaVersion != 1 || remoteManifest.Announcements is null)
            throw new JsonException("Unsupported remote announcement manifest.");

        var currentVersion = ParseVersion(AppConstants.AppVersion, allowSuffix: true);
        if (currentVersion is null)
        {
            logger.LogWarning("Cannot parse application version for remote announcements: {Version}.", AppConstants.AppVersion);
            return false;
        }

        var existing = _manifest?.Announcements.Where(e => e is not null && !string.IsNullOrWhiteSpace(e.Id))
            .GroupBy(e => e.Id, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal)
            ?? new Dictionary<string, RemoteAnnouncementManifestEntry>(StringComparer.Ordinal);
        var allSynced = true;
        var pendingBodies = new List<(string Path, byte[] Bytes)>();
        var seenManifestIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in remoteManifest.Announcements)
        {
            if (!seenManifestIds.Add(entry.Id))
            {
                logger.LogWarning("Duplicate remote announcement ID {Id}; skipping.", entry.Id);
                allSynced = false;
                continue;
            }
            if (!IsApplicable(entry, currentVersion, logger))
                continue;

            var relativePath = string.IsNullOrWhiteSpace(entry.Path)
                ? $"announcements/{entry.Id}.json"
                : entry.Path;
            if (!TryGetRawAnnouncementUri(rawBaseUri, relativePath, out var bodyUri))
            {
                logger.LogWarning("Invalid raw path for announcement {Id}: {Path}.", entry.Id, relativePath);
                allSynced = false;
                continue;
            }

            var path = GetCachePath(entry.Id);
            var cacheMatches = existing.TryGetValue(entry.Id, out var oldEntry)
                && string.Equals(oldEntry.Sha256, entry.Sha256, StringComparison.OrdinalIgnoreCase)
                && await IsValidCacheAsync(path, entry, cancellationToken);
            if (cacheMatches)
                continue;

            try
            {
                var bodyBytes = await client.GetByteArrayAsync(bodyUri, cancellationToken);
                var actualHash = Convert.ToHexString(SHA256.HashData(bodyBytes));
                if (!string.Equals(actualHash, entry.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogWarning("Announcement {Id} hash mismatch.", entry.Id);
                    allSynced = false;
                    continue;
                }
                var announcement = JsonSerializer.Deserialize<RemoteAnnouncement>(bodyBytes, JsonOptions);
                if (announcement?.SchemaVersion != 1 || announcement.Id != entry.Id
                    || !Enum.IsDefined(announcement.Level) || announcement.Title is null || announcement.Content is null)
                {
                    logger.LogWarning("Announcement {Id} has invalid JSON content.", entry.Id);
                    allSynced = false;
                    continue;
                }
                pendingBodies.Add((path, bodyBytes));
                logger.LogInformation("{Action} announcement {Id}.", oldEntry is null ? "Downloaded" : "Updated", entry.Id);
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException or JsonException
                || ex is OperationCanceledException && !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Remote announcement fetch failed for {Id}.", entry.Id);
                allSynced = false;
            }
        }

        if (!allSynced)
        {
            logger.LogWarning("Remote announcement sync incomplete; retaining last successful manifest.");
            return false;
        }

        foreach (var pending in pendingBodies)
            await WriteAtomicallyAsync(pending.Path, pending.Bytes, cancellationToken);
        await WriteAtomicallyAsync(AppConstants.RemoteAnnouncementsManifestPath, bytes, cancellationToken);
        _manifest = remoteManifest;
        _announcements = await LoadAnnouncementsAsync(remoteManifest, cancellationToken);
        logger.LogInformation("Remote announcement manifest synced; {Count} unseen.", UnseenCount);
        return true;
    }

    /// <inheritdoc />
    public async Task MarkAllCurrentAsSeenAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var next = new HashSet<string>(_seenIds, StringComparer.Ordinal);
            next.UnionWith(_announcements.Select(a => a.Id));
            var state = new RemoteAnnouncementState { SeenAnnouncementIds = next };
            await WriteAtomicallyAsync(AppConstants.RemoteAnnouncementsStatePath,
                JsonSerializer.SerializeToUtf8Bytes(state, JsonOptions), cancellationToken);
            _seenIds = next;
            AnnouncementsChanged?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IReadOnlyList<RemoteAnnouncement>> LoadAnnouncementsAsync(RemoteAnnouncementManifest manifest, CancellationToken cancellationToken)
    {
        var currentVersion = ParseVersion(AppConstants.AppVersion, allowSuffix: true);
        if (currentVersion is null)
            return [];
        var result = new List<RemoteAnnouncement>();
        foreach (var entry in manifest.Announcements.Where(e => e is not null).DistinctBy(e => e.Id))
        {
            if (!IsApplicable(entry, currentVersion, logger))
                continue;
            var path = GetCachePath(entry.Id);
            try
            {
                if (!await IsValidCacheAsync(path, entry, cancellationToken))
                    continue;
                var body = await ReadJsonAsync<RemoteAnnouncement>(path, cancellationToken);
                if (body?.SchemaVersion == 1 && body.Id == entry.Id && Enum.IsDefined(body.Level)
                    && body.Title is not null && body.Content is not null)
                    result.Add(body);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                logger.LogWarning(ex, "Cached announcement {Id} is damaged.", entry.Id);
            }
        }
        return result.OrderByDescending(a => a.PublishedAt).ThenByDescending(a => a.Id, StringComparer.Ordinal).ToArray();
    }

    private static async Task<bool> IsValidCacheAsync(string path, RemoteAnnouncementManifestEntry entry, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
            return false;
        try
        {
            var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
            return string.Equals(Convert.ToHexString(SHA256.HashData(bytes)), entry.Sha256, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static async Task<T?> ReadJsonAsync<T>(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
    }

    private static async Task WriteAtomicallyAsync(string path, byte[] bytes, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temporary, bytes, cancellationToken);
            File.Move(temporary, path, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static string GetCachePath(string id) => Path.Combine(AppConstants.RemoteAnnouncementsCachePath, id + ".json");

    private static bool TryGetRawAnnouncementUri(Uri rawBaseUri, string? relativePath, out Uri uri)
    {
        uri = null!;
        if (string.IsNullOrWhiteSpace(relativePath) || relativePath.StartsWith('/')
            || relativePath.Split('/').Any(segment => segment is "." or ".."
                || !SafeRawPathSegment().IsMatch(segment)))
            return false;

        var candidate = new Uri(rawBaseUri, relativePath);
        if (candidate.Scheme != Uri.UriSchemeHttps
            || !string.Equals(candidate.Host, rawBaseUri.Host, StringComparison.OrdinalIgnoreCase)
            || !candidate.AbsoluteUri.StartsWith(rawBaseUri.AbsoluteUri, StringComparison.Ordinal))
            return false;

        uri = candidate;
        return true;
    }

    private static bool IsApplicable(RemoteAnnouncementManifestEntry entry, Version currentVersion, ILogger logger)
    {
        if (!entry.Enabled) return false;
        if (string.IsNullOrWhiteSpace(entry.Id) || !SafeId().IsMatch(entry.Id)
            || !Regex.IsMatch(entry.Sha256 ?? "", "^[0-9a-fA-F]{64}$"))
        {
            logger.LogWarning("Invalid remote announcement ID or SHA-256: {Id}.", entry.Id);
            return false;
        }
        if (entry.Channels is null || !entry.Channels.Contains(CurrentChannel, StringComparer.OrdinalIgnoreCase))
            return false;
        var min = entry.MinAppVersion is null ? null : ParseVersion(entry.MinAppVersion);
        var max = entry.MaxAppVersion is null ? null : ParseVersion(entry.MaxAppVersion);
        if ((entry.MinAppVersion is not null && min is null) || (entry.MaxAppVersion is not null && max is null))
        {
            logger.LogWarning("Invalid version bounds for announcement {Id}.", entry.Id);
            return false;
        }
        return (min is null || currentVersion >= min) && (max is null || currentVersion <= max);
    }

    private static Version? ParseVersion(string value, bool allowSuffix = false)
    {
        var normalized = value.Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V')) normalized = normalized[1..];
        var match = VersionPrefix().Match(normalized);
        if (!match.Success || (!allowSuffix && match.Length != normalized.Length)
            || (match.Length < normalized.Length && normalized[match.Length] == '.'))
            return null;
        var parts = match.Value.Split('.');
        if (parts.Any(part => !int.TryParse(part, out _)))
            return null;
        var numbers = parts.Select(int.Parse).ToArray();
        return new Version(numbers[0], numbers[1],
            numbers.Length > 2 ? numbers[2] : 0,
            numbers.Length > 3 ? numbers[3] : 0);
    }

    private static string CurrentChannel
    {
        get
        {
#if BETA
            return "beta";
#elif PREVIEW
            return "preview";
#else
            return "release";
#endif
        }
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{1,100}$")]
    private static partial Regex SafeId();

    [GeneratedRegex("^[A-Za-z0-9._-]+$")]
    private static partial Regex SafeRawPathSegment();

    [GeneratedRegex("^\\d+(?:\\.\\d+){1,3}")]
    private static partial Regex VersionPrefix();
}
