namespace neo_bpsys_wpf.Core.Models.SmartBpModule;

/// <summary>
/// 持久化在应用配置目录中的 SmartBP 模块安装状态。
/// </summary>
public sealed class SmartBpModuleState
{
    /// <summary>
    /// 已安装模块的根目录。
    /// </summary>
    public string ModuleRoot { get; set; } = string.Empty;

    /// <summary>
    /// 已安装模块版本。
    /// </summary>
    public string? ModuleVersion { get; set; }

    /// <summary>
    /// 运行时 ABI 版本。
    /// </summary>
    public int? RuntimeAbiVersion { get; set; }

    /// <summary>
    /// 运行时标识符。
    /// </summary>
    public string? Rid { get; set; }

    /// <summary>
    /// 安装类型。
    /// </summary>
    public string InstallKind { get; set; } = "LiteDownload";

    /// <summary>
    /// 上一次加载是否成功。
    /// </summary>
    public bool LastLoadedSuccessfully { get; set; }

    /// <summary>
    /// 上一次成功加载的 UTC 时间。
    /// </summary>
    public DateTimeOffset? LastLoadedAt { get; set; }

    /// <summary>
    /// 旧版 OCR 模型迁移状态。
    /// </summary>
    public SmartBpLegacyOcrModelMigrationState LegacyOcrModelMigration { get; set; } = new();
}

/// <summary>
/// 旧版 OCR 模型迁移状态。
/// </summary>
public sealed class SmartBpLegacyOcrModelMigrationState
{
    /// <summary>
    /// 迁移是否已完成。
    /// </summary>
    public bool Completed { get; set; }

    /// <summary>
    /// 完成原因或上一次结果。
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// 仍需旧版清理的模型键。
    /// </summary>
    public List<string> PendingCleanupModelKeys { get; set; } = [];
}

/// <summary>
/// 待处理的 SmartBP 模块目录迁移标记。
/// </summary>
public sealed class SmartBpModuleMovePendingState
{
    /// <summary>
    /// 复制来源的模块目录。
    /// </summary>
    public string SourceRoot { get; set; } = string.Empty;

    /// <summary>
    /// 复制目标的模块目录。
    /// </summary>
    public string TargetRoot { get; set; } = string.Empty;

    /// <summary>
    /// 已准备好的模块目录，将在下次启动时替换 <see cref="TargetRoot"/>。
    /// </summary>
    public string? PreparedRoot { get; set; }

    /// <summary>
    /// 待处理操作应用后要持久化的安装类型。
    /// </summary>
    public string? InstallKind { get; set; }

    /// <summary>
    /// 创建迁移标记时的 UTC 时间。
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 上一次清理失败的错误信息。
    /// </summary>
    public string? LastCleanupError { get; set; }
}
