using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace neo_bpsys_wpf.Controls;

/// <summary>
/// 角色更换控件，用于在玩家之间交换角色。
/// </summary>
public class CharacterChanger : Control
{
    static CharacterChanger()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(CharacterChanger),
            new FrameworkPropertyMetadata(typeof(CharacterChanger))
        );
    }

    /// <summary>
    /// <see cref="Index"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty IndexProperty = DependencyProperty.Register(
        nameof(Index),
        typeof(int),
        typeof(CharacterChanger),
        new PropertyMetadata(0)
    );

    /// <summary>
    /// 获取或设置当前玩家索引。
    /// </summary>
    public int Index
    {
        get => (int)GetValue(IndexProperty);
        set => SetValue(IndexProperty, value);
    }

    /// <summary>
    /// <see cref="Command"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(
        nameof(Command),
        typeof(ICommand),
        typeof(CharacterChanger),
        new PropertyMetadata(null)
    );

    /// <summary>
    /// 获取或设置点击时执行的命令。
    /// </summary>
    public ICommand Command
    {
        get => (ICommand)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    /// <summary>
    /// <see cref="Spacing"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(
        nameof(Spacing),
        typeof(double),
        typeof(CharacterChanger),
        new PropertyMetadata(0.0)
    );

    /// <summary>
    /// 获取或设置按钮之间的间距。
    /// </summary>
    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <summary>
    /// 获取或设置一个值，指示控件是否应高亮显示。
    /// </summary>
    public bool IsHighlighted
    {
        get => (bool)GetValue(IsHighlightedProperty);
        set => SetValue(IsHighlightedProperty, value);
    }

    /// <summary>
    /// <see cref="IsHighlighted"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty IsHighlightedProperty =
        DependencyProperty.Register(nameof(IsHighlighted), typeof(bool), typeof(CharacterChanger), new PropertyMetadata(false));


}