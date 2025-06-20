namespace neo_bpsys_wpf.Controls
{
    public class CharacterChangerCommandParameter(int index, int buttonContent)
    {
        public int Target { get; set; } = index;
        public int Source { get; set; } = buttonContent;
    }
}
