using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;

namespace neo_bpsys_wpf.SmartBp.Module.PaddleRuntime;

/// <summary>
/// Paddle CUDA runtime 组件安装 manifest，序列化写入 <c>install.json</c>。
/// </summary>
/// <param name="PackageId">已安装包 ID。</param>
/// <param name="PackageVersion">已安装包版本。</param>
/// <param name="ComputeCapability">目标 Compute Capability（如 <c>8.6</c>）。</param>
/// <param name="InstalledAt">安装时间。</param>
/// <param name="PackageHash">包 SHA-256 哈希（小写十六进制）。</param>
/// <param name="NativeDirectory">native 文件目录绝对路径。</param>
/// <param name="Verified">是否已通过全部校验。</param>
internal sealed record PaddleRuntimeInstallManifest(
    string PackageId,
    string PackageVersion,
    string ComputeCapability,
    DateTimeOffset InstalledAt,
    string PackageHash,
    string NativeDirectory,
    bool Verified);

/// <summary>
/// <see cref="IPaddleRuntimeComponentService"/> 的实现。负责 Paddle CUDA runtime NuGet 包的
/// 下载、SHA-256 校验、ZIP 提取、原子安装、状态查询与删除。
/// 文件传输由宿主统一下载服务负责，并在完成后执行校验与安装。
/// </summary>
public sealed class PaddleRuntimeComponentService : IPaddleRuntimeComponentService
{
    private const string NativeEntryPrefix = "runtimes/win-x64/native/";
    private const string InstallManifestFileName = "install.json";
    private const string TempDownloadFolderName = "PaddleRuntimeDownload";

    private static readonly JsonSerializerOptions JsonWriteOptions = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions JsonReadOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ILogger<PaddleRuntimeComponentService> _logger;
    private readonly IPaddleRuntimeManifestProvider _manifestProvider;
    private readonly ISmartBpModuleStorageProvider _moduleStorage;

    private readonly Lock _downloadLock = new();
    private readonly IFileDownloadService _fileDownloadService;
    private CancellationTokenSource? _downloadCts;
    private IFileDownloadOperation? _currentDownload;

    private bool _isDownloading;
    private bool _isDownloadFinished;
    private bool? _lastInstallSucceeded;
    private double? _downloadProgress;
    private double? _downloadSpeed;

    /// <summary>
    /// 当前下载对应的临时文件路径，用于校验、安装和清理。
    /// </summary>
    private string? _pendingDownloadPath;

    /// <summary>
    /// 初始化 <see cref="PaddleRuntimeComponentService"/> 类的新实例。
    /// 复用宿主统一下载服务。
    /// CUDA runtime 下载与安装到模块目录（<see cref="ISmartBpModuleStorageProvider.ModuleRoot"/>）下。
    /// </summary>
    /// <param name="logger">日志记录器。</param>
    /// <param name="manifestProvider">Paddle runtime manifest 提供者，用于构造 NuGet 下载 URL。</param>
    /// <param name="moduleStorage">模块存储提供者，提供模块根目录用于存放下载与安装的 runtime 组件。</param>
    /// <param name="fileDownloadService">统一文件下载服务。</param>
    /// <exception cref="ArgumentNullException">任一参数为 <see langword="null"/>。</exception>
    public PaddleRuntimeComponentService(
        ILogger<PaddleRuntimeComponentService> logger,
        IPaddleRuntimeManifestProvider manifestProvider,
        ISmartBpModuleStorageProvider moduleStorage,
        IFileDownloadService fileDownloadService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _manifestProvider = manifestProvider ?? throw new ArgumentNullException(nameof(manifestProvider));
        _moduleStorage = moduleStorage ?? throw new ArgumentNullException(nameof(moduleStorage));
        _fileDownloadService = fileDownloadService ?? throw new ArgumentNullException(nameof(fileDownloadService));
    }

    /// <inheritdoc/>
    public bool IsDownloading
    {
        get
        {
            lock (_downloadLock)
            {
                return _isDownloading;
            }
        }
    }

    /// <inheritdoc/>
    public bool IsDownloadFinished
    {
        get
        {
            lock (_downloadLock)
            {
                return _isDownloadFinished;
            }
        }
    }

    /// <inheritdoc/>
    public bool IsDownloadPaused
    {
        get
        {
            lock (_downloadLock)
                return _currentDownload?.State == FileDownloadState.Paused;
        }
    }

