using neo_bpsys_wpf.Converters;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using System.IO;
using System.Text.Json;
using System.ComponentModel;
using neo_bpsys_wpf.Core.Events;
using BpWindowSettings = neo_bpsys_wpf.Core.Models.BpWindowSettings;
using WidgetsWindowSettings = neo_bpsys_wpf.Core.Models.WidgetsWindowSettings;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 设置服务, 实现了 <see cref="ISettingsHostService"/> 接口，负责设置相关的内容
/// </summary>
public class SettingsHostService : ISettingsHostService
{
    private Settings _settings = new();

    public Settings Settings
    {
        get => _settings;
        set
        {
            if (_settings == value) return;
            if (_settings != null)
                _settings.PropertyChanged -= OnSettingsPropertyChanged;
            _settings = value;
            if (_settings != null)
                _settings.PropertyChanged += OnSettingsPropertyChanged;
            SettingsChanged?.Invoke(this, value);
        }
    }

    private readonly IMessageBoxService _messageBoxService;

    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new FontWeightJsonConverter() }
    };

    public SettingsHostService(IMessageBoxService messageBoxService)
    {
        _messageBoxService = messageBoxService;
        LoadConfig();
    }

    /// <summary>
    /// 保存设置
    /// </summary>
    public void SaveConfig()
    {
        if (!Directory.Exists(AppConstants.AppDataPath))
            Directory.CreateDirectory(AppConstants.AppDataPath);
        try
        {
            var jsonStr = JsonSerializer.Serialize(Settings, _jsonSerializerOptions);
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData).Replace(@"\", @"\\");
            jsonStr = jsonStr.Replace(appDataPath, "%APPDATA%");
            File.WriteAllText(AppConstants.ConfigFilePath, jsonStr);
        }
        catch (Exception e)
        {
            _messageBoxService.ShowErrorAsync($"配置文件存储错误\n{e.Message}");
        }
    }

    /// <summary>
    /// 加载设置
    /// </summary>
    public void LoadConfig()
    {
        if (!File.Exists(AppConstants.ConfigFilePath))
            ResetConfig();
        var json = File.ReadAllText(AppConstants.ConfigFilePath);
        try
        {
            var settings = JsonSerializer.Deserialize<Settings>(json, _jsonSerializerOptions);
            if (settings != null)
            {
                Settings = settings;
            }
            else
            {
                _messageBoxService.ShowErrorAsync("配置文件为空");
                ResetConfig();
            }
        }
        catch (Exception e)
        {
            _messageBoxService.ShowErrorAsync($"读取配置文件错误\n{e.Message}");
            ResetConfig();
        }
    }

    /// <summary>
    /// 重置设置
    /// </summary>
    public void ResetConfig()
    {
        try
        {
            if (!Directory.Exists(AppConstants.AppDataPath))
                Directory.CreateDirectory(AppConstants.AppDataPath);

            Settings = new Settings();
            SaveConfig();
            LoadConfig();
        }
        catch (Exception e)
        {
            _messageBoxService.ShowErrorAsync($"重置配置文件错误\n{e.Message}");
            throw;
        }
    }

    /// <summary>
    /// 重置指定窗口的设置
    /// </summary>
    /// <param name="windowType">窗口类型</param>
    public void ResetConfig(FrontWindowType windowType)
    {
        try
        {
            if (!Directory.Exists(AppConstants.AppDataPath))
                Directory.CreateDirectory(AppConstants.AppDataPath);

            switch (windowType)
            {
                case FrontWindowType.BpWindow:
                    try
                    {
                        if (Settings.BpWindowSettings.BgImageUri != null)
                            File.Delete(Settings.BpWindowSettings.BgImageUri);
                        if (Settings.BpWindowSettings.PickingBorderImageUri != null)
                            File.Delete(Settings.BpWindowSettings.PickingBorderImageUri);
                        if (Settings.BpWindowSettings.GlobalBanLockImageUri != null)
                            File.Delete(Settings.BpWindowSettings.GlobalBanLockImageUri);
                        if (Settings.BpWindowSettings.CurrentBanLockImageUri != null)
                            File.Delete(Settings.BpWindowSettings.CurrentBanLockImageUri);
                    }
                    catch
                    {
                        // ignored
                    }

                    Settings.BpWindowSettings = new BpWindowSettings();
                    break;
                case FrontWindowType.CutSceneWindow:
                    try
                    {
                        if (Settings.CutSceneWindowSettings.BgUri != null)
                            File.Delete(Settings.CutSceneWindowSettings.BgUri);
                    }
                    catch
                    {
                        // ignored
                    }

                    Settings.CutSceneWindowSettings = new CutSceneWindowSettings();
                    break;
                case FrontWindowType.ScoreGlobalWindow:
                case FrontWindowType.ScoreSurWindow:
                case FrontWindowType.ScoreHunWindow:
                    try
                    {
                        if (Settings.ScoreWindowSettings.SurScoreBgImageUri != null)
                            File.Delete(Settings.ScoreWindowSettings.SurScoreBgImageUri);
                        if (Settings.ScoreWindowSettings.HunScoreBgImageUri != null)
                            File.Delete(Settings.ScoreWindowSettings.HunScoreBgImageUri);
                        if (Settings.ScoreWindowSettings.GlobalScoreBgImageUri != null)
                            File.Delete(Settings.ScoreWindowSettings.GlobalScoreBgImageUri);
                    }
                    catch
                    {
                        // ignored
                    }

                    Settings.ScoreWindowSettings = new ScoreWindowSettings();
                    break;
                case FrontWindowType.GameDataWindow:
                    try
                    {
                        if (Settings.GameDataWindowSettings.BgImageUri != null)
                            File.Delete(Settings.GameDataWindowSettings.BgImageUri);
                    }
                    catch
                    {
                        // ignored
                    }
                    Settings.GameDataWindowSettings = new GameDataWindowSettings();
                    break;
                case FrontWindowType.WidgetsWindow:
                    try
                    {
                        if (Settings.WidgetsWindowSettings.MapBpBgUri != null)
                            File.Delete(Settings.WidgetsWindowSettings.MapBpBgUri);
                        if (Settings.WidgetsWindowSettings.MapBpV2BgUri != null)
                            File.Delete(Settings.WidgetsWindowSettings.MapBpV2BgUri);
                        if (Settings.WidgetsWindowSettings.MapBpV2PickingBorderImageUri != null)
                            File.Delete(Settings.WidgetsWindowSettings.MapBpV2PickingBorderImageUri);
                        if (Settings.WidgetsWindowSettings.BpOverviewBgUri != null)
                            File.Delete(Settings.WidgetsWindowSettings.BpOverviewBgUri);
                        if (Settings.WidgetsWindowSettings.CurrentBanLockImageUri != null)
                            File.Delete(Settings.WidgetsWindowSettings.CurrentBanLockImageUri);
                        if (Settings.WidgetsWindowSettings.GlobalBanLockImageUri != null)
                            File.Delete(Settings.WidgetsWindowSettings.GlobalBanLockImageUri);
                    }
                    catch
                    {
                        // ignored
                    }
                    Settings.WidgetsWindowSettings = new WidgetsWindowSettings();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(windowType), windowType, null);
            }

            SaveConfig();
        }
        catch (Exception e)
        {
            _messageBoxService.ShowErrorAsync($"重置配置文件错误\n{e.Message}");
            throw;
        }
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (string.IsNullOrEmpty(args.PropertyName)
            || args.PropertyName == nameof(_settings.CultureInfo)
            || args.PropertyName == nameof(_settings.Language))
        {
            LanguageSettingChanged?.Invoke(this, new LanguageChangedEventArgs(_settings.CultureInfo));
        }
    }

    /// <summary>
    /// 配置文件改变事件
    /// </summary>
    public event EventHandler<Settings>? SettingsChanged;

    public event EventHandler<LanguageChangedEventArgs>? LanguageSettingChanged;
}
