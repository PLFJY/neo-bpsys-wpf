using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Core.Models.Legacy;
using neo_bpsys_wpf.Services;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

public class SettingsMigrationServiceTest
{
    [Fact]
    public void MissingVersionIsLegacy()
    {
        var info = SettingsConfigVersionHelper.InspectJson("""{ "GhProxyMirror": "https://example.test/" }""");

        Assert.True(info.IsLegacy);
        Assert.False(info.HasVersion);
    }

    [Fact]
    public void NullVersionIsLegacy()
    {
        var info = SettingsConfigVersionHelper.InspectJson("""{ "Version": null }""");

        Assert.True(info.IsLegacy);
        Assert.True(info.HasVersion);
        Assert.True(info.IsNullVersion);
    }

    [Fact]
    public void Version3IsNotLegacy()
    {
        var info = SettingsConfigVersionHelper.InspectJson("""{ "Version": 3 }""");

        Assert.False(info.IsLegacy);
        Assert.Equal(3, info.Version);
    }

    [Fact]
    public async Task MigrationWritesVersion3AndCreatesBackup()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tempDirectory = Path.Combine(Path.GetTempPath(), "neo-bpsys-wpf-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var configPath = Path.Combine(tempDirectory, "Config.json");
            const string originalJson = """{ "GhProxyMirror": "https://example.test/" }""";
            await File.WriteAllTextAsync(configPath, originalJson, cancellationToken);

            var service = new SettingsMigrationService(NullLogger<SettingsMigrationService>.Instance);
            var result = await service.MigrateLegacyConfigToV3Async(configPath, cancellationToken);

            Assert.True(result.Success);
            Assert.True(result.Migrated);
            Assert.NotNull(result.BackupPath);
            Assert.True(File.Exists(result.BackupPath));
            Assert.Equal(originalJson, await File.ReadAllTextAsync(result.BackupPath, cancellationToken));

            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(configPath, cancellationToken));
            var root = document.RootElement;
            Assert.Equal(3, root.GetProperty("Version").GetInt32());
            Assert.Equal("https://example.test/", root.GetProperty("GhProxyMirror").GetString());
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, true);
            }
        }
    }

    [Fact]
    public async Task MigrationRemovesLegacyFrontendFieldsFromSavedSettings()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tempDirectory = Path.Combine(Path.GetTempPath(), "neo-bpsys-wpf-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var configPath = Path.Combine(tempDirectory, "Config.json");
            await File.WriteAllTextAsync(
                configPath,
                """
                {
                  "GhProxyMirror": "https://example.test/",
                  "BpWindowSettings": {},
                  "CutSceneWindowSettings": {},
                  "ScoreWindowSettings": {},
                  "GameDataWindowSettings": {},
                  "WidgetsWindowSettings": {}
                }
                """,
                cancellationToken);

            var service = new SettingsMigrationService(NullLogger<SettingsMigrationService>.Instance);
            var result = await service.MigrateLegacyConfigToV3Async(configPath, cancellationToken);

            Assert.True(result.Success);
            var migratedJson = await File.ReadAllTextAsync(configPath, cancellationToken);
            Assert.DoesNotContain("BpWindowSettings", migratedJson, StringComparison.Ordinal);
            Assert.DoesNotContain("CutSceneWindowSettings", migratedJson, StringComparison.Ordinal);
            Assert.DoesNotContain("ScoreWindowSettings", migratedJson, StringComparison.Ordinal);
            Assert.DoesNotContain("GameDataWindowSettings", migratedJson, StringComparison.Ordinal);
            Assert.DoesNotContain("WidgetsWindowSettings", migratedJson, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, true);
            }
        }
    }

    [Fact]
    public void ActiveSettingsDoesNotExposeLegacyFrontendProperties()
    {
        var properties = typeof(Settings).GetProperties().Select(property => property.Name).ToArray();

        Assert.DoesNotContain("BpWindowSettings", properties);
        Assert.DoesNotContain("CutSceneWindowSettings", properties);
        Assert.DoesNotContain("ScoreWindowSettings", properties);
        Assert.DoesNotContain("GameDataWindowSettings", properties);
        Assert.DoesNotContain("WidgetsWindowSettings", properties);
    }

    [Fact]
    public void LegacySettingsDeserializesOldFrontendTextSettings()
    {
        var legacy = JsonSerializer.Deserialize<LegacySettings>(
            """
            {
              "BpWindowSettings": {
                "TextSettings": {
                  "Timer": {
                    "Color": "#FF112233",
                    "FontFamilySite": "Arial",
                    "FontWeight": "Bold",
                    "FontSize": 58
                  }
                }
              }
            }
            """,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new neo_bpsys_wpf.Converters.FontWeightJsonConverter() }
            });

        var timer = legacy?.BpWindowSettings?.TextSettings?.Timer;
        Assert.NotNull(timer);
    }
}
