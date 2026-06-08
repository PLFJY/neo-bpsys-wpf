namespace neo_bpsys_wpf.Core.Events;

/// <summary>
/// 对局引导状态变化事件参数
/// </summary>
/// <param name="isStarted">引导是否已启动</param>
public class GameGuidanceStateChangedEventArgs(bool isStarted) : EventArgs
{
    /// <summary>
    /// 引导是否已启动
    /// </summary>
    public bool IsStarted { get; } = isStarted;
}
