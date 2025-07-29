namespace neo_bpsys_wpf.Core.Messages;

public class MemberPropertyChangedMessage(object? sender)
{
    public object? Sender { get; set; } = sender;
}