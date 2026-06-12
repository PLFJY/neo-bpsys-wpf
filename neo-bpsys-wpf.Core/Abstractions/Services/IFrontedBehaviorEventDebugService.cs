using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// Global debug recorder for fronted behavior events.
/// </summary>
public interface IFrontedBehaviorEventDebugService : IDisposable
{
    /// <summary>
    /// Gets or sets whether incoming events should be recorded.
    /// </summary>
    bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets whether new incoming events should be ignored while preserving existing records.
    /// </summary>
    bool IsPaused { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of retained records.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is less than one.</exception>
    int MaxRecords { get; set; }

    /// <summary>
    /// Gets the current captured records in ascending sequence order.
    /// </summary>
    IReadOnlyList<FrontedBehaviorEventDebugRecord> Records { get; }

    /// <summary>
    /// Raised after a behavior event debug record is added.
    /// </summary>
    event EventHandler<FrontedBehaviorEventDebugRecord>? RecordAdded;

    /// <summary>
    /// Raised after all records are cleared.
    /// </summary>
    event EventHandler? RecordsCleared;

    /// <summary>
    /// Removes all captured records.
    /// </summary>
    void Clear();
}
