using System.Windows;
using System.Windows.Controls;
using PlayerData = neo_bpsys_wpf.Core.Models.PlayerData;

namespace neo_bpsys_wpf.Controls;

/// <summary>
/// 对局数据编辑器控件，用于编辑玩家数据。
/// </summary>
public class GameDataEditor : Control
{
    /// <summary>
    /// 获取或设置要编辑的玩家数据。
    /// </summary>
    public PlayerData PlayerData
    {
        get => (PlayerData)GetValue(PlayerDataProperty);
        set => SetValue(PlayerDataProperty, value);
    }

    /// <summary>
    /// <see cref="PlayerData"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty PlayerDataProperty =
        DependencyProperty.Register(nameof(PlayerData), typeof(PlayerData), typeof(GameDataEditor), new PropertyMetadata(null));

    /// <summary>
    /// 获取或设置一个值，指示是否为监管者模式。
    /// </summary>
    public bool IsHunMode
    {
        get => (bool)GetValue(IsHunModeProperty);
        set => SetValue(IsHunModeProperty, value);
    }

    /// <summary>
    /// <see cref="IsHunMode"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty IsHunModeProperty =
        DependencyProperty.Register(nameof(IsHunMode), typeof(bool), typeof(GameDataEditor), new PropertyMetadata(false));
}