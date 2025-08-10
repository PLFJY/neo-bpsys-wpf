using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Core.Messages;

/// <summary>
/// 高亮消息
/// </summary>
/// <param name="gameAction">当前操作</param>
/// <param name="index">当前操作的控件索引</param>
public class HighlightMessage(GameAction? gameAction, List<int>? index)
{
    public GameAction? GameAction { get; } = gameAction;
    public List<int>? Index { get; } = index;
}