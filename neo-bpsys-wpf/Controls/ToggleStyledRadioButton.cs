using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace neo_bpsys_wpf.Controls;

/// <summary>
/// 带样式的可切换单选按钮，支持图片显示和标签名称。
/// </summary>
public class ToggleStyledRadioButton : RadioButton
{
    /// <summary>
    /// 获取或设置按钮显示的图片源。
    /// </summary>
    public ImageSource ImageSource
    {
        get => (ImageSource)GetValue(ImageSourceProperty);
        set => SetValue(ImageSourceProperty, value);
    }

    /// <summary>
    /// <see cref="ImageSource"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty ImageSourceProperty = DependencyProperty.Register(
        nameof(ImageSource),
        typeof(ImageSource),
        typeof(ToggleStyledRadioButton),
        new PropertyMetadata(null)
    );

    /// <summary>
    /// 获取或设置按钮显示的标签名称。
    /// </summary>
    public string TagName
    {
        get => (string)GetValue(TagNameProperty);
        set => SetValue(TagNameProperty, value);
    }

    /// <summary>
    /// <see cref="TagName"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty TagNameProperty = DependencyProperty.Register(
        nameof(TagName),
        typeof(string),
        typeof(ToggleStyledRadioButton),
        new PropertyMetadata(null)
    );

    /// <summary>
    /// 获取或设置图片的高度。
    /// </summary>
    public double ImageHeight
    {
        get => (double)GetValue(ImageHeightProperty);
        set => SetValue(ImageHeightProperty, value);
    }

    /// <summary>
    /// <see cref="ImageHeight"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty ImageHeightProperty = DependencyProperty.Register(
        nameof(ImageHeight),
        typeof(double),
        typeof(ToggleStyledRadioButton),
        new PropertyMetadata(73.0)
    );

    /// <summary>
    /// 获取或设置图片的宽度。
    /// </summary>
    public double ImageWidth
    {
        get => (double)GetValue(ImageWidthProperty);
        set => SetValue(ImageWidthProperty, value);
    }

    /// <summary>
    /// <see cref="ImageWidth"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty ImageWidthProperty = DependencyProperty.Register(
        nameof(ImageWidth),
        typeof(double),
        typeof(ToggleStyledRadioButton),
        new PropertyMetadata(276.0)
    );

    /// <summary>
    /// 获取或设置标签名称的字体大小。
    /// </summary>
    public double TagNameFontSize
    {
        get => (double)GetValue(TagNameFontSizeProperty);
        set => SetValue(TagNameFontSizeProperty, value);
    }

    /// <summary>
    /// <see cref="TagNameFontSize"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty TagNameFontSizeProperty =
        DependencyProperty.Register(nameof(TagNameFontSize), typeof(double), typeof(ToggleStyledRadioButton), new PropertyMetadata(14.0));

    /// <summary>
    /// 获取或设置一个值，指示图片是否可见。
    /// </summary>
    public bool IsImageVisible
    {
        get => (bool)GetValue(IsImageVisibleProperty);
        set => SetValue(IsImageVisibleProperty, value);
    }

    /// <summary>
    /// <see cref="IsImageVisible"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty IsImageVisibleProperty =
        DependencyProperty.Register(nameof(IsImageVisible), typeof(bool), typeof(ToggleStyledRadioButton), new PropertyMetadata(true));

}