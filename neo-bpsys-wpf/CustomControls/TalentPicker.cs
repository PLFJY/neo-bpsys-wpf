using System.Windows;
using System.Windows.Controls;

namespace neo_bpsys_wpf.CustomControls
{
    public class TalentPicker : Control
    {
        public bool IsTypeHun
        {
            get { return (bool)GetValue(IsTypeHunProperty); }
            set { SetValue(IsTypeHunProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsTypeSur.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsTypeHunProperty = DependencyProperty.Register(
            "IsTypeHun",
            typeof(bool),
            typeof(TalentPicker),
            new PropertyMetadata(false)
        );

        public string CharacterName
        {
            get { return (string)GetValue(CharacterNameProperty); }
            set { SetValue(CharacterNameProperty, value); }
        }

        // Using a DependencyProperty as the backing store for CharacterName.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty CharacterNameProperty =
            DependencyProperty.Register(
                "CharacterName",
                typeof(string),
                typeof(TalentPicker),
                new PropertyMetadata(null)
            );

        public bool IsFlywheelEffectChecked
        {
            get { return (bool)GetValue(IsFlywheelEffectCheckedProperty); }
            set { SetValue(IsFlywheelEffectCheckedProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsFlywheelEffectChecked.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsFlywheelEffectCheckedProperty =
            DependencyProperty.Register(
                "IsFlywheelEffectChecked",
                typeof(bool),
                typeof(TalentPicker),
                new PropertyMetadata(false)
            );
    }
}
