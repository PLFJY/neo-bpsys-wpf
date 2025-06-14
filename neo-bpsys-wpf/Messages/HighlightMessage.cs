using neo_bpsys_wpf.Enums;

namespace neo_bpsys_wpf.Messages;

public class HighlightMessage(GameAction? gameAction, List<int>? index)
{
    public GameAction? GameAction { get; set; } = gameAction;
    public List<int>? Index { get; set; } = index;
}