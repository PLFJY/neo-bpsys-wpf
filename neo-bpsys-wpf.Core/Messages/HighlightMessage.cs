using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Core.Messages;

/// <summary>
/// 高亮消息
/// </summary>
/// <param name="GameAction">当前操作</param>
/// <param name="Index">当前操作的控件索引</param>
public record HighlightMessage(GameAction? GameAction, List<int>? Index);