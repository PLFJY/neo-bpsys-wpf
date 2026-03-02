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

    public string Mirror { get; set; } = DownloadMirrorPresets.DefaultMirror;

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

    public ObservableCollection<string> MirrorList { get; } = new(DownloadMirrorPresets.GhProxyMirrorList);

    #endregion
}

