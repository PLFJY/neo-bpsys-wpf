using neo_bpsys_wpf.Core.Models;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 设置迁移服务接口
/// </summary>
[Obsolete("Use ILegacyV2StartupMigrationService. This compatibility service performs no migration.")]
public interface ISettingsMigrationService
{
    /// <summary>
    /// 检查配置文件是否为 legacy 配置
    /// </summary>
    /// <param name="configFilePath">配置文件路径</param>
    /// <returns>始终返回 false；此入口不再检测配置。</returns>
    [Obsolete("Use SettingsConfigVersionHelper.InspectJson. This method always returns false.")]
    bool IsLegacyConfig(string configFilePath);

    /// <summary>
    /// 将 legacy 配置迁移到 v3
    /// </summary>
    /// <param name="configFilePath">配置文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示接口退役且未执行迁移的失败结果。</returns>
    [Obsolete("Use ILegacyV2StartupMigrationService. This method performs no migration.")]
    Task<SettingsMigrationResult> MigrateLegacyConfigToV3Async(
        string configFilePath,
        CancellationToken cancellationToken = default);
}
