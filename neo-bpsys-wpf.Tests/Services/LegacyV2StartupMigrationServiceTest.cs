#nullable enable

using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Services;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

public sealed class LegacyV2StartupMigrationServiceTest
{
    [Fact]
    public async Task LegacyConfigCreatesNormalPackageAndActivatesIt()
    {
        var fixture = await LegacyMigrationFixture.CreateAsync();
        try
        {
            await fixture.WriteLegacyConfigAsync();
            var service = fixture.CreateService();

            var result = await service.MigrateIfNeededAsync(TestContext.Current.CancellationToken);

            Assert.True(result.Success);
            Assert.True(result.Migrated);
            Assert.False(result.ReusedExistingPackage);
            Assert.StartsWith("converted-v2-", result.PackageId, StringComparison.Ordinal);
            Assert.True(File.Exists(result.BackupPath));

            var packageRoot = Path.Combine(fixture.PackageRoot, result.PackageId!);
            Assert.True(File.Exists(Path.Combine(packageRoot, "manifest.json")));
            Assert.True(File.Exists(Path.Combine(packageRoot, "migration-state.json")));
            Assert.True(File.Exists(Path.Combine(packageRoot, "FrontedLayouts", "BpWindow.json")));
            Assert.True(Directory.Exists(Path.Combine(packageRoot, "FrontedLayouts")));

            var activeState = await fixture.PackageManager.GetActivePackageStateAsync(TestContext.Current.CancellationToken);
            Assert.Equal(result.PackageId, activeState.PackageId);

            var migrated = JsonNode.Parse(await File.ReadAllTextAsync(AppConstants.ConfigFilePath))!.AsObject();
            Assert.Equal(SettingsConfigVersionHelper.CurrentSettingsVersion, migrated["Version"]!.GetValue<int>());
            Assert.False(migrated.ContainsKey("BpWindowSettings"));
            Assert.Empty(fixture.GetNewLooseLayoutFiles());
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public async Task SameLegacyHashReusesExistingPackage()
    {
        var fixture = await LegacyMigrationFixture.CreateAsync();
        try
        {
            var legacyJson = await fixture.WriteLegacyConfigAsync();
            var service = fixture.CreateService();
            var first = await service.MigrateIfNeededAsync(TestContext.Current.CancellationToken);

            await fixture.WriteConfigAsync(legacyJson);
            var second = await service.MigrateIfNeededAsync(TestContext.Current.CancellationToken);

            Assert.True(second.Success);
            Assert.True(second.ReusedExistingPackage);
            Assert.Equal(first.PackageId, second.PackageId);
            Assert.Single(Directory.EnumerateDirectories(fixture.PackageRoot, "converted-v2-*"));
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public async Task BackupIsCreatedBeforeWritingMigratedSettings()
    {
        var fixture = await LegacyMigrationFixture.CreateAsync();
        try
        {
            var legacyJson = await fixture.WriteLegacyConfigAsync();
            var result = await fixture.CreateService().MigrateIfNeededAsync(TestContext.Current.CancellationToken);

            Assert.True(File.Exists(result.BackupPath));
            Assert.Equal(legacyJson, await File.ReadAllTextAsync(result.BackupPath!));
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public async Task FailurePreservesOriginalConfigAndDoesNotActivatePartialPackage()
    {
        var fixture = await LegacyMigrationFixture.CreateAsync();
        try
        {
            var legacyJson = await fixture.WriteLegacyConfigAsync();
            var packageId = "converted-v2-" + ComputeHash(legacyJson)[..16].ToLowerInvariant();
            Directory.CreateDirectory(fixture.PackageRoot);
            await File.WriteAllTextAsync(Path.Combine(fixture.PackageRoot, packageId), "blocking-file");

            var result = await fixture.CreateService().MigrateIfNeededAsync(TestContext.Current.CancellationToken);

            Assert.False(result.Success);
            Assert.Equal(legacyJson, await File.ReadAllTextAsync(AppConstants.ConfigFilePath));
            var activeState = await fixture.PackageManager.GetActivePackageStateAsync(TestContext.Current.CancellationToken);
            Assert.Equal(FrontedLayoutPackageManager.BuiltInPackageId, activeState.PackageId);
            Assert.False(Directory.Exists(Path.Combine(fixture.PackageRoot, packageId, "FrontedLayouts")));
        }
        finally
        {
            fixture.Dispose();
        }
    }

    private static string ComputeHash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private sealed class LegacyMigrationFixture : IDisposable
    {
        private readonly string? _originalConfig;
        private readonly bool _hadOriginalConfig;
        private readonly string[] _originalLooseLayoutFiles;
        private readonly string[] _originalBackupFiles;

        private LegacyMigrationFixture(
            string packageRoot,
            FrontedLayoutPackageManager packageManager,
            string? originalConfig,
            bool hadOriginalConfig,
            string[] originalLooseLayoutFiles,
            string[] originalBackupFiles)
        {
            PackageRoot = packageRoot;
            PackageManager = packageManager;
            _originalConfig = originalConfig;
            _hadOriginalConfig = hadOriginalConfig;
            _originalLooseLayoutFiles = originalLooseLayoutFiles;
            _originalBackupFiles = originalBackupFiles;
        }

        internal string PackageRoot { get; }

        internal FrontedLayoutPackageManager PackageManager { get; }

        internal static async Task<LegacyMigrationFixture> CreateAsync()
        {
            var configPath = AppConstants.ConfigFilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            var hadOriginalConfig = File.Exists(configPath);
            var originalConfig = hadOriginalConfig ? await File.ReadAllTextAsync(configPath) : null;
            var originalLooseLayoutFiles = Directory.Exists(AppConstants.FrontedLayoutsPath)
                ? Directory.EnumerateFiles(AppConstants.FrontedLayoutsPath, "*", SearchOption.AllDirectories)
                    .Select(Path.GetFullPath)
                    .ToArray()
                : [];
            var originalBackupFiles = Directory.EnumerateFiles(
                    Path.GetDirectoryName(configPath)!,
                    "Config.json.v2*.backup")
                .Select(Path.GetFullPath)
                .ToArray();

            var packageRoot = Path.Combine(Path.GetTempPath(), "neo-bpsys-v2-migration-test-" + Guid.NewGuid().ToString("N"));
            var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
            var builtInLayoutsRoot = Path.Combine(repositoryRoot, "neo-bpsys-wpf", "Resources", "FrontedLayouts");
            var packageManager = new FrontedLayoutPackageManager(
                packageRoot,
                builtInLayoutsRoot,
                AppConstants.FrontedLayoutsPath,
                NullLogger<FrontedLayoutPackageManager>.Instance);

            return new LegacyMigrationFixture(
                packageRoot,
                packageManager,
                originalConfig,
                hadOriginalConfig,
                originalLooseLayoutFiles,
                originalBackupFiles);
        }

        internal LegacyV2StartupMigrationService CreateService()
        {
            var converter = new FrontedLayoutPackageLegacyConverter(
                Path.Combine(AppContext.BaseDirectory, "Resources", "FrontedLayouts"),
                Path.Combine(Path.GetTempPath(), "neo-bpsys-v2-converter-test-" + Guid.NewGuid().ToString("N")));

            return new LegacyV2StartupMigrationService(
                new LegacyV2ConfigDetector(),
                PackageManager,
                converter,
                NullLogger<LegacyV2StartupMigrationService>.Instance);
        }

        internal async Task<string> WriteLegacyConfigAsync()
        {
            var legacyJson = JsonSerializer.Serialize(new
            {
                BpWindowSettings = new
                {
                    WindowSize = new { Width = 1280, Height = 720 },
                    BgImageUri = "legacy-bp.png"
                },
                WidgetsWindowSettings = new
                {
                    BpOverviewBgUri = "legacy-overview.png",
                    MapBpV2BgUri = "legacy-map.png"
                },
                ScoreWindowSettings = new
                {
                    ScoreGlobalWindowSize = new { Width = 900, Height = 120 }
                }
            });
            await WriteConfigAsync(legacyJson);
            return legacyJson;
        }

        internal async Task WriteConfigAsync(string json)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AppConstants.ConfigFilePath)!);
            await File.WriteAllTextAsync(AppConstants.ConfigFilePath, json);
        }

        internal string[] GetNewLooseLayoutFiles()
        {
            if (!Directory.Exists(AppConstants.FrontedLayoutsPath))
            {
                return [];
            }

            var original = _originalLooseLayoutFiles.ToHashSet(StringComparer.OrdinalIgnoreCase);
            return Directory.EnumerateFiles(AppConstants.FrontedLayoutsPath, "*", SearchOption.AllDirectories)
                .Select(Path.GetFullPath)
                .Where(path => !original.Contains(path))
                .ToArray();
        }

        public void Dispose()
        {
            if (_hadOriginalConfig)
            {
                File.WriteAllText(AppConstants.ConfigFilePath, _originalConfig!);
            }
            else if (File.Exists(AppConstants.ConfigFilePath))
            {
                File.Delete(AppConstants.ConfigFilePath);
            }

            var originalBackups = _originalBackupFiles.ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var backup in Directory.EnumerateFiles(
                             Path.GetDirectoryName(AppConstants.ConfigFilePath)!,
                             "Config.json.v2*.backup")
                         .Select(Path.GetFullPath)
                         .Where(path => !originalBackups.Contains(path)))
            {
                File.Delete(backup);
            }

            if (Directory.Exists(PackageRoot))
            {
                Directory.Delete(PackageRoot, recursive: true);
            }
        }
    }
}
