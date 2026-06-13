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
/// Startup-only migration from legacy v2 <c>Config.json</c> frontend settings to a normal v3 package.
/// </summary>
public sealed class LegacyV2StartupMigrationService : ILegacyV2StartupMigrationService
{
    private const int MigrationSchemaVersion = 1;
    private const string ManifestFileName = "manifest.json";
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
    /// Initializes a legacy v2 startup migration service.
    /// </summary>
    /// <param name="detector">Legacy config detector.</param>
    /// <param name="packageManager">Fronted layout package manager.</param>
    /// <param name="legacyConverter">Legacy layout converter.</param>
    /// <param name="logger">Logger.</param>
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
        var hash = ComputeSha256(originalJson);
        var packageId = $"converted-v2-{hash[..16].ToLowerInvariant()}";
        var packageRoot = Path.Combine(_packageManager.GetPackageRootFolder(), packageId);
        string? backupPath = null;

        try
        {
            backupPath = CreateBackupPath(configFilePath);
            Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
            File.Copy(configFilePath, backupPath, overwrite: false);

            if (HasMatchingMigrationState(packageRoot, hash))
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

            var stagingRoot = Path.Combine(
                AppConstants.AppTempPath,
                "legacy-v2-startup-migration",
                $"{packageId}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(stagingRoot);
            try
            {
                await WritePackageAsync(stagingRoot, packageId, hash, backupPath, originalJson, cancellationToken);
                ValidatePackage(stagingRoot);

                Directory.CreateDirectory(_packageManager.GetPackageRootFolder());
                if (Directory.Exists(packageRoot))
                {
                    Directory.Delete(packageRoot, recursive: true);
                }

                Directory.Move(stagingRoot, packageRoot);
            }
            finally
            {
                TryDeleteDirectory(stagingRoot);
            }

            await _packageManager.ActivatePackageAsync(packageId, cancellationToken);
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

    private async Task WritePackageAsync(
        string packageRoot,
        string packageId,
        string hash,
        string backupPath,
        string originalJson,
        CancellationToken cancellationToken)
    {
        var configs = _legacyConverter.ConvertLegacyStartupConfigJson(originalJson);
        var manifest = new FrontedLayoutPackageManifest
        {
            PackageId = packageId,
            Name = "Converted v2 layout",
            Description = "Converted from legacy Config.json during startup migration.",
            Author = Environment.UserName ?? string.Empty,
            CreatedAt = DateTimeOffset.UtcNow,
            Format = "neo-bpsys-bpui",
            FormatVersion = 3,
            LayoutSchemaVersion = 3
        };

        foreach (var (window, config) in configs.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            var relativePath = Path.Combine("FrontedLayouts", $"{window}.json");
            var path = Path.Combine(packageRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(config, _jsonOptions), cancellationToken);
            manifest.Content.Layouts.Add(new FrontedLayoutPackageLayoutEntry
            {
                Window = window,
                Path = relativePath.Replace('\\', '/')
            });
        }

        await CopyBuiltInBehaviorDocumentsAsync(packageRoot, configs.Keys, cancellationToken);
        var state = new LegacyV2StartupMigrationState
        {
            SchemaVersion = MigrationSchemaVersion,
            SourceConfigSha256 = hash,
            PackageId = packageId,
            BackupPath = backupPath,
            MigratedAt = DateTimeOffset.UtcNow
        };

        await File.WriteAllTextAsync(
            Path.Combine(packageRoot, MigrationStateFileName),
            JsonSerializer.Serialize(state, _jsonOptions),
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(packageRoot, ManifestFileName),
            JsonSerializer.Serialize(manifest, _jsonOptions),
            cancellationToken);
    }

    private async Task CopyBuiltInBehaviorDocumentsAsync(
        string packageRoot,
        IEnumerable<string> windows,
        CancellationToken cancellationToken)
    {
        var builtInLayoutsRoot = _packageManager.GetPackageLayoutsRootFolder(FrontedLayoutPackageManager.BuiltInPackageId);
        var resourcesRoot = Path.GetDirectoryName(Path.GetFullPath(builtInLayoutsRoot));
        if (resourcesRoot is null)
        {
            return;
        }

        foreach (var window in windows)
        {
            var source = Path.Combine(resourcesRoot, "FrontedBehaviors", $"{window}.behaviors.json");
            if (!File.Exists(source))
            {
                continue;
            }

            var target = Path.Combine(packageRoot, "FrontedBehaviors", $"{window}.behaviors.json");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await using var sourceStream = File.OpenRead(source);
            await using var targetStream = File.Create(target);
            await sourceStream.CopyToAsync(targetStream, cancellationToken);
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

    private static void ValidatePackage(string packageRoot)
    {
        if (!File.Exists(Path.Combine(packageRoot, ManifestFileName)))
        {
            throw new FileNotFoundException("Converted package manifest is missing.");
        }

        if (!Directory.Exists(Path.Combine(packageRoot, "FrontedLayouts"))
            || !Directory.EnumerateFiles(Path.Combine(packageRoot, "FrontedLayouts"), "*.json").Any())
        {
            throw new InvalidDataException("Converted package contains no layouts.");
        }
    }

    private static bool HasMatchingMigrationState(string packageRoot, string hash)
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
            } && string.Equals(state.SourceConfigSha256, hash, StringComparison.OrdinalIgnoreCase);
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

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup.
        }
    }

    private sealed class LegacyV2StartupMigrationState
    {
        public int SchemaVersion { get; set; }

        public string SourceConfigSha256 { get; set; } = string.Empty;

        public string PackageId { get; set; } = string.Empty;

        public string BackupPath { get; set; } = string.Empty;

        public DateTimeOffset MigratedAt { get; set; }
    }
}
