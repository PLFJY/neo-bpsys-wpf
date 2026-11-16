using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace neo_bpsys_wpf.ProductTour;

/// <summary>
/// 持久化教程状态。
/// </summary>
public interface ITutorialStateStore
{
    /// <summary>加载教程状态。</summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>加载到的教程状态。</returns>
    Task<TutorialState> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>保存教程状态。</summary>
    /// <param name="state">要保存的状态。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task SaveAsync(TutorialState state, CancellationToken cancellationToken = default);

    /// <summary>清除所有教程状态。</summary>
    /// <param name="cancellationToken">取消令牌。</param>
    Task ResetAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 基于 JSON 文件的教程状态存储。
/// </summary>
public sealed class TutorialStateStore : ITutorialStateStore
{
    private const long MaxStateFileBytes = 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        MaxDepth = 32,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _statePath;
    private readonly ILogger<TutorialStateStore>? _logger;
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    /// <summary>
    /// 初始化 <see cref="TutorialStateStore"/> 类的新实例。
    /// </summary>
    /// <param name="logger">可选的日志记录器。</param>
    public TutorialStateStore(ILogger<TutorialStateStore>? logger = null)
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "neo-bpsys-wpf");
        _statePath = Path.Combine(appData, "TutorialState.json");
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TutorialState> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_statePath))
        {
            return new TutorialState();
        }

        try
        {
            await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }

        try
        {
            var fileInfo = new FileInfo(_statePath);
            if (fileInfo.Length > MaxStateFileBytes)
            {
                _logger?.LogWarning("教程状态文件过大，已保留原文件并忽略：{Path}", _statePath);
                return new TutorialState();
            }

            await using var stream = new FileStream(
                _statePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            return await JsonSerializer.DeserializeAsync<TutorialState>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false) ?? new TutorialState();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (JsonException ex)
        {
            // 只有格式损坏才备份并忽略；取消和文件占用绝不能删除用户进度。
            _logger?.LogWarning(ex, "教程状态文件损坏，已备份并忽略：{Path}", _statePath);
            TryBackupCorruptStateFile();
            return new TutorialState();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger?.LogWarning(ex, "读取教程状态失败，本次使用空状态但不修改原文件：{Path}", _statePath);
            return new TutorialState();
        }
        finally
        {
            _writeGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(TutorialState state, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_statePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        // 并发保存时使用唯一临时文件，避免两个写入者互相覆盖/争用同一个 .tmp。
        var temporaryPath = _statePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, state, JsonOptions, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, _statePath, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch
            {
                // 临时文件清理失败不影响已完成的保存结果。
            }

            _writeGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            TryDeleteStateFile();
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private void TryBackupCorruptStateFile()
    {
        try
        {
            if (!File.Exists(_statePath))
            {
                return;
            }

            var backupPath = _statePath + ".corrupt-" + DateTime.Now.ToString("yyyyMMddHHmmss");
            File.Move(_statePath, backupPath, overwrite: true);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "备份损坏的教程状态文件失败：{Path}", _statePath);
        }
    }

    private void TryDeleteStateFile()
    {
        try
        {
            if (File.Exists(_statePath))
            {
                File.Delete(_statePath);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "删除教程状态文件失败：{Path}", _statePath);
        }
    }
}
