using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Converters;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models;
using System.IO;
using System.Text.Json;

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

            var migratedJson = JsonSerializer.Serialize(settings, _jsonSerializerOptions);
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
