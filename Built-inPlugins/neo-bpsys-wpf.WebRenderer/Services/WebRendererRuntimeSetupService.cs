using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;

namespace neo_bpsys_wpf.WebRenderer.Services;

/// <summary>
/// ASP.NET Core Runtime 安装引导流程的状态。
/// </summary>
public enum WebRendererRuntimeSetupState
{
    /// <summary>空闲，尚未开始或已重置。</summary>
    Idle,
    /// <summary>正在在线查询最新版本。</summary>
    FetchingRelease,
    /// <summary>正在下载 installer。</summary>
    Downloading,
    /// <summary>installer 下载已暂停。</summary>
    Paused,
    /// <summary>正在校验 installer 哈希。</summary>
    Verifying,
    /// <summary>正在执行静默安装（等待 UAC 确认与 installer 退出）。</summary>
    Installing,
    /// <summary>安装已完成并通过重新检测，等待用户重启应用。</summary>
    AwaitingRestart,
    /// <summary>流程失败，参见 <see cref="WebRendererRuntimeSetupStatus.ErrorMessage"/>。</summary>
    Failed
}

/// <summary>
/// 表示 <see cref="WebRendererRuntimeSetupService"/> 的当前状态快照。
/// </summary>
/// <param name="State">当前阶段。</param>
/// <param name="DownloadProgress">下载进度，范围 0-100；非下载阶段为 0。</param>
/// <param name="PendingVersion">正在处理的 ASP.NET Core Runtime 版本号；尚未获取则为 <c>null</c>。</param>
/// <param name="ErrorMessage">失败原因；非失败状态为 <c>null</c>。</param>
public sealed record WebRendererRuntimeSetupStatus(
    WebRendererRuntimeSetupState State,
    double DownloadProgress,
    string? PendingVersion,
    string? ErrorMessage)
{
    /// <summary>
    /// 默认的空闲状态。
    /// </summary>
    public static WebRendererRuntimeSetupStatus Idle { get; } = new(WebRendererRuntimeSetupState.Idle, 0, null, null);

    /// <summary>
    /// 当前是否处于忙态（任何进行中的阶段）。
    /// </summary>
    public bool IsBusy => State is WebRendererRuntimeSetupState.FetchingRelease
        or WebRendererRuntimeSetupState.Downloading
        or WebRendererRuntimeSetupState.Paused
        or WebRendererRuntimeSetupState.Verifying
        or WebRendererRuntimeSetupState.Installing;
}

/// <summary>
/// 封装 ASP.NET Core Runtime 的下载、校验、静默安装与重启标记流程。
/// </summary>
public sealed class WebRendererRuntimeSetupService
{
    private static readonly TimeSpan InstallTimeout = TimeSpan.FromMinutes(5);
    private static readonly string DownloadDirectory = Path.Combine(Path.GetTempPath(), "neo-bpsys-wpf_WebRenderer");

    private readonly WebRendererRuntimeDetector _runtimeDetector;
    private readonly WebRendererRuntimeReleaseFeed _releaseFeed;
    private readonly IGlobalRestartService _globalRestartService;
    private readonly ILogger<WebRendererRuntimeSetupService> _logger;
    private readonly IFileDownloadService _fileDownloadService;
    private int _running;
    private WebRendererRuntimeSetupStatus _status = WebRendererRuntimeSetupStatus.Idle;
    private IFileDownloadOperation? _currentDownload;
    private CancellationTokenSource? _setupCancellation;

