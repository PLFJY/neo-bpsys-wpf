using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models;

namespace neo_bpsys_wpf.Services;

/// <summary>旧设置迁移服务的兼容入口，不再读取或修改配置。</summary>
[Obsolete("Use ILegacyV2StartupMigrationService. This compatibility service performs no migration.")]
public class SettingsMigrationService : ISettingsMigrationService
{
    /// <summary>保留旧构造签名，供已有调用方和 DI 解析。</summary>
    /// <param name="logger">兼容参数，不再使用。</param>
    public SettingsMigrationService(ILogger<SettingsMigrationService> logger)
    {
    }

    /// <summary>已退役的检测入口，不访问文件。</summary>
    /// <param name="configFilePath">兼容参数，不再使用。</param>
    /// <returns>始终返回 false。</returns>
    [Obsolete("Use SettingsConfigVersionHelper.InspectJson. This method always returns false.")]
    public bool IsLegacyConfig(string configFilePath) => false;

    /// <summary>已退役的迁移入口，不访问文件或安装布局包。</summary>
    /// <param name="configFilePath">兼容参数，不再使用。</param>
    /// <param name="cancellationToken">兼容参数，不再使用。</param>
    /// <returns>明确表示未执行迁移的失败结果。</returns>
    [Obsolete("Use ILegacyV2StartupMigrationService. This method performs no migration.")]
    public Task<SettingsMigrationResult> MigrateLegacyConfigToV3Async(
        string configFilePath,
        CancellationToken cancellationToken = default) => Task.FromResult(new SettingsMigrationResult
        {
            Success = false,
            Migrated = false,
            ErrorMessage = "SettingsMigrationService has been retired. Use ILegacyV2StartupMigrationService."
        });
}
