using System.Windows;
using System.Windows.Controls;
using Player = neo_bpsys_wpf.Core.Models.Player;

namespace neo_bpsys_wpf.Controls;

/// <summary>
/// 天赋选择器，传入玩家即可
/// </summary>
public class TalentPicker : Control
{
    /// <summary>
    /// 获取或设置一个值，指示是否为监管者类型。
    /// </summary>
    public bool IsTypeHun
    {
        get => (bool)GetValue(IsTypeHunProperty);
        set => SetValue(IsTypeHunProperty, value);
    }

    /// <summary>
    /// <see cref="IsTypeHun"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty IsTypeHunProperty = DependencyProperty.Register(
        nameof(IsTypeHun),
        typeof(bool),
        typeof(TalentPicker),
        new PropertyMetadata(false)
    );

    /// <summary>
    /// 获取或设置角色名称。
    /// </summary>
    public string CharacterName
    {
        get => (string)GetValue(CharacterNameProperty);
        set => SetValue(CharacterNameProperty, value);
    }

    /// <summary>
    /// <see cref="CharacterName"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty CharacterNameProperty =
        DependencyProperty.Register(nameof(CharacterName), typeof(string), typeof(TalentPicker), new PropertyMetadata(string.Empty));

    /// <summary>
    /// 获取或设置关联的玩家对象。
    /// </summary>
    public Player Player
    {
        get => (Player)GetValue(PlayerProperty);
        set => SetValue(PlayerProperty, value);
    }

    /// <summary>
    /// <see cref="Player"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty PlayerProperty =
        DependencyProperty.Register(nameof(Player), typeof(Player), typeof(TalentPicker), new PropertyMetadata(null));

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
    public static readonly DependencyProperty IsHighlightedProperty = DependencyProperty.Register(
        nameof(IsHighlighted), typeof(bool), typeof(TalentPicker), new PropertyMetadata(false));
}