    /// <inheritdoc/>
    public bool? LastInstallSucceeded
    {
        get
        {
            lock (_downloadLock)
            {
                return _lastInstallSucceeded;
            }
        }
    }

    /// <inheritdoc/>
    public double? DownloadProgress
    {
        get
        {
            lock (_downloadLock)
            {
                return _downloadProgress;
            }
        }
    }

    /// <inheritdoc/>
    public double? DownloadSpeed
    {
        get
        {
            lock (_downloadLock)
            {
                return _downloadSpeed;
            }
        }
    }

    /// <inheritdoc/>
    public event EventHandler? DownloadStateChanged;

    /// <inheritdoc/>
    public PaddleRuntimeInstallInfo GetInstallStatus()
    {
        var found = FindInstalledPackage();
        if (found is null)
        {
            return new PaddleRuntimeInstallInfo(
                PaddleRuntimeInstallStatus.NotInstalled,
                null, null, null, null, null, null, false);
        }

        var f = found.Value;
        var status = f.VersionMatches
            ? PaddleRuntimeInstallStatus.Installed
            : PaddleRuntimeInstallStatus.VersionMismatch;
        return new PaddleRuntimeInstallInfo(
            status,
            f.Manifest.PackageId,
            f.Manifest.PackageVersion,
            f.Manifest.ComputeCapability,
            f.Manifest.InstalledAt,
            f.Manifest.PackageHash,
            f.Manifest.NativeDirectory,
            f.Manifest.Verified);
    }

    /// <inheritdoc/>
    public bool IsCompatibleWithCurrentVersion()
        => GetInstallStatus().Status == PaddleRuntimeInstallStatus.Installed;

