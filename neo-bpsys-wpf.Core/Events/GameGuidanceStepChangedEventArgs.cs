using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Core.Events;

/// <summary>
/// 对局引导步骤变化事件参数
/// </summary>
/// <param name="stepIndex">当前步骤索引</param>
/// <param name="action">当前步骤操作</param>
/// <param name="index">当前操作的控件索引列表</param>
/// <param name="time">当前步骤计时（秒），可能为 null</param>
/// <param name="actionName">当前步骤的本地化名称</param>
public class GameGuidanceStepChangedEventArgs(
    int stepIndex,
    GameAction action,
    List<int>? index,
    int? time,
    string actionName) : EventArgs
{
    /// <summary>
    /// 当前步骤索引
    /// </summary>
    public int StepIndex { get; } = stepIndex;

    /// <summary>
    /// 当前步骤操作
    /// </summary>
    public GameAction Action { get; } = action;

    /// <summary>
    /// 当前操作的控件索引列表
    /// </summary>
    public List<int>? Index { get; } = index;

    /// <summary>
    /// 当前步骤计时（秒），可能为 null
    /// </summary>
    public int? Time { get; } = time;

    /// <summary>
    /// 当前步骤的本地化名称
    /// </summary>
    public string ActionName { get; } = actionName;
}
