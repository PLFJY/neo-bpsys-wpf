using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Threading;
using neo_bpsys_wpf.Helpers;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 更新服务
/// </summary>
public class UpdaterService : IUpdaterService
{
    private enum UpdateDownloadStage
    {
        None,
        Installer,
        Sha256
    }

    /// <summary>
    /// 新版本号。
    /// </summary>
    public string NewVersion { get; set; } = string.Empty;
    /// <summary>
    /// 新版本发布信息。
    /// </summary>
    public ReleaseInfo NewVersionInfo { get; set; } = new();
    /// <summary>
    /// 是否搜索预发布版本。
    /// </summary>
    public bool IsFindPreRelease { get; set; }
    private readonly IFileDownloadService _fileDownloadService;
    /// <summary>
    /// 当前是否正在下载。
    /// </summary>
    public bool IsDownloading { get; private set; }
    /// <summary>
    /// 当前更新下载是否已暂停。
    /// </summary>
    public bool IsDownloadPaused { get; private set; }
    /// <summary>
    /// 当前是否正在校验更新安装包的 SHA-256 哈希。
    /// </summary>
    public bool IsVerifyingHash { get; private set; }
    /// <summary>
    /// 当前下载进度，范围 0-100。
    /// </summary>
    public double DownloadProgress { get; private set; }
    /// <summary>
    /// 当前下载速度，单位为字节/秒。
    /// </summary>
    public double DownloadBytesPerSecond { get; private set; }
    /// <summary>
    /// 当前是否已下载完毕。
    /// </summary>
    public bool IsDownloadFinished { get; private set; }

    private const string ApiUrl = "https://api.github.com/repos/PLFJY/neo-bpsys-wpf/releases";
    private const string BackupApiUrl = "https://gh-releases.plfjy.top/?repo=PLFJY/neo-bpsys-wpf&ua=neo-bpsys-wpf";
    private const string InstallerFileName = "neo-bpsys-wpf_Installer.exe";
    private const string InstallerSha256FileName = InstallerFileName + ".sha256";
    private readonly HttpClient _httpClient;
    private readonly IInfoBarService _infoBarService;
    private readonly ILogger<UpdaterService> _logger;
    private readonly ISettingsHostService _settingsHostService;
    private readonly Lock _downloadLock = new();
    private IFileDownloadOperation? _currentDownload;
    private CancellationTokenSource? _downloadCts;
    private string _pendingSha256DownloadUrl = string.Empty;

    private static ILogger<UpdaterService>? StaticLogger => IAppHost.TryGetService<ILogger<UpdaterService>>();
    private UpdateDownloadStage _downloadStage = UpdateDownloadStage.None;

    /// <summary>
    /// 初始化更新服务。
    /// </summary>
    /// <param name="infoBarService">信息栏服务。</param>
    /// <param name="logger">日志记录器。</param>
    /// <param name="settingsHostService">设置服务。</param>
    /// <param name="fileDownloadService">统一文件下载服务。</param>
    public UpdaterService(IInfoBarService infoBarService, ILogger<UpdaterService> logger,
        ISettingsHostService settingsHostService,
        IFileDownloadService fileDownloadService)
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", AppConstants.AppName);
        _infoBarService = infoBarService;
        _logger = logger;
        _settingsHostService = settingsHostService;
        _fileDownloadService = fileDownloadService;
        IsFindPreRelease = _settingsHostService.Settings.IsFindPreRelease;

