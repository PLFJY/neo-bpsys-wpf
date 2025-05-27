namespace neo_bpsys_wpf.CustomControls
{
    public class CharacterChangerCommandParameter
    {
        public int Target { get; set; }
        public int Source { get; set; }

        public CharacterChangerCommandParameter(int index, int buttonContent)
        {
            Target = index;
            Source = buttonContent;
        }
    }
}
