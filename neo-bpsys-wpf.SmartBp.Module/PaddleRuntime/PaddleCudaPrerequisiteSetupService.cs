using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.SmartBp.Module.Abstractions;

namespace neo_bpsys_wpf.SmartBp.Module.PaddleRuntime;

/// <summary>
/// 优先复用完整且版本匹配的系统 CUDA/cuDNN；否则将官方运行库安装到 SmartBP 模块目录。
/// 不运行系统安装器、不修改环境变量、不复制 NVIDIA 驱动。
/// </summary>
public sealed class PaddleCudaPrerequisiteSetupService : IPaddleCudaPrerequisiteSetupService, IPaddleCudaPrerequisiteProgressSource
{
    private const string InstallManifestFileName = "install.json";
    private static readonly DependencyProfile[] Profiles = ReadProfiles();
    private readonly ILogger<PaddleCudaPrerequisiteSetupService> _logger;
    private readonly IFileDownloadService _fileDownloadService;
    private readonly string _dependencyRoot;
    private readonly string _downloadCacheDirectory;
    private readonly SemaphoreSlim _setupLock = new(1, 1);
    private PaddleCudaPrerequisiteSetupProgress _progress;
    private IFileDownloadOperation? _currentDownload;

    /// <summary>初始化 CUDA/cuDNN 依赖管理服务。</summary>
    /// <param name="logger">日志记录器。</param>
    /// <param name="moduleStorage">SmartBP 模块存储路径提供器。</param>
    /// <param name="fileDownloadService">统一文件下载服务。</param>
    /// <exception cref="ArgumentNullException">任一参数为 null。</exception>
    public PaddleCudaPrerequisiteSetupService(
        ILogger<PaddleCudaPrerequisiteSetupService> logger,
        ISmartBpModuleStorageProvider moduleStorage,
        IFileDownloadService fileDownloadService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentNullException.ThrowIfNull(moduleStorage);
        _fileDownloadService = fileDownloadService ?? throw new ArgumentNullException(nameof(fileDownloadService));
        _dependencyRoot = Path.GetFullPath(Path.Combine(moduleStorage.PaddleRuntimeRoot, "Dependencies"));
        _downloadCacheDirectory = Path.Combine(moduleStorage.PaddleRuntimeRoot, "Downloads", "NVIDIA");
        // 无包参数入口保留 11.8 语义；设备相关的调用必须传入明确的 Paddle 包。
        _progress = new(new(GetInstallStatus(Profiles[0]), false, 0, null, null, null), null);
    }

    /// <inheritdoc />
    public PaddleCudaPrerequisiteSetupStatus Status => _progress.Status;
    /// <inheritdoc />
    public PaddleCudaPrerequisiteSetupProgress Progress => _progress;
    /// <inheritdoc />
    public event EventHandler? StatusChanged;
    /// <inheritdoc />
    public void PauseDownload() => _currentDownload?.Pause();
    /// <inheritdoc />
    public void ResumeDownload() => _currentDownload?.Resume();
    /// <inheritdoc />
    public IReadOnlyList<string> GetDllSearchDirectories() => GetDllSearchDirectories(Profiles[0]);