    /// <summary>
    /// 初始化 <see cref="WebRendererRuntimeSetupService"/>。
    /// </summary>
    /// <param name="runtimeDetector">ASP.NET Core Runtime 检测器。</param>
    /// <param name="releaseFeed">官方 release metadata 查询服务。</param>
    /// <param name="globalRestartService">全局重启状态服务。</param>
    /// <param name="logger">日志记录器。</param>
    /// <param name="fileDownloadService">统一文件下载服务。</param>
    public WebRendererRuntimeSetupService(
        WebRendererRuntimeDetector runtimeDetector,
        WebRendererRuntimeReleaseFeed releaseFeed,
        IGlobalRestartService globalRestartService,
        ILogger<WebRendererRuntimeSetupService> logger,
        IFileDownloadService fileDownloadService)
    {
        _runtimeDetector = runtimeDetector;
        _releaseFeed = releaseFeed;
        _globalRestartService = globalRestartService;
        _logger = logger;
        _fileDownloadService = fileDownloadService;
    }

    /// <summary>
    /// 当前状态快照。
    /// </summary>
    public WebRendererRuntimeSetupStatus Status => _status;

    /// <summary>
    /// 状态变化时触发；订阅者应在 UI 线程上刷新。
    /// </summary>
    public event EventHandler? StatusChanged;

    /// <summary>暂停 runtime installer 下载。</summary>
    public void PauseDownload() => _currentDownload?.Pause();

    /// <summary>恢复 runtime installer 下载。</summary>
    public void ResumeDownload() => _currentDownload?.Resume();

    /// <summary>取消安装引导流程并保留部分下载文件。</summary>
    public void Cancel() => _setupCancellation?.Cancel();

