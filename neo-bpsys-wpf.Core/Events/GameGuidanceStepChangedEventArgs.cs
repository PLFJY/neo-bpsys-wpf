using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Core.Events;

/// <summary>
/// 对局引导步骤变化事件参数
/// </summary>
public class GameGuidanceStepChangedEventArgs : EventArgs
{
    /// <summary>
    /// 创建不包含上一步信息的对局引导步骤变化事件参数。
    /// </summary>
    /// <param name="stepIndex">当前步骤索引。</param>
    /// <param name="action">当前步骤操作。</param>
    /// <param name="index">当前操作的控件索引列表。</param>
    /// <param name="time">当前步骤计时（秒），可能为 null。</param>
    /// <param name="actionName">当前步骤的本地化名称。</param>
    public GameGuidanceStepChangedEventArgs(
        int stepIndex,
        GameAction action,
        List<int>? index,
        int? time,
        string actionName)
        : this(stepIndex, action, index, time, actionName, null, null, null, null, null)
    {
    }

    /// <summary>
    /// 创建包含当前步骤和上一步信息的对局引导步骤变化事件参数。
    /// </summary>
    /// <param name="stepIndex">当前步骤索引。</param>
    /// <param name="action">当前步骤操作。</param>
    /// <param name="index">当前操作的控件索引列表。</param>
    /// <param name="time">当前步骤计时（秒），可能为 null。</param>
    /// <param name="actionName">当前步骤的本地化名称。</param>
    /// <param name="previousStepIndex">上一步骤索引，没有上一步时为 null。</param>
    /// <param name="previousAction">上一步骤操作，没有上一步时为 null。</param>
    /// <param name="previousIndex">上一步骤的控件索引列表，没有上一步时为 null。</param>
    /// <param name="previousTime">上一步骤计时（秒），没有上一步时为 null。</param>
    /// <param name="previousActionName">上一步骤的本地化名称，没有上一步时为 null。</param>
    public GameGuidanceStepChangedEventArgs(
        int stepIndex,
        GameAction action,
        List<int>? index,
        int? time,
        string actionName,
        int? previousStepIndex,
        GameAction? previousAction,
        List<int>? previousIndex,
        int? previousTime,
        string? previousActionName)
    {
        StepIndex = stepIndex;
        Action = action;
        Index = index;
        Time = time;
        ActionName = actionName;
        PreviousStepIndex = previousStepIndex;
        PreviousAction = previousAction;
        PreviousIndex = previousIndex;
        PreviousTime = previousTime;
        PreviousActionName = previousActionName;
        IndexesText = GameGuidanceIndexFormatter.FormatIndexes(index);
        PreviousIndexesText = GameGuidanceIndexFormatter.FormatIndexes(previousIndex);
    }

    /// <summary>
    /// 当前步骤索引
    /// </summary>
    public int StepIndex { get; }

    /// <summary>
    /// 当前步骤操作
    /// </summary>
    public GameAction Action { get; }

    /// <summary>
    /// 当前操作的控件索引列表
    /// </summary>
    public List<int>? Index { get; }

    /// <summary>
    /// 当前操作的控件索引列表。
    /// </summary>
    public List<int>? Indexes => Index;

    /// <summary>
    /// 第一个操作控件索引，null 表示没有指定索引。
    /// </summary>
    public int? FirstIndex => Index?.FirstOrDefault();

    /// <summary>
    /// 当前步骤计时（秒），可能为 null
    /// </summary>
    public int? Time { get; }

    /// <summary>
    /// 当前步骤的本地化名称
    /// </summary>
    public string ActionName { get; }

    /// <summary>
    /// 上一步骤索引，没有上一步时为 null。
    /// </summary>
    public int? PreviousStepIndex { get; }

    /// <summary>
    /// 上一步骤操作，没有上一步时为 null。
    /// </summary>
    public GameAction? PreviousAction { get; }

    /// <summary>
    /// 上一步骤的控件索引列表，没有上一步时为 null。
    /// </summary>
    public List<int>? PreviousIndex { get; }

    /// <summary>
    /// 上一步骤的控件索引列表，没有上一步时为 null。
    /// </summary>
    public List<int>? PreviousIndexes => PreviousIndex;

    /// <summary>
    /// 上一步骤的第一个控件索引，没有上一步或未指定索引时为 null。
    /// </summary>
    public int? PreviousFirstIndex => PreviousIndex?.FirstOrDefault();

    /// <summary>
    /// 上一步骤计时（秒），没有上一步时为 null。
    /// </summary>
    public int? PreviousTime { get; }

    /// <summary>
    /// 上一步骤的本地化名称，没有上一步时为 null。
    /// </summary>
    public string? PreviousActionName { get; }

    /// <summary>
    /// 当前操作索引列表的稳定文本表示。
    /// </summary>
    public string IndexesText { get; }

    /// <summary>
    /// 上一步骤索引列表的稳定文本表示。
    /// </summary>
    public string PreviousIndexesText { get; }
}
