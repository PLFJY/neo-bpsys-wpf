namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// Migrates legacy v2 startup settings into Designer v3 package state.
/// </summary>
public interface ILegacyV2StartupMigrationService
{
    /// <summary>
    /// Migrates the legacy startup config when needed.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The migration result.</returns>
    Task<LegacyV2StartupMigrationResult> MigrateIfNeededAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a startup legacy v2 migration attempt.
/// </summary>
public sealed class LegacyV2StartupMigrationResult
{
    /// <summary>
    /// Whether the migration operation completed successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Whether legacy settings were converted during this call.
    /// </summary>
    public bool Migrated { get; set; }

    /// <summary>
    /// Whether an existing converted package was reused.
    /// </summary>
    public bool ReusedExistingPackage { get; set; }

    /// <summary>
    /// Converted package id when available.
    /// </summary>
    public string? PackageId { get; set; }

    /// <summary>
    /// Backup path created before migration.
    /// </summary>
    public string? BackupPath { get; set; }

    /// <summary>
    /// Diagnostic error message when migration failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