    /// <inheritdoc/>
    public Task DownloadAsync(
        PaddleRuntimePackageInfo package,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(package);

        lock (_downloadLock)
        {
            if (_isDownloading)
                throw new InvalidOperationException("Download already in progress.");

            _isDownloading = true;
            _isDownloadFinished = false;
            _lastInstallSucceeded = null;
            _downloadProgress = 0;
            _downloadSpeed = 0;
            _downloadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }

        RaiseDownloadStateChanged();

        var downloadUrl = _manifestProvider.GetNuGetDownloadUrl(package);
        var tempDownloadDir = Path.Combine(_moduleStorage.ModuleRoot, TempDownloadFolderName);
        Directory.CreateDirectory(tempDownloadDir);
        _pendingDownloadPath = Path.Combine(tempDownloadDir, $"{package.PackageId}.{package.Version}.nupkg");

        _logger.LogInformation(
            "Starting Paddle CUDA runtime download. PackageId={PackageId}, Version={Version}, Url={Url}",
            package.PackageId, package.Version, downloadUrl);

        try
        {
            var operation = _fileDownloadService.CreateDownload(new FileDownloadRequest(
                new Uri(downloadUrl, UriKind.Absolute),
                _pendingDownloadPath)
            {
                UserAgent = AppConstants.AppName
            });
            operation.StateChanged += OnDownloadOperationStateChanged;
            lock (_downloadLock)
                _currentDownload = operation;
            _ = RunDownloadAndInstallAsync(package, _pendingDownloadPath, operation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to start Paddle CUDA runtime download. PackageId={PackageId}", package.PackageId);
            TryCleanupDownloadResidue();
            ResetDownloadState(isDownloadFinished: false, installSucceeded: false);
        }

        // 参照 UpdaterService.DownloadUpdate：返回 Task.CompletedTask，不代表下载完成。
        // 调用方通过 DownloadStateChanged 事件 + IsDownloading/IsDownloadFinished/LastInstallSucceeded 属性感知状态。
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void CancelDownload()
    {
        lock (_downloadLock)
        {
            _downloadCts?.Cancel();
        }
        _currentDownload?.Cancel();
    }

    /// <inheritdoc/>
    public void PauseDownload() => _currentDownload?.Pause();

    /// <inheritdoc/>
    public void ResumeDownload() => _currentDownload?.Resume();

    /// <inheritdoc/>
    public bool DeleteComponent()
    {
        lock (_downloadLock)
        {
            if (_isDownloading)
            {
                _logger.LogWarning("Cannot delete component while a download is in progress.");
                return false;
            }
        }

        var found = FindInstalledPackage();
        if (found is null)
        {
            _logger.LogInformation("No installed Paddle CUDA runtime component to delete.");
            return false;
        }

        var dir = found.Value.Directory;
        try
        {
            Directory.Delete(dir, recursive: true);
            _logger.LogInformation(
                "Deleted Paddle CUDA runtime component. Directory={Directory}", dir);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to delete Paddle CUDA runtime component. Directory={Directory}", dir);
            return false;
        }
    }

    private void OnDownloadOperationStateChanged(object? sender, EventArgs e)
    {
        if (sender is not IFileDownloadOperation operation)
            return;
        lock (_downloadLock)
        {
            _downloadProgress = operation.Progress.Percentage;
            _downloadSpeed = operation.State == FileDownloadState.Paused
                ? 0
                : operation.Progress.BytesPerSecond;

        }

        RaiseDownloadStateChanged();
    }

    /// <summary>
    /// 等待文件下载完成并执行 SHA-256 校验、ZIP 提取和原子安装。
    /// </summary>
    private async Task RunDownloadAndInstallAsync(
        PaddleRuntimePackageInfo package,
        string downloadPath,
        IFileDownloadOperation operation)
    {
        try
        {
            await operation.StartAsync(_downloadCts?.Token ?? CancellationToken.None).ConfigureAwait(false);
            var success = await Task.Run(() => VerifyAndInstallAsync(package, downloadPath, CancellationToken.None))
                .ConfigureAwait(false);
            TryCleanupDownloadResidue();
            ResetDownloadState(isDownloadFinished: success, installSucceeded: success);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Paddle CUDA runtime download cancelled. PackageId={PackageId}", package.PackageId);
            ResetDownloadState(isDownloadFinished: false, installSucceeded: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Paddle CUDA runtime download or install failed. PackageId={PackageId}", package.PackageId);
            TryCleanupDownloadResidue();
            ResetDownloadState(isDownloadFinished: false, installSucceeded: false);
        }
        finally
        {
            operation.StateChanged -= OnDownloadOperationStateChanged;
        }
    }

    /// <summary>
    /// 执行 SHA-256 校验、ZIP 提取、原子安装。
    /// </summary>
    /// <param name="package">包信息。</param>
    /// <param name="downloadPath">已下载的 .nupkg 临时文件路径。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>安装成功返回 <see langword="true"/>。</returns>
    private bool VerifyAndInstallAsync(PaddleRuntimePackageInfo package, string downloadPath, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Download completed. Verifying SHA-256. PackageId={PackageId}", package.PackageId);

            var actualHash = ComputeSha256(downloadPath);
            if (string.IsNullOrEmpty(package.PackageHashSha256))
            {
                _logger.LogWarning(
                    "PackageHashSha256 is empty, skipping hash verification. PackageId={PackageId}. " +
                    "This is a temporary behavior before the real hash is filled in.",
                    package.PackageId);
            }
            else if (!string.Equals(actualHash, package.PackageHashSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"SHA-256 hash mismatch for package {package.PackageId}. " +
                    $"Expected={package.PackageHashSha256}, Actual={actualHash}");
            }

            _logger.LogInformation("Hash verification passed. PackageId={PackageId}", package.PackageId);

            var finalDir = GetFinalInstallDirectory(package.PackageId);
            var tempInstallDir = $"{finalDir}.tmp.{Guid.NewGuid():N}";
            Directory.CreateDirectory(tempInstallDir);

            try
            {
                _logger.LogInformation(
                    "Extracting native files. PackageId={PackageId}, TempDir={TempDir}",
                    package.PackageId, tempInstallDir);
                ExtractNativeFiles(downloadPath, tempInstallDir, package.ExpectedNativeFiles, cancellationToken);

                var finalNativeDir = Path.GetFullPath(Path.Combine(finalDir, "native"));
                var installManifest = new PaddleRuntimeInstallManifest(
                    PackageId: package.PackageId,
                    PackageVersion: package.Version,
                    ComputeCapability: $"{package.ComputeCapabilityMajor}.{package.ComputeCapabilityMinor}",
                    InstalledAt: DateTimeOffset.UtcNow,
                    PackageHash: actualHash,
                    NativeDirectory: finalNativeDir,
                    Verified: true);

                WriteInstallManifest(tempInstallDir, installManifest);

                _logger.LogInformation(
                    "Install manifest written. Replacing final directory. FinalDir={FinalDir}", finalDir);

                if (Directory.Exists(finalDir))
                    Directory.Delete(finalDir, recursive: true);
                Directory.Move(tempInstallDir, finalDir);

                _logger.LogInformation(
                    "Paddle CUDA runtime installed successfully. PackageId={PackageId}, FinalDir={FinalDir}",
                    package.PackageId, finalDir);

                return true;
            }
            finally
            {
                // tempInstallDir 在成功 Move 后不再存在；失败时清理残留。
                if (Directory.Exists(tempInstallDir))
                    TryDeleteDirectory(tempInstallDir);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Paddle CUDA runtime install cancelled. PackageId={PackageId}", package.PackageId);
            return false;
        }
    }

    /// <summary>
    /// 重置下载状态并通知订阅者。参照 <see cref="UpdaterService.ResetDownloadState"/>。
    /// </summary>
    /// <param name="isDownloadFinished">下载是否已完成（含校验与安装）。</param>
    /// <param name="installSucceeded">本次安装结果；<see langword="null"/> 表示无安装（如无进行中任务）。</param>
    private void ResetDownloadState(bool isDownloadFinished, bool? installSucceeded)
    {
        lock (_downloadLock)
        {
            _isDownloading = false;
            _isDownloadFinished = isDownloadFinished;
            if (installSucceeded.HasValue)
            {
                _lastInstallSucceeded = installSucceeded.Value;
            }
            _downloadProgress = null;
            _downloadSpeed = null;
            _downloadCts?.Dispose();
            _downloadCts = null;
            _pendingDownloadPath = null;
            _currentDownload = null;
        }

        RaiseDownloadStateChanged();
    }

    /// <summary>
    /// 清理临时下载文件（若存在）。
    /// </summary>
    private void TryCleanupDownloadResidue()
    {
        string? path;
        lock (_downloadLock)
        {
            path = _pendingDownloadPath;
        }

        if (path is not null)
            TryDeleteFile(path);
    }

    /// <summary>
    /// 计算指定文件的 SHA-256 哈希（小写十六进制）。
    /// </summary>
    /// <param name="filePath">文件路径。</param>
    /// <returns>小写十六进制哈希字符串。</returns>
    private static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hashBytes = SHA256.HashData(stream);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// 从 <c>.nupkg</c> 中提取 <c>runtimes/win-x64/native/</c> 下的 native 文件到
    /// 临时安装目录的 <c>native</c> 子目录，对每个条目执行 Zip Slip 防护。
    /// </summary>
    /// <param name="nupkgPath">.nupkg 文件路径。</param>
    /// <param name="tempInstallDir">临时安装目录。</param>
    /// <param name="expectedFiles">期望的 native 文件列表，用于完成后验证。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <exception cref="IOException">ZIP 条目路径不安全或逃逸目标目录。</exception>
    /// <exception cref="InvalidDataException">缺少期望的 native 文件。</exception>
    private static void ExtractNativeFiles(
        string nupkgPath, string tempInstallDir, IReadOnlyList<string> expectedFiles, CancellationToken cancellationToken)
    {
        using var archive = new ZipArchive(File.OpenRead(nupkgPath), ZipArchiveMode.Read);
        var nativeDir = Path.Combine(tempInstallDir, "native");
        Directory.CreateDirectory(nativeDir);
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!entry.FullName.StartsWith(NativeEntryPrefix, StringComparison.OrdinalIgnoreCase))
                continue;
            if (entry.FullName.EndsWith('/'))
                continue;
            var relativePath = entry.FullName.Substring(NativeEntryPrefix.Length);
            if (string.IsNullOrEmpty(relativePath))
                continue;
            var destPath = SafeGetDestinationPath(relativePath, nativeDir);
            var parentDir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(parentDir))
                Directory.CreateDirectory(parentDir);
            entry.ExtractToFile(destPath, overwrite: true);
        }

        // 验证关键 native 文件存在
        foreach (var file in expectedFiles)
        {
            if (string.IsNullOrEmpty(file))
                continue;
            var path = Path.Combine(nativeDir, file);
            if (!File.Exists(path))
                throw new InvalidDataException($"Expected native file not found in package: {file}");
        }
    }

