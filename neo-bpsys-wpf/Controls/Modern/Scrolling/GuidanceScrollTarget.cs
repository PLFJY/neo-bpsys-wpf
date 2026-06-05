using System.Windows;
using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Controls.Modern.Scrolling;

public static class GuidanceScrollTarget
{
    public static readonly DependencyProperty ActionProperty =
        DependencyProperty.RegisterAttached(
            "Action",
            typeof(GameAction?),
            typeof(GuidanceScrollTarget),
            new FrameworkPropertyMetadata(null));

    public static readonly DependencyProperty IndexProperty =
        DependencyProperty.RegisterAttached(
            "Index",
            typeof(int?),
            typeof(GuidanceScrollTarget),
            new FrameworkPropertyMetadata(null));

    public static readonly DependencyProperty KeyProperty =
        DependencyProperty.RegisterAttached(
            "Key",
            typeof(string),
            typeof(GuidanceScrollTarget),
            new FrameworkPropertyMetadata(null));

    public static GameAction? GetAction(DependencyObject obj) => (GameAction?)obj.GetValue(ActionProperty);

    public static void SetAction(DependencyObject obj, GameAction? value) => obj.SetValue(ActionProperty, value);

    public static int? GetIndex(DependencyObject obj) => (int?)obj.GetValue(IndexProperty);

    public static void SetIndex(DependencyObject obj, int? value) => obj.SetValue(IndexProperty, value);

    public static string? GetKey(DependencyObject obj) => (string?)obj.GetValue(KeyProperty);

    public static void SetKey(DependencyObject obj, string? value) => obj.SetValue(KeyProperty, value);
}
