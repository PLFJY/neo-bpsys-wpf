using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Converters;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
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
            await MigrateLegacyFrontendSettingsAsync(json, cancellationToken);

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
        await MigrateCanvasAsync("BpWindow", "BaseCanvas", legacySettings.BpWindowSettings?.BgImageUri, cancellationToken);
        await MigrateCanvasAsync("CutSceneWindow", "BaseCanvas", legacySettings.CutSceneWindowSettings?.BgUri, cancellationToken);
        await MigrateCanvasAsync("ScoreSurWindow", "BaseCanvas", legacySettings.ScoreWindowSettings?.SurScoreBgImageUri, cancellationToken);
        await MigrateCanvasAsync("ScoreHunWindow", "BaseCanvas", legacySettings.ScoreWindowSettings?.HunScoreBgImageUri, cancellationToken);
        await MigrateCanvasAsync("ScoreGlobalWindow", "BaseCanvas", legacySettings.ScoreWindowSettings?.GlobalScoreBgImageUri, cancellationToken);
        await MigrateCanvasAsync("GameDataWindow", "BaseCanvas", legacySettings.GameDataWindowSettings?.BgImageUri, cancellationToken);
        await MigrateCanvasAsync("WidgetsWindow", "MapBpCanvas", legacySettings.WidgetsWindowSettings?.MapBpBgUri, cancellationToken);
        await MigrateCanvasAsync("WidgetsWindow", "BpOverViewCanvas", legacySettings.WidgetsWindowSettings?.BpOverviewBgUri, cancellationToken);
        await MigrateCanvasAsync("WidgetsWindow", "MapV2Canvas", legacySettings.WidgetsWindowSettings?.MapBpV2BgUri, cancellationToken);

        var optionsService = new FrontedWindowLayoutOptionsService(layoutRoot);
        await SaveWindowOptionsAsync(optionsService, "BpWindow", legacySettings.BpWindowSettings?.AllowsWindowTransparency == true, cancellationToken);
        await SaveWindowOptionsAsync(optionsService, "ScoreGlobalWindow", legacySettings.ScoreWindowSettings?.AllowsScoreGlobalWindowTransparency == true, cancellationToken);
        await SaveWindowOptionsAsync(optionsService, "WidgetsWindow", legacySettings.WidgetsWindowSettings?.AllowsWindowTransparency == true, cancellationToken);

        if (legacySettings.CutSceneWindowSettings?.IsBlackTalentAndTraitEnable == true)
        {
            _logger.LogWarning("Legacy CutSceneWindowSettings.IsBlackTalentAndTraitEnable has no active Designer v3 runtime setting and was not migrated.");
        }

        if (legacySettings.ScoreWindowSettings?.IsCampIconBlackVerEnabled == true
            || legacySettings.WidgetsWindowSettings?.IsCampIconBlackVerEnabled == true)
        {
            _logger.LogWarning("Legacy camp icon black-version settings have no active Designer v3 runtime setting and were not migrated.");
        }

        async Task MigrateCanvasAsync(string window, string canvas, string? backgroundImage, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(backgroundImage) && !HasLegacyTextStyles(window, legacySettings))
            {
                return;
            }

            var builtInPath = Path.Combine(builtInRoot, window, $"{canvas}.json");
            if (!File.Exists(builtInPath))
            {
                _logger.LogWarning("Built-in v3 layout missing during legacy migration: {Window}/{Canvas}", window, canvas);
                return;
            }

            var config = JsonSerializer.Deserialize<FrontedCanvasConfig>(
                await File.ReadAllTextAsync(builtInPath, ct),
                _jsonSerializerOptions);
            if (config is null)
            {
                _logger.LogWarning("Built-in v3 layout could not be read during legacy migration: {Window}/{Canvas}", window, canvas);
                return;
            }

            if (!string.IsNullOrWhiteSpace(backgroundImage))
            {
                config.BackgroundImage = backgroundImage;
            }

            ApplyLegacyTextStyles(config, window, legacySettings);
            var targetPath = Path.Combine(layoutRoot, window, $"{canvas}.json");
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            await File.WriteAllTextAsync(targetPath, JsonSerializer.Serialize(config, _jsonSerializerOptions), ct);
            _logger.LogInformation("Migrated legacy frontend background to Designer v3 layout: {Window}/{Canvas}", window, canvas);
        }
    }

    private static bool HasLegacyTextStyles(string window, LegacySettings legacySettings)
    {
        return window switch
        {
            "BpWindow" => legacySettings.BpWindowSettings?.TextSettings is not null,
            "CutSceneWindow" => legacySettings.CutSceneWindowSettings?.TextSettings is not null,
            "ScoreSurWindow" or "ScoreHunWindow" or "ScoreGlobalWindow" => legacySettings.ScoreWindowSettings?.TextSettings is not null,
            "GameDataWindow" => legacySettings.GameDataWindowSettings?.TextSettings is not null,
            "WidgetsWindow" => legacySettings.WidgetsWindowSettings?.TextSettings is not null,
            _ => false
        };
    }

    private static void ApplyLegacyTextStyles(FrontedCanvasConfig config, string window, LegacySettings legacySettings)
    {
        switch (window)
        {
            case "BpWindow":
                ApplyStyleByNames(config, legacySettings.BpWindowSettings?.TextSettings?.Timer, "Timer");
                ApplyStyleByNames(config, legacySettings.BpWindowSettings?.TextSettings?.TeamName, "TeamName");
                ApplyStyleByNames(config, legacySettings.BpWindowSettings?.TextSettings?.GameScores, "GameScores");
                ApplyStyleByNames(config, legacySettings.BpWindowSettings?.TextSettings?.MajorPoints, "MajorPoints");
                ApplyStyleByContains(config, legacySettings.BpWindowSettings?.TextSettings?.PlayerId, "PlayerName", "PlayerId");
                ApplyStyleByNames(config, legacySettings.BpWindowSettings?.TextSettings?.MapName, "MapName");
                ApplyStyleByNames(config, legacySettings.BpWindowSettings?.TextSettings?.GameProgress, "GameProgress");
                break;
            case "CutSceneWindow":
                ApplyStyleByNames(config, legacySettings.CutSceneWindowSettings?.TextSettings?.TeamName, "TeamName");
                ApplyStyleByNames(config, legacySettings.CutSceneWindowSettings?.TextSettings?.MajorPoints, "MajorPoints");
                ApplyStyleByContains(config, legacySettings.CutSceneWindowSettings?.TextSettings?.SurPlayerId, "SurPlayerName", "SurPlayerId");
                ApplyStyleByContains(config, legacySettings.CutSceneWindowSettings?.TextSettings?.HunPlayerId, "HunPlayerName", "HunPlayerId");
                ApplyStyleByNames(config, legacySettings.CutSceneWindowSettings?.TextSettings?.MapName, "MapName");
                ApplyStyleByNames(config, legacySettings.CutSceneWindowSettings?.TextSettings?.GameProgress, "GameProgress");
                break;
            case "ScoreSurWindow":
            case "ScoreHunWindow":
                ApplyStyleByNames(config, legacySettings.ScoreWindowSettings?.TextSettings?.TeamName, "TeamName");
                ApplyStyleByNames(config, legacySettings.ScoreWindowSettings?.TextSettings?.GameScores, "GameScores");
                ApplyStyleByNames(config, legacySettings.ScoreWindowSettings?.TextSettings?.MajorPoints, "MajorPoints");
                break;
            case "ScoreGlobalWindow":
                ApplyStyleByContains(config, legacySettings.ScoreWindowSettings?.TextSettings?.ScoreGlobal_TeamName, "TeamName");
                ApplyStyleByContains(config, legacySettings.ScoreWindowSettings?.TextSettings?.ScoreGlobal_Data, "ScoreData", "GlobalScore");
                ApplyStyleByContains(config, legacySettings.ScoreWindowSettings?.TextSettings?.ScoreGlobal_Total, "Total");
                break;
            case "GameDataWindow":
                ApplyStyleByNames(config, legacySettings.GameDataWindowSettings?.TextSettings?.TeamName, "TeamName");
                ApplyStyleByNames(config, legacySettings.GameDataWindowSettings?.TextSettings?.GameScores, "GameScores");
                ApplyStyleByNames(config, legacySettings.GameDataWindowSettings?.TextSettings?.MajorPoints, "MajorPoints");
                ApplyStyleByContains(config, legacySettings.GameDataWindowSettings?.TextSettings?.PlayerId, "PlayerName", "PlayerId");
                ApplyStyleByNames(config, legacySettings.GameDataWindowSettings?.TextSettings?.MapName, "MapName");
                ApplyStyleByNames(config, legacySettings.GameDataWindowSettings?.TextSettings?.GameProgress, "GameProgress");
                ApplyStyleByContains(config, legacySettings.GameDataWindowSettings?.TextSettings?.SurDataHeader, "SurDataHeader");
                ApplyStyleByContains(config, legacySettings.GameDataWindowSettings?.TextSettings?.HunDataHeader, "HunDataHeader");
                ApplyStyleByContains(config, legacySettings.GameDataWindowSettings?.TextSettings?.SurData, "SurData");
                ApplyStyleByContains(config, legacySettings.GameDataWindowSettings?.TextSettings?.HunData, "HunData");
                break;
            case "WidgetsWindow":
                ApplyStyleByContains(config, legacySettings.WidgetsWindowSettings?.TextSettings?.MapBp_MapName, "MapName");
                ApplyStyleByContains(config, legacySettings.WidgetsWindowSettings?.TextSettings?.MapBp_TeamName, "TeamName");
                ApplyStyleByContains(config, legacySettings.WidgetsWindowSettings?.TextSettings?.BpOverview_TeamName, "TeamNameInOverview");
                ApplyStyleByNames(config, legacySettings.WidgetsWindowSettings?.TextSettings?.BpOverview_GameProgress, "GameProgress");
                ApplyStyleByNames(config, legacySettings.WidgetsWindowSettings?.TextSettings?.BpOverview_GameScores, "GameScores");
                break;
        }
    }

    private static void ApplyStyleByNames(
        FrontedCanvasConfig config,
        LegacyTextSettings? style,
        params string[] suffixes)
    {
        if (style is null)
        {
            return;
        }

        foreach (var (name, control) in config.Controls)
        {
            if (suffixes.Any(suffix => name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)))
            {
                ApplyTextStyle(control, style);
            }
        }
    }

    private static void ApplyStyleByContains(
        FrontedCanvasConfig config,
        LegacyTextSettings? style,
        params string[] fragments)
    {
        if (style is null)
        {
            return;
        }

        foreach (var (name, control) in config.Controls)
        {
            if (fragments.Any(fragment => name.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
            {
                ApplyTextStyle(control, style);
            }
        }
    }

    private static void ApplyTextStyle(FrontedControlConfigBase control, LegacyTextSettings style)
    {
        switch (control)
        {
            case TextFrontedControlConfig text:
                Apply(style, text);
                break;
            case LocalizedTextControlConfig localizedText:
                Apply(style, localizedText);
                break;
            case MapNameTextControlConfig mapName:
                Apply(style, mapName);
                break;
            case GameProgressTextControlConfig progress:
                Apply(style, progress);
                break;
        }
    }

    private static void Apply(LegacyTextSettings style, dynamic target)
    {
        if (!string.IsNullOrWhiteSpace(style.Color))
        {
            target.Color = style.Color;
        }

        if (!string.IsNullOrWhiteSpace(style.FontFamilySite))
        {
            target.FontFamily = style.FontFamilySite;
        }

        if (style.FontSize > 0)
        {
            target.FontSize = style.FontSize;
        }

        target.FontWeight = style.FontWeight.ToString();
    }

    private static async Task SaveWindowOptionsAsync(
        FrontedWindowLayoutOptionsService optionsService,
        string window,
        bool allowTransparency,
        CancellationToken cancellationToken)
    {
        if (!allowTransparency)
        {
            return;
        }

        await optionsService.SaveOptionsAsync(
            window,
            new FrontedWindowLayoutOptions { AllowTransparency = true },
            cancellationToken);
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
