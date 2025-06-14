using neo_bpsys_wpf.Models;
using System.Windows;
using System.Windows.Controls;

namespace neo_bpsys_wpf.CustomControls
{
    public class GameDataEditor : Control
    {
        public PlayerData PlayerData
        {
            get => (PlayerData)GetValue(PlayerDataProperty);
            set => SetValue(PlayerDataProperty, value);
        }

        // Using a DependencyProperty as the backing store for PlayerData.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty PlayerDataProperty =
            DependencyProperty.Register(nameof(PlayerData), typeof(PlayerData), typeof(GameDataEditor), new PropertyMetadata(null));

        public bool IsHunMode
        {
            get => (bool)GetValue(IsHunModeProperty);
            set => SetValue(IsHunModeProperty, value);
        }

        // Using a DependencyProperty as the backing store for IsHunMode.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsHunModeProperty =
            DependencyProperty.Register(nameof(IsHunMode), typeof(bool), typeof(GameDataEditor), new PropertyMetadata(false));
    }
}