    /// <summary>
    /// 安全地将 ZIP 条目相对路径解析为目标目录内的绝对路径，防止 Zip Slip 攻击。
    /// 拒绝包含 <c>..</c> 的路径、绝对路径，以及最终路径逃逸目标目录的条目。
    /// </summary>
    /// <param name="relativePath">ZIP 条目剥离前缀后的相对路径。</param>
    /// <param name="targetDir">目标目录。</param>
    /// <returns>目标绝对路径。</returns>
    /// <exception cref="IOException">路径包含 <c>..</c>、为绝对路径，或最终路径逃逸目标目录。</exception>
    private static string SafeGetDestinationPath(string relativePath, string targetDir)
    {
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        if (normalized.Contains("..") || Path.IsPathRooted(normalized))
            throw new IOException($"Unsafe ZIP entry path: {relativePath}");
        var fullPath = Path.GetFullPath(Path.Combine(targetDir, normalized));
        var fullTarget = Path.GetFullPath(targetDir)
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(fullTarget, StringComparison.OrdinalIgnoreCase))
            throw new IOException($"ZIP entry escapes target directory: {relativePath}");
        return fullPath;
    }

    /// <summary>
    /// 计算最终安装目录：<c>{ModuleRoot}/Runtime/Paddle/cuda/{PaddleInferenceRuntimeVersion}/{PackageId}</c>。
    /// CUDA runtime 安装在模块目录下，与模块版本绑定。
    /// </summary>
    /// <param name="packageId">包 ID。</param>
    /// <returns>最终安装目录绝对路径。</returns>
    private string GetFinalInstallDirectory(string packageId)
        => Path.Combine(
            _moduleStorage.ModuleRoot,
            "Runtime",
            "Paddle",
            "cuda",
            _manifestProvider.PaddleInferenceVersion,
            packageId);

    /// <summary>
    /// 将安装 manifest 序列化为 JSON 写入指定目录的 <c>install.json</c>。
    /// </summary>
    /// <param name="dir">目标目录。</param>
    /// <param name="manifest">安装 manifest。</param>
    private static void WriteInstallManifest(string dir, PaddleRuntimeInstallManifest manifest)
    {
        var path = Path.Combine(dir, InstallManifestFileName);
        var json = JsonSerializer.Serialize(manifest, JsonWriteOptions);
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// 读取指定路径的 <c>install.json</c>；文件不存在或解析失败时返回 <see langword="null"/>。
    /// </summary>
    /// <param name="path">install.json 路径。</param>
    /// <returns>安装 manifest；失败时为 <see langword="null"/>。</returns>
    private static PaddleRuntimeInstallManifest? TryReadInstallManifest(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<PaddleRuntimeInstallManifest>(json, JsonReadOptions);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 在 <c>{ModuleRoot}/Runtime/Paddle/cuda/</c> 下查找已安装的组件。
    /// 优先返回版本匹配的条目；其次返回版本不匹配的条目；都没有时返回 <see langword="null"/>。
    /// </summary>
    /// <returns>找到的安装条目（目录、manifest、版本是否匹配）；未找到时为 <see langword="null"/>。</returns>
    private FoundInstall? FindInstalledPackage()
    {
        var cudaRoot = Path.Combine(_moduleStorage.ModuleRoot, "Runtime", "Paddle", "cuda");
        if (!Directory.Exists(cudaRoot))
            return null;

        FoundInstall? mismatched = null;
        foreach (var versionDir in Directory.EnumerateDirectories(cudaRoot))
        {
            foreach (var packageDir in Directory.EnumerateDirectories(versionDir))
            {
                var manifestPath = Path.Combine(packageDir, InstallManifestFileName);
                if (!File.Exists(manifestPath))
                    continue;
                var manifest = TryReadInstallManifest(manifestPath);
                if (manifest is null)
                    continue;
                var versionMatches = string.Equals(
                    manifest.PackageVersion,
                    _manifestProvider.PaddleInferenceVersion,
                    StringComparison.Ordinal);
                var found = new FoundInstall(packageDir, manifest, versionMatches);
                if (versionMatches)
                    return found;
                mismatched ??= found;
            }
        }

        return mismatched;
    }

    /// <summary>
    /// 删除指定文件，忽略不存在或失败的情况（仅记录调试日志）。
    /// </summary>
    /// <param name="path">文件路径。</param>
    private void TryDeleteFile(string path)
    {
        if (!File.Exists(path))
            return;
        try
        {
            File.Delete(path);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to delete temporary file. Path={Path}", path);
        }
    }

    /// <summary>
    /// 删除指定目录（递归），忽略不存在或失败的情况（仅记录调试日志）。
    /// </summary>
    /// <param name="path">目录路径。</param>
    private void TryDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
            return;
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to delete temporary directory. Path={Path}", path);
        }
    }

    /// <summary>
    /// 触发 <see cref="DownloadStateChanged"/> 事件。
    /// </summary>
    private void RaiseDownloadStateChanged()
    {
        DownloadStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private readonly record struct FoundInstall(
        string Directory,
        PaddleRuntimeInstallManifest Manifest,
        bool VersionMatches);
}
