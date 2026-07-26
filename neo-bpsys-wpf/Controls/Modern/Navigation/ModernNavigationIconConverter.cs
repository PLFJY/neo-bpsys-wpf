#nullable enable

using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Controls.Modern.Navigation;

/// <summary>
/// 将导航项图标转换为 <see cref="FrameworkElement"/> 的值转换器。
/// </summary>
public sealed class ModernNavigationIconConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return CreateIcon(value);
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;

    /// <summary>
    /// 根据图标对象创建对应的 <see cref="FrameworkElement"/>。
    /// </summary>
    /// <param name="icon">图标对象，可以是 <see cref="SymbolRegular"/>、<see cref="SymbolIcon"/>、<see cref="IconElement"/> 或 <see cref="FrameworkElement"/>。</param>
    /// <returns>创建的图标元素。</returns>
    public static FrameworkElement CreateIcon(object? icon)
    {
        switch (icon)
        {
            case null:
                return CreateFallbackIcon();
            case SymbolRegular symbol:
                return CreateSymbolIcon(symbol);
            case SymbolIcon symbolIcon:
                return CreateSymbolIcon(symbolIcon.Symbol, symbolIcon.FontSize, symbolIcon.Filled);
            case IconElement iconElement:
                return TryCloneIconElement(iconElement) ?? CreateUnsupportedIcon(icon.GetType());
            case FrameworkElement frameworkElement when frameworkElement.Parent is null:
                return frameworkElement;
            case FrameworkElement frameworkElement:
                Debug.WriteLine($"ModernNavigationView unsupported already-parented icon: {frameworkElement.GetType().FullName}");
                return CreateFallbackIcon();
            default:
                Debug.WriteLine($"ModernNavigationView unsupported icon type: {icon.GetType().FullName}");
                return CreateFallbackIcon();
        }
    }

    private static FrameworkElement? TryCloneIconElement(IconElement iconElement)
    {
        if (iconElement is SymbolIcon symbolIcon)
        {
            return CreateSymbolIcon(symbolIcon.Symbol, symbolIcon.FontSize, symbolIcon.Filled);
        }

        return null;
    }

    private static SymbolIcon CreateSymbolIcon(
        SymbolRegular symbol,
        double fontSize = 20D,
        bool filled = false)
    {
        var icon = new SymbolIcon(symbol, fontSize <= 0 ? 20D : fontSize, filled)
        {
            Width = 22,
            Height = 22,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        return icon;
    }

    private static FrameworkElement CreateUnsupportedIcon(Type iconType)
    {
        Debug.WriteLine($"ModernNavigationView falling back for unsupported icon type: {iconType.FullName}");
        return CreateFallbackIcon();
    }

    private static FrameworkElement CreateFallbackIcon() =>
        CreateSymbolIcon(SymbolRegular.Document24);
}
