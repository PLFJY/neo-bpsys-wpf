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
#pragma warning disable CS8618
    public SettingPageViewModel()
#pragma warning restore CS8618
    {
        // Decorative constructor for design-time only.
    }

    private readonly List<FontFamily> _systemFonts =
    [
        new(new Uri("pack://application:,,,/Assets/Fonts/"), "./#汉仪第五人格体简"),
        new(new Uri("pack://application:,,,/Assets/Fonts/"), "./#华康POP1体W5"),
        new(new Uri("pack://application:,,,/Assets/Fonts/"), "./#Essay Text"),
        new(new Uri("pack://application:,,,/Assets/Fonts/"), "./#Selawik"),
        new(new Uri("pack://application:,,,/Assets/Fonts/"), "./#Noto Sans"),
    ];

    private readonly ISettingsHostService _settingsHostService;
    private readonly ITextSettingsNavigationService _textSettingsNavigationService;
    private readonly IFrontedWindowService _frontedWindowService;
    private readonly IFilePickerService _filePickerService;
    private readonly ISharedDataService _sharedDataService;
    private readonly ILogger<SettingPageViewModel> _logger;
    public IUpdaterService UpdaterService { get; }
    private readonly DownloadService? _downloader;

    public SettingPageViewModel(IUpdaterService updaterService, ISettingsHostService settingsHostService,
        ITextSettingsNavigationService textSettingsNavigationService, IFrontedWindowService frontedWindowService,
        IFilePickerService filePickerService, ISharedDataService sharedDataService,
        ILogger<SettingPageViewModel> logger)
    {
        AppVersion = AppConstants.AppVersion;
        UpdaterService = updaterService;
        _settingsHostService = settingsHostService;
        _textSettingsNavigationService = textSettingsNavigationService;
        _frontedWindowService = frontedWindowService;
        _filePickerService = filePickerService;
        _sharedDataService = sharedDataService;
        _logger = logger;

        if (updaterService.Downloader is DownloadService downloader)
        {
            _downloader = downloader;
            _downloader.DownloadProgressChanged += Downloader_DownloadProgressChanged;
            _downloader.DownloadFileCompleted += Downloader_DownloadFileCompleted;
            _downloader.DownloadStarted += Downloader_DownloadStarted;
        }

        _systemFonts = [.. _systemFonts, .. FontsHelper.GetSystemFonts()];

        //设置项列表初始化
        BpWindowTextSettings = new Dictionary<string, TextSettings>
        {
            { "Timer", _settingsHostService.Settings.BpWindowSettings.TextSettings.Timer },
            { "TeamName", _settingsHostService.Settings.BpWindowSettings.TextSettings.TeamName },
            { "GameScores", _settingsHostService.Settings.BpWindowSettings.TextSettings.GameScores },
            { "MatchScores", _settingsHostService.Settings.BpWindowSettings.TextSettings.MajorPoints },
            { "PlayerID", _settingsHostService.Settings.BpWindowSettings.TextSettings.PlayerId },
            { "MapName", _settingsHostService.Settings.BpWindowSettings.TextSettings.MapName },
            { "GameProgress", _settingsHostService.Settings.BpWindowSettings.TextSettings.GameProgress }
        };

        CutSceneWindowTextSettings = new Dictionary<string, TextSettings>
        {
            { "TeamName", _settingsHostService.Settings.CutSceneWindowSettings.TextSettings.TeamName },
            { "MatchScores", _settingsHostService.Settings.CutSceneWindowSettings.TextSettings.MajorPoints },
            { "SurvivorPlayerID", _settingsHostService.Settings.CutSceneWindowSettings.TextSettings.SurPlayerId },
            { "HunterPlayerID", _settingsHostService.Settings.CutSceneWindowSettings.TextSettings.HunPlayerId },
            { "MapName", _settingsHostService.Settings.CutSceneWindowSettings.TextSettings.MapName },
            { "GameProgress", _settingsHostService.Settings.CutSceneWindowSettings.TextSettings.GameProgress }
        };

        ScoreWindowTextSettings = new Dictionary<string, TextSettings>
        {
            { "GameScores", _settingsHostService.Settings.ScoreWindowSettings.TextSettings.GameScores },
            { "MatchScore", _settingsHostService.Settings.ScoreWindowSettings.TextSettings.MajorPoints },
            { "TeamName", _settingsHostService.Settings.ScoreWindowSettings.TextSettings.TeamName },
            {
                "TeamNameInScoreStatistics",
                _settingsHostService.Settings.ScoreWindowSettings.TextSettings.ScoreGlobal_TeamName
            },
            {
                "GameScoresInScoreStatistics",
                _settingsHostService.Settings.ScoreWindowSettings.TextSettings.ScoreGlobal_Data
            },
            {
                "TotalGameScoresInScoreStatistics",
                _settingsHostService.Settings.ScoreWindowSettings.TextSettings.ScoreGlobal_Total
            }
        };

        GameDataWindowTextSettings = new Dictionary<string, TextSettings>
        {
            { "TeamName", _settingsHostService.Settings.GameDataWindowSettings.TextSettings.TeamName },
            { "GameScores", _settingsHostService.Settings.GameDataWindowSettings.TextSettings.GameScores },
            { "MatchScores", _settingsHostService.Settings.GameDataWindowSettings.TextSettings.MajorPoints },
            { "PlayerID", _settingsHostService.Settings.GameDataWindowSettings.TextSettings.PlayerId },
            { "MapName", _settingsHostService.Settings.GameDataWindowSettings.TextSettings.MapName },
            { "GameProgress", _settingsHostService.Settings.GameDataWindowSettings.TextSettings.GameProgress },
            { "SurvivorData", _settingsHostService.Settings.GameDataWindowSettings.TextSettings.SurData },
            { "HunterData", _settingsHostService.Settings.GameDataWindowSettings.TextSettings.HunData }
        };

        WidgetsWindowTextSettings = new Dictionary<string, TextSettings>
        {
            { "MapNameInMapBP", _settingsHostService.Settings.WidgetsWindowSettings.TextSettings.MapBp_MapName },
            { "PickWordInMapBP", _settingsHostService.Settings.WidgetsWindowSettings.TextSettings.MapBp_PickWord },
            { "BanWordInMapBP", _settingsHostService.Settings.WidgetsWindowSettings.TextSettings.MapBp_BanWord },
            { "TeamNameInMapBP", _settingsHostService.Settings.WidgetsWindowSettings.TextSettings.MapBp_TeamName },
            { "MapNameInMapBPV2", _settingsHostService.Settings.WidgetsWindowSettings.TextSettings.MapBpV2_MapName },
            { "TeamNameInMapBPV2", _settingsHostService.Settings.WidgetsWindowSettings.TextSettings.MapBpV2_TeamName },
            { "CampNameInMapBPV2", _settingsHostService.Settings.WidgetsWindowSettings.TextSettings.MapBpV2_CampWords },
            {
                "TeamNameInBPOverview",
                _settingsHostService.Settings.WidgetsWindowSettings.TextSettings.BpOverview_TeamName
            },
            {
                "GameProgressInBPOverview",
                _settingsHostService.Settings.WidgetsWindowSettings.TextSettings.BpOverview_GameProgress
            },
            {
                "GameScoresInBPOverview",
                _settingsHostService.Settings.WidgetsWindowSettings.TextSettings.BpOverview_GameScores
            }
        };

        BpWindowPickingColorSettings = _settingsHostService.Settings.BpWindowSettings.PickingBorderColor.ToColor();
        BpWindowBackgroundColorSettings = _settingsHostService.Settings.BpWindowSettings.BackgroundColor.ToColor();
        ScoreGlobalWindowBackgroundColorSettings =
            _settingsHostService.Settings.ScoreWindowSettings.ScoreGlobalWindowBackgroundColor.ToColor();
        WidgetsWindowBackgroundColorSettings =
            _settingsHostService.Settings.WidgetsWindowSettings.BackgroundColor.ToColor();
        MapBpV2PickingColorSettings =
            _settingsHostService.Settings.WidgetsWindowSettings.MapBpV2_PickingBorderColor.ToColor();

        GlobalScoreTotalMargin = _settingsHostService.Settings.ScoreWindowSettings.GlobalScoreTotalMargin;
        _sharedDataService.GlobalScoreTotalMargin = GlobalScoreTotalMargin;

        //读取设置语言
        SelectedLanguage = _settingsHostService.Settings.Language;
    }

    #region 调试选项

    /// <summary>
    /// 手动触发GC (调试选项)
    /// </summary>
    [RelayCommand]
    private static void ManualGc()
    {
        GC.Collect();
    }

    /// <summary>
    /// 跳转到日志目录
    /// </summary>
    [RelayCommand]
    private static void HopToLogDir()
    {
        Process.Start("explorer.exe", AppConstants.LogPath);
    }

    /// <summary>
    /// 切换全局分数调试开启状态
    /// </summary>
    [RelayCommand]
    private void SwitchDebugGlobalScore()
    {
        IAppHost.Host!.Services.GetRequiredService<ScorePageViewModel>().IsDebugContentVisible =
            !IAppHost.Host.Services.GetRequiredService<ScorePageViewModel>().IsDebugContentVisible;
        _ = MessageBoxHelper.ShowInfoAsync(
            $"ScorePageViewModel.IsDebugContentVisible 已设置为 {IAppHost.Host.Services.GetRequiredService<ScorePageViewModel>().IsDebugContentVisible}");
    }

    /// <summary>
    /// 打开启动提示
    /// </summary>
    [RelayCommand]
    private void OpenTip()
    {
        _settingsHostService.Settings.ShowAfterUpdateTip = true;
        _ = _settingsHostService.SaveConfigAsync();
        _ = MessageBoxHelper.ShowInfoAsync("Settings.ShowTip has been set to true");
    }

    #endregion

    #region 快捷入口

    /// <summary>
    /// 跳转到配置目录
    /// </summary>
    [RelayCommand]
    private static void HopToConfigDir()
    {
        Process.Start("explorer.exe", AppConstants.AppDataPath);
    }

    /// <summary>
    /// 跳转到游戏输出目录
    /// </summary>
    [RelayCommand]
    private static void HopToGameOutputDir()
    {
        var path = Path.Combine(AppConstants.AppOutputPath, "GameInfoOutput");
        Process.Start("explorer.exe", path);
    }

    #endregion
}