    /// <inheritdoc />
    public PaddleCudaPrerequisiteInstallStatus GetInstallStatus(PaddleRuntimePackageInfo package)
    {
        ArgumentNullException.ThrowIfNull(package);
        var profile = ResolveProfile(package);
        return profile is null ? PaddleCudaPrerequisiteInstallStatus.Invalid : GetInstallStatus(profile);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetDllSearchDirectories(PaddleRuntimePackageInfo package)
    {
        ArgumentNullException.ThrowIfNull(package);
        var profile = ResolveProfile(package);
        return profile is null ? [] : GetDllSearchDirectories(profile);
    }

    /// <inheritdoc />
    public Task InstallAsync(PaddleRuntimePackageInfo package, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(package);
        // 缓存校验和解压即使同步命中，也不能留在调用方的 WPF UI 线程。
        return Task.Run(() => InstallCoreAsync(package, cancellationToken), cancellationToken);
    }

    private async Task InstallCoreAsync(PaddleRuntimePackageInfo package, CancellationToken cancellationToken)
    {
        if (!await _setupLock.WaitAsync(0, cancellationToken).ConfigureAwait(false)) return;
        DependencyProfile? profile = null;
        string? stagingDirectory = null;
        try
        {
            profile = ResolveProfile(package) ?? throw new InvalidOperationException(
                $"Unsupported prerequisites for {package.PackageId}: CUDA {package.CudaVersion}, cuDNN {package.CudnnVersion}. Supported combinations: CUDA 11.8 / cuDNN 8.9; CUDA 12.9 / cuDNN 9.10.");
            if (GetInstallStatus(profile) == PaddleCudaPrerequisiteInstallStatus.Installed)
            {
                _logger.LogInformation("Reusing CUDA dependencies. Profile={Profile}, Directories={Directories}",
                    profile.Id, string.Join(";", GetDllSearchDirectories(profile)));
                SetStatus(new(PaddleCudaPrerequisiteInstallStatus.Installed, false, 100, null, null, null));
                return;
            }

            var finalDirectory = GetInstallDirectory(profile);
            stagingDirectory = finalDirectory + ".tmp." + Guid.NewGuid().ToString("N");
            var nativeDirectory = Path.Combine(stagingDirectory, "native");
            Directory.CreateDirectory(nativeDirectory);
            _logger.LogInformation("Installing module-local CUDA dependencies. PackageId={PackageId}, Profile={Profile}, Directory={Directory}",
                package.PackageId, profile.Id, finalDirectory);
            long completedBytes = 0;
            var totalBytes = profile.Archives.Sum(asset => asset.Size);
            for (var index = 0; index < profile.Archives.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var asset = profile.Archives[index];
                var component = new PaddleCudaPrerequisiteComponentProgress(
                    asset.Id, Path.GetFileName(new Uri(asset.Url).AbsolutePath), index + 1, profile.Archives.Length,
                    completedBytes, totalBytes, asset.Size, 0, 0);
                _logger.LogInformation("Preparing NVIDIA dependency component {Index}/{Count}. Component={Component}, File={File}, Size={Size}, Url={Url}",
                    component.Index, component.Count, component.Id, component.FileName, asset.Size, asset.Url);
                var archivePath = await GetOrDownloadCachedPackageAsync(asset, component, cancellationToken).ConfigureAwait(false);
                SetBusy(asset.Id == "cudnn" ? "InstallingCuDnn" : "InstallingCudaToolkit",
                    component with { FileBytesReceived = asset.Size, FilePercentage = 100 });
                await ExtractArchiveAsync(asset, archivePath, stagingDirectory, cancellationToken).ConfigureAwait(false);
                completedBytes += asset.Size;
                _logger.LogInformation("NVIDIA dependency component {Index}/{Count} verified and extracted. Component={Component}, Path={Path}",
                    component.Index, component.Count, component.Id, archivePath);
            }

            var files = profile.Archives.SelectMany(asset => asset.RequiredDlls)
                .Select(name => new InstalledFile(name, new FileInfo(Path.Combine(nativeDirectory, name)).Length)).ToArray();
            if (files.Any(file => file.Size <= 0)) throw new InvalidDataException("A CUDA dependency DLL is empty.");
            var manifest = new DependencyInstallManifest(1, GetContractHash(profile), files);
            await File.WriteAllTextAsync(Path.Combine(stagingDirectory, InstallManifestFileName),
                JsonSerializer.Serialize(manifest), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            CommitInstallation(stagingDirectory, finalDirectory);
            stagingDirectory = null;
            var finalStatus = GetInstallStatus(profile);
            if (finalStatus != PaddleCudaPrerequisiteInstallStatus.Installed)
                throw new InvalidDataException($"CUDA dependency installation verification failed: {profile.Id}.");
            SetStatus(new(finalStatus, false, 100, null, null, null));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            SetStatus(new(profile is null ? PaddleCudaPrerequisiteInstallStatus.Invalid : GetInstallStatus(profile),
                false, 0, null, null, "Installation cancelled."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CUDA dependency setup failed. PackageId={PackageId}, Component={Component}, File={File}",
                package.PackageId, _progress.ComponentProgress?.Id, _progress.ComponentProgress?.FileName);
            SetStatus(new(profile is null ? PaddleCudaPrerequisiteInstallStatus.Invalid : GetInstallStatus(profile),
                false, 0, null, null, ex.Message));
        }
        finally
        {
            if (stagingDirectory is not null) TryDeleteOwnedDirectory(stagingDirectory);
            _setupLock.Release();
        }
    }

    private async Task<string> GetOrDownloadCachedPackageAsync(
        DependencyArchive asset, PaddleCudaPrerequisiteComponentProgress component, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_downloadCacheDirectory);
        var source = new Uri(asset.Url);
        var cachedPath = Path.Combine(_downloadCacheDirectory, Path.GetFileName(source.AbsolutePath));
        var isCudnn = asset.Id == "cudnn";
        var verifyStep = isCudnn ? "VerifyingCuDnn" : "VerifyingCudaToolkit";
        if (File.Exists(cachedPath))
        {
            SetBusy(verifyStep, component);
            if (await VerifyArchiveAsync(cachedPath, asset, cancellationToken).ConfigureAwait(false))
            {
                _logger.LogInformation("Reusing verified NVIDIA download cache. Path={CachePath}", cachedPath);
                return cachedPath;
            }
            _logger.LogWarning("Removing corrupt NVIDIA download cache. Path={CachePath}", cachedPath);
            File.Delete(cachedPath);
        }

        SetBusy(isCudnn ? "DownloadingCuDnn" : "DownloadingCudaToolkit", component);
        _logger.LogInformation("Downloading NVIDIA dependency archive. Component={Component}, File={File}, Url={Url}, Destination={Destination}",
            component.Id, component.FileName, source, cachedPath);
        var operation = _fileDownloadService.CreateDownload(new FileDownloadRequest(source, cachedPath) { UserAgent = "neo-bpsys-wpf" });
        _currentDownload = operation;
        operation.StateChanged += OnStateChanged;
        try { await operation.StartAsync(cancellationToken).ConfigureAwait(false); }
        finally
        {
            operation.StateChanged -= OnStateChanged;
            _currentDownload = null;
        }
        SetBusy(verifyStep, component with { FileBytesReceived = asset.Size, FilePercentage = 100 });
        if (!await VerifyArchiveAsync(cachedPath, asset, cancellationToken).ConfigureAwait(false))
        {
            File.Delete(cachedPath);
            throw new InvalidDataException($"NVIDIA dependency archive integrity verification failed: {component.FileName}.");
        }
        return cachedPath;

        void OnStateChanged(object? sender, EventArgs args)
        {
            var progress = operation.Progress;
            SetStatus(Status with
            {
                DownloadProgress = progress.Percentage ?? 0,
                DownloadSpeed = operation.State == FileDownloadState.Paused ? 0 : progress.BytesPerSecond,
                IsPaused = operation.State == FileDownloadState.Paused
            }, component with { FileBytesReceived = progress.BytesReceived, FilePercentage = progress.Percentage });
        }
    }

    private async Task<bool> VerifyArchiveAsync(string path, DependencyArchive asset, CancellationToken cancellationToken)
    {
        var actualSize = new FileInfo(path).Length;
        if (actualSize != asset.Size)
        {
            _logger.LogWarning("NVIDIA archive size mismatch. Path={Path}, ExpectedSize={ExpectedSize}, ActualSize={ActualSize}",
                path, asset.Size, actualSize);
            return false;
        }
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        var actualHash = Convert.ToHexString(hash);
        if (string.Equals(actualHash, asset.Sha256, StringComparison.OrdinalIgnoreCase)) return true;
        _logger.LogWarning("NVIDIA archive SHA-256 mismatch. Path={Path}, ExpectedHash={ExpectedHash}, ActualHash={ActualHash}",
            path, asset.Sha256, actualHash);
        return false;
    }

    private static async Task ExtractArchiveAsync(DependencyArchive asset, string archivePath, string stagingDirectory, CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        var extracted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var segments = entry.FullName.Replace('\\', '/').Split('/');
            if (Path.IsPathRooted(entry.FullName) || segments.Any(segment => segment is "." or ".." || segment.Contains(':')))
                throw new InvalidDataException($"Unsafe NVIDIA archive entry: {entry.FullName}");
            var name = segments[^1];
            string? destination = null;
            if (asset.RequiredDlls.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                if (segments.Length != 3 || segments[1] != "bin" || !extracted.Add(name))
                    throw new InvalidDataException($"Unexpected NVIDIA DLL entry: {entry.FullName}");
                destination = Path.Combine(stagingDirectory, "native", name);
            }
            else if (segments.Length == 2 && (name.StartsWith("LICENSE", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("EULA", StringComparison.OrdinalIgnoreCase)))
                destination = Path.Combine(stagingDirectory, "licenses", asset.Id, name);
            if (destination is null) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await using var input = entry.Open();
            await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
        }
        if (asset.RequiredDlls.Any(name => !extracted.Contains(name)))
            throw new InvalidDataException($"NVIDIA archive is missing required DLLs: {asset.Id}.");
    }

    private void CommitInstallation(string stagingDirectory, string finalDirectory)
    {
        var backup = finalDirectory + ".backup." + Guid.NewGuid().ToString("N");
        if (Directory.Exists(finalDirectory)) Directory.Move(finalDirectory, backup);
        try { Directory.Move(stagingDirectory, finalDirectory); }
        catch
        {
            if (Directory.Exists(backup)) Directory.Move(backup, finalDirectory);
            throw;
        }
        TryDeleteOwnedDirectory(backup);
    }

    private PaddleCudaPrerequisiteInstallStatus GetInstallStatus(DependencyProfile profile)
        => FindSystemDirectories(profile).Count > 0 ? PaddleCudaPrerequisiteInstallStatus.Installed : GetLocalInstallStatus(profile);

    private IReadOnlyList<string> GetDllSearchDirectories(DependencyProfile profile)
    {
        var systemDirectories = FindSystemDirectories(profile);
        if (systemDirectories.Count > 0) return systemDirectories;
        return GetLocalInstallStatus(profile) == PaddleCudaPrerequisiteInstallStatus.Installed
            ? [Path.Combine(GetInstallDirectory(profile), "native")] : [];
    }

    private PaddleCudaPrerequisiteInstallStatus GetLocalInstallStatus(DependencyProfile profile)
    {
        var directory = GetInstallDirectory(profile);
        if (!Directory.Exists(directory)) return PaddleCudaPrerequisiteInstallStatus.NotInstalled;
        try
        {
            var manifestPath = Path.Combine(directory, InstallManifestFileName);
            if (!File.Exists(manifestPath)) return PaddleCudaPrerequisiteInstallStatus.Invalid;
            var manifest = JsonSerializer.Deserialize<DependencyInstallManifest>(File.ReadAllText(manifestPath));
            var expectedNames = profile.Archives.SelectMany(asset => asset.RequiredDlls).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (manifest is null || manifest.Version != 1 || manifest.ContractHash != GetContractHash(profile)
                || manifest.Files is null || manifest.Files.Length != expectedNames.Count)
                return PaddleCudaPrerequisiteInstallStatus.Invalid;
            foreach (var file in manifest.Files)
            {
                if (file is null || !expectedNames.Remove(file.Name) || file.Size <= 0) return PaddleCudaPrerequisiteInstallStatus.Invalid;
                var path = Path.Combine(directory, "native", file.Name);
                if (!File.Exists(path) || new FileInfo(path).Length != file.Size) return PaddleCudaPrerequisiteInstallStatus.Invalid;
            }
            return PaddleCudaPrerequisiteInstallStatus.Installed;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.LogDebug(ex, "Module-local CUDA dependency record is invalid. Profile={Profile}", profile.Id);
            return PaddleCudaPrerequisiteInstallStatus.Invalid;
        }
    }

    private IReadOnlyList<string> FindSystemDirectories(DependencyProfile profile)
    {
        var candidates = new List<string>();
        foreach (var target in new[] { EnvironmentVariableTarget.Machine, EnvironmentVariableTarget.User, EnvironmentVariableTarget.Process })
        {
            foreach (var key in new[] { "CUDA_PATH_V" + profile.CudaVersion.Replace('.', '_'), "CUDA_PATH", "CUDNN_PATH" })
            {
                var root = Environment.GetEnvironmentVariable(key, target);
                if (!string.IsNullOrWhiteSpace(root) && Path.IsPathFullyQualified(root))
                {
                    candidates.Add(Path.Combine(root, "bin"));
                    candidates.Add(root);
                }
            }
            candidates.AddRange((Environment.GetEnvironmentVariable("PATH", target) ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }
        // 仅使用环境变量明确发布的绝对路径；模块私有目录不算系统安装。
        var directories = candidates.Select(path => path.Trim('"')).Where(Path.IsPathFullyQualified)
            .Where(path => !IsOwnedDirectory(path)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var cudaFiles = profile.Archives.Where(asset => asset.Id != "cudnn").SelectMany(asset => asset.RequiredDlls).ToArray();
        var runtimeName = cudaFiles.First(name => name.StartsWith("cudart64_", StringComparison.Ordinal));
        // NVIDIA cudart 的 Win32 文件版本是 6.14.11.11080 / 6.14.11.12090，
        // 并不是 Toolkit 的 11.8 / 12.9；按官方 DLL 中实际存在的版本资源匹配。
        var cudaFileVersion = Version.Parse(profile.CudaRuntimeFileVersion);
        var cudaBin = directories.FirstOrDefault(directory => HasAllFiles(directory, cudaFiles)
            && ReadFileVersion(Path.Combine(directory, runtimeName)) == cudaFileVersion);
        if (cudaBin is null) return [];
        var cudnnFiles = profile.Archives.Single(asset => asset.Id == "cudnn").RequiredDlls;
        var minimumCudnnVersion = Version.Parse(profile.CudnnRuntimeVersion);
        var cudnnBin = directories.Prepend(cudaBin).FirstOrDefault(directory => HasAllFiles(directory, cudnnFiles)
            && ReadFileVersion(Path.Combine(directory, cudnnFiles[0])) is { } version
            && version.Major == minimumCudnnVersion.Major && version.Minor == minimumCudnnVersion.Minor
            && version >= minimumCudnnVersion
            && cudnnFiles.All(name => ReadFileVersion(Path.Combine(directory, name)) == version));
        return cudnnBin is null ? [] : new[] { cudaBin, cudnnBin }.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static bool HasAllFiles(string directory, IEnumerable<string> names) => names.All(name => File.Exists(Path.Combine(directory, name)));

    private static Version? ReadFileVersion(string path)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(path);
            return new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart, info.FilePrivatePart);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { return null; }
    }

    private static DependencyProfile? ResolveProfile(PaddleRuntimePackageInfo package)
        => Profiles.FirstOrDefault(profile => profile.CudaVersion == package.CudaVersion
            && (package.CudnnVersion == profile.CudnnVersion
                || (profile.CudaVersion == "11.8" ? package.CudnnVersion.StartsWith("8.9", StringComparison.Ordinal)
                    : package.CudnnVersion.StartsWith(profile.CudnnVersion + ".", StringComparison.Ordinal))));

    private string GetInstallDirectory(DependencyProfile profile) => Path.Combine(_dependencyRoot, profile.Id);
    private static string GetContractHash(DependencyProfile profile) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(profile)));

    private static DependencyProfile[] ReadProfiles()
    {
        using var stream = typeof(PaddleCudaPrerequisiteSetupService).Assembly.GetManifestResourceStream(
            "neo_bpsys_wpf.PaddleRuntime.PaddleCudaDependencies.json")
            ?? throw new InvalidDataException("Missing embedded CUDA dependency manifest.");
        return JsonSerializer.Deserialize<DependencyProfile[]>(stream)
            ?? throw new InvalidDataException("Invalid embedded CUDA dependency manifest.");
    }

    private bool IsOwnedDirectory(string path) => string.Equals(Path.GetFullPath(path), _dependencyRoot, StringComparison.OrdinalIgnoreCase)
        || Path.GetFullPath(path).StartsWith(_dependencyRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private void TryDeleteOwnedDirectory(string path)
    {
        if (!IsOwnedDirectory(path) || string.Equals(Path.GetFullPath(path), _dependencyRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("CUDA cleanup path is outside the dependency directory.");
        try { if (Directory.Exists(path)) Directory.Delete(path, true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { _logger.LogDebug(ex, "CUDA dependency staging cleanup failed. Path={Path}", path); }
    }

    private void SetBusy(string step, PaddleCudaPrerequisiteComponentProgress component)
        => SetStatus(new(PaddleCudaPrerequisiteInstallStatus.NotInstalled, true, 0, null, step, null), component);
    private void SetStatus(PaddleCudaPrerequisiteSetupStatus status, PaddleCudaPrerequisiteComponentProgress? component = null)
    {
        _progress = new(status, component);
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>固定的运行库版本组合。</summary>
    /// <param name="Id">模块内安装目录名。</param>
    /// <param name="CudaVersion">CUDA 主次版本。</param>
    /// <param name="CudaRuntimeFileVersion">官方 cudart 的 Win32 文件版本资源。</param>
    /// <param name="CudnnVersion">Paddle 包声明的 cuDNN 主次版本。</param>
    /// <param name="CudnnRuntimeVersion">最低系统 cuDNN 版本及私有分发版本。</param>
    /// <param name="Archives">固定的官方下载契约。</param>
    private sealed record DependencyProfile(string Id, string CudaVersion, string CudaRuntimeFileVersion, string CudnnVersion, string CudnnRuntimeVersion, DependencyArchive[] Archives);
    /// <summary>一个经过固定 SHA-256 校验的官方归档。</summary>
    /// <param name="Id">组件标识。</param>
    /// <param name="Url">官方下载地址。</param>
    /// <param name="Sha256">官方 SHA-256。</param>
    /// <param name="Size">官方归档字节数。</param>
    /// <param name="RequiredDlls">从 bin 目录提取的 x64 DLL 白名单。</param>
    private sealed record DependencyArchive(string Id, string Url, string Sha256, long Size, string[] RequiredDlls);
    /// <summary>可随模块目录移动的相对路径安装记录。</summary>
    /// <param name="Version">记录格式版本。</param>
    /// <param name="ContractHash">固定依赖契约指纹。</param>
    /// <param name="Files">成功安装的 DLL 清单。</param>
    private sealed record DependencyInstallManifest(int Version, string ContractHash, InstalledFile[] Files);
    /// <summary>已安装文件的相对名称和大小。</summary>
    /// <param name="Name">DLL 文件名。</param>
    /// <param name="Size">文件字节数。</param>
    private sealed record InstalledFile(string Name, long Size);
}