        CleanupResidualUpdateFile(InstallerFileName);
        CleanupResidualUpdateFile(InstallerSha256FileName);
    }

    /// <summary>
    /// 下载更新。
    /// </summary>
    /// <param name="mirror">下载镜像地址。</param>
    /// <returns>异步任务。</returns>
    public Task DownloadUpdate(string mirror = "")
    {
        mirror = NormalizeMirror(mirror);
        var asset = NewVersionInfo.Assets.FirstOrDefault(a => a.Name == InstallerFileName);
        var sha256Asset = NewVersionInfo.Assets.FirstOrDefault(a => a.Name == InstallerSha256FileName);
        if (asset == null
            || sha256Asset == null
            || string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl)
            || string.IsNullOrWhiteSpace(sha256Asset.BrowserDownloadUrl))
        {
            CleanupDownloadedUpdateFiles();
            return MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "AppUpdateHashFileMissing"));
        }

        lock (_downloadLock)
        {
            if (IsDownloading)
            {
                return Task.CompletedTask;
            }

            IsDownloading = true;
            IsDownloadFinished = false;
            DownloadProgress = 0;
            DownloadBytesPerSecond = 0;
            _downloadCts = new CancellationTokenSource();
            _pendingSha256DownloadUrl = mirror + sha256Asset.BrowserDownloadUrl;
            _downloadStage = UpdateDownloadStage.Installer;
        }

        RaiseDownloadStateChanged();

        var downloadUrl = asset.BrowserDownloadUrl;
        try
        {
            _ = DownloadUpdateFilesAsync(mirror + downloadUrl, _downloadCts.Token);
        }
        catch (Exception ex)
        {
            CleanupDownloadedUpdateFiles();
            ResetDownloadState(isDownloadFinished: false);
            return MessageBoxHelper.ShowErrorAsync($"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "DownloadFails")}: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    private async Task DownloadUpdateFilesAsync(string installerUrl, CancellationToken cancellationToken)
    {
        try
        {
            await DownloadUpdateFileAsync(
                installerUrl,
                Path.Combine(Path.GetTempPath(), InstallerFileName),
                UpdateDownloadStage.Installer,
                cancellationToken);
            lock (_downloadLock)
            {
                _downloadStage = UpdateDownloadStage.Sha256;
                DownloadProgress = 0;
                DownloadBytesPerSecond = 0;
            }
            RaiseDownloadStateChanged();
            await DownloadUpdateFileAsync(
                _pendingSha256DownloadUrl,
                Path.Combine(Path.GetTempPath(), InstallerSha256FileName),
                UpdateDownloadStage.Sha256,
                cancellationToken);

            lock (_downloadLock)
            {
                IsVerifyingHash = true;
                IsDownloadPaused = false;
                DownloadProgress = 0;
                DownloadBytesPerSecond = 0;
            }
            RaiseDownloadStateChanged();
            await Task.Run(
                () => ValidateDownloadedInstaller(
                    Path.Combine(Path.GetTempPath(), InstallerFileName),
                    Path.Combine(Path.GetTempPath(), InstallerSha256FileName)),
                cancellationToken);
            ResetDownloadState(isDownloadFinished: true);
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                if (await MessageBoxHelper.ShowConfirmAsync(
                        I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "DownloadFinished"),
                        I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "DownloadTip"),
                        I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "Install"),
                        I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Cancel")))
                {
                    _ = InstallUpdate();
                }
            });
        }
        catch (OperationCanceledException)
        {
            ResetDownloadState(isDownloadFinished: false);
        }
        catch (Exception ex)
        {
            CleanupDownloadedUpdateFiles();
            ResetDownloadState(isDownloadFinished: false);
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                await MessageBoxHelper.ShowErrorAsync(
                    $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "DownloadFails")}: {ex.Message}");
            });
        }
    }

    private async Task DownloadUpdateFileAsync(
        string sourceUrl,
        string destinationPath,
        UpdateDownloadStage stage,
        CancellationToken cancellationToken)
    {
        var operation = _fileDownloadService.CreateDownload(new FileDownloadRequest(
            new Uri(sourceUrl, UriKind.Absolute),
            destinationPath)
        {
            UserAgent = AppConstants.AppName
        });
        operation.StateChanged += OnCurrentDownloadStateChanged;
        lock (_downloadLock)
        {
            _downloadStage = stage;
            _currentDownload = operation;
        }
        try
        {
            await operation.StartAsync(cancellationToken);
        }
        finally
        {
            operation.StateChanged -= OnCurrentDownloadStateChanged;
            lock (_downloadLock)
            {
                if (ReferenceEquals(_currentDownload, operation))
                    _currentDownload = null;
            }
        }
    }

    private void OnCurrentDownloadStateChanged(object? sender, EventArgs e)
    {
        if (sender is not IFileDownloadOperation operation)
            return;
        lock (_downloadLock)
        {
            DownloadProgress = operation.Progress.Percentage ?? 0;
            DownloadBytesPerSecond = operation.Progress.BytesPerSecond;
            IsDownloadPaused = operation.State == FileDownloadState.Paused;
        }
        RaiseDownloadStateChanged();
    }

    /// <inheritdoc/>
    public void CancelDownload()
    {
        IFileDownloadOperation? operation;
        lock (_downloadLock)
        {
            _downloadCts?.Cancel();
            operation = _currentDownload;
        }
        operation?.Cancel();
    }

    /// <inheritdoc/>
    public void PauseDownload()
    {
        IFileDownloadOperation? operation;
        lock (_downloadLock)
            operation = _currentDownload;
        operation?.Pause();
    }

    /// <inheritdoc/>
    public void ResumeDownload()
    {
        IFileDownloadOperation? operation;
        lock (_downloadLock)
            operation = _currentDownload;
        operation?.Resume();
    }

    /// <summary>
    /// 检查更新。
    /// </summary>
    /// <param name="isInitial">是否为启动时的初始检查。</param>
    /// <param name="mirror">下载镜像地址。</param>
    /// <returns>如果有新版本则返回 <see langword="true"/>，反之为 <see langword="false"/>。</returns>
    public async Task<bool> UpdateCheck(bool isInitial = false, string mirror = "")
    {
        mirror = NormalizeMirror(mirror);
        await GetNewVersionInfoAsync();
        if (string.IsNullOrEmpty(NewVersionInfo.TagName))
        {
            await MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "CheckForUpdatesFailed"));
            return false;
        }

        if (NewVersionInfo.TagName != AppConstants.AppVersion)
        {
            if (!isInitial)
            {
                var result = await MessageBoxHelper.ShowConfirmAsync(I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "CheckForUpdates"),
                    $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "NewUpdateFound")}: {NewVersionInfo.TagName}",
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "Update"), I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Cancel"));
                if (result)
                    await DownloadUpdate(mirror);
            }
            else
            {
                _infoBarService.ShowSuccessInfoBar(
                    $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "NewUpdateFound")}：{NewVersionInfo.TagName}");
            }

            NewVersionInfoChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        if (!isInitial)
        {
            await MessageBoxHelper.ShowInfoAsync(I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "NoUpdatesAvailable"),
                I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "CheckForUpdates"));
        }

        NewVersionInfoChanged?.Invoke(this, EventArgs.Empty);
        return false;
    }

    /// <inheritdoc/>
    public event EventHandler? NewVersionInfoChanged;
    /// <inheritdoc/>
    public event EventHandler? DownloadStateChanged;

    /// <summary>
    /// 获取新版本信息。优先请求 <see cref="ApiUrl"/>，失败时回退到 <see cref="BackupApiUrl"/>；
    /// 仅当两个地址均失败时才向用户提示错误。
    /// </summary>
    /// <returns>异步任务。</returns>
    private async Task GetNewVersionInfoAsync()
    {
        NewVersionInfo = new ReleaseInfo();

        var (success, errorMessage) = await TryFetchReleaseInfoAsync(ApiUrl, "/latest");
        if (success) return;

        (success, errorMessage) = await TryFetchReleaseInfoAsync(BackupApiUrl, "&latest=true");
        if (success) return;

        if (!string.IsNullOrEmpty(errorMessage))
        {
            await MessageBoxHelper.ShowErrorAsync(errorMessage);
        }
    }

    /// <summary>
    /// 尝试从指定的 API 地址获取发布信息并写入 <see cref="NewVersionInfo"/>。
    /// </summary>
    /// <param name="apiUrl">API 地址。</param>
    /// <param name="latestSuffix">非预发布模式下追加到 <paramref name="apiUrl"/> 末尾的后缀，主线路为 <c>/latest</c>，备份线路为 <c>&amp;latest=true</c>。</param>
    /// <returns>
    /// 元组：第一项表示是否成功获取到有效发布信息；第二项为失败时的错误消息（成功时为 <c>null</c>）。
    /// 异常会被记录到日志，不会向外抛出。
    /// </returns>
    private async Task<(bool Success, string? ErrorMessage)> TryFetchReleaseInfoAsync(string apiUrl, string latestSuffix)
    {
        try
        {
            var response =
                await _httpClient.GetAsync(
                    $"{apiUrl}{(IsFindPreRelease ? string.Empty : latestSuffix)}");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(content)) return (false, null);

            if (!IsFindPreRelease)
            {
                var releaseInfo = JsonSerializer.Deserialize<ReleaseInfo>(content);
                if (releaseInfo != null)
                {
                    NewVersionInfo = releaseInfo;
                    return (true, null);
                }
            }
            else
            {
                var releaseInfoArray = JsonSerializer.Deserialize<ReleaseInfo[]>(content);
                if (releaseInfoArray != null && releaseInfoArray.Length > 0)
                {
                    NewVersionInfo = releaseInfoArray[0];
                    return (true, null);
                }
            }

            return (false, null);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError($"HTTP request error: {ex.Message}");
            return (false, $"HTTP request error: {ex.Message}");
        }
        catch (JsonException ex)
        {
            _logger.LogError($"JSON parsing error: {ex.Message}");
            return (false, $"JSON parsing error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Unknown error: {ex.Message}");
            return (false, $"Unknown error: {ex.Message}");
        }
    }

    private void RaiseDownloadStateChanged()
    {
        DownloadStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ResetDownloadState(bool isDownloadFinished)
    {
        lock (_downloadLock)
        {
            IsDownloading = false;
            IsDownloadPaused = false;
            IsVerifyingHash = false;
            IsDownloadFinished = isDownloadFinished;
            DownloadProgress = 0;
            DownloadBytesPerSecond = 0;
            _pendingSha256DownloadUrl = string.Empty;
            _downloadStage = UpdateDownloadStage.None;
            _currentDownload = null;
            _downloadCts?.Dispose();
            _downloadCts = null;
        }

        RaiseDownloadStateChanged();
    }

    private string NormalizeMirror(string mirror)
    {
        if (!_settingsHostService.Settings.CultureInfo.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return string.IsNullOrWhiteSpace(mirror) ? _settingsHostService.Settings.GhProxyMirror : mirror;
    }

    /// <summary>
    /// 安装更新。
    /// </summary>
    /// <returns>异步任务。</returns>
    public async Task InstallUpdate()
    {
        _settingsHostService.Settings.ShowAfterUpdateTip = true;
        await _settingsHostService.SaveConfigAsync();
        var fileName = Path.Combine(
            Path.GetTempPath(),
            NewVersionInfo.Assets.First(a => a.Name == InstallerFileName).Name
        );
        Process p = new();
        p.StartInfo.FileName = fileName;
        p.StartInfo.Arguments = "/silent";
        p.Start();
        Application.Current.Shutdown();
    }

    private static void ValidateDownloadedInstaller(string installerPath, string sha256FilePath)
    {
        var expectedHash = ReadExpectedSha256(sha256FilePath);
        var actualHash = ComputeFileSha256(installerPath);
        if (!string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
        {
            CleanupFileIfExists(installerPath);
            CleanupFileIfExists(sha256FilePath);
            throw new InvalidOperationException(I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "AppUpdateSha256Mismatch"));
        }
    }

    private static string ReadExpectedSha256(string sha256FilePath)
    {
        var content = File.ReadAllText(sha256FilePath).Trim();
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException(I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "AppUpdateInvalidHashFile"));
        }

        var hash = content
            .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(hash))
        {
            throw new InvalidOperationException(I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "AppUpdateInvalidHashFile"));
        }

        return NormalizeSha256(hash);
    }

    private static string ComputeFileSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hashBytes = SHA256.HashData(stream);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static string NormalizeSha256(string value)
    {
        var normalized = value.Trim().Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        if (normalized.Length != 64 || normalized.Any(c => !Uri.IsHexDigit(c)))
        {
            StaticLogger?.LogError("Invalid hash value: {Value}", value);
            throw new InvalidOperationException(I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "AppUpdateInvalidHashFile"));
        }

        return normalized;
    }

    private void CleanupResidualUpdateFile(string fileName)
    {
        CleanupFileIfExists(Path.Combine(Path.GetTempPath(), fileName));
    }

    private void CleanupDownloadedUpdateFiles()
    {
        CleanupResidualUpdateFile(InstallerFileName);
        CleanupResidualUpdateFile(InstallerSha256FileName);
    }

    private static void CleanupFileIfExists(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        try
        {
            File.Delete(filePath);
        }
        catch (Exception ex)
        {
            _ = MessageBoxHelper.ShowErrorAsync(ex.Message,
                I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "ErrorWhenCleanUpResidualUpdateFiles"));
        }
    }
}
