using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using neo_bpsys_wpf.Controls;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Models.Plugins;
using neo_bpsys_wpf.Views.Windows;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using WPFLocalizeExtension.Engine;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class SettingPageViewModel : ViewModelBase
{
    #region 自动更新
    private bool _isSyncingMirror;
    private bool _isSyncingPreRelease;

    /// <summary>
    /// 应用版本号。
    /// </summary>
    [ObservableProperty]
    public partial string AppVersion { get; set; } = string.Empty;

    /// <summary>
    /// 是否正在下载更新。
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UpdateCheckCommand))]
    public partial bool IsDownloading { get; set; }

    /// <summary>
    /// 更新是否已下载完成。
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InstallUpdateCommand))]
    public partial bool IsDownloadFinished { get; set; }

    /// <summary>
    /// 下载进度文本（百分比）。
    /// </summary>
    [ObservableProperty]
    public partial string DownloadProgressText { get; set; } = string.Empty;

    /// <summary>
    /// 下载进度值（0-100）。
    /// </summary>
    [ObservableProperty]
    public partial double DownloadProgress { get; set; }

    /// <summary>
    /// 下载速度文本（MB/s）。
    /// </summary>
    [ObservableProperty]
    public partial string MbPerSecondSpeed { get; set; } = string.Empty;

    /// <summary>
    /// GitHub 代理镜像地址。
    /// </summary>
    [ObservableProperty]
    public partial string Mirror { get; set; } = DownloadMirrorPresets.DefaultMirror;

    /// <summary>
    /// 获取或设置 GitHub 镜像设置入口是否可见。
    /// </summary>
    [ObservableProperty]
    public partial bool IsGitHubMirrorSettingVisible { get; set; }

    /// <summary>
    /// 是否查找预发布版本。
    /// </summary>
    [ObservableProperty]
    public partial bool IsFindPreRelease { get; set; }

    /// <summary>
    /// 代理镜像选项列表。
    /// </summary>
    public ObservableCollection<PluginMarketMirrorOption> MirrorList { get; } =
        new(DownloadMirrorPresets.GhProxyMirrorList.Select(
            mirror => new PluginMarketMirrorOption
            {
                DisplayNameKey = string.IsNullOrWhiteSpace(mirror)
                    ? "PluginMarketDirectConnectionNoProxy"
                    : mirror,
                Value = mirror
            }));

    partial void OnMirrorChanged(string value)
    {
        if (_isSyncingMirror || _settingsHostService == null)
        {
            return;
        }

        _settingsHostService.Settings.GhProxyMirror = value;
        _pluginMarketService.ResetMirrorCache();
        _ = _settingsHostService.SaveConfigAsync();
    }

    partial void OnIsFindPreReleaseChanged(bool value)
    {
        if (_isSyncingPreRelease || _settingsHostService == null)
        {
            return;
        }

        _settingsHostService.Settings.IsFindPreRelease = value;
        UpdaterService.IsFindPreRelease = value;
        _ = _settingsHostService.SaveConfigAsync();
    }

    private void UpdaterService_DownloadStateChanged(object? sender, EventArgs e)
    {
        if (Application.Current.Dispatcher.CheckAccess())
        {
            RefreshUpdateDownloadState();
        }
        else
        {
            Application.Current.Dispatcher.Invoke(RefreshUpdateDownloadState);
        }
    }

    private void RefreshUpdateDownloadState()
    {
        IsDownloading = UpdaterService.IsDownloading;
        DownloadProgress = UpdaterService.DownloadProgress;
        DownloadProgressText = IsDownloading ? $"{DownloadProgress:0.00}%" : string.Empty;
        MbPerSecondSpeed = IsDownloading
            ? $"{(UpdaterService.DownloadBytesPerSecond / 1024 / 1024):0.00} MB/s"
            : string.Empty;
        IsDownloadFinished = UpdaterService.IsDownloadFinished;
    }

    [RelayCommand(CanExecute = nameof(CanUpdateCheckExecute))]
    private async Task UpdateCheck()
    {
        await UpdaterService.UpdateCheck(false, Mirror);
    }

    private bool CanUpdateCheckExecute() => !IsDownloading;

    [RelayCommand(CanExecute = nameof(CanInstallExecute))]
    private void InstallUpdate()
    {
        UpdaterService.InstallUpdate();
    }

    private bool CanInstallExecute() => IsDownloadFinished;

    [RelayCommand]
    private void CancelDownload()
    {
        UpdaterService.CancelDownload();
    }
    
    private void SyncMirrorFromSettings()
    {
        var mirror = _settingsHostService.Settings.GhProxyMirror;
        _isSyncingMirror = true;
        _isSyncingPreRelease = true;
        try
        {
            Mirror = mirror;
            IsGitHubMirrorSettingVisible = IsChineseCultureForGitHubMirror();
            IsFindPreRelease = _settingsHostService.Settings.IsFindPreRelease;
            UpdaterService.IsFindPreRelease = IsFindPreRelease;
        }
        finally
        {
            _isSyncingMirror = false;
            _isSyncingPreRelease = false;
        }
    }

    private bool IsChineseCultureForGitHubMirror() =>
        _settingsHostService.Settings.CultureInfo.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 是否正在测试镜像延迟。
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TestMirrorLatencyCommand))]
    public partial bool IsTestingLatency { get; set; }

    /// <summary>
    /// 连通性测试使用的 Chrome 浏览器 User-Agent，避免部分 ghproxy 镜像拦截无 UA 请求。
    /// </summary>
    private const string MirrorLatencyTestUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
        "(KHTML, like Gecko) Chrome/138.0.0.0 Safari/537.36";

    /// <summary>
    /// 测试所有镜像的延迟。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanTestMirrorLatency))]
    private async Task TestMirrorLatency()
    {
        if (IsTestingLatency) return;

        IsTestingLatency = true;
        try
        {
            // 重置所有延迟
            foreach (var item in MirrorList)
            {
                item.LatencyMs = null;
            }

            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(MirrorLatencyTestUserAgent);

            var tasks = MirrorList.Select(async item =>
            {
                // 直连模式测试 GitHub 本身
                var testUrl = string.IsNullOrWhiteSpace(item.Value)
                    ? "https://github.com/"
                    : item.Value;

                try
                {
                    var sw = Stopwatch.StartNew();
                    using var request = new HttpRequestMessage(HttpMethod.Head, testUrl);
                    using var response = await httpClient.SendAsync(request);
                    sw.Stop();

                    item.LatencyMs = response.IsSuccessStatusCode
                        ? (int)sw.ElapsedMilliseconds
                        : -1;
                }
                catch
                {
                    item.LatencyMs = -1;
                }
            });

            await Task.WhenAll(tasks);

            if (DownloadMirrorPresets.FindLowestLatencyOption(MirrorList) is { } fastestMirror)
            {
                Mirror = fastestMirror.Value;
            }
        }
        finally
        {
            IsTestingLatency = false;
        }
    }

    private bool CanTestMirrorLatency() => !IsTestingLatency;

    #endregion
}

