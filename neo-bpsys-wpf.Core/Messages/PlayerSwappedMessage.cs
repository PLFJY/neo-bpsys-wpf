namespace neo_bpsys_wpf.Core.Messages;

public class PlayerSwappedMessage(object? sender)
{
    public object? Sender { get; } = sender;
}