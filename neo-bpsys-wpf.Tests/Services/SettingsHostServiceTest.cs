using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Services;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>验证设置重置的持久化、订阅和退役接口边界。</summary>
public class SettingsHostServiceTest
{
    /// <summary>重置恢复默认值，旧接口不写文件，旧设置不再发布事件。</summary>
    /// <returns>验证完成的任务。</returns>
    [Fact]
    public async Task ResetPersistsDefaultsAndReplacesSettingsWithoutMigratingOrChangingLayouts()
    {
        var root = Path.Combine(Path.GetTempPath(), "settings-reset-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var configPath = Path.Combine(root, "Config.json");
            var layoutPath = Path.Combine(root, "layout.json");
            await File.WriteAllTextAsync(layoutPath, "unchanged");
            var migration = new Mock<ILegacyV2StartupMigrationService>(MockBehavior.Strict);
            var service = new SettingsHostService(NullLogger<SettingsHostService>.Instance, migration.Object, configPath);
            await File.WriteAllTextAsync(configPath, "{\"Version\":null}");
#pragma warning disable CS0618 // Verify the intentionally retained compatibility contract.
            var retired = new SettingsMigrationService(NullLogger<SettingsMigrationService>.Instance);
            Assert.False(retired.IsLegacyConfig(configPath));
            var retiredResult = await retired.MigrateLegacyConfigToV3Async(configPath, new System.Threading.CancellationToken(true));
            Assert.False(retiredResult.Success);
            Assert.False(retiredResult.Migrated);
            Assert.Contains("retired", retiredResult.ErrorMessage);
            await service.ResetConfigAsync((FrontedWindowType)(-1));
#pragma warning restore CS0618
            Assert.Equal("{\"Version\":null}", await File.ReadAllTextAsync(configPath));
            Assert.Equal(2, Directory.GetFiles(root).Length);
            var defaults = new Settings();
            var previous = new Settings { GhProxyMirror = "custom", Language = LanguageKey.en_US };
            if (Equals(previous.CultureInfo, defaults.CultureInfo)) previous.Language = LanguageKey.ja_JP;
            service.Settings = previous;
            var changes = 0;
            var languages = 0;
            service.SettingsChanged += (_, _) => changes++;
            service.LanguageSettingChanged += (_, args) => languages++;

            await service.ResetConfigAsync();

            Assert.NotSame(previous, service.Settings);
            Assert.Equal(defaults.GhProxyMirror, service.Settings.GhProxyMirror);
            Assert.Equal(defaults.Language, service.Settings.Language);
            Assert.Equal(1, changes);
            Assert.Equal(1, languages);
            var persisted = System.Text.Json.JsonSerializer.Deserialize<Settings>(await File.ReadAllTextAsync(configPath));
            Assert.Equal(defaults.GhProxyMirror, persisted!.GhProxyMirror);
            Assert.Equal(defaults.Version, persisted.Version);
            previous.Language = LanguageKey.zh_Hans;
            Assert.Equal(1, languages);
            Assert.Equal("unchanged", await File.ReadAllTextAsync(layoutPath));
            migration.VerifyNoOtherCalls();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
