using neo_bpsys_wpf.Converters;
using neo_bpsys_wpf.Core.Models;
using System.IO;
using System.Text.Json;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using BpWindowSettings = neo_bpsys_wpf.Core.Models.BpWindowSettings;
using WidgetsWindowSettings = neo_bpsys_wpf.Core.Models.WidgetsWindowSettings;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 设置服务, 实现了 <see cref="ISettingsHostService"/> 接口，负责设置相关的内容
/// </summary>
public class SettingsHostService : ISettingsHostService
{
    public Settings Settings { get; set; } = new();
    private readonly IMessageBoxService _messageBoxService;
    private const string SettingFileName = "Config.json";
    private readonly string _settingFilePath;
    private readonly string _settingFileDirectory;

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
        _settingFileDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "neo-bpsys-wpf");
        _settingFilePath = Path.Combine(_settingFileDirectory, SettingFileName);
        LoadConfig();
    }

    /// <summary>
    /// 保存设置
    /// </summary>
    public void SaveConfig()
    {
        if (!Directory.Exists(_settingFileDirectory))
            Directory.CreateDirectory(_settingFileDirectory);
        try
        {
            File.WriteAllText(_settingFilePath, JsonSerializer.Serialize(Settings, _jsonSerializerOptions));
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
        if (!File.Exists(_settingFilePath))
            ResetConfig();
        var json = File.ReadAllText(_settingFilePath);
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
            if (!Directory.Exists(_settingFileDirectory))
                Directory.CreateDirectory(_settingFileDirectory);

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
            if (!Directory.Exists(_settingFileDirectory))
                Directory.CreateDirectory(_settingFileDirectory);

            switch (windowType)
            {
                case FrontWindowType.BpWindow:
                    Settings.BpWindowSettings = new BpWindowSettings();
                    break;
                case FrontWindowType.CutSceneWindow:
                    Settings.CutSceneWindowSettings = new CutSceneWindowSettings();
                    break;
                case FrontWindowType.ScoreGlobalWindow:
                    Settings.ScoreWindowSettings = new ScoreWindowSettings();
                    break;
                case FrontWindowType.GameDataWindow:
                    Settings.GameDataWindowSettings = new GameDataWindowSettings();
                    break;
                case FrontWindowType.WidgetsWindow:
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
}