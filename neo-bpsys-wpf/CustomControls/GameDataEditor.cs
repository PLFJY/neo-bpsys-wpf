using System.Windows;
using System.Windows.Controls;

namespace neo_bpsys_wpf.CustomControls
{
    public class GameDataEditor : Control
    {
        public string GameDataValue
        {
            get { return (string)GetValue(GameDataValueProperty); }
            set { SetValue(GameDataValueProperty, value); }
        }

        // Using a DependencyProperty as the backing store for GameDataValue.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty GameDataValueProperty =
            DependencyProperty.Register(
                "GameDataValue",
                typeof(string),
                typeof(GameDataEditor),
                new PropertyMetadata(null)
            );

        public string GameDataType
        {
            get { return (string)GetValue(GameDataTypeProperty); }
            set { SetValue(GameDataTypeProperty, value); }
        }

        // Using a DependencyProperty as the backing store for GameDataType.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty GameDataTypeProperty =
            DependencyProperty.Register(
                "GameDataType",
                typeof(string),
                typeof(GameDataEditor),
                new PropertyMetadata(null)
            );
    }
}
