using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Abstractions.Services;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Xml.Serialization;
using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Controls;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Messages;
using neo_bpsys_wpf.Views.Windows;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class SettingPageViewModel : ObservableObject
{
    public IUpdaterService UpdaterService { get; }
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
    public SettingPageViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
    {
        //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
    }

    private readonly List<FontFamily> _systemFonts;
    private readonly ISettingsHostService _settingsHostService;
    private readonly ITextSettingsNavigationService _textSettingsNavigationService;
    private readonly IFrontService _frontService;
    private readonly IFilePickerService _filePickerService;
    private readonly ISharedDataService _sharedDataService;
    private readonly IMessageBoxService _messageBoxService;

    public SettingPageViewModel(IUpdaterService updaterService, ISettingsHostService settingsHostService,
        ITextSettingsNavigationService textSettingsNavigationService, IFrontService frontService,
        IFilePickerService filePickerService, IMessageBoxService messageBoxService,
        ISharedDataService sharedDataService)
    {
        AppVersion = "版本 v" + Application.ResourceAssembly.GetName().Version!;
        UpdaterService = updaterService;
        _settingsHostService = settingsHostService;
        _textSettingsNavigationService = textSettingsNavigationService;
        _frontService = frontService;
        _filePickerService = filePickerService;
        _sharedDataService = sharedDataService;
        _messageBoxService = messageBoxService;
        UpdaterService.Downloader.DownloadProgressChanged += Downloader_DownloadProgressChanged;
        UpdaterService.Downloader.DownloadFileCompleted += Downloader_DownloadFileCompleted;
        UpdaterService.Downloader.DownloadStarted += Downloader_DownloadStarted;
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
            { "玩家ID", _settingsHostService.Settings.CutSceneWindowSettings.TextSettings.SurPlayerId },
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
        MapBpV2PickingColorSettings = _settingsHostService.Settings.WidgetsWindowSettings.MapBpV2_PickingBorderColor.ToColor();

        GlobalScoreTotalMargin = _settingsHostService.Settings.ScoreWindowSettings.GlobalScoreTotalMargin;
        _sharedDataService.GlobalScoreTotalMargin = GlobalScoreTotalMargin;
    }

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
        UpdaterService.Downloader.CancelAsync();
    }

    [RelayCommand]
    private static void HopToConfigDir()
    {
        Process.Start("explorer.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "neo-bpsys-wpf"));
    }

    [RelayCommand]
    private static void HopToGameOutputDir()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "neo-bpsys-wpf\\GameInfoOutput"
        );
        Process.Start("explorer.exe", path);
    }

    [RelayCommand]
    private static void HopToLogDir()
    {
        Process.Start("explorer.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "neo-bpsys-wpf", "Log"));
    }

    [RelayCommand]
    private static void ManualGc()
    {
        GC.Collect();
    }

    /// <summary>
    /// 设置UI图片
    /// </summary>
    /// <param name="setAction">应用设置的Action</param>
    /// <param name="windowTypes">窗口类型</param>
    private void SetUiImage(Action<string?> setAction, FrontWindowType[] windowTypes)
    {
        var fileName = _filePickerService.PickImage();
        if (fileName == null) return;
        var destDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "neo-bpsys-wpf", "CustomUi");
        var destFileName = Path.Combine(destDir, Path.GetFileName(fileName));

        if(!Directory.Exists(destDir))
            Directory.CreateDirectory(destDir);

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
        foreach (var windowType in windowTypes)
        {
            WeakReferenceMessenger.Default.Send(new SettingsChangedMessage(windowType));
        }
        _settingsHostService.SaveConfig();
    }

    [RelayCommand]
    private void EditBpWindowImages(string arg)
    {
        var settings = _settingsHostService.Settings.BpWindowSettings;

        var propertyMap = new Dictionary<string, Action<string?>>
        {
            { "BgImageUri", value => settings.BgImageUri = value },
            { "CurrentBanLockImageUri", value => settings.CurrentBanLockImageUri = value },
            { "GlobalBanLockImageUri", value => settings.GlobalBanLockImageUri = value },
            { "PickingBorderImageUri", value => settings.PickingBorderImageUri = value }
        };

        if (!propertyMap.TryGetValue(arg, out var action)) return;
        SetUiImage(action, [FrontWindowType.BpWindow]);
    }

    [RelayCommand]
    private void EditCutSceneWindowImages()
    {
        var settings = _settingsHostService.Settings.CutSceneWindowSettings;
        SetUiImage(value => { settings.BgUri = value; }, [FrontWindowType.CutSceneWindow]);
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

        var propertyMap = new Dictionary<string, Action<string?>>
        {
            { "SurScoreBgImageUri", value => settings.SurScoreBgImageUri = value },
            { "HunScoreBgImageUri", value => settings.HunScoreBgImageUri = value },
            { "GlobalScoreBgImageUri", value => settings.GlobalScoreBgImageUri = value },
            { "GlobalScoreBgImageUriBo3", value => settings.GlobalScoreBgImageUriBo3 = value }
        };

        if (!propertyMap.TryGetValue(arg, out var action)) return;
        SetUiImage(action, [FrontWindowType.ScoreSurWindow, FrontWindowType.ScoreHunWindow, FrontWindowType.ScoreGlobalWindow]);
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
        SetUiImage(value => { settings.BgImageUri = value; }, [FrontWindowType.GameDataWindow]);
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

        var propertyMap = new Dictionary<string, Action<string?>>
        {
            { "MapBpBgUri", value => settings.MapBpBgUri = value },
            { "MapBpV2BgUri", value => settings.MapBpV2BgUri = value },
            { "BpOverviewBgUri", value => settings.BpOverviewBgUri = value },
            { "CurrentBanLockImageUri", value => settings.CurrentBanLockImageUri = value },
            { "GlobalBanLockImageUri", value => settings.GlobalBanLockImageUri = value },
        };

        if (!propertyMap.TryGetValue(arg, out var action)) return;
        SetUiImage(action, [FrontWindowType.WidgetsWindow]);
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
        if (!settingsMap.TryGetValue(type, out var settings) || settings == null)
        {
            return;
        }

        _textSettingsNavigationService.Navigate(
            type,
            new TextSettingsEditControl(_systemFonts, settings, SaveAction, CloseAction));

        return;

        void CloseAction()
        {
            _textSettingsNavigationService.Close(type);
        }

        void SaveAction()
        {
            _settingsHostService.SaveConfig();
        }
    }
    
    [RelayCommand]
    private void SaveBpWindowPickingBorderColor()
    {
        _settingsHostService.Settings.BpWindowSettings.PickingBorderColor = BpWindowPickingColorSettings.ToArgbHexString();
        _settingsHostService.SaveConfig();
        _messageBoxService.ShowInfoAsync("重启后生效");
    }
    
    [RelayCommand]
    private void SaveMapBpV2PickingBorderColor()
    {
        _settingsHostService.Settings.WidgetsWindowSettings.MapBpV2_PickingBorderColor =
            MapBpV2PickingColorSettings.ToArgbHexString();
        _settingsHostService.SaveConfig();
        _messageBoxService.ShowInfoAsync("重启后生效");
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

    public ObservableCollection<string> MirrorList { get; } =
    [
        "https://ghproxy.net/",
        "https://gh.plfjy.top/",
        "https://ghfast.top/",
        ""
    ];
}