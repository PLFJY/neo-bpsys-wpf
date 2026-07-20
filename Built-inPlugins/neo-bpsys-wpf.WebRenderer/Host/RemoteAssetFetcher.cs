using neo_bpsys_wpf.WebRenderer.Protocol;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace neo_bpsys_wpf.WebRenderer.Host;

internal interface IRemoteAssetAddressPolicy
{
    Task ValidateAsync(Uri uri, CancellationToken cancellationToken);
}

internal sealed class RemoteAssetAddressPolicy : IRemoteAssetAddressPolicy
{
    public async Task ValidateAsync(Uri uri, CancellationToken cancellationToken)
    {
        ValidateUri(uri);
        var addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, cancellationToken);
        if (addresses.Length == 0 || addresses.Any(address => !IsPublicAddress(address)))
            throw new RemoteAssetException("RemoteAssetAddressRejected");
    }

    internal static void ValidateUri(Uri uri)
    {
        if (!uri.IsAbsoluteUri || uri.Scheme is not ("http" or "https")
            || !string.IsNullOrEmpty(uri.UserInfo) || string.IsNullOrWhiteSpace(uri.Host)
            || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
            throw new RemoteAssetException("RemoteAssetUriRejected");
    }

    internal static bool IsPublicAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any)
            || address.IsIPv6LinkLocal || address.IsIPv6Multicast || address.IsIPv6SiteLocal) return false;
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var bytes = address.GetAddressBytes();
            return (bytes[0] & 0xfe) != 0xfc;
        }
        if (address.AddressFamily != AddressFamily.InterNetwork) return false;
        var value = address.GetAddressBytes();
        return value[0] != 0
               && value[0] != 10
               && value[0] != 127
               && !(value[0] == 100 && value[1] is >= 64 and <= 127)
               && !(value[0] == 169 && value[1] == 254)
               && !(value[0] == 172 && value[1] is >= 16 and <= 31)
               && !(value[0] == 192 && value[1] == 168)
               && !(value[0] == 192 && value[1] == 0)
               && !(value[0] == 192 && value[1] == 0 && value[2] == 2)
               && !(value[0] == 198 && value[1] is 18 or 19)
               && !(value[0] == 198 && value[1] == 51 && value[2] == 100)
               && !(value[0] == 203 && value[1] == 0 && value[2] == 113)
               && value[0] < 224;
    }

    internal static async ValueTask<Stream> ConnectPublicAsync(SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
        var allowed = addresses.Where(IsPublicAddress).ToArray();
        if (allowed.Length == 0 || allowed.Length != addresses.Length)
            throw new RemoteAssetException("RemoteAssetAddressRejected");

        Exception? last = null;
        foreach (var address in allowed)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception ex) when (ex is SocketException or OperationCanceledException)
            {
                socket.Dispose();
                last = ex;
                if (ex is OperationCanceledException) throw;
            }
        }
        throw new HttpRequestException("Remote asset connection failed.", last);
    }
}

internal sealed class RemoteAssetFetcher
{
    internal const long MaxResponseBytes = 10 * 1024 * 1024;
    private const long MaxMemoryBytes = 64 * 1024 * 1024;
    private const long MaxDiskBytes = 512 * 1024 * 1024;
    private const int MaxRedirects = 5;
    private static readonly TimeSpan TotalTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan MaxDiskAge = TimeSpan.FromDays(7);
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
        { "image/png", "image/jpeg", "image/webp", "image/gif" };
    private readonly HttpClient _httpClient;
    private readonly IRemoteAssetAddressPolicy _addressPolicy;
    private readonly string _cacheRoot;
    private readonly TimeSpan _totalTimeout;
    private readonly long _maxResponseBytes;
    private readonly int _maxRedirects;
    private readonly object _gate = new();
    private readonly Dictionary<string, RemoteAssetCacheEntry> _authorized = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MemoryEntry> _memory = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Lazy<Task<RemoteAssetCacheEntry>>> _inflight = new(StringComparer.Ordinal);
    private long _memoryBytes;
    private long _generation;
    private CancellationTokenSource _generationCancellation = new();

