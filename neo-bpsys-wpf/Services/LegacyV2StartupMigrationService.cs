using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Converters;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using System.IO;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 仅在启动时将旧版 v2 <c>Config.json</c> 前台设置迁移到普通 v3 包。
/// </summary>
public sealed class LegacyV2StartupMigrationService : ILegacyV2StartupMigrationService
{
    private const int MigrationSchemaVersion = 2;
    private const string MigrationStateFileName = "migration-state.json";

    private static readonly string[] LegacyFrontendKeys =
    [
        "BpWindowSettings",
        "CutSceneWindowSettings",
        "ScoreWindowSettings",
        "GameDataWindowSettings",
        "WidgetsWindowSettings"
    ];

    private readonly ILegacyV2ConfigDetector _detector;
    private readonly IFrontedLayoutPackageManager _packageManager;
    private readonly FrontedLayoutPackageLegacyConverter _legacyConverter;
    private readonly ILogger<LegacyV2StartupMigrationService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        MaxDepth = FrontedLayoutLimits.MaxJsonDepth,
        Converters = { new FontWeightJsonConverter() }
    };

    /// <summary>
    /// 初始化旧版 v2 启动迁移服务。
    /// </summary>
    /// <param name="detector">旧版配置检测器。</param>
    /// <param name="packageManager">前台布局包管理器。</param>
    /// <param name="legacyConverter">旧版布局转换器。</param>
    /// <param name="logger">日志记录器。</param>
    public LegacyV2StartupMigrationService(
        ILegacyV2ConfigDetector detector,
        IFrontedLayoutPackageManager packageManager,
        FrontedLayoutPackageLegacyConverter legacyConverter,
        ILogger<LegacyV2StartupMigrationService> logger)
    {
        _detector = detector;
        _packageManager = packageManager;
        _legacyConverter = legacyConverter;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<LegacyV2StartupMigrationResult> MigrateIfNeededAsync(
        CancellationToken cancellationToken = default)
    {
        var configFilePath = AppConstants.ConfigFilePath;
        if (!File.Exists(configFilePath))
        {
            return new LegacyV2StartupMigrationResult { Success = true };
        }

        var originalJson = await File.ReadAllTextAsync(configFilePath, cancellationToken);
        if (!_detector.IsLegacyV2Config(originalJson))
        {
            return new LegacyV2StartupMigrationResult { Success = true };
        }

        var originalActiveState = await _packageManager.GetActivePackageStateAsync(cancellationToken);
        var layoutHashes = ComputeLegacyLayoutHashes(AppConstants.AppDataPath);
        var hash = ComputeSourceHash(originalJson, layoutHashes);
        var packageId = $"converted-v2-{hash[..16].ToLowerInvariant()}";
        var packageRoot = Path.Combine(_packageManager.GetPackageRootFolder(), packageId);
        string? backupPath = null;

        try
        {
            backupPath = CreateBackupPath(configFilePath);
            Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
            File.Copy(configFilePath, backupPath, overwrite: false);

            if (HasMatchingMigrationState(packageRoot, ComputeSha256(originalJson), layoutHashes))
            {
                await _packageManager.ActivatePackageAsync(packageId, cancellationToken);
                await WriteMigratedSettingsAsync(configFilePath, originalJson, cancellationToken);
                return new LegacyV2StartupMigrationResult
                {
                    Success = true,
                    Migrated = true,
                    ReusedExistingPackage = true,
                    PackageId = packageId,
                    BackupPath = backupPath
                };
            }

            var convertResult = await _legacyConverter.ConvertLocalAppDataAsync(
                AppConstants.AppDataPath,
                new FrontedLayoutPackageLegacyConvertRequest
                {
                    PackageId = packageId,
                    Name = "Converted v2 layout",
                    Description = "Converted from legacy local frontend layout files during startup migration.",
                    Author = Environment.UserName ?? string.Empty,
                    InstallAfterConvert = true,
                    ActivateAfterInstall = true
                },
                cancellationToken);
            if (!convertResult.Success)
            {
                throw new InvalidOperationException(convertResult.ErrorMessage ?? "Legacy frontend conversion failed.");
            }

            var state = new LegacyV2StartupMigrationState
            {
                SchemaVersion = MigrationSchemaVersion,
                SourceConfigSha256 = ComputeSha256(originalJson),
                LegacyLayoutSha256 = layoutHashes,
                PackageId = packageId,
                BackupPath = backupPath,
                MigratedAt = DateTimeOffset.UtcNow
            };
            await File.WriteAllTextAsync(
                Path.Combine(packageRoot, MigrationStateFileName),
                JsonSerializer.Serialize(state, _jsonOptions),
                cancellationToken);
            await WriteMigratedSettingsAsync(configFilePath, originalJson, cancellationToken);

            _logger.LogInformation(
                "Legacy v2 frontend settings migrated to Designer v3 package {PackageId}. Backup: {BackupPath}",
                packageId,
                backupPath);

            return new LegacyV2StartupMigrationResult
            {
                Success = true,
                Migrated = true,
                PackageId = packageId,
                BackupPath = backupPath
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Legacy v2 startup migration failed.");
            if (!string.IsNullOrWhiteSpace(backupPath) && File.Exists(backupPath))
            {
                File.Copy(backupPath, configFilePath, overwrite: true);
            }

            await RestoreActivePackageAsync(originalActiveState.PackageId, cancellationToken);
            return new LegacyV2StartupMigrationResult
            {
                Success = false,
                Migrated = false,
                PackageId = packageId,
                BackupPath = backupPath,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task WriteMigratedSettingsAsync(
        string configFilePath,
        string originalJson,
        CancellationToken cancellationToken)
    {
        var settings = JsonSerializer.Deserialize<Settings>(originalJson, _jsonOptions)
                       ?? new Settings();
        settings.Version = SettingsConfigVersionHelper.CurrentSettingsVersion;

        var node = JsonSerializer.SerializeToNode(settings, _jsonOptions) as JsonObject
                   ?? [];
        foreach (var key in LegacyFrontendKeys)
        {
            node.Remove(key);
        }

        var migratedJson = node.ToJsonString(_jsonOptions);
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData).Replace(@"\", @"\\");
        migratedJson = migratedJson.Replace(appDataPath, "%APPDATA%");
        await File.WriteAllTextAsync(configFilePath, migratedJson, cancellationToken);
    }

    private static bool HasMatchingMigrationState(
        string packageRoot,
        string configHash,
        IReadOnlyDictionary<string, string> layoutHashes)
    {
        var statePath = Path.Combine(packageRoot, MigrationStateFileName);
        if (!File.Exists(statePath))
        {
            return false;
        }

        try
        {
            var state = JsonSerializer.Deserialize<LegacyV2StartupMigrationState>(
                File.ReadAllText(statePath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return state is
            {
                SchemaVersion: MigrationSchemaVersion
            } && string.Equals(state.SourceConfigSha256, configHash, StringComparison.OrdinalIgnoreCase)
              && DictionariesEqual(state.LegacyLayoutSha256, layoutHashes);
        }
        catch
        {
            return false;
        }
    }

    private async Task RestoreActivePackageAsync(string packageId, CancellationToken cancellationToken)
    {
        try
        {
            if (string.Equals(packageId, FrontedLayoutPackageManager.BuiltInPackageId, StringComparison.OrdinalIgnoreCase))
            {
                await _packageManager.ActivatePackageAsync(FrontedLayoutPackageManager.BuiltInPackageId, cancellationToken);
                return;
            }

            await _packageManager.ActivatePackageAsync(packageId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to restore previous active package after migration failure.");
            await _packageManager.ActivatePackageAsync(FrontedLayoutPackageManager.BuiltInPackageId, cancellationToken);
        }
    }

    private static string ComputeSha256(string text)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static Dictionary<string, string> ComputeLegacyLayoutHashes(string appDataRoot)
    {
        if (!Directory.Exists(appDataRoot))
        {
            return [];
        }

        return Directory.EnumerateFiles(appDataRoot, "*Config-*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                path => Path.GetFileName(path)
                        ?? throw new InvalidDataException($"Legacy layout path has no file name: {path}"),
                path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))),
                StringComparer.OrdinalIgnoreCase);
    }

    private static string ComputeSourceHash(string configJson, IReadOnlyDictionary<string, string> layoutHashes)
    {
        var builder = new System.Text.StringBuilder(configJson);
        foreach (var item in layoutHashes.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append('\n').Append(item.Key).Append('=').Append(item.Value);
        }

        builder.Append('\n').Append(MigrationSchemaVersion);
        return ComputeSha256(builder.ToString());
    }

    private static bool DictionariesEqual(
        IReadOnlyDictionary<string, string>? left,
        IReadOnlyDictionary<string, string> right)
    {
        return left is not null
               && left.Count == right.Count
               && right.All(item => left.TryGetValue(item.Key, out var value)
                                    && string.Equals(value, item.Value, StringComparison.OrdinalIgnoreCase));
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

    private sealed class LegacyV2StartupMigrationState
    {
        public int SchemaVersion { get; set; }

        public string SourceConfigSha256 { get; set; } = string.Empty;

        public Dictionary<string, string> LegacyLayoutSha256 { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public string PackageId { get; set; } = string.Empty;

        public string BackupPath { get; set; } = string.Empty;

        public DateTimeOffset MigratedAt { get; set; }
    }
}
