using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace neo_bpsys_wpf.Controls.FrontedLayout;

internal static class CutSceneFrontedControlHelper
{
    public static Border CreateOuterBorder(string name, FrontedControlConfigBase config)
    {
        var border = new Border { Name = name };
        Canvas.SetLeft(border, config.Left);
        Canvas.SetTop(border, config.Top);
        Panel.SetZIndex(border, config.ZIndex);

        if (config.Width.HasValue)
        {
            border.Width = config.Width.Value;
        }

        if (config.Height.HasValue)
        {
            border.Height = config.Height.Value;
        }

        return border;
    }

    public static void ApplyTextStyle(
        TextBlock textBlock,
        string? horizontalAlignment,
        string? verticalAlignment,
        string? textAlignment,
        string? fontFamily,
        string? fontWeight,
        string? color,
        double fontSize,
        ILogger? logger)
    {
        TryApplyEnum<HorizontalAlignment>(
            horizontalAlignment,
            value => textBlock.HorizontalAlignment = value,
            logger,
            nameof(horizontalAlignment));
        TryApplyEnum<VerticalAlignment>(
            verticalAlignment,
            value => textBlock.VerticalAlignment = value,
            logger,
            nameof(verticalAlignment));
        TryApplyEnum<TextAlignment>(
            textAlignment,
            value => textBlock.TextAlignment = value,
            logger,
            nameof(textAlignment));
        TryApplyTypeConverter<FontWeight>(
            fontWeight,
            value => textBlock.FontWeight = value,
            logger,
            nameof(fontWeight));
        TryApplyTypeConverter<Brush>(
            color,
            value => textBlock.Foreground = value,
            logger,
            nameof(color));

        if (!string.IsNullOrWhiteSpace(fontFamily))
        {
            textBlock.FontFamily = fontFamily.Contains("pack://application:,,,")
                ? new FontFamily(new Uri(fontFamily[..fontFamily.IndexOf('#')]), "./" + fontFamily[fontFamily.IndexOf('#')..])
                : new FontFamily(fontFamily);
        }

        if (fontSize > 0)
        {
            textBlock.FontSize = fontSize;
        }
    }

    public static void TryApplyEnum<T>(
        string? value,
        Action<T> apply,
        ILogger? logger,
        string propertyName)
        where T : struct
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (Enum.TryParse(value, true, out T result))
        {
            apply(result);
            return;
        }

        logger?.LogWarning(
            "Invalid CutScene fronted control enum value. Property: {PropertyName}, Value: {Value}",
            propertyName,
            value);
    }

    private static void TryApplyTypeConverter<T>(
        string? value,
        Action<T> apply,
        ILogger? logger,
        string propertyName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        try
        {
            var converter = TypeDescriptor.GetConverter(typeof(T));
            if (converter.ConvertFromString(value) is T result)
            {
                apply(result);
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(
                ex,
                "Invalid CutScene fronted control style value. Property: {PropertyName}, Value: {Value}",
                propertyName,
                value);
        }
    }
}
