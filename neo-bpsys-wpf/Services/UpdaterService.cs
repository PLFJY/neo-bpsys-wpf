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
using System.Text.Json;
using System.Windows;
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

    private const string ApiUrl = "https://gh-releases.plfjy.top/?repo=PLFJY/neo-bpsys-wpf&ua=neo-bpsys-wpf";
    private const string InstallerFileName = "neo-bpsys-wpf_Installer.exe";
    private readonly HttpClient _httpClient;
    private readonly IInfoBarService _infoBarService;
    private readonly ILogger<UpdaterService> _logger;

    public UpdaterService(IInfoBarService infoBarService, ILogger<UpdaterService> logger)
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", AppConstants.AppName);
        _infoBarService = infoBarService;
        _logger = logger;
        var downloadOpt = new DownloadConfiguration()
        {
            ChunkCount = 8,
            ParallelDownload = true,
            MaxTryAgainOnFailure = 5,
            ParallelCount = 6,
        };

        _downloader = new DownloadService(downloadOpt);
        _downloader.DownloadFileCompleted += OnDownloadFileCompletedAsync;

        var fileName = Path.Combine(Path.GetTempPath(), "neo-bpsys-wpf_Installer.exe");
        if (!File.Exists(fileName)) return;
        try
        {
            File.Delete(fileName);
        }
        catch (Exception ex)
        {
            _ = MessageBoxHelper.ShowErrorAsync(ex.Message, I18nHelper.GetLocalizedString("ErrorWhenCleanUpResidualUpdateFiles"));
        }
    }

    /// <summary>
    /// 下载更新
    /// </summary>
    public async Task DownloadUpdate(string mirror = "")
    {
        var fileName = Path.Combine(Path.GetTempPath(), InstallerFileName);
        var downloadUrl = NewVersionInfo.Assets.First(a => a.Name == InstallerFileName).BrowserDownloadUrl;
        try
        {
            await _downloader.DownloadFileTaskAsync(mirror + downloadUrl, fileName);
        }
        catch (Exception ex)
        {
            await MessageBoxHelper.ShowErrorAsync($"{I18nHelper.GetLocalizedString("DownloadFails")}: {ex.Message}");
        }
    }

    private async void OnDownloadFileCompletedAsync(object? sender, AsyncCompletedEventArgs e)
    {
        if (e.Error != null)
        {
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                await MessageBoxHelper.ShowErrorAsync($"{I18nHelper.GetLocalizedString("DownloadFails")}: {e.Error.Message}");
            });
            return;
        }

        await Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            if (await MessageBoxHelper.ShowConfirmAsync(I18nHelper.GetLocalizedString("DownloadFinished"), I18nHelper.GetLocalizedString("DownloadTip"), I18nHelper.GetLocalizedString("Install")))
            {
                if (await MessageBoxHelper.ShowConfirmAsync(I18nHelper.GetLocalizedString("DownloadFinished"),
                        I18nHelper.GetLocalizedString("DownloadTip"),
                        I18nHelper.GetLocalizedString("Install"),
                        I18nHelper.GetLocalizedString("Cancel")))
                {
                    InstallUpdate();
                }
            }
        });
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
                var result = await MessageBoxHelper.ShowConfirmAsync(I18nHelper.GetLocalizedString("CheckForUpdates"), $"{I18nHelper.GetLocalizedString("NewUpdateFound")}: {NewVersionInfo.TagName}", I18nHelper.GetLocalizedString("Update"), I18nHelper.GetLocalizedString("Cancel"));
                if (result)
                    await DownloadUpdate(mirror);
            }
            else
            {
                _infoBarService.ShowSuccessInfoBar(
                    $"{I18nHelper.GetLocalizedString("NewUpdateFound")}：{NewVersionInfo.TagName}");
            }

            return true;
        }

        if (!isInitial)
        {
            await MessageBoxHelper.ShowInfoAsync(I18nHelper.GetLocalizedString("NoUpdatesAvailable"), I18nHelper.GetLocalizedString("CheckForUpdates"));
        }

        return false;
    }

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

    /// <summary>
    /// 安装更新
    /// </summary>
    public void InstallUpdate()
    {
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