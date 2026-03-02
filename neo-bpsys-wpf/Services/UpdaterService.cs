using Downloader;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
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
    public string NewVersion { get; set; } = string.Empty;
    public ReleaseInfo NewVersionInfo { get; set; } = new();
    public bool IsFindPreRelease { get; set; } =
#if BETA
        true;
#else
        false;
#endif
    private readonly DownloadService _downloader;
    public object Downloader => _downloader;
    public bool IsDownloading { get; private set; }
    public double DownloadProgress { get; private set; }
    public double DownloadBytesPerSecond { get; private set; }
    public bool IsDownloadFinished { get; private set; }

    private const string ApiUrl = "https://gh-releases.plfjy.top/?repo=PLFJY/neo-bpsys-wpf&ua=neo-bpsys-wpf";
    private const string InstallerFileName = "neo-bpsys-wpf_Installer.exe";
    private readonly HttpClient _httpClient;
    private readonly IInfoBarService _infoBarService;
    private readonly ILogger<UpdaterService> _logger;
    private readonly ISettingsHostService _settingsHostService;
    private readonly Lock _downloadLock = new();
    private CancellationTokenSource? _downloadCts;

    public UpdaterService(IInfoBarService infoBarService, ILogger<UpdaterService> logger,
        ISettingsHostService settingsHostService)
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", AppConstants.AppName);
        _infoBarService = infoBarService;
        _logger = logger;
        _settingsHostService = settingsHostService;
        var downloadOpt = new DownloadConfiguration()
        {
            ChunkCount = 8,
            ParallelDownload = true,
            MaxTryAgainOnFailure = 5,
            ParallelCount = 6,
        };

        _downloader = new DownloadService(downloadOpt);
        _downloader.DownloadProgressChanged += Downloader_DownloadProgressChanged;

        var fileName = Path.Combine(Path.GetTempPath(), "neo-bpsys-wpf_Installer.exe");
        if (!File.Exists(fileName)) return;
        try
        {
            File.Delete(fileName);
        }
        catch (Exception ex)
        {
            _ = MessageBoxHelper.ShowErrorAsync(ex.Message,
                I18nHelper.GetLocalizedString("ErrorWhenCleanUpResidualUpdateFiles"));
        }
    }

    /// <summary>
    /// 下载更新
    /// </summary>
    public async Task DownloadUpdate(string mirror = "")
    {
        var asset = NewVersionInfo.Assets.FirstOrDefault(a => a.Name == InstallerFileName);
        if (asset == null || string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl))
        {
            await MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString("CheckForUpdatesFailed"));
            return;
        }

        CancellationTokenSource localCts;
        lock (_downloadLock)
        {
            if (IsDownloading)
            {
                return;
            }

            IsDownloading = true;
            IsDownloadFinished = false;
            DownloadProgress = 0;
            DownloadBytesPerSecond = 0;
            _downloadCts = new CancellationTokenSource();
            localCts = _downloadCts;
        }

        RaiseDownloadStateChanged();

        var fileName = Path.Combine(Path.GetTempPath(), InstallerFileName);
        var downloadUrl = asset.BrowserDownloadUrl;
        try
        {
            await _downloader.DownloadFileTaskAsync(mirror + downloadUrl, fileName);
            if (!localCts.IsCancellationRequested)
            {
                lock (_downloadLock)
                {
                    IsDownloadFinished = true;
                }
                RaiseDownloadStateChanged();

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
        }
        catch (OperationCanceledException)
        {
            // 用户取消下载时静默结束。
            lock (_downloadLock)
            {
                IsDownloadFinished = false;
            }
        }
        catch (Exception ex)
        {
            lock (_downloadLock)
            {
                IsDownloadFinished = false;
            }
            if (!localCts.IsCancellationRequested)
            {
                await MessageBoxHelper.ShowErrorAsync($"{I18nHelper.GetLocalizedString("DownloadFails")}: {ex.Message}");
            }
        }
        finally
        {
            lock (_downloadLock)
            {
                IsDownloading = false;
                DownloadProgress = 0;
                DownloadBytesPerSecond = 0;
                _downloadCts?.Dispose();
                _downloadCts = null;
            }

            RaiseDownloadStateChanged();
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
}