    public RemoteAssetFetcher(IHttpClientFactory httpClientFactory, RemoteAssetAddressPolicy addressPolicy)
        : this(httpClientFactory.CreateClient("RemoteAssets"), addressPolicy,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "neo-bpsys-wpf", "WebRenderer", "RemoteAssets"))
    {
    }

    internal RemoteAssetFetcher(HttpClient httpClient, IRemoteAssetAddressPolicy addressPolicy, string cacheRoot,
        TimeSpan? totalTimeout = null, long maxResponseBytes = MaxResponseBytes, int maxRedirects = MaxRedirects)
    {
        _httpClient = httpClient;
        _addressPolicy = addressPolicy;
        _cacheRoot = cacheRoot;
        _totalTimeout = totalTimeout ?? TotalTimeout;
        _maxResponseBytes = maxResponseBytes;
        _maxRedirects = maxRedirects;
        Directory.CreateDirectory(_cacheRoot);
        foreach (var temporary in Directory.EnumerateFiles(_cacheRoot, "*.tmp"))
            try { File.Delete(temporary); } catch { }
        PruneDiskCache();
    }

    public void SetGeneration(long generation)
    {
        lock (_gate)
        {
            if (generation == _generation) return;
            _generationCancellation.Cancel();
            _generationCancellation.Dispose();
            _generationCancellation = new CancellationTokenSource();
            _generation = generation;
            _authorized.Clear();
        }
    }

    public async Task<RemoteAssetCacheEntry> FetchAsync(WebRemoteAssetFetch request,
        CancellationToken cancellationToken)
    {
        if (request.Generation != Volatile.Read(ref _generation) || !IsToken(request.Token)
            || !IsToken(request.Revision)) throw new RemoteAssetException("RemoteAssetStaleGeneration");
        CancellationToken generationToken;
        lock (_gate) generationToken = _generationCancellation.Token;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, generationToken);
        var uri = new Uri(request.NormalizedUri, UriKind.Absolute);
        await _addressPolicy.ValidateAsync(uri, linked.Token);

        if (TryReadMemory(request.Revision, out var memory)) return Authorize(request, memory);
        var disk = await TryReadDiskAsync(request.Revision, linked.Token);
        if (disk is not null) return Authorize(request, disk);

        var lazy = _inflight.GetOrAdd(request.Revision, _ => new Lazy<Task<RemoteAssetCacheEntry>>(
            () => DownloadAsync(uri, request.Revision, generationToken), LazyThreadSafetyMode.ExecutionAndPublication));
        try
        {
            var entry = await lazy.Value.WaitAsync(linked.Token);
            return Authorize(request, entry);
        }
        finally
        {
            if (lazy.IsValueCreated && lazy.Value.IsCompleted) _inflight.TryRemove(request.Revision, out _);
        }
    }

    public bool TryGet(string token, long generation, out RemoteAssetCacheEntry entry)
    {
        lock (_gate)
        {
            if (generation == _generation && _authorized.TryGetValue(token, out entry!)) return true;
        }
        entry = null!;
        return false;
    }

    private RemoteAssetCacheEntry Authorize(WebRemoteAssetFetch request, RemoteAssetCacheEntry entry)
    {
        lock (_gate)
        {
            if (request.Generation != _generation) throw new RemoteAssetException("RemoteAssetStaleGeneration");
            _authorized[request.Token] = entry;
        }
        return entry;
    }

    private async Task<RemoteAssetCacheEntry> DownloadAsync(Uri initialUri, string revision,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_totalTimeout);
        var current = initialUri;
        for (var redirects = 0; ; redirects++)
        {
            await _addressPolicy.ValidateAsync(current, timeout.Token);
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if ((int)response.StatusCode is >= 300 and < 400)
            {
                if (redirects >= _maxRedirects || response.Headers.Location is null)
                    throw new RemoteAssetException("RemoteAssetRedirectLimitExceeded");
                current = response.Headers.Location.IsAbsoluteUri
                    ? response.Headers.Location
                    : new Uri(current, response.Headers.Location);
                continue;
            }
            if (!response.IsSuccessStatusCode) throw new RemoteAssetException("RemoteAssetHttpFailure");
            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType is null || !AllowedContentTypes.Contains(contentType))
                throw new RemoteAssetException("RemoteAssetContentTypeRejected");
            if (response.Content.Headers.ContentLength is > 0 && response.Content.Headers.ContentLength > _maxResponseBytes)
                throw new RemoteAssetException("RemoteAssetTooLarge");

            await using var input = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var output = new MemoryStream();
            var buffer = new byte[81920];
            while (true)
            {
                var read = await input.ReadAsync(buffer, timeout.Token);
                if (read == 0) break;
                if (output.Length + read > _maxResponseBytes) throw new RemoteAssetException("RemoteAssetTooLarge");
                output.Write(buffer, 0, read);
            }
            var bytes = output.ToArray();
            if (!MatchesSignature(contentType, bytes))
                throw new RemoteAssetException("RemoteAssetSignatureRejected");
            var entry = new RemoteAssetCacheEntry(bytes, contentType, revision);
            await WriteDiskAsync(entry, timeout.Token);
            AddMemory(entry);
            return entry;
        }
    }

    private bool TryReadMemory(string revision, out RemoteAssetCacheEntry entry)
    {
        lock (_gate)
        {
            if (_memory.TryGetValue(revision, out var memory))
            {
                memory.LastAccessUtc = DateTime.UtcNow;
                entry = memory.Entry;
                return true;
            }
        }
        entry = null!;
        return false;
    }

    private async Task<RemoteAssetCacheEntry?> TryReadDiskAsync(string revision,
        CancellationToken cancellationToken)
    {
        var metadataPath = Path.Combine(_cacheRoot, revision + ".json");
        var dataPath = Path.Combine(_cacheRoot, revision + ".bin");
        if (!File.Exists(metadataPath) || !File.Exists(dataPath)) return null;
        try
        {
            var metadata = JsonSerializer.Deserialize<DiskMetadata>(
                await File.ReadAllTextAsync(metadataPath, cancellationToken));
            if (metadata is null || metadata.Revision != revision || !AllowedContentTypes.Contains(metadata.ContentType)
                || DateTime.UtcNow - metadata.LastAccessUtc > MaxDiskAge) return null;
            var info = new FileInfo(dataPath);
            if (info.Length is <= 0 or > MaxResponseBytes) return null;
            var bytes = await File.ReadAllBytesAsync(dataPath, cancellationToken);
            if (!MatchesSignature(metadata.ContentType, bytes)) return null;
            var entry = new RemoteAssetCacheEntry(bytes, metadata.ContentType, revision);
            AddMemory(entry);
            var updated = metadata with { LastAccessUtc = DateTime.UtcNow };
            await File.WriteAllTextAsync(metadataPath, JsonSerializer.Serialize(updated), cancellationToken);
            File.SetLastWriteTimeUtc(dataPath, updated.LastAccessUtc);
            return entry;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private async Task WriteDiskAsync(RemoteAssetCacheEntry entry, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid().ToString("N");
        var dataPath = Path.Combine(_cacheRoot, entry.Revision + ".bin");
        var metadataPath = Path.Combine(_cacheRoot, entry.Revision + ".json");
        var temporaryData = Path.Combine(_cacheRoot, id + ".tmp");
        var temporaryMetadata = Path.Combine(_cacheRoot, id + ".json.tmp");
        try
        {
            await File.WriteAllBytesAsync(temporaryData, entry.Bytes, cancellationToken);
            await File.WriteAllTextAsync(temporaryMetadata,
                JsonSerializer.Serialize(new DiskMetadata(entry.Revision, entry.ContentType, DateTime.UtcNow)), cancellationToken);
            File.Move(temporaryData, dataPath, true);
            File.Move(temporaryMetadata, metadataPath, true);
        }
        finally
        {
            try { File.Delete(temporaryData); } catch { }
            try { File.Delete(temporaryMetadata); } catch { }
        }
        PruneDiskCache();
    }

    private void AddMemory(RemoteAssetCacheEntry entry)
    {
        lock (_gate)
        {
            if (_memory.ContainsKey(entry.Revision)) return;
            _memory[entry.Revision] = new(entry, DateTime.UtcNow);
            _memoryBytes += entry.Bytes.LongLength;
            foreach (var pair in _memory.OrderBy(pair => pair.Value.LastAccessUtc).ToArray())
            {
                if (_memoryBytes <= MaxMemoryBytes) break;
                _memory.Remove(pair.Key);
                _memoryBytes -= pair.Value.Entry.Bytes.LongLength;
            }
        }
    }

    private void PruneDiskCache()
    {
        try
        {
            var entries = Directory.EnumerateFiles(_cacheRoot, "*.bin")
                .Select(path => new FileInfo(path)).OrderByDescending(info => info.LastWriteTimeUtc).ToArray();
            long retained = 0;
            foreach (var info in entries)
            {
                retained += info.Length;
                if (retained <= MaxDiskBytes && DateTime.UtcNow - info.LastWriteTimeUtc <= MaxDiskAge) continue;
                try { info.Delete(); } catch { }
                try { File.Delete(Path.ChangeExtension(info.FullName, ".json")); } catch { }
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static bool MatchesSignature(string contentType, byte[] bytes) => contentType.ToLowerInvariant() switch
    {
        "image/png" => bytes.AsSpan().StartsWith(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }),
        "image/jpeg" => bytes.AsSpan().StartsWith(new byte[] { 0xff, 0xd8, 0xff }),
        "image/gif" => bytes.AsSpan().StartsWith("GIF87a"u8) || bytes.AsSpan().StartsWith("GIF89a"u8),
        "image/webp" => bytes.Length >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8)
                                          && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8),
        _ => false
    };

    private static bool IsToken(string value) => value.Length == 64 && value.All(Uri.IsHexDigit);

    private sealed class MemoryEntry(RemoteAssetCacheEntry entry, DateTime lastAccessUtc)
    {
        public RemoteAssetCacheEntry Entry { get; } = entry;
        public DateTime LastAccessUtc { get; set; } = lastAccessUtc;
    }

    private sealed record DiskMetadata(string Revision, string ContentType, DateTime LastAccessUtc);
}

internal sealed record RemoteAssetCacheEntry(byte[] Bytes, string ContentType, string Revision);

internal sealed class RemoteAssetException(string diagnostic) : Exception(diagnostic)
{
    public string Diagnostic { get; } = diagnostic;
}
