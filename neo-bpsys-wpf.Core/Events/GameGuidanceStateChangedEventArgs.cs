namespace neo_bpsys_wpf.Core.Events;

/// <summary>
/// 对局引导状态变化事件参数
/// </summary>
/// <param name="isStarted">引导是否已启动</param>
/// <param name="reason">稳定的机器可读原因。</param>
/// <param name="time">当前或上一步的计时器值。</param>
/// <param name="previousStepIndex">上一步引导步骤索引。</param>
/// <param name="previousAction">上一步引导操作。</param>
/// <param name="previousIndex">上一步引导索引列表。</param>
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
    /// 稳定的机器可读原因。
    /// </summary>
    public string? Reason { get; } = reason;

    /// <summary>
    /// 当前或上一步的计时器值。
    /// </summary>
    public int? Time { get; } = time;

    /// <summary>
    /// 上一步引导步骤索引。
    /// </summary>
    public int? PreviousStepIndex { get; } = previousStepIndex;

    /// <summary>
    /// 上一步引导操作。
    /// </summary>
    public Enums.GameAction? PreviousAction { get; } = previousAction;

    /// <summary>
    /// 上一步引导索引列表。
    /// </summary>
    public List<int>? PreviousIndex { get; } = previousIndex;

    /// <summary>
    /// 上一步引导索引列表。
    /// </summary>
    public List<int>? PreviousIndexes => PreviousIndex;

    /// <summary>
    /// 上一步引导索引列表的稳定文本表示。
    /// </summary>
    public string PreviousIndexesText { get; } = GameGuidanceIndexFormatter.FormatIndexes(previousIndex);
}
