using neo_bpsys_wpf.Core.Models;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 设置迁移服务接口
/// </summary>
public interface ISettingsMigrationService
{
    /// <summary>
    /// 检查配置文件是否为 legacy 配置
    /// </summary>
    /// <param name="configFilePath">配置文件路径</param>
    bool IsLegacyConfig(string configFilePath);

    /// <summary>
    /// 将 legacy 配置迁移到 v3
    /// </summary>
    /// <param name="configFilePath">配置文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<SettingsMigrationResult> MigrateLegacyConfigToV3Async(
        string configFilePath,
        CancellationToken cancellationToken = default);
}
