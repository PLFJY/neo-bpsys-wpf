using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Converters;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Events;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Helpers;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 设置服务, 实现了 <see cref="ISettingsHostService"/> 接口，负责设置相关的内容
/// </summary>
public class SettingsHostService : ISettingsHostService
{
    private readonly ILogger<SettingsHostService> _logger;
    private readonly ISettingsMigrationService _settingsMigrationService;
    private readonly ILegacyV2StartupMigrationService _legacyV2StartupMigrationService;
    private Settings _settings = new();
    private bool _isBulk;

    /// <summary>
    /// 当前应用设置。
    /// </summary>
    public Settings Settings
    {
        get => _settings;
        set
        {
            if (_settings == value)
            {
                return;
            }

            _settings.PropertyChanged -= OnSettingsPropertyChanged;
            _settings = value;
            _settings.PropertyChanged += OnSettingsPropertyChanged;

            SettingsChanged?.Invoke(this, value);
        }
    }

    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    /// <summary>
    /// 初始化设置服务。
    /// </summary>
    /// <param name="logger">日志记录器。</param>
    /// <param name="settingsMigrationService">设置迁移服务。</param>
    /// <param name="legacyV2StartupMigrationService">旧版 v2 启动迁移服务。</param>
        Converters = { new FontWeightJsonConverter() }
    };

    public SettingsHostService(
        ILogger<SettingsHostService> logger,
        ISettingsMigrationService settingsMigrationService,
        ILegacyV2StartupMigrationService legacyV2StartupMigrationService)
    {
        _logger = logger;
        _settingsMigrationService = settingsMigrationService;
        _legacyV2StartupMigrationService = legacyV2StartupMigrationService;
        // Config loading is intentionally triggered and awaited from App.OnStartup.
    }

    /// <summary>
    /// 保存设置
    /// </summary>
    public async Task SaveConfigAsync()
    {
        if (!Directory.Exists(AppConstants.AppDataPath))
        {
            Directory.CreateDirectory(AppConstants.AppDataPath);
        }

        try
        {
            var jsonStr = JsonSerializer.Serialize(Settings, _jsonSerializerOptions);
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData).Replace(@"\", @"\\");
            jsonStr = jsonStr.Replace(appDataPath, "%APPDATA%");
            await File.WriteAllTextAsync(AppConstants.ConfigFilePath, jsonStr);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Configuration file save error");
            _ = MessageBoxHelper.ShowErrorAsync(
                $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "ConfigurationFileSaveError")}\n{e.Message}");
        }
    }

    /// <summary>
    /// 加载设置
    /// </summary>
    public async Task LoadConfig()
    {
        if (!File.Exists(AppConstants.ConfigFilePath))
        {
            await ResetConfigAsync();
        }

        var json = await File.ReadAllTextAsync(AppConstants.ConfigFilePath);
        try
        {
            var versionInfo = SettingsConfigVersionHelper.InspectJson(json);
            if (versionInfo.IsLegacy)
            {
                var result = await _legacyV2StartupMigrationService.MigrateIfNeededAsync();
                if (!result.Success)
                {
                    throw new InvalidOperationException(result.ErrorMessage ?? "Legacy v2 startup migration failed.");
                }

                if (result.Migrated)
                {
                    json = await File.ReadAllTextAsync(AppConstants.ConfigFilePath);
                }
            }
            else if (versionInfo.Version != SettingsConfigVersionHelper.CurrentSettingsVersion)
            {
                _logger.LogWarning(
                    "Configuration file version is not supported explicitly. Version: {Version}, has version field: {HasVersion}",
                    versionInfo.Version,
                    versionInfo.HasVersion);
            }

            var settings = JsonSerializer.Deserialize<Settings>(json, _jsonSerializerOptions);
            if (settings != null)
            {
                Settings = settings;
                Settings.Version ??= SettingsConfigVersionHelper.CurrentSettingsVersion;
            }
            else
            {
                _ = MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "ConfigurationFileEmpty"));
                await ResetConfigAsync();
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Reading configuration file error");

            if (await MessageBoxHelper.ShowConfirmAsync(
                    $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "ResetConfigurationFileToSolveTheProblem")}?",
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "FailedToReadConfigurationFile"),
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Confirm"), I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Cancel")))
            {
                await ResetConfigAsync();
            }
            else
            {
                Application.Current.Shutdown();
            }
        }
    }

    /// <summary>
    /// 重置设置
    /// </summary>
    public async Task ResetConfigAsync()
    {
        try
        {
            if (!Directory.Exists(AppConstants.AppDataPath))
            {
                Directory.CreateDirectory(AppConstants.AppDataPath);
            }

            _isBulk = true;
            foreach (var window in Enum.GetValues<FrontedWindowType>())
            {
                if (window is FrontedWindowType.ScoreGlobalWindow or FrontedWindowType.ScoreSurWindow
                    or FrontedWindowType.ScoreHunWindow)
                    continue;
                await ResetConfigAsync(window);
            }

            _isBulk = false;
            await SaveConfigAsync();
            await LoadConfig();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Reset configuration file error");
            _ = MessageBoxHelper.ShowErrorAsync(
                $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "ResetConfigurationFileError")}\n{e.Message}");
        }
    }

    /// <summary>
    /// 重置指定窗口的设置
    /// </summary>
    /// <param name="windowType">窗口类型</param>
    public async Task ResetConfigAsync(FrontedWindowType windowType)
    {
        try
        {
            if (!Directory.Exists(AppConstants.AppDataPath))
            {
                Directory.CreateDirectory(AppConstants.AppDataPath);
            }

            switch (windowType)
            {
                case FrontedWindowType.BpWindow:
                    break;
                case FrontedWindowType.CutSceneWindow:
                    break;
                case FrontedWindowType.ScoreWindow:
                case FrontedWindowType.ScoreGlobalWindow:
                case FrontedWindowType.ScoreSurWindow:
                case FrontedWindowType.ScoreHunWindow:
                    break;
                case FrontedWindowType.GameDataWindow:
                    break;
                case FrontedWindowType.BpOverviewWindow:
                case FrontedWindowType.MapV2Window:
                    break;
                default:
                    _logger.LogWarning("Unsupported window type for config reset: {WindowType}", windowType);
                    throw new ArgumentOutOfRangeException(nameof(windowType), windowType, null);
            }

            if (_isBulk)
                await SaveConfigAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Reset Configuration file error");
            _ = MessageBoxHelper.ShowErrorAsync(
                $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "ResetConfigurationFileError")}\n{e.Message}");
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
    /// <summary>
    /// 语言设置变更事件。
    /// </summary>
    /// </summary>
    public event EventHandler<Settings>? SettingsChanged;

    public event EventHandler<LanguageChangedEventArgs>? LanguageSettingChanged;
}