    /// <summary>
    /// 执行完整的下载-校验-安装-重启标记流程。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>异步任务。流程结束后的最终状态通过 <see cref="Status"/> 与 <see cref="StatusChanged"/> 暴露。</returns>
    public async Task RunSetupAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
            return;
        _setupCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            await RunSetupCoreAsync(_setupCancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_setupCancellation.IsCancellationRequested)
        {
            SetStatus(WebRendererRuntimeSetupStatus.Idle);
        }
        finally
        {
            _setupCancellation.Dispose();
            _setupCancellation = null;
            _running = 0;
        }
    }

    private async Task RunSetupCoreAsync(CancellationToken cancellationToken)
    {
        SetStatus(new WebRendererRuntimeSetupStatus(WebRendererRuntimeSetupState.FetchingRelease, 0, null, null));

        var release = await _releaseFeed.FetchLatestAsync(cancellationToken).ConfigureAwait(false);
        if (release is null)
        {
            _logger.LogWarning("Online release metadata fetch failed; falling back to known version {Version}", WebRendererRuntimeReleaseFeed.KnownFallbackVersion);
            release = _releaseFeed.GetFallback();
        }

        SetStatus(new WebRendererRuntimeSetupStatus(WebRendererRuntimeSetupState.Downloading, 0, release.Version, null));

        Directory.CreateDirectory(DownloadDirectory);
        var installerPath = Path.Combine(DownloadDirectory, $"aspnetcore-runtime-{release.Version}-win-x64.exe");
        CleanupFile(installerPath);

        try
        {
            await DownloadInstallerAsync(release, installerPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Failed to download ASP.NET Core runtime installer from {Url}", release.DownloadUrl);
            CleanupFile(installerPath);
            SetStatus(new WebRendererRuntimeSetupStatus(WebRendererRuntimeSetupState.Failed, 0, release.Version, $"下载失败：{ex.Message}"));
            return;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            CleanupFile(installerPath);
            SetStatus(WebRendererRuntimeSetupStatus.Idle);
            return;
        }

        if (!string.IsNullOrWhiteSpace(release.Sha512))
        {
            SetStatus(new WebRendererRuntimeSetupStatus(WebRendererRuntimeSetupState.Verifying, 0, release.Version, null));
            if (!await VerifySha512Async(installerPath, release.Sha512!, cancellationToken).ConfigureAwait(false))
            {
                CleanupFile(installerPath);
                SetStatus(new WebRendererRuntimeSetupStatus(WebRendererRuntimeSetupState.Failed, 0, release.Version, "installer 校验失败，文件可能已损坏。"));
                return;
            }
        }
        else
        {
            _logger.LogWarning("Skipping installer hash verification because release metadata did not provide SHA-512.");
        }

        SetStatus(new WebRendererRuntimeSetupStatus(WebRendererRuntimeSetupState.Installing, 0, release.Version, null));

        int exitCode;
        try
        {
            exitCode = await RunInstallerAsync(installerPath, cancellationToken).ConfigureAwait(false);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            _logger.LogWarning(ex, "UAC elevation was declined by the user.");
            SetStatus(new WebRendererRuntimeSetupStatus(WebRendererRuntimeSetupState.Failed, 0, release.Version, "安装被取消（UAC 未授权）。可点击“打开官方下载页”手动安装。"));
            return;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Installer execution failed.");
            SetStatus(new WebRendererRuntimeSetupStatus(WebRendererRuntimeSetupState.Failed, 0, release.Version, $"安装执行失败：{ex.Message}"));
            return;
        }

        if (exitCode != 0)
        {
            _logger.LogError("ASP.NET Core runtime installer exited with code {ExitCode}", exitCode);
            SetStatus(new WebRendererRuntimeSetupStatus(WebRendererRuntimeSetupState.Failed, 0, release.Version, $"安装失败，installer 退出码：{exitCode}。可点击“打开官方下载页”手动安装。"));
            return;
        }

        var recheck = await _runtimeDetector.DetectAsync().ConfigureAwait(false);
        if (!recheck.IsAvailable)
        {
            _logger.LogError("Recheck after install reported runtime still unavailable: {Error}", recheck.ErrorMessage);
            SetStatus(new WebRendererRuntimeSetupStatus(WebRendererRuntimeSetupState.Failed, 0, release.Version, "安装已完成，但未能检测到 ASP.NET Core Runtime。请重启应用后再试，或手动从官方下载页安装。"));
            return;
        }

        _globalRestartService.IsRestartRequired = true;
        SetStatus(new WebRendererRuntimeSetupStatus(WebRendererRuntimeSetupState.AwaitingRestart, 100, release.Version, null));
    }

    private async Task DownloadInstallerAsync(WebRendererRuntimeReleaseInfo release, string installerPath, CancellationToken cancellationToken)
    {
        var operation = _fileDownloadService.CreateDownload(new FileDownloadRequest(
            new Uri(release.DownloadUrl, UriKind.Absolute),
            installerPath)
        {
            UserAgent = "neo-bpsys-wpf"
        });
        _currentDownload = operation;
        operation.StateChanged += OnStateChanged;
        try
        {
            await operation.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            operation.StateChanged -= OnStateChanged;
            if (ReferenceEquals(_currentDownload, operation))
                _currentDownload = null;
        }

        void OnStateChanged(object? sender, EventArgs args)
        {
            var state = operation.State == FileDownloadState.Paused
                ? WebRendererRuntimeSetupState.Paused
                : WebRendererRuntimeSetupState.Downloading;
            SetStatus(new WebRendererRuntimeSetupStatus(
                state,
                operation.Progress.Percentage ?? 0,
                release.Version,
                null));
        }
    }

    private static async Task<bool> VerifySha512Async(string filePath, string expectedHash, CancellationToken cancellationToken)
    {
        var normalized = expectedHash.Trim().ToUpperInvariant();
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
        var actualBytes = await SHA512.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        var actual = Convert.ToHexString(actualBytes);
        return string.Equals(actual, normalized, StringComparison.Ordinal);
    }

    private async Task<int> RunInstallerAsync(string installerPath, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(installerPath)
        {
            Arguments = "/quiet /norestart",
            Verb = "runas",
            UseShellExecute = true
        };
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("无法启动 installer 进程。");
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(InstallTimeout);
        try
        {
            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            if (!process.HasExited)
            {
                try { process.Kill(true); } catch { }
            }
            throw new TimeoutException($"安装超时（{InstallTimeout.TotalMinutes:0} 分钟）。");
        }
        return process.ExitCode;
    }

    private void SetStatus(WebRendererRuntimeSetupStatus status)
    {
        _status = status;
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void CleanupFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
