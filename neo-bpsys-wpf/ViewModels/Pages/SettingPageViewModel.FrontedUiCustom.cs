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
    #region 前台UI自定义

    #region 共有逻辑

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
            _ = MessageBoxHelper.ShowErrorAsync($"{I18nHelper.GetLocalizedString("FailedToApplyPicture")}\n{e}");
            return;
        }

        setAction.Invoke(destFileName);
        _settingsHostService.SaveConfigAsync();
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

    public List<WindowSize> RegularWindowSizesSelectionList { get; } =
    [
        new(1440, 810),
        new(1280, 720),
        new(1920, 1080),
        new(2560, 1440),
        new(3840, 2160)
    ];

    #endregion

    #region BP窗口设置

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
    private void SaveBpWindowPickingBorderColor()
    {
        _settingsHostService.Settings.BpWindowSettings.PickingBorderColor =
            BpWindowPickingColorSettings.ToArgbHexString();
        _settingsHostService.SaveConfigAsync();
    }

    [RelayCommand]
    private void SaveBpWindowBackgroundColor()
    {
        _settingsHostService.Settings.BpWindowSettings.BackgroundColor =
            BpWindowBackgroundColorSettings.ToArgbHexString();
        _settingsHostService.SaveConfigAsync();
    }

    public Color BpWindowPickingColorSettings { get; set; }

    [ObservableProperty] private Color _bpWindowBackgroundColorSettings;

    public bool AllowsBpWindowTransparency
    {
        get => _settingsHostService.Settings.BpWindowSettings.AllowsWindowTransparency;
        set => _ = SaveBpWindowTransparency(value);
    }

    private async Task SaveBpWindowTransparency(bool value)
    {
        _settingsHostService.Settings.BpWindowSettings.AllowsWindowTransparency = value;
        _ = _settingsHostService.SaveConfigAsync();
        OnPropertyChanged(nameof(AllowsBpWindowTransparency));
        if (await MessageBoxHelper.ShowConfirmAsync(I18nHelper.GetLocalizedString("RestartToApply"),
                I18nHelper.GetLocalizedString("RestartNeeded"), I18nHelper.GetLocalizedString("Restart"),
                I18nHelper.GetLocalizedString("NotNow")))
        {
            AppBase.Current.Restart();
        }
    }

    public WindowSize SelectedBpWindowSize
    {
        get => _settingsHostService.Settings.BpWindowSettings.WindowSize;
        set
        {
            _settingsHostService.Settings.BpWindowSettings.WindowSize.SetNewValue(value);
            _settingsHostService.SaveConfigAsync();
            OnPropertyChanged();
        }
    }

    #endregion

    #region 过场窗口设置

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
        if (await MessageBoxHelper.ShowConfirmAsync(
                $"{I18nHelper.GetLocalizedString("AreYouSureToResetMapBP")} {(isBlackVer ? I18nHelper.GetLocalizedString("Black") : I18nHelper.GetLocalizedString("White"))}？",
                I18nHelper.GetLocalizedString("Tips"), I18nHelper.GetLocalizedString("Confirm"),
                I18nHelper.GetLocalizedString("Cancel")))
        {
            _settingsHostService.Settings.CutSceneWindowSettings.IsBlackTalentAndTraitEnable = isBlackVer;
            await _settingsHostService.SaveConfigAsync();
            _ = MessageBoxHelper.ShowInfoAsync(I18nHelper.GetLocalizedString("RestartToApply"));
        }

        OnPropertyChanged(nameof(IsTalentAndTraitBlackVerEnable));
    }

    public WindowSize SelectedCutSceneWindowSize
    {
        get => _settingsHostService.Settings.CutSceneWindowSettings.WindowSize;
        set
        {
            _settingsHostService.Settings.CutSceneWindowSettings.WindowSize.SetNewValue(value);
            _settingsHostService.SaveConfigAsync();
            OnPropertyChanged();
        }
    }

    #endregion

    #region 比分窗口设置

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
        if (await MessageBoxHelper.ShowConfirmAsync(
                I18nHelper.GetLocalizedString("AreYouSureToSaveTheShiftDistanceOfTotalScore"),
                I18nHelper.GetLocalizedString("Tips"),
                I18nHelper.GetLocalizedString("Confirm"), I18nHelper.GetLocalizedString("Cancel")))
        {
            _sharedDataService.GlobalScoreTotalMargin = GlobalScoreTotalMargin;
            _settingsHostService.Settings.ScoreWindowSettings.GlobalScoreTotalMargin = GlobalScoreTotalMargin;
            await _settingsHostService.SaveConfigAsync();
        }
    }

    public bool IsScoreGlobalCampIconBlackVerEnable
    {
        get => _settingsHostService.Settings.ScoreWindowSettings.IsCampIconBlackVerEnabled;
        set => _ = SetScoreGlobalCampIconBlackVerAsync(value);
    }

    private async Task SetScoreGlobalCampIconBlackVerAsync(bool isBlackVer)
    {
        if (await MessageBoxHelper.ShowConfirmAsync(
                $"{I18nHelper.GetLocalizedString("AreYouSureToSetCampIconTo")} {(isBlackVer ? I18nHelper.GetLocalizedString("Black") : I18nHelper.GetLocalizedString("White"))}？",
                I18nHelper.GetLocalizedString("Tips"), I18nHelper.GetLocalizedString("Confirm"),
                I18nHelper.GetLocalizedString("Cancel")))
        {
            _settingsHostService.Settings.ScoreWindowSettings.IsCampIconBlackVerEnabled = isBlackVer;
            await _settingsHostService.SaveConfigAsync();
            _ = MessageBoxHelper.ShowInfoAsync(I18nHelper.GetLocalizedString("RestartToApply"));
        }

        OnPropertyChanged(nameof(IsScoreGlobalCampIconBlackVerEnable));
    }

    [RelayCommand]
    private void SaveScoreGlobalWindowBackgroundColor()
    {
        _settingsHostService.Settings.ScoreWindowSettings.ScoreGlobalWindowBackgroundColor =
            ScoreGlobalWindowBackgroundColorSettings.ToArgbHexString();
        _settingsHostService.SaveConfigAsync();
    }

    [ObservableProperty] private Color _scoreGlobalWindowBackgroundColorSettings;

    public bool AllowsScoreGlobalWindowTransparency
    {
        get => _settingsHostService.Settings.ScoreWindowSettings.AllowsScoreGlobalWindowTransparency;
        set => _ = SaveScoreGlobalWindowTransparency(value);
    }

    private async Task SaveScoreGlobalWindowTransparency(bool value)
    {
        _settingsHostService.Settings.ScoreWindowSettings.AllowsScoreGlobalWindowTransparency = value;
        _ = _settingsHostService.SaveConfigAsync();
        OnPropertyChanged(nameof(AllowsScoreGlobalWindowTransparency));
        if (await MessageBoxHelper.ShowConfirmAsync(I18nHelper.GetLocalizedString("RestartToApply"),
                I18nHelper.GetLocalizedString("RestartNeeded"), I18nHelper.GetLocalizedString("Restart"),
                I18nHelper.GetLocalizedString("NotNow")))
        {
            AppBase.Current.Restart();
        }
    }

    public List<WindowSize> ScoreInGameWindowSizesList { get; } =
    [
        new(480, 152),
        new(240, 76),
        new(160, 51),
        new(960, 304),
        new(1200, 380),
    ];

    public List<WindowSize> ScoreGlobalWindowSizeList { get; } =
    [
        new(1440, 195),
        new(640, 87),
        new(960, 130),
        new(1280, 173),
    ];

    public WindowSize ScoreInGameWindowSize
    {
        get => _settingsHostService.Settings.ScoreWindowSettings.ScoreInGameWindowSize;
        set
        {
            _settingsHostService.Settings.ScoreWindowSettings.ScoreInGameWindowSize.SetNewValue(value);
            _settingsHostService.SaveConfigAsync();
            OnPropertyChanged();
        }
    }

    public WindowSize ScoreGlobalWindowSize
    {
        get => _settingsHostService.Settings.ScoreWindowSettings.ScoreGlobalWindowSize;
        set
        {
            _settingsHostService.Settings.ScoreWindowSettings.ScoreGlobalWindowSize.SetNewValue(value);
            _settingsHostService.SaveConfigAsync();
            OnPropertyChanged();
        }
    }

    #endregion

    #region 赛后数据

    [RelayCommand]
    private void EditGameDataWindowImages()
    {
        var settings = _settingsHostService.Settings.GameDataWindowSettings;
        SetUiImage(value => { settings.BgImageUri = value; }, settings.BgImageUri);
    }

    public WindowSize SelectedGameDataWindowSize
    {
        get => _settingsHostService.Settings.GameDataWindowSettings.WindowSize;
        set
        {
            _settingsHostService.Settings.GameDataWindowSettings.WindowSize.SetNewValue(value);
            _settingsHostService.SaveConfigAsync();
            OnPropertyChanged();
        }
    }

    #endregion

    #region 小组件设置

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
    private void SaveMapBpV2PickingBorderColor()
    {
        _settingsHostService.Settings.WidgetsWindowSettings.MapBpV2_PickingBorderColor =
            MapBpV2PickingColorSettings.ToArgbHexString();
        _settingsHostService.SaveConfigAsync();
    }

    public bool IsMapBpV2CampIconBlackVerEnable
    {
        get => _settingsHostService.Settings.WidgetsWindowSettings.IsCampIconBlackVerEnabled;
        set => _ = SetMapBpV2CampIconBlackVerAsync(value);
    }

    private async Task SetMapBpV2CampIconBlackVerAsync(bool isBlackVer)
    {
        if (await MessageBoxHelper.ShowConfirmAsync(
                $"{I18nHelper.GetLocalizedString("AreYouSureToSetCampIconTo")} {(isBlackVer
                    ? I18nHelper.GetLocalizedString("Black")
                    : I18nHelper.GetLocalizedString("White"))}? ",
                I18nHelper.GetLocalizedString("Tips"), I18nHelper.GetLocalizedString("Confirm"),
                I18nHelper.GetLocalizedString("Cancel")))
        {
            _settingsHostService.Settings.WidgetsWindowSettings.IsCampIconBlackVerEnabled = isBlackVer;
            await _settingsHostService.SaveConfigAsync();
            _ = MessageBoxHelper.ShowInfoAsync(I18nHelper.GetLocalizedString("RestartToApply"));
        }

        OnPropertyChanged(nameof(IsMapBpV2CampIconBlackVerEnable));
    }

    public Color MapBpV2PickingColorSettings { get; set; }

    [RelayCommand]
    private void SaveWidgetsWindowBackgroundColor()
    {
        _settingsHostService.Settings.WidgetsWindowSettings.BackgroundColor =
            WidgetsWindowBackgroundColorSettings.ToArgbHexString();
        _settingsHostService.SaveConfigAsync();
    }

    [ObservableProperty] private Color _widgetsWindowBackgroundColorSettings;

    public bool AllowsWidgetsTransparency
    {
        get => _settingsHostService.Settings.WidgetsWindowSettings.AllowsWindowTransparency;
        set => _ = SaveWidgetsWindowTransparency(value);
    }

    private async Task SaveWidgetsWindowTransparency(bool value)
    {
        _settingsHostService.Settings.WidgetsWindowSettings.AllowsWindowTransparency = value;
        _ = _settingsHostService.SaveConfigAsync();
        OnPropertyChanged(nameof(AllowsWidgetsTransparency));
        if (await MessageBoxHelper.ShowConfirmAsync(I18nHelper.GetLocalizedString("RestartToApply"),
                I18nHelper.GetLocalizedString("RestartNeeded"), I18nHelper.GetLocalizedString("Restart"),
                I18nHelper.GetLocalizedString("NotNow")))
        {
            AppBase.Current.Restart();
        }
    }

    public List<WindowSize> WidgetsWindowSizesList { get; } =
    [
        new(1440, 716),
        new(640, 318),
        new(960, 477),
        new(1280, 636),
    ];

    public WindowSize WidgetsWindowSize
    {
        get => _settingsHostService.Settings.WidgetsWindowSettings.WindowSize;
        set
        {
            _settingsHostService.Settings.WidgetsWindowSettings.WindowSize.SetNewValue(value);
            _settingsHostService.SaveConfigAsync();
            OnPropertyChanged();
        }
    }

    #endregion

    #region 文字设置

    [RelayCommand]
    private void EditTextSettings(FrontedWindowType type)
    {
        var settingsMap = new Dictionary<FrontedWindowType, TextSettings?>
        {
            { FrontedWindowType.BpWindow, SelectedBpWindowTextSettings },
            { FrontedWindowType.CutSceneWindow, SelectedCutSceneWindowTextSettings },
            { FrontedWindowType.ScoreGlobalWindow, SelectedScoreWindowTextSettings },
            { FrontedWindowType.GameDataWindow, SelectedGameDataWindowTextSettings },
            { FrontedWindowType.WidgetsWindow, SelectedWidgetsWindowTextSettings }
        };

        if (!settingsMap.TryGetValue(type, out var settings) || settings == null) return;

        _textSettingsNavigationService.Navigate(
            type,
            new TextSettingsEditControl(_systemFonts,
                settings,
                () => { },
                () => _settingsHostService.SaveConfigAsync(),
                () => _textSettingsNavigationService.Close(type)));
    }

    public Dictionary<string, TextSettings> BpWindowTextSettings { get; }
    public TextSettings? SelectedBpWindowTextSettings { get; set; }

    public Dictionary<string, TextSettings> CutSceneWindowTextSettings { get; }
    public TextSettings? SelectedCutSceneWindowTextSettings { get; set; }

    public Dictionary<string, TextSettings> ScoreWindowTextSettings { get; }
    public TextSettings? SelectedScoreWindowTextSettings { get; set; }

    public Dictionary<string, TextSettings> GameDataWindowTextSettings { get; }
    public TextSettings? SelectedGameDataWindowTextSettings { get; set; }

    public Dictionary<string, TextSettings> WidgetsWindowTextSettings { get; }
    public TextSettings? SelectedWidgetsWindowTextSettings { get; set; }

    #endregion

    #region 重置设置

    [RelayCommand]
    private async Task ResetAsync(FrontedWindowType windowType)
    {
        if (!await MessageBoxHelper.ShowConfirmAsync(
                I18nHelper.GetLocalizedString("AreYouSureToResetAllPersonalSettingsOfThisWindow"),
                I18nHelper.GetLocalizedString("ResetTip"), I18nHelper.GetLocalizedString("Confirm"),
                I18nHelper.GetLocalizedString("Cancel"))) return;
        await _settingsHostService.ResetConfigAsync(windowType);
        if (await MessageBoxHelper.ShowConfirmAsync(I18nHelper.GetLocalizedString("RestartToApply"),
                I18nHelper.GetLocalizedString("RestartNeeded"), I18nHelper.GetLocalizedString("Restart"),
                I18nHelper.GetLocalizedString("NotNow")))
        {
            AppBase.Current.Restart();
        }
    }

    [RelayCommand]
    private async Task ResetAllAsync()
    {
        if (!await MessageBoxHelper.ShowConfirmAsync(
                I18nHelper.GetLocalizedString("AreYouSureToResetPersonalSettingsOfAllWindows"),
                I18nHelper.GetLocalizedString("Tips"),
                I18nHelper.GetLocalizedString("Confirm"), I18nHelper.GetLocalizedString("Cancel"))) return;
        await _settingsHostService.ResetConfigAsync();
        if (await MessageBoxHelper.ShowConfirmAsync(I18nHelper.GetLocalizedString("RestartToApply"),
                I18nHelper.GetLocalizedString("RestartNeeded"), I18nHelper.GetLocalizedString("Restart"),
                I18nHelper.GetLocalizedString("NotNow")))
        {
            AppBase.Current.Restart();
        }
    }

    #endregion

    #endregion
}

