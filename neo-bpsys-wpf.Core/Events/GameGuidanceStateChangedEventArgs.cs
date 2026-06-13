namespace neo_bpsys_wpf.Core.Events;

/// <summary>
/// 对局引导状态变化事件参数
/// </summary>
/// <param name="isStarted">引导是否已启动</param>
/// <param name="reason">Stable machine-readable reason.</param>
/// <param name="time">Current or previous step timer value.</param>
/// <param name="previousStepIndex">Previous guidance step index.</param>
/// <param name="previousAction">Previous guidance action.</param>
/// <param name="previousIndex">Previous guidance indexes.</param>
public class GameGuidanceStateChangedEventArgs(
    bool isStarted,
    string? reason = null,
    int? time = null,
    int? previousStepIndex = null,
    Enums.GameAction? previousAction = null,
    List<int>? previousIndex = null) : EventArgs
{
    /// <summary>
    /// 引导是否已启动
    /// </summary>
    public bool IsStarted { get; } = isStarted;

    /// <summary>
    /// Stable machine-readable reason.
    /// </summary>
    public string? Reason { get; } = reason;

    /// <summary>
    /// Current or previous step timer value.
    /// </summary>
    public int? Time { get; } = time;

    /// <summary>
    /// Previous guidance step index.
    /// </summary>
    public int? PreviousStepIndex { get; } = previousStepIndex;

    /// <summary>
    /// Previous guidance action.
    /// </summary>
    public Enums.GameAction? PreviousAction { get; } = previousAction;

    /// <summary>
    /// Previous guidance indexes.
    /// </summary>
    public List<int>? PreviousIndex { get; } = previousIndex;

    /// <summary>
    /// Previous guidance indexes.
    /// </summary>
    public List<int>? PreviousIndexes => PreviousIndex;

    /// <summary>
    /// Stable text representation of previous guidance indexes.
    /// </summary>
    public string PreviousIndexesText { get; } = GameGuidanceIndexFormatter.FormatIndexes(previousIndex);
}
