using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace neo_bpsys_wpf.CustomControls
{
    public class CharacterChanger : Control
    {
        static CharacterChanger()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(CharacterChanger),
                new FrameworkPropertyMetadata(typeof(CharacterChanger))
            );
        }

        public static readonly DependencyProperty IndexProperty = DependencyProperty.Register(
            "Index",
            typeof(int),
            typeof(CharacterChanger),
            new PropertyMetadata(0)
        );

        public int Index
        {
            get { return (int)GetValue(IndexProperty); }
            set { SetValue(IndexProperty, value); }
        }

        public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(
            "Command",
            typeof(ICommand),
            typeof(CharacterChanger),
            new PropertyMetadata(null)
        );

        public ICommand Command
        {
            get { return (ICommand)GetValue(CommandProperty); }
            set { SetValue(CommandProperty, value); }
        }

        public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(
            "Spacing",
            typeof(double),
            typeof(CharacterChanger),
            new PropertyMetadata(0.0)
        );
        public double Spacing
        {
            get { return (double)GetValue(SpacingProperty); }
            set { SetValue(SpacingProperty, value); }
        }
    }
}
