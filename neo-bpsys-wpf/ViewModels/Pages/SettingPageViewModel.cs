using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Downloader;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using neo_bpsys_wpf.Controls;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Abstractions.ViewModels;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Messages;
using neo_bpsys_wpf.Core.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using neo_bpsys_wpf.Locales;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class SettingPageViewModel : ViewModelBase
{
    public SettingPageViewModel()
    {
        //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
    }

    private readonly List<FontFamily> _systemFonts;
    private readonly ISettingsHostService _settingsHostService;
    private readonly ITextSettingsNavigationService _textSettingsNavigationService;
    private readonly IFrontService _frontService;
    private readonly IFilePickerService _filePickerService;
    private readonly ISharedDataService _sharedDataService;
    private readonly ILogger<SettingPageViewModel> _logger;
    private readonly IMessageBoxService _messageBoxService;
    public IUpdaterService UpdaterService { get; }
    private readonly DownloadService? _downloader;

    public SettingPageViewModel(IUpdaterService updaterService, ISettingsHostService settingsHostService,
        ITextSettingsNavigationService textSettingsNavigationService, IFrontService frontService,
        IFilePickerService filePickerService, IMessageBoxService messageBoxService,
        ISharedDataService sharedDataService, ILogger<SettingPageViewModel> logger)
    {
        AppVersion = "版本 v" + Application.ResourceAssembly.GetName().Version!;
        UpdaterService = updaterService;
        _settingsHostService = settingsHostService;
        _textSettingsNavigationService = textSettingsNavigationService;
        _frontService = frontService;
        _filePickerService = filePickerService;
        _sharedDataService = sharedDataService;
        _logger = logger;
        _messageBoxService = messageBoxService;

        if (updaterService.Downloader is Downloader.DownloadService downloader)
        {
            _downloader = downloader;
            _downloader.DownloadProgressChanged += Downloader_DownloadProgressChanged;
            _downloader.DownloadFileCompleted += Downloader_DownloadFileCompleted;
            _downloader.DownloadStarted += Downloader_DownloadStarted;
        }

        _systemFonts = FontsHelper.GetSystemFonts();

        //设置项列表初始化
        BpWindowTextSettings = new Dictionary<string, TextSettings>
        {
            { "计时器", _settingsHostService.Settings.BpWindowSettings.TextSettings.Timer },
            { "队伍名称", _settingsHostService.Settings.BpWindowSettings.TextSettings.TeamName },
            { "小比分", _settingsHostService.Settings.BpWindowSettings.TextSettings.MinorPoints },
            { "大比分", _settingsHostService.Settings.BpWindowSettings.TextSettings.MajorPoints },
            { "玩家ID", _settingsHostService.Settings.BpWindowSettings.TextSettings.PlayerId },
            { "地图名称", _settingsHostService.Settings.BpWindowSettings.TextSettings.MapName },
            { "对局进度", _settingsHostService.Settings.BpWindowSettings.TextSettings.GameProgress }
        };

        CutSceneWindowTextSettings = new Dictionary<string, TextSettings>
        {
            { "队伍名称", _settingsHostService.Settings.CutSceneWindowSettings.TextSettings.TeamName },
            { "大比分", _settingsHostService.Settings.CutSceneWindowSettings.TextSettings.MajorPoints },
            { "求生者玩家ID", _settingsHostService.Settings.CutSceneWindowSettings.TextSettings.SurPlayerId },
            { "监管者玩家ID", _settingsHostService.Settings.CutSceneWindowSettings.TextSettings.HunPlayerId },
            { "地图名称", _settingsHostService.Settings.CutSceneWindowSettings.TextSettings.MapName },
            { "对局进度", _settingsHostService.Settings.CutSceneWindowSettings.TextSettings.GameProgress }
        };

        ScoreWindowTextSettings = new Dictionary<string, TextSettings>
        {
            { "小比分", _settingsHostService.Settings.ScoreWindowSettings.TextSettings.MinorPoints },
            { "大比分", _settingsHostService.Settings.ScoreWindowSettings.TextSettings.MajorPoints },
            { "队伍名称", _settingsHostService.Settings.ScoreWindowSettings.TextSettings.TeamName },
            { "分数统计_队伍名称", _settingsHostService.Settings.ScoreWindowSettings.TextSettings.ScoreGlobal_TeamName },
            { "分数统计_分数", _settingsHostService.Settings.ScoreWindowSettings.TextSettings.ScoreGlobal_Data },
            { "分数统计_总小比分", _settingsHostService.Settings.ScoreWindowSettings.TextSettings.ScoreGlobal_Total }
        };

        GameDataWindowTextSettings = new Dictionary<string, TextSettings>
        {
            { "队伍名称", _settingsHostService.Settings.GameDataWindowSettings.TextSettings.TeamName },
            { "小比分", _settingsHostService.Settings.GameDataWindowSettings.TextSettings.MinorPoints },
            { "大比分", _settingsHostService.Settings.GameDataWindowSettings.TextSettings.MajorPoints },
            { "玩家ID", _settingsHostService.Settings.GameDataWindowSettings.TextSettings.PlayerId },
            { "地图名称", _settingsHostService.Settings.GameDataWindowSettings.TextSettings.MapName },
            { "对局进度", _settingsHostService.Settings.GameDataWindowSettings.TextSettings.GameProgress },
            { "求生者数据", _settingsHostService.Settings.GameDataWindowSettings.TextSettings.SurData },
            { "监管者数据", _settingsHostService.Settings.GameDataWindowSettings.TextSettings.HunData }
        };

        WidgetsWindowTextSettings = new Dictionary<string, TextSettings>
        {
            { "地图BP-地图名称", _settingsHostService.Settings.WidgetsWindowSettings.TextSettings.MapBp_MapName },
            { "地图BP-\"选用\"文字", _settingsHostService.Settings.WidgetsWindowSettings.TextSettings.MapBp_PickWord },
            { "地图BP-\"禁用\"文字", _settingsHostService.Settings.WidgetsWindowSettings.TextSettings.MapBp_BanWord },
            { "地图BP-队伍名称", _settingsHostService.Settings.WidgetsWindowSettings.TextSettings.MapBp_TeamName },
            { "地图BPV2-地图名称", _settingsHostService.Settings.WidgetsWindowSettings.TextSettings.MapBpV2_MapName },
            { "地图BPV2-队伍名称", _settingsHostService.Settings.WidgetsWindowSettings.TextSettings.MapBpV2_TeamName },
            { "地图BPV2-阵营文字", _settingsHostService.Settings.WidgetsWindowSettings.TextSettings.MapBpV2_CampWords },
            { "BP概览-队伍名称", _settingsHostService.Settings.WidgetsWindowSettings.TextSettings.BpOverview_TeamName },
            {
                "BP概览-对局进度",
                _settingsHostService.Settings.WidgetsWindowSettings.TextSettings.BpOverview_GameProgress
            },
            { "BP概览-小比分", _settingsHostService.Settings.WidgetsWindowSettings.TextSettings.BpOverview_MinorPoints }
        };

        BpWindowPickingColorSettings = _settingsHostService.Settings.BpWindowSettings.PickingBorderColor.ToColor();
        MapBpV2PickingColorSettings =
            _settingsHostService.Settings.WidgetsWindowSettings.MapBpV2_PickingBorderColor.ToColor();

        GlobalScoreTotalMargin = _settingsHostService.Settings.ScoreWindowSettings.GlobalScoreTotalMargin;
        _sharedDataService.GlobalScoreTotalMargin = GlobalScoreTotalMargin;

        //读取设置语言
        SelectedLanguage = _settingsHostService.Settings.Language;
    }

    #region 自动更新

    [ObservableProperty] private string _appVersion = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UpdateCheckCommand))]
    private bool _isDownloading;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InstallUpdateCommand))]
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
        DownloadProgressText = e.ProgressPercentage.ToString("0.00") + "%";
        MbPerSecondSpeed = (e.BytesPerSecondSpeed / 1024 / 1024).ToString("0.00") + " MB/s";
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

    #region 语言设置
    private LanguageKey _selectedLanguage = LanguageKey.System;

    public LanguageKey SelectedLanguage
    {
        get => _selectedLanguage;
        set => SetPropertyWithAction(ref _selectedLanguage, value, _ =>
        {
            _settingsHostService.Settings.Language = value;
            _settingsHostService.SaveConfig();
            if (LanguageKey.System == value)
            {
                CultureInfo systemCulture = CultureInfo.CurrentUICulture;
                string systemLanguage = systemCulture.Name;
                I18NExtension.Culture = new CultureInfo(systemLanguage);
                _logger.LogInformation("System language detected: {systemLanguage}, set language to {appLanguage}", systemLanguage, systemLanguage);
                return;
            }

            I18NExtension.Culture = new CultureInfo(value.ToString().Replace('_', '-'));
            _logger.LogInformation("Set language to {appLanguage}", value.ToString());
        });
    }

    public Dictionary<string, LanguageKey> LanguageList { get; } = new()
    {
        {LangKeys.FollowSystem, LanguageKey.System},
        {"简体中文" , LanguageKey.zh_Hans},
        {"English" , LanguageKey.en_US},
        //{"日本語" , LanguageKey.ja_JP }
    };
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
        App.Services.GetRequiredService<ScorePageViewModel>().IsDebugContentVisible =
            !App.Services.GetRequiredService<ScorePageViewModel>().IsDebugContentVisible;
        _messageBoxService.ShowInfoAsync($"ScorePageViewModel.IsDebugContentVisible 已设置为 {App.Services.GetRequiredService<ScorePageViewModel>().IsDebugContentVisible}");
    }

    /// <summary>
    /// 打开启动提示
    /// </summary>
    [RelayCommand]
    private void OpenTip()
    {
        _settingsHostService.Settings.ShowTip = true;
        _settingsHostService.SaveConfig();
        _messageBoxService.ShowInfoAsync("Settings.ShowTip 已设置为 true");
    }

    #endregion

    #region 前台UI自定义

    /// <summary>
    /// 设置UI图片
    /// </summary>
    /// <param name="setAction">应用设置的Action</param>
    /// <param name="originalFileName">原始文件名</param>
    private void SetUiImage(Action<string?> setAction, string? originalFileName = null)
    {
        var fileName = _filePickerService.PickImage();
        if (fileName == null) return;

        var destFileName = Path.Combine(AppConstants.CustomUiPath, Path.GetFileName(fileName));

        if (!Directory.Exists(AppConstants.CustomUiPath))
            Directory.CreateDirectory(AppConstants.CustomUiPath);

        try
        {
            File.Copy(fileName, destFileName, true);
        }
        catch (Exception e)
        {
            _messageBoxService.ShowErrorAsync($"应用图片失败\n{e}");
            return;
        }

        setAction.Invoke(destFileName);
        _settingsHostService.SaveConfig();
        if (originalFileName == null) return;
        try
        {
            File.Delete(originalFileName);
        }
        catch
        {
            // ignored
        }
    }

    [RelayCommand]
    private void EditBpWindowImages(string arg)
    {
        var settings = _settingsHostService.Settings.BpWindowSettings;
        //Value Tuple的Value1是应用的行为，Value2是原设置中的文件名
        var propertyMap = new Dictionary<string, (Action<string?>, string?)>
        {
            { "BgImageUri", (value => settings.BgImageUri = value, settings.BgImageUri) },
            {
                "CurrentBanLockImageUri",
                (value => settings.CurrentBanLockImageUri = value, settings.CurrentBanLockImageUri)
            },
            {
                "GlobalBanLockImageUri",
                (value => settings.GlobalBanLockImageUri = value, settings.GlobalBanLockImageUri)
            },
            {
                "PickingBorderImageUri",
                (value => settings.PickingBorderImageUri = value, settings.PickingBorderImageUri)
            }
        };

        if (!propertyMap.TryGetValue(arg, out var valueTuple)) return;
        SetUiImage(valueTuple.Item1, valueTuple.Item2);
    }

    [RelayCommand]
    private void EditCutSceneWindowImages()
    {
        var settings = _settingsHostService.Settings.CutSceneWindowSettings;
        SetUiImage(value => { settings.BgUri = value; }, settings.BgUri);
    }

    public bool IsTalentAndTraitBlackVerEnable
    {
        get => _settingsHostService.Settings.CutSceneWindowSettings.IsBlackTalentAndTraitEnable;
        set => _ = SetTalentAndTraitBlackVerAsync(value);
    }

    private async Task SetTalentAndTraitBlackVerAsync(bool isBlackVer)
    {
        if (await _messageBoxService.ShowConfirmAsync("确认提示", $"是否切换天赋和辅助特质为{(isBlackVer ? "黑色" : "白色")}图标？"))
        {
            _settingsHostService.Settings.CutSceneWindowSettings.IsBlackTalentAndTraitEnable = isBlackVer;
            _settingsHostService.SaveConfig();
            _ = _messageBoxService.ShowInfoAsync("重启后生效");
        }

        OnPropertyChanged(nameof(IsTalentAndTraitBlackVerEnable));
    }

    [RelayCommand]
    private void EditScoreWindowImages(string arg)
    {
        var settings = _settingsHostService.Settings.ScoreWindowSettings;

        //Value Tuple的Value1是应用的行为，Value2是原设置中的文件名
        var propertyMap = new Dictionary<string, (Action<string?>, string?)>
        {
            { "SurScoreBgImageUri", (value => settings.SurScoreBgImageUri = value, settings.SurScoreBgImageUri) },
            { "HunScoreBgImageUri", (value => settings.HunScoreBgImageUri = value, settings.HunScoreBgImageUri) },
            {
                "GlobalScoreBgImageUri",
                (value => settings.GlobalScoreBgImageUri = value, settings.GlobalScoreBgImageUri)
            },
            {
                "GlobalScoreBgImageUriBo3",
                (value => settings.GlobalScoreBgImageUriBo3 = value, settings.GlobalScoreBgImageUriBo3)
            }
        };

        if (!propertyMap.TryGetValue(arg, out var valueTuple)) return;
        SetUiImage(valueTuple.Item1, valueTuple.Item2);
    }

    [ObservableProperty] private double _globalScoreTotalMargin = 390;

    [RelayCommand]
    private async Task SaveGlobalScoreTotalMargin()
    {
        if (await _messageBoxService.ShowConfirmAsync("确认提示", "是否保存 BO3 和 BO5模式切换之间\"Total\"相差的距离(下一次切换生效)"))
        {
            _sharedDataService.GlobalScoreTotalMargin = GlobalScoreTotalMargin;
            _settingsHostService.Settings.ScoreWindowSettings.GlobalScoreTotalMargin = GlobalScoreTotalMargin;
            _settingsHostService.SaveConfig();
        }
    }

    public bool IsScoreGlobalCampIconBlackVerEnable
    {
        get => _settingsHostService.Settings.ScoreWindowSettings.IsCampIconBlackVerEnabled;
        set => _ = SetScoreGlobalCampIconBlackVerAsync(value);
    }

    private async Task SetScoreGlobalCampIconBlackVerAsync(bool isBlackVer)
    {
        if (await _messageBoxService.ShowConfirmAsync("确认提示", $"是否切换阵营图标为{(isBlackVer ? "黑色" : "白色")}？"))
        {
            _settingsHostService.Settings.ScoreWindowSettings.IsCampIconBlackVerEnabled = isBlackVer;
            _settingsHostService.SaveConfig();
            _ = _messageBoxService.ShowInfoAsync("重启后生效");
        }

        OnPropertyChanged(nameof(IsScoreGlobalCampIconBlackVerEnable));
    }

    [RelayCommand]
    private void EditGameDataWindowImages()
    {
        var settings = _settingsHostService.Settings.GameDataWindowSettings;
        SetUiImage(value => { settings.BgImageUri = value; }, settings.BgImageUri);
    }


    public bool IsMapBpV2CampIconBlackVerEnable
    {
        get => _settingsHostService.Settings.WidgetsWindowSettings.IsCampIconBlackVerEnabled;
        set => _ = SetMapBpV2CampIconBlackVerAsync(value);
    }

    private async Task SetMapBpV2CampIconBlackVerAsync(bool isBlackVer)
    {
        if (await _messageBoxService.ShowConfirmAsync("确认提示", $"是否切换MapBpV2的阵营图标为{(isBlackVer ? "黑色" : "白色")}？"))
        {
            _settingsHostService.Settings.WidgetsWindowSettings.IsCampIconBlackVerEnabled = isBlackVer;
            _settingsHostService.SaveConfig();
            _ = _messageBoxService.ShowInfoAsync("重启后生效");
        }

        OnPropertyChanged(nameof(IsMapBpV2CampIconBlackVerEnable));
    }

    [RelayCommand]
    private void EditWidgetsWindowImages(string arg)
    {
        var settings = _settingsHostService.Settings.WidgetsWindowSettings;

        //Value Tuple的Value1是应用的行为，Value2是原设置中的文件名
        var propertyMap = new Dictionary<string, (Action<string?>, string?)>
        {
            { "MapBpBgUri", (value => settings.MapBpBgUri = value, settings.MapBpBgUri) },
            { "MapBpV2BgUri", (value => settings.MapBpV2BgUri = value, settings.MapBpV2BgUri) },
            {
                "MapBpV2PickingBorderImageUri",
                (value => settings.MapBpV2PickingBorderImageUri = value, settings.MapBpV2PickingBorderImageUri)
            },
            { "BpOverviewBgUri", (value => settings.BpOverviewBgUri = value, settings.BpOverviewBgUri) },
            {
                "CurrentBanLockImageUri",
                (value => settings.CurrentBanLockImageUri = value, settings.CurrentBanLockImageUri)
            },
            {
                "GlobalBanLockImageUri",
                (value => settings.GlobalBanLockImageUri = value, settings.GlobalBanLockImageUri)
            }
        };

        if (!propertyMap.TryGetValue(arg, out var valueTuple)) return;
        SetUiImage(valueTuple.Item1, valueTuple.Item2);
    }

    [RelayCommand]
    private void EditTextSettings(FrontWindowType type)
    {
        var settingsMap = new Dictionary<FrontWindowType, TextSettings?>
        {
            { FrontWindowType.BpWindow, SelectedBpWindowTextSettings },
            { FrontWindowType.CutSceneWindow, SelectedCutSceneWindowTextSettings },
            { FrontWindowType.ScoreGlobalWindow, SelectedScoreWindowTextSettings },
            { FrontWindowType.GameDataWindow, SelectedGameDataWindowTextSettings },
            { FrontWindowType.WidgetsWindow, SelectedWidgetsWindowTextSettings }
        };

        if (!settingsMap.TryGetValue(type, out var settings) || settings == null) return;

        _textSettingsNavigationService.Navigate(
            type,
            new TextSettingsEditControl(_systemFonts,
                settings,
                () => { },
                () => _settingsHostService.SaveConfig(),
                () => _textSettingsNavigationService.Close(type)));
    }

    [RelayCommand]
    private void SaveBpWindowPickingBorderColor()
    {
        _settingsHostService.Settings.BpWindowSettings.PickingBorderColor =
            BpWindowPickingColorSettings.ToArgbHexString();
        _settingsHostService.SaveConfig();
    }

    [RelayCommand]
    private void SaveMapBpV2PickingBorderColor()
    {
        _settingsHostService.Settings.WidgetsWindowSettings.MapBpV2_PickingBorderColor =
            MapBpV2PickingColorSettings.ToArgbHexString();
        _settingsHostService.SaveConfig();
    }

    [RelayCommand]
    private async Task ResetAsync(FrontWindowType windowType)
    {
        if (!await _messageBoxService.ShowConfirmAsync("重置提示", $"是否重置{windowType}的个性化设置")) return;
        _settingsHostService.ResetConfig(windowType);
        _ = _messageBoxService.ShowInfoAsync("部分设置重启后生效");
    }

    [RelayCommand]
    private async Task ResetAllAsync()
    {
        if (!await _messageBoxService.ShowConfirmAsync("重置提示", $"是否重置所有前台窗口的个性化设置")) return;
        _settingsHostService.ResetConfig();
        _ = _messageBoxService.ShowInfoAsync("部分设置重启后生效");
    }

    public Color BpWindowPickingColorSettings { get; set; }
    public Dictionary<string, TextSettings> BpWindowTextSettings { get; }
    public TextSettings? SelectedBpWindowTextSettings { get; set; }

    public Dictionary<string, TextSettings> CutSceneWindowTextSettings { get; }
    public TextSettings? SelectedCutSceneWindowTextSettings { get; set; }

    public Dictionary<string, TextSettings> ScoreWindowTextSettings { get; }
    public TextSettings? SelectedScoreWindowTextSettings { get; set; }

    public Dictionary<string, TextSettings> GameDataWindowTextSettings { get; }
    public TextSettings? SelectedGameDataWindowTextSettings { get; set; }

    public Color MapBpV2PickingColorSettings { get; set; }
    public Dictionary<string, TextSettings> WidgetsWindowTextSettings { get; }
    public TextSettings? SelectedWidgetsWindowTextSettings { get; set; }

    #endregion

    #region 前台UI导入导出

    //==============================================================
    // bpui文件目录结构:
    // *.bpui/
    // ├── CustomUi/
    // ├── FrontElementsConfig/
    // └── Config.json
    // 导出过程：先触发一次UI保存逻辑，复制Config.json到临时目录
    // 导入UI对象，利用反射拿到所有自定义UI的路径，复制所有自定义UI文件到临时目录
    // 复制前台元素位置文件到临时目录
    // 打包，改名，输出
    // 
    // 导入过程: 读取文件，解压，复制，覆盖，删除
    //==============================================================

    /// <summary>
    /// 临时文件路径
    /// </summary>
    private static readonly string TempPath = Path.Combine(AppConstants.AppTempPath, "UiPackage");

    /// <summary>
    /// 自定义UI临时文件路径
    /// </summary>
    private static readonly string CustomUiTempPath = Path.Combine(TempPath, "CustomUi");

    /// <summary>
    /// 配置临时文件路径
    /// </summary>
    private static readonly string ConfigTempPath = Path.Combine(TempPath, "Config.json");

    /// <summary>
    /// 前台元素位置临时文件路径
    /// </summary>
    private static readonly string FrontElementsConfigTempPath = Path.Combine(TempPath, "FrontElementsConfig");

    /// <summary>
    /// 导出UI配置
    /// </summary>
    [RelayCommand]
    private async Task ExportUiConfigAsync()
    {
        //打开通用对话框选择保存路径
        var dialog = new SaveFileDialog
        {
            Filter = "bpui文件(*.bpui)|*.bpui|所有文件(*.*)|*.*",
            DefaultExt = ".bpui",
            AddExtension = true,
            DefaultDirectory = AppConstants.AppOutputPath,
            Title = "保存为",
            FileName = "saved_ui",
            OverwritePrompt = false
        };
        var result = (bool)dialog.ShowDialog()!;
        //如果用户没选择直接退出
        if (!result) return;

        //准备一些路径
        var savePath = dialog.FileName;
        //先保存一遍配置保证地址格式已被转换
        _settingsHostService.SaveConfig();
        try
        {
            //创建临时文件夹
            if (Directory.Exists(TempPath))
                Directory.Delete(TempPath, true);

            Directory.CreateDirectory(TempPath);

            //复制Config文件
            File.Copy(AppConstants.ConfigFilePath, ConfigTempPath);
            //复制自定义UI
            CopyCustomUiToTemp(_settingsHostService.Settings, CustomUiTempPath);
            //复制前台配置文件
            foreach (var valueTuple in _frontService.FrontCanvas)
            {
                var windowName = _frontService.GetWindowName(valueTuple.Item1);
                if (windowName == null) continue;
                CopyFrontElementsPositionFileToTemp(windowName, valueTuple.Item2);
            }

            //打包
            var zipPath = Path.Combine(AppConstants.AppTempPath, Path.GetFileName(savePath));
            if (File.Exists(zipPath)) File.Delete(zipPath);
            ZipFile.CreateFromDirectory(TempPath, zipPath);
            //保存
            if (await _messageBoxService.ShowConfirmAsync("覆盖提示", $"{savePath}已存在，是否覆盖"))
                File.Delete(savePath);
            else
            {
                //删除作案痕迹
                Directory.Delete(TempPath, true);
                File.Delete(zipPath);
                return;
            }

            File.Copy(zipPath, savePath);
            //删除作案痕迹
            Directory.Delete(TempPath, true);
            File.Delete(zipPath);
            //提示用户已完成
            await _messageBoxService.ShowInfoAsync($"UI 配置已被保存为{savePath}");
        }
        catch (Exception e)
        {
            await _messageBoxService.ShowErrorAsync(e.Message, "打包UI错误");
        }
    }

    /// <summary>
    /// 复制自定义UI位置文件
    /// </summary>
    /// <param name="windowName">窗口名称</param>
    /// <param name="canvasName">画布名称</param>
    private static void CopyFrontElementsPositionFileToTemp(string windowName,
        string canvasName = "BaseCanvas")
    {
        var path = Path.Combine(AppConstants.AppDataPath, $"{windowName}Config-{canvasName}.json");
        var destPath = Path.Combine(FrontElementsConfigTempPath, $"{windowName}Config-{canvasName}.json");
        if (!Directory.Exists(FrontElementsConfigTempPath)) Directory.CreateDirectory(FrontElementsConfigTempPath);
        if (File.Exists(path))
            File.Copy(path, destPath);
    }

    /// <summary>
    /// 复制自定义UI图片文件
    /// </summary>
    /// <param name="settings">设置</param>
    /// <param name="targetPath">目标路径</param>
    private static void CopyCustomUiToTemp(Settings settings, string targetPath)
    {
        var paths = CollectValidImagePathsIterative(settings);
        if (!Directory.Exists(targetPath)) Directory.CreateDirectory(targetPath);
        foreach (var path in paths)
        {
            File.Copy(path, Path.Combine(targetPath, Path.GetFileName(path)), true);
        }
    }

    /// <summary>
    /// 递归获取有效的图片路径
    /// </summary>
    /// <param name="root">根对象</param>
    /// <returns>有效的图片路径</returns>
    private static HashSet<string> CollectValidImagePathsIterative(object root)
    {
        var validPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visitedObjects = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var queue = new Queue<object>();

        queue.Enqueue(root);
        visitedObjects.Add(root);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var type = current.GetType();

            // 仅处理我们关心的命名空间（避免进入WPF内部对象）
            if (!type.Namespace?.StartsWith("neo_bpsys_wpf.Core.Models") == true)
                continue;

            // 处理当前对象的所有属性
            foreach (var prop in GetRelevantProperties(type))
            {
                try
                {
                    var value = prop.GetValue(current);

                    // 处理字符串属性（图片URI）
                    if (prop.PropertyType == typeof(string))
                    {
                        ProcessStringProperty(value as string, validPaths);
                    }
                    // 处理嵌套对象
                    else if (value != null &&
                             !visitedObjects.Contains(value) &&
                             !IsWpfResourceType(prop.PropertyType))
                    {
                        visitedObjects.Add(value);
                        queue.Enqueue(value);
                    }
                }
                catch
                {
                    // 忽略无法访问的属性
                }
            }
        }

        return validPaths;
    }

    /// <summary>
    /// 获取所有可访问的属性路径
    /// </summary>
    /// <param name="type">类型</param>
    /// <returns>属性信息列表</returns>
    private static IEnumerable<PropertyInfo> GetRelevantProperties(Type type)
    {
        // 仅获取：公共实例属性 + 非索引器 + (字符串或自定义类型)
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0 &&
                        (p.PropertyType == typeof(string) ||
                         !p.PropertyType.IsValueType));
    }

    /// <summary>
    /// 获取所有可序列化的属性
    /// </summary>
    /// <param name="path">路径</param>
    /// <param name="validPaths">有效路径列表</param>
    private static void ProcessStringProperty(string? path, HashSet<string> validPaths)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        // 排除WPF资源路径
        if (path.StartsWith("pack://", StringComparison.OrdinalIgnoreCase))
            return;

        // 排除颜色代码（#FFFFFFFF）
        if (Regex.IsMatch(path, "^#[0-9A-Fa-f]{6,8}$"))
            return;

        // 处理环境变量
        var expandedPath = Environment.ExpandEnvironmentVariables(path);

        // 规范化路径
        if (TryNormalizePath(expandedPath, out var normalizedPath) &&
            File.Exists(normalizedPath))
        {
            validPaths.Add(normalizedPath);
        }
    }

    /// <summary>
    /// 是否是WPF的资源类型，如果是返回否，不进行递归
    /// </summary>
    /// <param name="type">类型</param>
    /// <returns>是否是WPF的资源类型</returns>
    private static bool IsWpfResourceType(Type type)
    {
        // 排除WPF资源类型，防止进入复杂对象图
        return type.Namespace?.StartsWith("System.Windows") == true ||
               type.Namespace?.StartsWith("System.Media") == true ||
               type == typeof(FontFamily) ||
               type == typeof(Brush) ||
               type == typeof(ImageSource);
    }

    /// <summary>
    /// 尝试使路径规则化
    /// </summary>
    /// <param name="inputPath">输入路径</param>
    /// <param name="normalizedPath">正规化后的路径</param>
    /// <returns>是否成功</returns>
    private static bool TryNormalizePath(string inputPath, out string? normalizedPath)
    {
        normalizedPath = null;

        try
        {
            // 获取绝对路径，确保路径斜线格式正确
            var cleanPath = Path.GetFullPath(
                inputPath.Replace('/', Path.DirectorySeparatorChar)
                    .Replace('\\', Path.DirectorySeparatorChar)
            );

            // 验证路径是否在应用程序目录或用户目录内（安全检查）
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var userDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            if (cleanPath.StartsWith(appDir, StringComparison.OrdinalIgnoreCase) ||
                cleanPath.StartsWith(userDir, StringComparison.OrdinalIgnoreCase))
            {
                normalizedPath = cleanPath;
                return true;
            }
        }
        catch
        {
            // ignored
        }

        return false;
    }

    /// <summary>
    /// 导入UI配置
    /// </summary>
    [RelayCommand]
    private async Task ImportUiConfigAsync()
    {
        //打开通用对话框选择保存路径
        var dialog = new OpenFileDialog()
        {
            Filter = "bpui文件(*.bpui)|*.bpui|所有文件(*.*)|*.*",
            InitialDirectory = AppConstants.AppOutputPath,
            RestoreDirectory = false
        };
        var result = (bool)dialog.ShowDialog()!;
        //如果用户没选择直接退出
        if (!result) return;

        //准备ui文件名
        var uiFilePath = dialog.FileName;

        //如果存在了文件夹直接删除
        if (Directory.Exists(TempPath))
            Directory.Delete(TempPath, true);

        try
        {
            //解压UI包
            ZipFile.ExtractToDirectory(uiFilePath, TempPath);

            //拷贝配置文件
            File.Copy(ConfigTempPath, AppConstants.ConfigFilePath, true);

            //拷贝自定义UI图片
            var customUiFiles = Directory.GetFiles(CustomUiTempPath);
            if (!Directory.Exists(AppConstants.CustomUiPath))
                Directory.CreateDirectory(AppConstants.CustomUiPath);
            foreach (var customUiFile in customUiFiles)
            {
                File.Copy(customUiFile, Path.Combine(AppConstants.CustomUiPath, Path.GetFileName(customUiFile)), true);
            }

            //拷贝前台位置配置文件
            var frontElementConfigures = Directory.GetFiles(FrontElementsConfigTempPath);
            foreach (var frontElementConfigure in frontElementConfigures)
            {
                File.Copy(frontElementConfigure, Path.Combine(AppConstants.AppDataPath, Path.GetFileName(frontElementConfigure)), true);
            }

            //清理作案痕迹
            Directory.Delete(TempPath, true);

            //告诉用户已经导入完了
            await _messageBoxService.ShowInfoAsync("UI导入完成，应用将自动重启", "UI 导入提示");

            //重启应用程序
            App.Restart();
        }
        catch (Exception e)
        {
            await _messageBoxService.ShowErrorAsync(e.Message, "加载UI包失败");
        }
    }

    #endregion
}