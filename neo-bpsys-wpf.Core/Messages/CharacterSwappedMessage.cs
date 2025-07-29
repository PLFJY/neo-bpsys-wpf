namespace neo_bpsys_wpf.Core.Messages;

public class CharacterSwappedMessage(object? sender)
{
    public object? Sender { get; set; } = sender;
}