namespace neo_bpsys_wpf.Messages
{
    public class MemberPropertyChangedMessage(object? sender)
    {
        public object? Sender { get; set; } = sender;
    }
}
