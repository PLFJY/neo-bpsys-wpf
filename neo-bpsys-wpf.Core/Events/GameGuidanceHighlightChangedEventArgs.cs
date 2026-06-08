using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Core.Events;

/// <summary>
/// 对局引导高亮变化事件参数
/// </summary>
/// <param name="gameAction">当前高亮对应的操作，null 表示取消高亮</param>
/// <param name="index">高亮控件的索引列表，null 表示取消高亮</param>
public class GameGuidanceHighlightChangedEventArgs(GameAction? gameAction, List<int>? index) : EventArgs
{
    /// <summary>
    /// 当前高亮对应的操作，null 表示取消高亮
    /// </summary>
    public GameAction? GameAction { get; } = gameAction;

    /// <summary>
    /// 当前高亮对应的操作，null 表示取消高亮。
    /// </summary>
    public GameAction? Action => GameAction;

    /// <summary>
    /// 高亮控件的索引列表，null 表示取消高亮
    /// </summary>
    public List<int>? Index { get; } = index;

    /// <summary>
    /// 高亮控件的索引列表，null 表示取消高亮。
    /// </summary>
    public List<int>? Indexes => Index;

    /// <summary>
    /// 第一个高亮控件索引，null 表示没有指定索引。
    /// </summary>
    public int? FirstIndex => Index?.FirstOrDefault();

}
