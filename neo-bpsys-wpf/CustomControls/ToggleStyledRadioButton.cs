using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace neo_bpsys_wpf.CustomControls
{
    public class ToggleStyledRadioButton : RadioButton
    {
        public ImageSource ImageSource
        {
            get { return (ImageSource)GetValue(ImageSourceProperty); }
            set { SetValue(ImageSourceProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ImageSource.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ImageSourceProperty = DependencyProperty.Register(
            "ImageSource",
            typeof(ImageSource),
            typeof(ToggleStyledRadioButton),
            new PropertyMetadata(null)
        );

        public string TagName
        {
            get { return (string)GetValue(TagNameProperty); }
            set { SetValue(TagNameProperty, value); }
        }

        // Using a DependencyProperty as the backing store for TagName.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TagNameProperty = DependencyProperty.Register(
            "TagName",
            typeof(string),
            typeof(ToggleStyledRadioButton),
            new PropertyMetadata(null)
        );

        public double ImageHeight
        {
            get { return (double)GetValue(ImageHeightProperty); }
            set { SetValue(ImageHeightProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ImageHeight.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ImageHeightProperty = DependencyProperty.Register(
            "ImageHeight",
            typeof(double),
            typeof(ToggleStyledRadioButton),
            new PropertyMetadata(73.0)
        );

        public double ImageWidth
        {
            get { return (double)GetValue(ImageWidthProperty); }
            set { SetValue(ImageWidthProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ImageWidth.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ImageWidthProperty = DependencyProperty.Register(
            "ImageWidth",
            typeof(double),
            typeof(ToggleStyledRadioButton),
            new PropertyMetadata(276.0)
        );

        public ICommand CheckedCommand
        {
            get { return (ICommand)GetValue(CheckedCommandProperty); }
            set { SetValue(CheckedCommandProperty, value); }
        }

        // Using a DependencyProperty as the backing store for CheckedCommand.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty CheckedCommandProperty =
            DependencyProperty.Register(
                "CheckedCommand",
                typeof(ICommand),
                typeof(ToggleStyledRadioButton),
                new PropertyMetadata(null)
            );
    }
}
