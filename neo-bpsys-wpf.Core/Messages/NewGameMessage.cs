namespace neo_bpsys_wpf.Core.Messages;

public class NewGameMessage(object? sender, bool isNewGameCreated)
{
    public object? Sender { get; set; } = sender;
    public bool IsNewGameCreated { get; set; } = isNewGameCreated;
}