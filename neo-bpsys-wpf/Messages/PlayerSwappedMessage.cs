namespace neo_bpsys_wpf.Messages
{
    public class PlayerSwappedMessage(object? sender)
    {
        public object? Sender { get; } = sender;
    }
}
