namespace neo_bpsys_wpf.Messages
{
    public class MemberStateChangedMessage(object? sender)
    {
        public object? Sender { get; set; } = sender;
    }
}
