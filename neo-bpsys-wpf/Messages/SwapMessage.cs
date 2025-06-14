namespace neo_bpsys_wpf.Messages
{
    public class SwapMessage(object? sender, bool isSwapped)
    {
        public object? Sender { get; } = sender;
        public bool IsSwapped { get; set; } = isSwapped;
    }
}
