using neo_bpsys.Core.Enums;

namespace neo_bpsys.Core.Messages;

public class HighlightMessage
{
    public HighlightMessage(GameAction? gameAction, List<int>? index)
    {
        GameAction = gameAction;
        Index = index;
    }

    public GameAction? GameAction { get; }
    public List<int>? Index { get; }
}
