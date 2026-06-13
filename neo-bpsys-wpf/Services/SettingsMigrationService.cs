using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Converters;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.Legacy;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 设置迁移服务
/// </summary>
public class SettingsMigrationService : ISettingsMigrationService
{
    private const string DefaultOpaqueBackgroundColor = "#FF00FF00";

    private readonly ILogger<SettingsMigrationService> _logger;

    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new FontWeightJsonConverter() }
    };

    public SettingsMigrationService(ILogger<SettingsMigrationService> logger)
    {
        _logger = logger;
    }

    public bool IsLegacyConfig(string configFilePath)
    {
        if (!File.Exists(configFilePath))
        {
            return false;
        }

        var json = File.ReadAllText(configFilePath);
        return SettingsConfigVersionHelper.InspectJson(json).IsLegacy;
    }

    public async Task<SettingsMigrationResult> MigrateLegacyConfigToV3Async(
        string configFilePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(configFilePath))
            {
                return new SettingsMigrationResult
                {
                    Success = true,
                    Migrated = false
                };
            }

            _logger.LogInformation("Starting legacy settings migration to v3: {ConfigFilePath}", configFilePath);

            var json = await File.ReadAllTextAsync(configFilePath, cancellationToken);
            if (!SettingsConfigVersionHelper.InspectJson(json).IsLegacy)
            {
                return new SettingsMigrationResult
                {
                    Success = true,
                    Migrated = false
                };
            }

            var backupPath = CreateBackupPath(configFilePath);
            File.Copy(configFilePath, backupPath);
            var settings = JsonSerializer.Deserialize<Settings>(json, _jsonSerializerOptions);
            if (settings == null)
            {
                return new SettingsMigrationResult
                {
                    Success = false,
                    Migrated = false,
                    BackupPath = backupPath,
                    ErrorMessage = "Configuration file is empty."
                };
            }

            settings.Version = SettingsConfigVersionHelper.CurrentSettingsVersion;

            var migratedJson = CleanLegacyFrontendFields(JsonSerializer.Serialize(settings, _jsonSerializerOptions));
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData).Replace(@"\", @"\\");
            migratedJson = migratedJson.Replace(appDataPath, "%APPDATA%");
            await File.WriteAllTextAsync(configFilePath, migratedJson, cancellationToken);

            _logger.LogInformation(
                "Legacy settings migration to v3 completed: {ConfigFilePath}, backup: {BackupPath}",
                configFilePath,
                backupPath);

            return new SettingsMigrationResult
            {
                Success = true,
                Migrated = true,
                BackupPath = backupPath
            };
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Legacy settings migration to v3 failed: {ConfigFilePath}", configFilePath);
            return new SettingsMigrationResult
            {
                Success = false,
                Migrated = false,
                ErrorMessage = e.Message
            };
        }
    }

    private async Task MigrateLegacyFrontendSettingsAsync(string json, CancellationToken cancellationToken)
    {
        LegacySettings? legacySettings;
        try
        {
            legacySettings = JsonSerializer.Deserialize<LegacySettings>(json, _jsonSerializerOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Legacy frontend settings could not be deserialized; frontend layout migration skipped.");
            return;
        }

        if (legacySettings is null || !HasLegacyFrontendSettings(legacySettings))
        {
            return;
        }

        var layoutRoot = AppConstants.FrontedLayoutsPath;
        var builtInRoot = Path.Combine(AppConstants.ResourcesPath, "FrontedLayouts");
        var legacyRoot = JsonNode.Parse(json) as JsonObject;
        await MigrateWindowAsync(
            "BpWindow",
            "BaseCanvas",
            "BpWindow",
            legacySettings.BpWindowSettings?.BgImageUri,
            legacySettings.BpWindowSettings?.WindowSize,
            legacySettings.BpWindowSettings?.BackgroundColor,
            HasLegacyProperty(legacyRoot, "BpWindowSettings", "AllowsWindowTransparency")
                ? legacySettings.BpWindowSettings?.AllowsWindowTransparency
                : null,
            cancellationToken);
        await MigrateWindowAsync(
            "CutSceneWindow",
            "BaseCanvas",
            "CutSceneWindow",
            legacySettings.CutSceneWindowSettings?.BgUri,
            legacySettings.CutSceneWindowSettings?.WindowSize,
            null,
            null,
            cancellationToken);
        await MigrateWindowAsync(
            "ScoreSurWindow",
            "BaseCanvas",
            "ScoreSurWindow",
            legacySettings.ScoreWindowSettings?.SurScoreBgImageUri,
            legacySettings.ScoreWindowSettings?.ScoreInGameWindowSize,
            null,
            null,
            cancellationToken);
        await MigrateWindowAsync(
            "ScoreHunWindow",
            "BaseCanvas",
            "ScoreHunWindow",
            legacySettings.ScoreWindowSettings?.HunScoreBgImageUri,
            legacySettings.ScoreWindowSettings?.ScoreInGameWindowSize,
            null,
            null,
            cancellationToken);
        await MigrateWindowAsync(
            "ScoreGlobalWindow",
            "BaseCanvas",
            "ScoreGlobalWindow",
            legacySettings.ScoreWindowSettings?.GlobalScoreBgImageUri,
            legacySettings.ScoreWindowSettings?.ScoreGlobalWindowSize,
            legacySettings.ScoreWindowSettings?.ScoreGlobalWindowBackgroundColor,
            HasLegacyProperty(legacyRoot, "ScoreWindowSettings", "AllowsScoreGlobalWindowTransparency")
                ? legacySettings.ScoreWindowSettings?.AllowsScoreGlobalWindowTransparency
                : null,
            cancellationToken);
        await MigrateWindowAsync(
            "GameDataWindow",
            "BaseCanvas",
            "GameDataWindow",
            legacySettings.GameDataWindowSettings?.BgImageUri,
            legacySettings.GameDataWindowSettings?.WindowSize,
            null,
            null,
            cancellationToken);
        if (!string.IsNullOrWhiteSpace(legacySettings.WidgetsWindowSettings?.MapBpBgUri))
        {
            _logger.LogInformation("Legacy WidgetsWindow/MapBpCanvas was skipped because MapV1 is no longer supported.");
        }

        await MigrateWindowAsync(
            "WidgetsWindow",
            "BpOverViewCanvas",
            "BpOverviewWindow",
            legacySettings.WidgetsWindowSettings?.BpOverviewBgUri,
            legacySettings.WidgetsWindowSettings?.WindowSize,
            legacySettings.WidgetsWindowSettings?.BackgroundColor,
            HasLegacyProperty(legacyRoot, "WidgetsWindowSettings", "AllowsWindowTransparency")
                ? legacySettings.WidgetsWindowSettings?.AllowsWindowTransparency
                : null,
            cancellationToken);
        await MigrateWindowAsync(
            "WidgetsWindow",
            "MapV2Canvas",
            "MapV2Window",
            legacySettings.WidgetsWindowSettings?.MapBpV2BgUri,
            legacySettings.WidgetsWindowSettings?.WindowSize,
            legacySettings.WidgetsWindowSettings?.BackgroundColor,
            HasLegacyProperty(legacyRoot, "WidgetsWindowSettings", "AllowsWindowTransparency")
                ? legacySettings.WidgetsWindowSettings?.AllowsWindowTransparency
                : null,
            cancellationToken);

        if (legacySettings.CutSceneWindowSettings?.IsBlackTalentAndTraitEnable == true)
        {
            _logger.LogWarning("Legacy CutSceneWindowSettings.IsBlackTalentAndTraitEnable has no active Designer v3 runtime setting and was not migrated.");
        }

        if (legacySettings.ScoreWindowSettings?.IsCampIconBlackVerEnabled == true
            || legacySettings.WidgetsWindowSettings?.IsCampIconBlackVerEnabled == true)
        {
            _logger.LogWarning("Legacy camp icon black-version settings have no active Designer v3 runtime setting and were not migrated.");
        }

        async Task MigrateWindowAsync(
            string legacyWindow,
            string legacyCanvas,
            string outputWindow,
            string? backgroundImage,
            WindowSize? windowSize,
            string? backgroundColor,
            bool? allowTransparency,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(backgroundImage)
                && windowSize is null
                && string.IsNullOrWhiteSpace(backgroundColor)
                && allowTransparency is null
                && !LegacyFrontedTextStyleMigrator.HasLegacyTextStyles(legacyWindow, legacySettings))
            {
                return;
            }

            var builtInPath = Path.Combine(builtInRoot, $"{outputWindow}.json");
            if (!File.Exists(builtInPath))
            {
                _logger.LogWarning("Built-in v3 layout missing during legacy migration: {Window}", outputWindow);
                return;
            }

            var windowConfig = JsonSerializer.Deserialize<FrontedWindowConfig>(
                await File.ReadAllTextAsync(builtInPath, ct),
                _jsonSerializerOptions);
            if (windowConfig is null)
            {
                _logger.LogWarning("Built-in v3 layout could not be read during legacy migration: {Window}", outputWindow);
                return;
            }

            var config = FrontedWindowConfigCanvasAdapter.ToCanvasConfig(windowConfig);
            if (!string.IsNullOrWhiteSpace(backgroundImage))
            {
                config.BackgroundImage = backgroundImage;
            }

            LegacyFrontedTextStyleMigrator.Apply(config, legacyWindow, legacyCanvas, legacySettings);
            var targetConfig = FrontedWindowConfigCanvasAdapter.FromCanvasConfig(config);
            targetConfig.WindowSettings = windowConfig.WindowSettings;
            ApplyLegacyWindowSettings(targetConfig.WindowSettings, windowSize, backgroundColor, allowTransparency);
            var targetPath = Path.Combine(layoutRoot, $"{outputWindow}.json");
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            await File.WriteAllTextAsync(targetPath, JsonSerializer.Serialize(targetConfig, _jsonSerializerOptions), ct);
            _logger.LogInformation("Migrated legacy frontend settings to Designer v3 window layout: {Window}", outputWindow);
        }
    }

    private static void ApplyLegacyWindowSettings(
        FrontedWindowSettings target,
        WindowSize? windowSize,
        string? backgroundColor,
        bool? allowTransparency)
    {
        if (windowSize is not null)
        {
            if (double.IsFinite(windowSize.Width) && windowSize.Width > 0D)
            {
                target.WindowWidth = windowSize.Width;
            }

            if (double.IsFinite(windowSize.Height) && windowSize.Height > 0D)
            {
                target.WindowHeight = windowSize.Height;
            }
        }

        if (allowTransparency.HasValue)
        {
            target.AllowsTransparency = allowTransparency.Value;
        }

        if (!string.IsNullOrWhiteSpace(backgroundColor))
        {
            target.BackgroundColor = backgroundColor;
            return;
        }

        if (allowTransparency == true)
        {
            target.BackgroundColor = "#00000000";
        }
        else if (allowTransparency == false)
        {
            target.BackgroundColor = DefaultOpaqueBackgroundColor;
        }
    }

    private static bool HasLegacyProperty(JsonObject? root, string settingsName, string propertyName)
    {
        return root?[settingsName] is JsonObject settings
               && settings.ContainsKey(propertyName);
    }

    private static bool HasLegacyFrontendSettings(LegacySettings settings)
    {
        return settings.BpWindowSettings is not null
               || settings.CutSceneWindowSettings is not null
               || settings.ScoreWindowSettings is not null
               || settings.GameDataWindowSettings is not null
               || settings.WidgetsWindowSettings is not null;
    }

    private static string CleanLegacyFrontendFields(string json)
    {
        var node = JsonNode.Parse(json) as JsonObject;
        if (node is null)
        {
            return json;
        }

        foreach (var key in LegacyFrontendKeys)
        {
            node.Remove(key);
        }

        return node.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }

    private static readonly string[] LegacyFrontendKeys =
    [
        "BpWindowSettings",
        "CutSceneWindowSettings",
        "ScoreWindowSettings",
        "GameDataWindowSettings",
        "WidgetsWindowSettings"
    ];

    private static string CreateBackupPath(string configFilePath)
    {
        var backupPath = configFilePath + ".v2.backup";
        if (!File.Exists(backupPath))
        {
            return backupPath;
        }

        var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
        backupPath = configFilePath + $".v2.{timestamp}.backup";
        if (!File.Exists(backupPath))
        {
            return backupPath;
        }

        for (var index = 1; ; index++)
        {
            backupPath = configFilePath + $".v2.{timestamp}.{index}.backup";
            if (!File.Exists(backupPath))
            {
                return backupPath;
            }
        }
    }
}
