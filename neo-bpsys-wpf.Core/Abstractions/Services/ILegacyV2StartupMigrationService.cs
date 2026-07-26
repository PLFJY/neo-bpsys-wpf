namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 将旧版 v2 启动设置迁移到设计器 v3 包状态。
/// </summary>
public interface ILegacyV2StartupMigrationService
{
    /// <summary>
    /// 在需要时迁移旧版启动配置。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>迁移结果。</returns>
    Task<LegacyV2StartupMigrationResult> MigrateIfNeededAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 启动时旧版 v2 迁移尝试的结果。
/// </summary>
public sealed class LegacyV2StartupMigrationResult
{
    /// <summary>
    /// 迁移操作是否成功完成。
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 本次调用中是否转换了旧版设置。
    /// </summary>
    public bool Migrated { get; set; }

    /// <summary>
    /// 是否复用了已存在的转换后包。
    /// </summary>
    public bool ReusedExistingPackage { get; set; }

    /// <summary>
    /// 转换后的包 ID（可用时）。
    /// </summary>
    public string? PackageId { get; set; }

    /// <summary>
    /// 迁移前创建的备份路径。
    /// </summary>
    public string? BackupPath { get; set; }

    /// <summary>
    /// 迁移失败时的诊断错误消息。
    /// </summary>
    public string? ErrorMessage { get; set; }
}
