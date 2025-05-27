namespace neo_bpsys_wpf.Messages
{
    public class NewGameMessage(object? sender, bool isNewGameCreated)
    {
        public bool IsNewGameCreated { get; set; } = isNewGameCreated;

        public object? Sender { get; set; } = sender;
    }
}
