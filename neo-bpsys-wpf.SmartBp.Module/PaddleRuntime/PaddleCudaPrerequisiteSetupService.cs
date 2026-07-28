using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;

namespace neo_bpsys_wpf.SmartBp.Module.PaddleRuntime;

/// <summary>
/// 安装并检测系统级 CUDA 11.8 与 cuDNN 8.9。Paddle native DLL 仍归 SmartBP 模块管理；
/// CUDA Toolkit 及其 PATH 则属于系统级 NVIDIA 前置条件。
/// </summary>
public sealed class PaddleCudaPrerequisiteSetupService : IPaddleCudaPrerequisiteSetupService
{
    private const string CudaEnvironmentName = "CUDA_PATH_V11_8";
    private const string CudaRuntimeFileName = "cudart64_110.dll";
    private const string CudaInstallerFileName = "cuda_11.8.0_522.06_windows.exe";
    private const string CudaInstallerMd5 = "894c61ba173d26dc667e95ee734d3c5a";
    private const string CudnnVersion = "8.9.6.50";
    private const string CudnnFileName = "cudnn-windows-x86_64-8.9.6.50_cuda11-archive.zip";
    private const string CudnnSha256 = "f7a013f9181c863d68e67083813ecc87c0f781ef7481467fa20095373798de50";
    private const string CudnnVersionMarkerFileName = ".neo-bpsys-wpf-cudnn-version";
    private static readonly string[] CudnnRequiredFileNames =
    [
        "cudnn64_8.dll",
        "cudnn_adv_infer64_8.dll",
        "cudnn_adv_train64_8.dll",
        "cudnn_cnn_infer64_8.dll",
        "cudnn_cnn_train64_8.dll",
        "cudnn_ops_infer64_8.dll",
        "cudnn_ops_train64_8.dll"
    ];
    private static readonly Uri CudaInstallerUri = new("https://developer.download.nvidia.com/compute/cuda/11.8.0/local_installers/" + CudaInstallerFileName);
    private static readonly Uri CudnnUri = new("https://developer.download.nvidia.com/compute/cudnn/redist/cudnn/windows-x86_64/" + CudnnFileName);
    private static readonly HttpClient HttpClient = new() { Timeout = Timeout.InfiniteTimeSpan };
    private readonly ILogger<PaddleCudaPrerequisiteSetupService> _logger;
    private readonly string _downloadCacheDirectory;
    private readonly SemaphoreSlim _setupLock = new(1, 1);
    private PaddleCudaPrerequisiteSetupStatus _status;

