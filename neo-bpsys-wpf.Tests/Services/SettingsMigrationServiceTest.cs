using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Services;
using System;
using System.IO;
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
}
