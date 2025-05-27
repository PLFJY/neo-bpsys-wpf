using System.Windows;
using System.Windows.Controls;

namespace neo_bpsys_wpf.CustomControls
{
    /// <summary>
    /// 12, 3 ,6 ,9 分别对应的是天赋图在时钟对应的方向如下所示
    /// 
    ///    12
    ///  9     3
    ///     6
    /// </summary>
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
            DependencyProperty.Register("CharacterName", typeof(string), typeof(TalentPicker), new PropertyMetadata(string.Empty));

        public bool Is12Checked
        {
            get { return (bool)GetValue(Is12CheckedProperty); }
            set { SetValue(Is12CheckedProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Is12Checked.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty Is12CheckedProperty =
            DependencyProperty.Register("Is12Checked", typeof(bool), typeof(TalentPicker), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public bool Is3Checked
        {
            get { return (bool)GetValue(Is3CheckedProperty); }
            set { SetValue(Is3CheckedProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Is3Checked.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty Is3CheckedProperty =
            DependencyProperty.Register("Is3Checked", typeof(bool), typeof(TalentPicker), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));


        public bool Is6Checked
        {
            get { return (bool)GetValue(Is6CheckedProperty); }
            set { SetValue(Is6CheckedProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Is6Checked.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty Is6CheckedProperty =
            DependencyProperty.Register("Is6Checked", typeof(bool), typeof(TalentPicker), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));


        public bool Is9Checked
        {
            get { return (bool)GetValue(Is9CheckedProperty); }
            set { SetValue(Is9CheckedProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Is9Checked.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty Is9CheckedProperty =
            DependencyProperty.Register("Is9Checked", typeof(bool), typeof(TalentPicker), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    }
}
