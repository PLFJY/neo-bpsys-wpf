using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Abstractions.Services;
using neo_bpsys_wpf.Converters;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Models;
using neo_bpsys_wpf.Views.Windows;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace neo_bpsys_wpf.Services
{
    /// <summary>
    /// 设置服务, 实现了 <see cref="ISettingsHostService"/> 接口，负责设置相关的内容
    /// </summary>
    public class SettingsHostService : ISettingsHostService
    {
        private readonly ILogger<SettingsHostService> _logger;

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

        public SettingsHostService(IMessageBoxService messageBoxService, ILogger<SettingsHostService> logger)
        {
            _messageBoxService = messageBoxService;
            _logger = logger;
            _logger.LogInformation("SettingsHostService initialized");

            _settingFileDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "neo-bpsys-wpf");
            _settingFilePath = Path.Combine(_settingFileDirectory, SettingFileName);

            _logger.LogDebug("Configuration file path: {Path}", _settingFilePath);

            LoadConfig();
        }

        /// <summary>
        /// 保存设置
        /// </summary>
        public void SaveConfig()
        {
            _logger.LogInformation("Saving settings configuration");

            try
            {
                if (!Directory.Exists(_settingFileDirectory))
                {
                    _logger.LogDebug("Creating settings directory: {Directory}", _settingFileDirectory);
                    Directory.CreateDirectory(_settingFileDirectory);
                }

                var json = JsonSerializer.Serialize(Settings, _jsonSerializerOptions);
                File.WriteAllText(_settingFilePath, json);

                _logger.LogInformation("Settings successfully saved to {Path}", _settingFilePath);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to save settings to {Path}", _settingFilePath);
                _messageBoxService.ShowErrorAsync($"配置文件存储错误\n{e.Message}");
            }
        }

        /// <summary>
        /// 加载设置
        /// </summary>
        public void LoadConfig()
        {
            _logger.LogInformation("Loading settings configuration");

            if (!File.Exists(_settingFilePath))
            {
                _logger.LogWarning("Settings file not found at {Path}, resetting to default", _settingFilePath);
                ResetConfig();
                return;
            }

            try
            {
                _logger.LogDebug("Reading settings file: {Path}", _settingFilePath);
                var json = File.ReadAllText(_settingFilePath);
                var settings = JsonSerializer.Deserialize<Settings>(json, _jsonSerializerOptions);

                if (settings != null)
                {
                    Settings = settings;
                    _logger.LogInformation("Settings successfully loaded from {Path}", _settingFilePath);
                    _logger.LogDebug("Loaded settings: {@Settings}", Settings);
                }
                else
                {
                    _logger.LogWarning("Settings file contains no data, resetting to default");
                    _messageBoxService.ShowErrorAsync("配置文件为空");
                    ResetConfig();
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to load settings from {Path}", _settingFilePath);
                _messageBoxService.ShowErrorAsync($"读取配置文件错误\n{e.Message}");
                ResetConfig();
            }
        }

        /// <summary>
        /// 重置设置
        /// </summary>
        public void ResetConfig()
        {
            _logger.LogInformation("Resetting settings to default");

            try
            {
                if (!Directory.Exists(_settingFileDirectory))
                {
                    _logger.LogDebug("Creating settings directory: {Directory}", _settingFileDirectory);
                    Directory.CreateDirectory(_settingFileDirectory);
                }

                Settings = new Settings();

                _logger.LogDebug("Reset settings to default values");
                _logger.LogDebug("Default settings: {@Settings}", Settings);

                SaveConfig();
                LoadConfig();

                _logger.LogInformation("Settings reset successfully");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to reset settings");
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
            _logger.LogInformation("Resetting settings for {WindowType}", windowType);

            try
            {
                if (!Directory.Exists(_settingFileDirectory))
                {
                    _logger.LogDebug("Creating settings directory: {Directory}", _settingFileDirectory);
                    Directory.CreateDirectory(_settingFileDirectory);
                }

                switch (windowType)
                {
                    case FrontWindowType.BpWindow:
                        Settings.BpWindowSettings = new BpWindowSettings();
                        _logger.LogDebug("Reset settings for BpWindow");
                        break;
                    case FrontWindowType.CutSceneWindow:
                        Settings.CutSceneWindowSettings = new CutSceneWindowSettings();
                        _logger.LogDebug("Reset settings for CutSceneWindow");
                        break;
                    case FrontWindowType.ScoreWindow:
                        Settings.ScoreWindowSettings = new ScoreWindowSettings();
                        _logger.LogDebug("Reset settings for ScoreWindow");
                        break;
                    case FrontWindowType.GameDataWindow:
                        Settings.GameDataWindowSettings = new GameDataWindowSettings();
                        _logger.LogDebug("Reset settings for GameDataWindow");
                        break;
                    case FrontWindowType.WidgetsWindow:
                        Settings.WidgetsWindowSettings = new WidgetsWindowSettings();
                        _logger.LogDebug("Reset settings for WidgetsWindow");
                        break;
                    default:
                        _logger.LogWarning("Attempted to reset settings for unknown window type: {WindowType}", windowType);
                        throw new ArgumentOutOfRangeException(nameof(windowType), windowType, null);
                }

                SaveConfig();
                _logger.LogInformation("Settings for {WindowType} reset successfully", windowType);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to reset settings for {WindowType}", windowType);
                _messageBoxService.ShowErrorAsync($"重置配置文件错误\n{e.Message}");
                throw;
            }
        }
    }
}