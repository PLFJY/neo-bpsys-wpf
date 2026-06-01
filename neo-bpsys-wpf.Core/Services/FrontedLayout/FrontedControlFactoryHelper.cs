using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

internal static class FrontedControlFactoryHelper
{
    public static Border CreateOuterBorder(string name, FrontedControlConfigBase config)
    {
        var border = new Border { Name = name };
        ApplyCanvasLayout(border, config);

        return border;
    }

    public static void ApplyCanvasLayout(FrameworkElement element, FrontedControlConfigBase config)
    {
        Canvas.SetLeft(element, config.Left);
        Canvas.SetTop(element, config.Top);
        Panel.SetZIndex(element, config.ZIndex);

        if (config.Width is not null)
        {
            element.Width = config.Width.Value;
        }

        if (config.Height is not null)
        {
            element.Height = config.Height.Value;
        }
    }

    public static void TryApplyEnum<T>(
        string? value,
        Action<T> apply,
        FrontedControlBuildContext context,
        string propertyName)
        where T : struct
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (Enum.TryParse<T>(value, true, out var result))
        {
            apply(result);
            return;
        }

        context.Logger?.LogWarning(
            "Invalid fronted control enum value. Property: {PropertyName}, Value: {Value}",
            propertyName,
            value);
    }

    public static void TryApplyTypeConverter<T>(
        string? value,
        Action<T> apply,
        FrontedControlBuildContext context,
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
            context.Logger?.LogWarning(
                ex,
                "Invalid fronted control style value. Property: {PropertyName}, Value: {Value}",
                propertyName,
                value);
        }
    }
}
