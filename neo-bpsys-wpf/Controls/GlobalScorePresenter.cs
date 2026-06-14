using System.Windows;
using System.Windows.Controls;

namespace neo_bpsys_wpf.Controls;

/// <summary>
/// 全局比分展示控件，显示比分文本和阵营图标。
/// </summary>
public class GlobalScorePresenter : Control
{
    /// <summary>
    /// 获取或设置显示的比分文本。
    /// </summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>
    /// <see cref="Text"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(GlobalScorePresenter), new FrameworkPropertyMetadata("-", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>
    /// 获取或设置一个值，指示是否显示为监管者阵营图标。
    /// </summary>
    public bool IsHunIcon
    {
        get => (bool)GetValue(IsHunIconProperty);
        set => SetValue(IsHunIconProperty, value);
    }

    /// <summary>
    /// <see cref="IsHunIcon"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty IsHunIconProperty =
        DependencyProperty.Register(nameof(IsHunIcon), typeof(bool), typeof(GlobalScorePresenter), new PropertyMetadata(false));


    /// <summary>
    /// 获取或设置一个值，指示阵营图标是否可见。
    /// </summary>
    public bool IsCampVisible
    {
        get => (bool)GetValue(IsCampVisibleProperty);
        set => SetValue(IsCampVisibleProperty, value);
    }

    /// <summary>
    /// <see cref="IsCampVisible"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty IsCampVisibleProperty =
        DependencyProperty.Register(nameof(IsCampVisible), typeof(bool), typeof(GlobalScorePresenter), new PropertyMetadata(false));


}