namespace neo_bpsys_wpf.Core.Messages;

public class MemberOnFieldChangedMessage(object? sender)
{
    public object? Sender { get; } = sender;
}