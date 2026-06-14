using System.Windows;
using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Controls.Modern.Scrolling;

/// <summary>
/// 为引导系统提供滚动目标标识的附加属性。
/// </summary>
public static class GuidanceScrollTarget
{
    /// <summary>
    /// <see cref="ActionProperty"/> 附加属性的标识符。
    /// </summary>
    public static readonly DependencyProperty ActionProperty =
        DependencyProperty.RegisterAttached(
            "Action",
            typeof(GameAction?),
            typeof(GuidanceScrollTarget),
            new FrameworkPropertyMetadata(null));

    /// <summary>
    /// <see cref="IndexProperty"/> 附加属性的标识符。
    /// </summary>
    public static readonly DependencyProperty IndexProperty =
        DependencyProperty.RegisterAttached(
            "Index",
            typeof(int?),
            typeof(GuidanceScrollTarget),
            new FrameworkPropertyMetadata(null));

    /// <summary>
    /// <see cref="KeyProperty"/> 附加属性的标识符。
    /// </summary>
    public static readonly DependencyProperty KeyProperty =
        DependencyProperty.RegisterAttached(
            "Key",
            typeof(string),
            typeof(GuidanceScrollTarget),
            new FrameworkPropertyMetadata(null));

    /// <summary>
    /// 获取指定元素的 <see cref="ActionProperty"/> 附加属性值。
    /// </summary>
    /// <param name="obj">要获取属性值的元素。</param>
    /// <returns>对应的游戏动作，如果未设置则为 <c>null</c>。</returns>
    public static GameAction? GetAction(DependencyObject obj) => (GameAction?)obj.GetValue(ActionProperty);

    /// <summary>
    /// 设置指定元素的 <see cref="ActionProperty"/> 附加属性值。
    /// </summary>
    /// <param name="obj">要设置属性值的元素。</param>
    /// <param name="value">对应的游戏动作。</param>
    public static void SetAction(DependencyObject obj, GameAction? value) => obj.SetValue(ActionProperty, value);

    /// <summary>
    /// 获取指定元素的 <see cref="IndexProperty"/> 附加属性值。
    /// </summary>
    /// <param name="obj">要获取属性值的元素。</param>
    /// <returns>目标索引，如果未设置则为 <c>null</c>。</returns>
    public static int? GetIndex(DependencyObject obj) => (int?)obj.GetValue(IndexProperty);

    /// <summary>
    /// 设置指定元素的 <see cref="IndexProperty"/> 附加属性值。
    /// </summary>
    /// <param name="obj">要设置属性值的元素。</param>
    /// <param name="value">目标索引。</param>
    public static void SetIndex(DependencyObject obj, int? value) => obj.SetValue(IndexProperty, value);

    /// <summary>
    /// 获取指定元素的 <see cref="KeyProperty"/> 附加属性值。
    /// </summary>
    /// <param name="obj">要获取属性值的元素。</param>
    /// <returns>目标键，如果未设置则为 <c>null</c>。</returns>
    public static string? GetKey(DependencyObject obj) => (string?)obj.GetValue(KeyProperty);

    /// <summary>
    /// 设置指定元素的 <see cref="KeyProperty"/> 附加属性值。
    /// </summary>
    /// <param name="obj">要设置属性值的元素。</param>
    /// <param name="value">目标键。</param>
    public static void SetKey(DependencyObject obj, string? value) => obj.SetValue(KeyProperty, value);
}
