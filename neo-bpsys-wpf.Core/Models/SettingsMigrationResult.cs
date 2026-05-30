namespace neo_bpsys_wpf.Core.Models;

/// <summary>
/// 设置迁移结果
/// </summary>
public class SettingsMigrationResult
{
    /// <summary>
    /// 迁移是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 是否实际执行了迁移
    /// </summary>
    public bool Migrated { get; set; }

    /// <summary>
    /// 备份文件路径
    /// </summary>
    public string? BackupPath { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }
}
