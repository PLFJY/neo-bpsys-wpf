using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Downloader;
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
using neo_bpsys_wpf.Views.Windows;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
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

    [ObservableProperty] private string _appVersion = string.Empty;

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(UpdateCheckCommand))]
    private bool _isDownloading;

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(InstallUpdateCommand))]
    private bool _isDownloadFinished;

    [ObservableProperty] private string _downloadProgressText = string.Empty;

    [ObservableProperty] private double _downloadProgress;

    [ObservableProperty] private string _mbPerSecondSpeed = string.Empty;

    public string Mirror { get; set; } = "https://ghproxy.net/";

    private void Downloader_DownloadStarted(object? sender, Downloader.DownloadStartedEventArgs e)
    {
        Application.Current.Dispatcher.Invoke(() => { IsDownloading = true; });
    }

    private void Downloader_DownloadFileCompleted(object? sender, System.ComponentModel.AsyncCompletedEventArgs e)
    {
        if (e.Error == null && !e.Cancelled)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsDownloadFinished = true;
                IsDownloading = false;
            });
            return;
        }

        Application.Current.Dispatcher.Invoke(() => { IsDownloading = false; });
    }

    private void Downloader_DownloadProgressChanged(object? sender, Downloader.DownloadProgressChangedEventArgs e)
    {
        DownloadProgress = e.ProgressPercentage;
        DownloadProgressText = $"{e.ProgressPercentage:0.00}%";
        MbPerSecondSpeed = $"{(e.BytesPerSecondSpeed / 1024 / 1024):0.00} MB/s";
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
        _downloader?.CancelAsync();
    }

    public ObservableCollection<string> MirrorList { get; } =
    [
        @"https://gh-proxy.com/",
        @"https://ghproxy.net/",
        @"https://ghfast.top/",
        @"https://hk.gh-proxy.com/",
        @"https://cdn.gh-proxy.com/",
        @"https://edgeone.gh-proxy.com/",
        @"https://gh.plfjy.top/",
        @""
    ];

    #endregion
}

