using Downloader;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Threading;
using I18nHelper = neo_bpsys_wpf.Helpers.I18nHelper;

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

    public string NewVersion { get; set; } = string.Empty;
    public ReleaseInfo NewVersionInfo { get; set; } = new();
    public bool IsFindPreRelease { get; set; }
    private readonly DownloadService _downloader;
    public object Downloader => _downloader;
    public bool IsDownloading { get; private set; }
    public double DownloadProgress { get; private set; }
    public double DownloadBytesPerSecond { get; private set; }
    public bool IsDownloadFinished { get; private set; }

    private const string ApiUrl = "https://gh-releases.plfjy.top/?repo=PLFJY/neo-bpsys-wpf&ua=neo-bpsys-wpf";
    private const string InstallerFileName = "neo-bpsys-wpf_Installer.exe";
    private const string InstallerSha256FileName = InstallerFileName + ".sha256";
    private readonly HttpClient _httpClient;
    private readonly IInfoBarService _infoBarService;
    private readonly ILogger<UpdaterService> _logger;
    private readonly ISettingsHostService _settingsHostService;
    private readonly Lock _downloadLock = new();
    private CancellationTokenSource? _downloadCts;
    private string _pendingSha256DownloadUrl = string.Empty;
    private UpdateDownloadStage _downloadStage = UpdateDownloadStage.None;

    public UpdaterService(IInfoBarService infoBarService, ILogger<UpdaterService> logger,
        ISettingsHostService settingsHostService)
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", AppConstants.AppName);
        _infoBarService = infoBarService;
        _logger = logger;
        _settingsHostService = settingsHostService;
        IsFindPreRelease = _settingsHostService.Settings.IsFindPreRelease;
        var downloadOpt = new DownloadConfiguration()
        {
            ChunkCount = 8,
            ParallelDownload = true,
            MaxTryAgainOnFailure = 5,
            ParallelCount = 6,
        };

        _downloader = new DownloadService(downloadOpt);
        _downloader.DownloadProgressChanged += Downloader_DownloadProgressChanged;
        _downloader.DownloadFileCompleted += OnDownloadFileCompletedAsync;

        CleanupResidualUpdateFile(InstallerFileName);
        CleanupResidualUpdateFile(InstallerSha256FileName);
    }

    /// <summary>
    /// 下载更新
    /// </summary>
    public Task DownloadUpdate(string mirror = "")
    {
        mirror = string.IsNullOrWhiteSpace(mirror) ? _settingsHostService.Settings.GhProxyMirror : mirror;
        var asset = NewVersionInfo.Assets.FirstOrDefault(a => a.Name == InstallerFileName);
        var sha256Asset = NewVersionInfo.Assets.FirstOrDefault(a => a.Name == InstallerSha256FileName);
        if (asset == null
            || sha256Asset == null
            || string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl)
            || string.IsNullOrWhiteSpace(sha256Asset.BrowserDownloadUrl))
        {
            CleanupDownloadedUpdateFiles();
            return MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString("AppUpdateHashFileMissing"));
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

        var fileName = Path.Combine(Path.GetTempPath(), InstallerFileName);
        var downloadUrl = asset.BrowserDownloadUrl;
        try
        {
            _ = _downloader.DownloadFileTaskAsync(mirror + downloadUrl, fileName);
        }
        catch (Exception ex)
        {
            CleanupDownloadedUpdateFiles();
            ResetDownloadState(isDownloadFinished: false);
            return MessageBoxHelper.ShowErrorAsync($"{I18nHelper.GetLocalizedString("DownloadFails")}: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    private async void OnDownloadFileCompletedAsync(object? sender, AsyncCompletedEventArgs e)
    {
        UpdateDownloadStage completedStage;
        lock (_downloadLock)
        {
            completedStage = _downloadStage;
        }

        if (completedStage == UpdateDownloadStage.None)
        {
            return;
        }

        if (e.Cancelled)
        {
            CleanupDownloadedUpdateFiles();
            ResetDownloadState(isDownloadFinished: false);
            return;
        }

        if (e.Error != null)
        {
            CleanupDownloadedUpdateFiles();
            ResetDownloadState(isDownloadFinished: false);
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                await MessageBoxHelper.ShowErrorAsync(
                    $"{I18nHelper.GetLocalizedString("DownloadFails")}: {e.Error.Message}");
            });
            return;
        }

        if (completedStage == UpdateDownloadStage.Installer)
        {
            try
            {
                lock (_downloadLock)
                {
                    _downloadStage = UpdateDownloadStage.Sha256;
                    DownloadProgress = 0;
                    DownloadBytesPerSecond = 0;
                }
                RaiseDownloadStateChanged();
                _ = _downloader.DownloadFileTaskAsync(
                    _pendingSha256DownloadUrl,
                    Path.Combine(Path.GetTempPath(), InstallerSha256FileName));
            }
            catch (Exception ex)
            {
                CleanupDownloadedUpdateFiles();
                ResetDownloadState(isDownloadFinished: false);
                await Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    await MessageBoxHelper.ShowErrorAsync(
                        $"{I18nHelper.GetLocalizedString("DownloadFails")}: {ex.Message}");
                });
            }

            return;
        }

        if (completedStage == UpdateDownloadStage.Sha256)
        {
            try
            {
                ValidateDownloadedInstaller(
                    Path.Combine(Path.GetTempPath(), InstallerFileName),
                    Path.Combine(Path.GetTempPath(), InstallerSha256FileName));
                ResetDownloadState(isDownloadFinished: true);
                await Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    if (await MessageBoxHelper.ShowConfirmAsync(I18nHelper.GetLocalizedString("DownloadFinished"),
                            I18nHelper.GetLocalizedString("DownloadTip"),
                            I18nHelper.GetLocalizedString("Install"),
                            I18nHelper.GetLocalizedString("Cancel")))
                    {
                        _ = InstallUpdate();
                    }
                });
            }
            catch (Exception ex)
            {
                CleanupDownloadedUpdateFiles();
                ResetDownloadState(isDownloadFinished: false);
                await Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    await MessageBoxHelper.ShowErrorAsync(
                        $"{I18nHelper.GetLocalizedString("DownloadFails")}: {ex.Message}");
                });
            }
        }
    }

    /// <inheritdoc/>
    public void CancelDownload()
    {
        lock (_downloadLock)
        {
            _downloadCts?.Cancel();
        }

        _downloader.CancelAsync();
    }

    /// <summary>
    /// 检查更新
    /// </summary>
    /// <returns>如果有新版本则返回true，反之为false</returns>
    public async Task<bool> UpdateCheck(bool isInitial = false, string mirror = "")
    {
        mirror = string.IsNullOrWhiteSpace(mirror) ? _settingsHostService.Settings.GhProxyMirror : mirror;
        await GetNewVersionInfoAsync();
        if (string.IsNullOrEmpty(NewVersionInfo.TagName))
        {
            await MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString("CheckForUpdatesFailed"));
            return false;
        }

        if (NewVersionInfo.TagName != AppConstants.AppVersion)
        {
            if (!isInitial)
            {
                var result = await MessageBoxHelper.ShowConfirmAsync(I18nHelper.GetLocalizedString("CheckForUpdates"),
                    $"{I18nHelper.GetLocalizedString("NewUpdateFound")}: {NewVersionInfo.TagName}",
                    I18nHelper.GetLocalizedString("Update"), I18nHelper.GetLocalizedString("Cancel"));
                if (result)
                    await DownloadUpdate(mirror);
            }
            else
            {
                _infoBarService.ShowSuccessInfoBar(
                    $"{I18nHelper.GetLocalizedString("NewUpdateFound")}：{NewVersionInfo.TagName}");
            }

            NewVersionInfoChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        if (!isInitial)
        {
            await MessageBoxHelper.ShowInfoAsync(I18nHelper.GetLocalizedString("NoUpdatesAvailable"),
                I18nHelper.GetLocalizedString("CheckForUpdates"));
        }

        NewVersionInfoChanged?.Invoke(this, EventArgs.Empty);
        return false;
    }

    /// <inheritdoc/>
    public event EventHandler? NewVersionInfoChanged;
    /// <inheritdoc/>
    public event EventHandler? DownloadStateChanged;

    /// <summary>
    /// 获取新版本信息
    /// </summary>
    /// <returns></returns>
    private async Task GetNewVersionInfoAsync()
    {
        NewVersionInfo = new ReleaseInfo();
        try
        {
            var response =
                await _httpClient.GetAsync(
                    $"{ApiUrl}{(IsFindPreRelease ? string.Empty : "&latest=true")}");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(content)) return;
            if (!IsFindPreRelease)
            {
                var releaseInfo = JsonSerializer.Deserialize<ReleaseInfo>(content);
                if (releaseInfo != null)
                {
                    NewVersionInfo = releaseInfo;
                }
            }
            else
            {
                var releaseInfoArray = JsonSerializer.Deserialize<ReleaseInfo[]>(content);
                if (releaseInfoArray != null && releaseInfoArray.Length > 0)
                {
                    NewVersionInfo = releaseInfoArray[0];
                }
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError($"HTTP request error: {ex.Message}");
            await MessageBoxHelper.ShowErrorAsync($"HTTP request error: {ex.Message}");
        }
        catch (JsonException ex)
        {
            _logger.LogError($"JSON parsing error: {ex.Message}");
            await MessageBoxHelper.ShowErrorAsync($"JSON parsing error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Unknown error: {ex.Message}");
            await MessageBoxHelper.ShowErrorAsync($"Unknown error: {ex.Message}");
        }
    }

    private void Downloader_DownloadProgressChanged(object? sender, DownloadProgressChangedEventArgs e)
    {
        lock (_downloadLock)
        {
            DownloadProgress = e.ProgressPercentage;
            DownloadBytesPerSecond = e.BytesPerSecondSpeed;
        }

        RaiseDownloadStateChanged();
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
            IsDownloadFinished = isDownloadFinished;
            DownloadProgress = 0;
            DownloadBytesPerSecond = 0;
            _pendingSha256DownloadUrl = string.Empty;
            _downloadStage = UpdateDownloadStage.None;
            _downloadCts?.Dispose();
            _downloadCts = null;
        }

        RaiseDownloadStateChanged();
    }

    /// <summary>
    /// 安装更新
    /// </summary>
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
            throw new InvalidOperationException(I18nHelper.GetLocalizedString("AppUpdateSha256Mismatch"));
        }
    }

    private static string ReadExpectedSha256(string sha256FilePath)
    {
        var content = File.ReadAllText(sha256FilePath).Trim();
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException(I18nHelper.GetLocalizedString("AppUpdateInvalidHashFile"));
        }

        var hash = content
            .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(hash))
        {
            throw new InvalidOperationException(I18nHelper.GetLocalizedString("AppUpdateInvalidHashFile"));
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
            throw new InvalidOperationException(I18nHelper.GetLocalizedString("AppUpdateInvalidHashFile"));
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
                I18nHelper.GetLocalizedString("ErrorWhenCleanUpResidualUpdateFiles"));
        }
    }
}
