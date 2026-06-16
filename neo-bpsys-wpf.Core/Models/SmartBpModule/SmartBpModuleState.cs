namespace neo_bpsys_wpf.Core.Models.SmartBpModule;

/// <summary>
/// SmartBP module installation state persisted in the app config directory.
/// </summary>
public sealed class SmartBpModuleState
{
    /// <summary>
    /// Installed module root directory.
    /// </summary>
    public string ModuleRoot { get; set; } = string.Empty;

    /// <summary>
    /// Installed module version.
    /// </summary>
    public string? ModuleVersion { get; set; }

    /// <summary>
    /// Runtime ABI version.
    /// </summary>
    public int? RuntimeAbiVersion { get; set; }

    /// <summary>
    /// Runtime identifier.
    /// </summary>
    public string? Rid { get; set; }

    /// <summary>
    /// Installation kind.
    /// </summary>
    public string InstallKind { get; set; } = "LiteDownload";

    /// <summary>
    /// Whether the last load succeeded.
    /// </summary>
    public bool LastLoadedSuccessfully { get; set; }

    /// <summary>
    /// Last successful load time in UTC.
    /// </summary>
    public DateTimeOffset? LastLoadedAt { get; set; }

    /// <summary>
    /// Legacy OCR model migration state.
    /// </summary>
    public SmartBpLegacyOcrModelMigrationState LegacyOcrModelMigration { get; set; } = new();
}

/// <summary>
/// Legacy OCR model migration state.
/// </summary>
public sealed class SmartBpLegacyOcrModelMigrationState
{
    /// <summary>
    /// Whether migration has completed.
    /// </summary>
    public bool Completed { get; set; }

    /// <summary>
    /// Completion reason or last result.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Model keys that still need legacy cleanup.
    /// </summary>
    public List<string> PendingCleanupModelKeys { get; set; } = [];
}

/// <summary>
/// Pending SmartBP module directory migration marker.
/// </summary>
public sealed class SmartBpModuleMovePendingState
{
    /// <summary>
    /// Source module directory copied from.
    /// </summary>
    public string SourceRoot { get; set; } = string.Empty;

    /// <summary>
    /// Target module directory copied to.
    /// </summary>
    public string TargetRoot { get; set; } = string.Empty;

    /// <summary>
    /// UTC time when the migration marker was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Last cleanup failure message.
    /// </summary>
    public string? LastCleanupError { get; set; }
}
