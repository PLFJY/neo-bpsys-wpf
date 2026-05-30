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
        Canvas.SetLeft(border, config.Left);
        Canvas.SetTop(border, config.Top);
        Panel.SetZIndex(border, config.ZIndex);

        if (config.Width is not null)
        {
            border.Width = config.Width.Value;
        }

        if (config.Height is not null)
        {
            border.Height = config.Height.Value;
        }

        return border;
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
