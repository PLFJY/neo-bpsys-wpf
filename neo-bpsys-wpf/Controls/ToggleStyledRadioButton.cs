using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace neo_bpsys_wpf.Controls
{
    public class ToggleStyledRadioButton : RadioButton
    {
        public ImageSource ImageSource
        {
            get => (ImageSource)GetValue(ImageSourceProperty);
            set => SetValue(ImageSourceProperty, value);
        }

        // Using a DependencyProperty as the backing store for ImageSource.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ImageSourceProperty = DependencyProperty.Register(
            nameof(ImageSource),
            typeof(ImageSource),
            typeof(ToggleStyledRadioButton),
            new PropertyMetadata(null)
        );

        public string TagName
        {
            get => (string)GetValue(TagNameProperty);
            set => SetValue(TagNameProperty, value);
        }

        // Using a DependencyProperty as the backing store for TagName.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TagNameProperty = DependencyProperty.Register(
            nameof(TagName),
            typeof(string),
            typeof(ToggleStyledRadioButton),
            new PropertyMetadata(null)
        );

        public double ImageHeight
        {
            get => (double)GetValue(ImageHeightProperty);
            set => SetValue(ImageHeightProperty, value);
        }

        // Using a DependencyProperty as the backing store for ImageHeight.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ImageHeightProperty = DependencyProperty.Register(
            nameof(ImageHeight),
            typeof(double),
            typeof(ToggleStyledRadioButton),
            new PropertyMetadata(73.0)
        );

        public double ImageWidth
        {
            get => (double)GetValue(ImageWidthProperty);
            set => SetValue(ImageWidthProperty, value);
        }

        // Using a DependencyProperty as the backing store for ImageWidth.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ImageWidthProperty = DependencyProperty.Register(
            nameof(ImageWidth),
            typeof(double),
            typeof(ToggleStyledRadioButton),
            new PropertyMetadata(276.0)
        );

        public double TagNameFontSize
        {
            get { return (double)GetValue(TagNameFontSizeProperty); }
            set { SetValue(TagNameFontSizeProperty, value); }
        }

        // Using a DependencyProperty as the backing store for TagNameFontSize.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TagNameFontSizeProperty =
            DependencyProperty.Register("TagNameFontSize", typeof(double), typeof(ToggleStyledRadioButton), new PropertyMetadata(14.0));

        public bool IsImageVisible
        {
            get { return (bool)GetValue(IsImageVisibleProperty); }
            set { SetValue(IsImageVisibleProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsImageVisible.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsImageVisibleProperty =
            DependencyProperty.Register("IsImageVisible", typeof(bool), typeof(ToggleStyledRadioButton), new PropertyMetadata(true));

    }
}
