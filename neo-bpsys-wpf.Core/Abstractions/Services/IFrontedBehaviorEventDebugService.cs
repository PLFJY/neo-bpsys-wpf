using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 前台行为事件的全局调试记录器。
/// </summary>
public interface IFrontedBehaviorEventDebugService : IDisposable
{
    /// <summary>
    /// 获取或设置是否记录传入的事件。
    /// </summary>
    bool IsEnabled { get; set; }

    /// <summary>
    /// 获取或设置是否忽略新传入的事件，同时保留已有记录。
    /// </summary>
    bool IsPaused { get; set; }

    /// <summary>
    /// 获取或设置保留的最大记录数。
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">当值小于 1 时抛出。</exception>
    int MaxRecords { get; set; }

    /// <summary>
    /// 获取当前捕获的记录，按序列号升序排列。
    /// </summary>
    IReadOnlyList<FrontedBehaviorEventDebugRecord> Records { get; }

    /// <summary>
    /// 在添加行为事件调试记录后触发。
    /// </summary>
    event EventHandler<FrontedBehaviorEventDebugRecord>? RecordAdded;

    /// <summary>
    /// 在清除所有记录后触发。
    /// </summary>
    event EventHandler? RecordsCleared;

    /// <summary>
    /// 移除所有已捕获的记录。
    /// </summary>
    void Clear();
}