    /// <summary>初始化系统 CUDA 前置条件安装服务。</summary>
    /// <param name="logger">日志记录器。</param>
    /// <param name="moduleStorage">SmartBP 模块存储路径提供器。</param>
    public PaddleCudaPrerequisiteSetupService(
        ILogger<PaddleCudaPrerequisiteSetupService> logger,
        ISmartBpModuleStorageProvider moduleStorage)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentNullException.ThrowIfNull(moduleStorage);
        _downloadCacheDirectory = Path.Combine(moduleStorage.PaddleRuntimeRoot, "Downloads", "NVIDIA");
        _status = new PaddleCudaPrerequisiteSetupStatus(GetInstallStatus(), false, 0, null, null, null);
    }

    /// <inheritdoc />
    public PaddleCudaPrerequisiteSetupStatus Status => _status;

    /// <inheritdoc />
    public event EventHandler? StatusChanged;

    /// <inheritdoc />
    public IReadOnlyList<string> GetDllSearchDirectories()
    {
        var binDirectory = GetCudaBinDirectory();
        return binDirectory is not null && GetInstallStatus() == PaddleCudaPrerequisiteInstallStatus.Installed
            ? [binDirectory]
            : [];
    }

    /// <inheritdoc />
    public async Task InstallAsync(PaddleRuntimePackageInfo package, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(package);
        if (!string.Equals(package.CudaVersion, "11.8", StringComparison.Ordinal) || !package.CudnnVersion.StartsWith("8.9", StringComparison.Ordinal))
        {
            SetStatus(new(PaddleCudaPrerequisiteInstallStatus.Invalid, false, 0, null, null, "This Paddle package requires an unsupported CUDA prerequisite version."));
            return;
        }

        if (GetInstallStatus() == PaddleCudaPrerequisiteInstallStatus.Installed)
        {
            SetStatus(new(PaddleCudaPrerequisiteInstallStatus.Installed, false, 100, null, null, null));
            return;
        }

        if (!await _setupLock.WaitAsync(0, cancellationToken).ConfigureAwait(false)) return;

        var tempDirectory = Path.Combine(Path.GetTempPath(), "neo-bpsys-wpf_SmartBpCuda_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDirectory);
            if (GetCudaBinDirectory() is null)
            {
                var installerPath = await GetOrDownloadCachedPackageAsync(
                    CudaInstallerUri,
                    CudaInstallerFileName,
                    CudaInstallerMd5,
                    HasExpectedMd5,
                    "DownloadingCudaToolkit",
                    "VerifyingCudaToolkit",
                    cancellationToken).ConfigureAwait(false);
                SetStatus(new(PaddleCudaPrerequisiteInstallStatus.NotInstalled, true, 0, null, "InstallingCudaToolkit", null));
                await RunElevatedAsync(installerPath, "-n", showWindow: true, cancellationToken).ConfigureAwait(false);
            }

            var cudaBin = GetCudaBinDirectory() ?? throw new InvalidOperationException("CUDA 11.8 installation did not create CUDA_PATH_V11_8.");
            var cudnnArchivePath = await GetOrDownloadCachedPackageAsync(
                CudnnUri,
                CudnnFileName,
                CudnnSha256,
                HasExpectedSha256,
                "DownloadingCuDnn",
                "VerifyingCuDnn",
                cancellationToken).ConfigureAwait(false);
            SetStatus(new(PaddleCudaPrerequisiteInstallStatus.NotInstalled, true, 0, null, "InstallingCuDnn", null));
            await InstallCuDnnElevatedAsync(cudnnArchivePath, cudaBin, tempDirectory, cancellationToken).ConfigureAwait(false);

            SetStatus(new(GetInstallStatus(), false, 100, null, null, null));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            SetStatus(new(GetInstallStatus(), false, 0, null, null, "Installation cancelled."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "System CUDA prerequisite installation failed.");
            SetStatus(new(GetInstallStatus(), false, 0, null, null, ex.Message));
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
            _setupLock.Release();
        }
    }

    private async Task<string> GetOrDownloadCachedPackageAsync(
        Uri source,
        string fileName,
        string expectedHash,
        Func<string, string, bool> hasExpectedHash,
        string downloadStep,
        string verifyStep,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_downloadCacheDirectory);
        var cachedPath = Path.Combine(_downloadCacheDirectory, fileName);
        if (File.Exists(cachedPath))
        {
            SetStatus(new(PaddleCudaPrerequisiteInstallStatus.NotInstalled, true, 0, null, verifyStep, null));
            if (hasExpectedHash(cachedPath, expectedHash))
            {
                _logger.LogInformation("Reusing verified NVIDIA dependency download cache. Path={CachePath}", cachedPath);
                return cachedPath;
            }

            _logger.LogWarning("Discarding NVIDIA dependency download cache because hash verification failed. Path={CachePath}", cachedPath);
            File.Delete(cachedPath);
        }

        var legacyPath = FindVerifiedLegacyDownload(fileName, expectedHash, hasExpectedHash, verifyStep);
        if (legacyPath is not null)
        {
            try
            {
                File.Move(legacyPath, cachedPath, overwrite: true);
                _logger.LogInformation(
                    "Migrated verified legacy NVIDIA dependency download into the SmartBP cache. Source={SourcePath}, Destination={CachePath}",
                    legacyPath,
                    cachedPath);
                return cachedPath;
            }
            catch (IOException ex)
            {
                _logger.LogDebug(ex, "Could not move verified legacy NVIDIA dependency download; reusing it in place. Path={LegacyPath}", legacyPath);
                return legacyPath;
            }
        }

        var partialPath = cachedPath + ".download";
        TryDeleteFile(partialPath);
        try
        {
            SetStatus(new(PaddleCudaPrerequisiteInstallStatus.NotInstalled, true, 0, null, downloadStep, null));
            await DownloadAsync(source, partialPath, cancellationToken).ConfigureAwait(false);
            SetStatus(new(PaddleCudaPrerequisiteInstallStatus.NotInstalled, true, 0, null, verifyStep, null));
            if (!hasExpectedHash(partialPath, expectedHash))
                throw new InvalidDataException("Downloaded NVIDIA installer integrity verification failed.");

            File.Move(partialPath, cachedPath, overwrite: true);
            return cachedPath;
        }
        catch
        {
            TryDeleteFile(partialPath);
            throw;
        }
    }

    private string? FindVerifiedLegacyDownload(
        string fileName,
        string expectedHash,
        Func<string, string, bool> hasExpectedHash,
        string verifyStep)
    {
        try
        {
            foreach (var directory in Directory.EnumerateDirectories(Path.GetTempPath(), "neo-bpsys-wpf_SmartBpCuda_*")
                         .OrderByDescending(Directory.GetLastWriteTimeUtc))
            {
                var candidate = Path.Combine(directory, fileName);
                if (!File.Exists(candidate))
                    continue;

                SetStatus(new(PaddleCudaPrerequisiteInstallStatus.NotInstalled, true, 0, null, verifyStep, null));
                if (hasExpectedHash(candidate, expectedHash))
                    return candidate;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug(ex, "Legacy NVIDIA dependency download discovery failed.");
        }

        return null;
    }

    private async Task DownloadAsync(Uri source, string destination, CancellationToken cancellationToken)
    {
        using var response = await HttpClient.GetAsync(source, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var length = response.Content.Headers.ContentLength;
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        var buffer = new byte[81920];
        long received = 0;
        long lastReportedBytes = 0;
        var speedWatch = Stopwatch.StartNew();
        var lastReportAt = TimeSpan.Zero;
        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            received += read;
            var elapsedSinceReport = speedWatch.Elapsed - lastReportAt;
            if (elapsedSinceReport < TimeSpan.FromMilliseconds(200))
                continue;

            var speed = (received - lastReportedBytes) / elapsedSinceReport.TotalSeconds;
            SetStatus(_status with
            {
                DownloadProgress = length is > 0 ? received * 100d / length.Value : 0,
                DownloadSpeed = speed
            });
            lastReportedBytes = received;
            lastReportAt = speedWatch.Elapsed;
        }

        SetStatus(_status with
        {
            DownloadProgress = length is > 0 ? 100 : _status.DownloadProgress,
            DownloadSpeed = null
        });
    }

    private async Task InstallCuDnnElevatedAsync(string archivePath, string cudaBin, string tempDirectory, CancellationToken cancellationToken)
    {
        var scriptPath = Path.Combine(tempDirectory, "install-cudnn.ps1");
        var errorPath = Path.Combine(tempDirectory, "install-cudnn-error.txt");
        var archiveLiteral = archivePath.Replace("'", "''");
        var cudaBinLiteral = cudaBin.Replace("'", "''");
        var errorLiteral = errorPath.Replace("'", "''");
        var markerLiteral = Path.Combine(cudaBin, CudnnVersionMarkerFileName).Replace("'", "''");
        var script = "param()\n$ErrorActionPreference='Stop'\ntry {\n" +
            "$extract=Join-Path $env:TEMP ('neo-bpsys-wpf-cudnn-'+[guid]::NewGuid().ToString('N'))\n" +
            "Expand-Archive -LiteralPath '" + archiveLiteral + "' -DestinationPath $extract -Force\n" +
            "Get-ChildItem -LiteralPath $extract -Recurse -Filter *.dll | Copy-Item -Destination '" + cudaBinLiteral + "' -Force\n" +
            "$machinePath=[Environment]::GetEnvironmentVariable('Path','Machine')\n" +
            "if (($machinePath -split ';' | Where-Object { $_ -ieq '" + cudaBinLiteral + "' }).Count -eq 0) { [Environment]::SetEnvironmentVariable('Path', ($machinePath.TrimEnd(';')+';'+'" + cudaBinLiteral + "'), 'Machine') }\n" +
            "Remove-Item -LiteralPath $extract -Recurse -Force\n" +
            "Set-Content -LiteralPath '" + markerLiteral + "' -Value '" + CudnnVersion + "' -Encoding ASCII -NoNewline\n" +
            "} catch {\n$_ | Out-String | Set-Content -LiteralPath '" + errorLiteral + "' -Encoding UTF8\nexit 1\n}\n";
        await File.WriteAllTextAsync(scriptPath, script, cancellationToken).ConfigureAwait(false);
        try
        {
            await RunElevatedAsync(
                "powershell.exe",
                "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" + scriptPath + "\"",
                showWindow: false,
                cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex) when (File.Exists(errorPath))
        {
            var detail = (await File.ReadAllTextAsync(errorPath, cancellationToken).ConfigureAwait(false)).Trim();
            throw new InvalidOperationException($"cuDNN installation failed: {detail}", ex);
        }
    }

    private static async Task RunElevatedAsync(
        string fileName,
        string arguments,
        bool showWindow,
        CancellationToken cancellationToken)
    {
        using var process = Process.Start(new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = showWindow ? ProcessWindowStyle.Normal : ProcessWindowStyle.Hidden
        })
            ?? throw new InvalidOperationException("Unable to start elevated installer.");
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        if (process.ExitCode != 0) throw new InvalidOperationException($"Installer exited with code {process.ExitCode}.");
    }

    private PaddleCudaPrerequisiteInstallStatus GetInstallStatus()
    {
        var binDirectory = GetCudaBinDirectory();
        if (binDirectory is null) return PaddleCudaPrerequisiteInstallStatus.NotInstalled;
        if (!File.Exists(Path.Combine(binDirectory, CudaRuntimeFileName))
            || CudnnRequiredFileNames.Any(fileName => !File.Exists(Path.Combine(binDirectory, fileName))))
        {
            return PaddleCudaPrerequisiteInstallStatus.Invalid;
        }

        var markerPath = Path.Combine(binDirectory, CudnnVersionMarkerFileName);
        try
        {
            return File.Exists(markerPath)
                   && string.Equals(File.ReadAllText(markerPath).Trim(), CudnnVersion, StringComparison.Ordinal)
                ? PaddleCudaPrerequisiteInstallStatus.Installed
                : PaddleCudaPrerequisiteInstallStatus.Invalid;
        }
        catch (IOException)
        {
            return PaddleCudaPrerequisiteInstallStatus.Invalid;
        }
        catch (UnauthorizedAccessException)
        {
            return PaddleCudaPrerequisiteInstallStatus.Invalid;
        }
    }

    private static string? GetCudaBinDirectory()
    {
        var root = Environment.GetEnvironmentVariable(CudaEnvironmentName, EnvironmentVariableTarget.Machine)
            ?? Environment.GetEnvironmentVariable(CudaEnvironmentName);
        var bin = string.IsNullOrWhiteSpace(root) ? null : Path.Combine(root, "bin");
        return bin is not null && Directory.Exists(bin) ? bin : null;
    }

    private static bool HasExpectedSha256(string path, string expected) => HasExpectedHash(path, expected, SHA256.Create());
    private static bool HasExpectedMd5(string path, string expected) => HasExpectedHash(path, expected, MD5.Create());
    private static bool HasExpectedHash(string path, string expected, HashAlgorithm algorithm)
    {
        using (algorithm)
        {
            using var stream = File.OpenRead(path);
            return string.Equals(Convert.ToHexString(algorithm.ComputeHash(stream)), expected, StringComparison.OrdinalIgnoreCase);
        }
    }

    private void SetStatus(PaddleCudaPrerequisiteSetupStatus status) { _status = status; StatusChanged?.Invoke(this, EventArgs.Empty); }
    private void TryDeleteFile(string path) { try { if (File.Exists(path)) File.Delete(path); } catch (Exception ex) { _logger.LogDebug(ex, "Temporary NVIDIA dependency download cleanup failed."); } }
    private void TryDeleteDirectory(string path) { try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch (Exception ex) { _logger.LogDebug(ex, "Temporary CUDA installer cleanup failed."); } }
}
