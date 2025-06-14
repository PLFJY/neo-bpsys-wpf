using neo_bpsys_wpf.Models;
using System.Windows;
using System.Windows.Controls;

namespace neo_bpsys_wpf.CustomControls
{
    /// <summary>
    /// 天赋选择器，传入玩家即可
    /// </summary>
    public class TalentPicker : Control
    {
        public bool IsTypeHun
        {
            get => (bool)GetValue(IsTypeHunProperty);
            set => SetValue(IsTypeHunProperty, value);
        }

        // Using a DependencyProperty as the backing store for IsTypeSur.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsTypeHunProperty = DependencyProperty.Register(
            nameof(IsTypeHun),
            typeof(bool),
            typeof(TalentPicker),
            new PropertyMetadata(false)
        );

        public string CharacterName
        {
            get => (string)GetValue(CharacterNameProperty);
            set => SetValue(CharacterNameProperty, value);
        }

        // Using a DependencyProperty as the backing store for CharacterName.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty CharacterNameProperty =
            DependencyProperty.Register(nameof(CharacterName), typeof(string), typeof(TalentPicker), new PropertyMetadata(string.Empty));

        public Player Player
        {
            get => (Player)GetValue(PlayerProperty);
            set => SetValue(PlayerProperty, value);
        }

        // Using a DependencyProperty as the backing store for Player.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty PlayerProperty =
            DependencyProperty.Register(nameof(Player), typeof(Player), typeof(TalentPicker), new PropertyMetadata(null));

        public bool IsHighlighted
        {
            get => (bool)GetValue(IsHighlightedProperty);
            set => SetValue(IsHighlightedProperty, value);
        }

        public static readonly DependencyProperty IsHighlightedProperty = DependencyProperty.Register(
            nameof(IsHighlighted), typeof(bool), typeof(TalentPicker), new PropertyMetadata(false));
    }
}
