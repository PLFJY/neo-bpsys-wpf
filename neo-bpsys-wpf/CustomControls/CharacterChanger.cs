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
            nameof(Index),
            typeof(int),
            typeof(CharacterChanger),
            new PropertyMetadata(0)
        );

        public int Index
        {
            get => (int)GetValue(IndexProperty);
            set => SetValue(IndexProperty, value);
        }

        public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(
            nameof(Command),
            typeof(ICommand),
            typeof(CharacterChanger),
            new PropertyMetadata(null)
        );

        public ICommand Command
        {
            get => (ICommand)GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(
            nameof(Spacing),
            typeof(double),
            typeof(CharacterChanger),
            new PropertyMetadata(0.0)
        );
        public double Spacing
        {
            get => (double)GetValue(SpacingProperty);
            set => SetValue(SpacingProperty, value);
        }

        public bool IsHighlighted
        {
            get { return (bool)GetValue(IsHighlightedProperty); }
            set { SetValue(IsHighlightedProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsHighlighted.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsHighlightedProperty =
            DependencyProperty.Register("IsHighlighted", typeof(bool), typeof(CharacterChanger), new PropertyMetadata(false));


    }
}
