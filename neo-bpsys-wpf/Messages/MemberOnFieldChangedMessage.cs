namespace neo_bpsys_wpf.Messages;

public class MemberOnFieldChangedMessage(object? sender)
{
    public object? Sender { get; } = sender;
}