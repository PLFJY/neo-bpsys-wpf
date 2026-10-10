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
    private readonly ILegacyV2StartupMigrationService _legacyV2StartupMigrationService;
    private readonly string _configFilePath;
    private Settings _settings = new();

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

            var previousCulture = _settings.CultureInfo;
            _settings.PropertyChanged -= OnSettingsPropertyChanged;
            _settings = value;
            _settings.PropertyChanged += OnSettingsPropertyChanged;

            SettingsChanged?.Invoke(this, value);
            if (!Equals(previousCulture, value.CultureInfo))
            {
                LanguageSettingChanged?.Invoke(this, new LanguageChangedEventArgs(value.CultureInfo));
            }
        }
    }

    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new FontWeightJsonConverter() }
    };

    /// <summary>初始化设置服务；配置由 App.OnStartup 显式加载。</summary>
    /// <param name="logger">日志记录器。</param>
    /// <param name="settingsMigrationService">保留旧构造签名的兼容参数，不再使用。</param>
    /// <param name="legacyV2StartupMigrationService">旧版 v2 启动迁移服务。</param>
    public SettingsHostService(
        ILogger<SettingsHostService> logger,
        ISettingsMigrationService settingsMigrationService,
        ILegacyV2StartupMigrationService legacyV2StartupMigrationService)
        : this(logger, legacyV2StartupMigrationService, AppConstants.ConfigFilePath)
    {
    }

    internal SettingsHostService(
        ILogger<SettingsHostService> logger,
        ILegacyV2StartupMigrationService legacyV2StartupMigrationService,
        string configFilePath)
    {
        _logger = logger;
        _configFilePath = configFilePath;
        _settings.PropertyChanged += OnSettingsPropertyChanged;
        _legacyV2StartupMigrationService = legacyV2StartupMigrationService;
        // Config loading is intentionally triggered and awaited from App.OnStartup.
    }

    /// <summary>
    /// 保存设置
    /// </summary>
    public async Task SaveConfigAsync()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_configFilePath)!);
            var jsonStr = JsonSerializer.Serialize(Settings, _jsonSerializerOptions);
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData).Replace(@"\", @"\\");
            jsonStr = jsonStr.Replace(appDataPath, "%APPDATA%");
            await File.WriteAllTextAsync(_configFilePath, jsonStr);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Configuration file save error");
            _ = MessageBoxHelper.ShowErrorAsync(
                $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "ConfigurationFileSaveError")}\n{e.Message}");
        }
    }

    /// <summary>
    /// 加载设置
    /// </summary>
    public async Task LoadConfig()
    {
        if (!File.Exists(_configFilePath))
        {
            await ResetConfigAsync();
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_configFilePath);
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
                    json = await File.ReadAllTextAsync(_configFilePath);
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
                _ = MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "ConfigurationFileEmpty"));
                await ResetConfigAsync();
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Reading configuration file error");

            if (await MessageBoxHelper.ShowConfirmAsync(
                    $"{I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "ResetConfigurationFileToSolveTheProblem")}?",
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "FailedToReadConfigurationFile"),
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

    /// <summary>恢复应用设置默认值并保存一次，不改变布局包或比赛数据。</summary>
    /// <returns>保存处理完成后结束的任务。</returns>
    public async Task ResetConfigAsync()
    {
        Settings = new Settings();
        await SaveConfigAsync();
    }

    /// <summary>已退役的窗口设置重置入口，不执行任何操作。</summary>
    /// <param name="windowType">兼容参数，不再使用。</param>
    /// <returns>已完成的任务。</returns>
    [Obsolete("Window configuration reset is retired. Reset layouts through the Designer.")]
    public Task ResetConfigAsync(FrontedWindowType windowType) => Task.CompletedTask;

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

    /// <summary>语言设置变更事件。</summary>
    public event EventHandler<LanguageChangedEventArgs>? LanguageSettingChanged;
}
