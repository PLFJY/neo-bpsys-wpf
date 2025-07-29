using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Core.Messages;

public class HighlightMessage(GameAction? gameAction, List<int>? index)
{
    public GameAction? GameAction { get; set; } = gameAction;
    public List<int>? Index { get; set; } = index;
}