using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Models.Plugins;
using neo_bpsys_wpf.Services.Abstractions;

namespace neo_bpsys_wpf.Services;

/// <summary>按源、语言与镜像隔离的目录磁盘缓存，所有磁盘及 JSON 工作在后台执行。</summary>
public sealed class PluginMarketCatalogCache : IPluginMarketCatalogCache
{
    private const long MaximumBytes = 8 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true
    };
    private readonly string _directory;
    private readonly ILogger<PluginMarketCatalogCache> _logger;
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    /// <summary>创建使用应用用户数据目录的缓存。</summary>
    /// <param name="logger">诊断日志。</param>
    public PluginMarketCatalogCache(ILogger<PluginMarketCatalogCache> logger)
        : this(AppConstants.PluginMarketCachePath, logger) { }

    internal PluginMarketCatalogCache(string directory, ILogger<PluginMarketCatalogCache> logger)
    {
        _directory = directory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<PluginMarketItem>?> ReadAsync(PluginMarketCatalogContext context, CancellationToken cancellationToken = default)
        => Task.Run<IReadOnlyList<PluginMarketItem>?>(async () =>
        {
            try
            {
                await using var file = new FileStream(GetPath(context), FileMode.Open, FileAccess.Read,
                    FileShare.Read | FileShare.Delete, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
                if (file.Length > MaximumBytes) throw new InvalidDataException("Plugin catalog cache is too large.");
                var snapshot = await JsonSerializer.DeserializeAsync<Snapshot>(file, JsonOptions, cancellationToken).ConfigureAwait(false);
                if (snapshot is null || snapshot.SchemaVersion != 1 || snapshot.Context != context) return null;
                Validate(snapshot.Items);
                return snapshot.Items.Select(entry => entry.ToModel()).ToArray();
            }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
            {
                _logger.LogWarning(ex, "Plugin catalog cache is unavailable for {Source}.", context.Source);
                return null;
            }
        }, cancellationToken);

    /// <inheritdoc/>
    public Task WriteAsync(PluginMarketCatalogContext context, IReadOnlyList<PluginMarketItem> items, CancellationToken cancellationToken = default)
    {
        // 先在调用方线程捕获不可变目录字段，后台不读取可能正在更新的行对象。
        var snapshot = new Snapshot(1, context, DateTimeOffset.UtcNow, items.Select(Entry.FromModel).ToArray());
        return Task.Run(async () =>
        {
            await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            string? temporaryPath = null;
            try
            {
                Validate(snapshot.Items);
                Directory.CreateDirectory(_directory);
                var path = GetPath(context);
                temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                await using (var file = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                    FileShare.None, 4096, FileOptions.Asynchronous))
                {
                    await JsonSerializer.SerializeAsync(file, snapshot, JsonOptions, cancellationToken).ConfigureAwait(false);
                    if (file.Length > MaximumBytes) throw new InvalidDataException("Plugin catalog cache is too large.");
                    await file.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temporaryPath, path, overwrite: true);
                temporaryPath = null;
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
            {
                _logger.LogWarning(ex, "Could not save plugin catalog cache for {Source}.", context.Source);
            }
            finally
            {
                if (temporaryPath is not null)
                {
                    try { File.Delete(temporaryPath); }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    { _logger.LogWarning(ex, "Could not remove temporary plugin catalog cache."); }
                }
                _writeGate.Release();
            }
        }, cancellationToken);
    }

    private string GetPath(PluginMarketCatalogContext context)
    {
        var identity = JsonSerializer.Serialize(context, JsonOptions);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        return Path.Combine(_directory, hash + ".json");
    }

    private static void Validate(Entry[] entries)
    {
        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
            if (entry is null || string.IsNullOrWhiteSpace(entry.Id) || !identities.Add(entry.Id))
                throw new InvalidDataException("Plugin catalog cache contains missing or duplicate identities.");
    }

    private sealed record Snapshot(int SchemaVersion, PluginMarketCatalogContext Context, DateTimeOffset UpdatedAt, Entry[] Items);

    // 缓存格式仅包含目录字段；模型新增业务/呈现属性不会意外进入磁盘契约。
    private sealed record Entry(string Id, string Name, string Description, string Version, string ApiVersion,
        string Author, string Icon, string Readme, string Url, string DownloadUrl, string Sha256,
        string ResolvedIconUrl, string ResolvedReadmeUrl, string ResolvedDownloadUrl)
    {
        internal static Entry FromModel(PluginMarketItem item) => new(item.Id, item.Name, item.Description,
            item.Version, item.ApiVersion, item.Author, item.Icon, item.Readme, item.Url, item.DownloadUrl,
            item.Sha256, item.ResolvedIconUrl, item.ResolvedReadmeUrl, item.ResolvedDownloadUrl);

        internal PluginMarketItem ToModel() => new()
        {
            Id = Id, Name = Name, Description = Description, Version = Version, ApiVersion = ApiVersion,
            Author = Author, Icon = Icon, Readme = Readme, Url = Url, DownloadUrl = DownloadUrl, Sha256 = Sha256,
            ResolvedIconUrl = ResolvedIconUrl, ResolvedReadmeUrl = ResolvedReadmeUrl, ResolvedDownloadUrl = ResolvedDownloadUrl
        };
    }
}
