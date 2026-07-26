using neo_bpsys_wpf.Core.Models.FrontedLayout.Binding;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 格式化设计器 v3 文本 MultiBinding 的有序值。
/// </summary>
public sealed class FrontedTextMultiBindingConverter : IMultiValueConverter
{
    /// <inheritdoc />
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var expression = parameter as FrontedTextBindingExpression ?? new FrontedTextBindingExpression();
        var effectiveCulture = culture == CultureInfo.InvariantCulture
            ? CultureInfo.CurrentCulture
            : culture;

        if (values.Any(IsUnavailable))
        {
            return !string.IsNullOrEmpty(expression.FallbackText)
                ? expression.FallbackText
                : Join(values, expression, effectiveCulture);
        }

        var arguments = values
            .Select(value => value is null ? expression.NullText ?? string.Empty : value)
            .ToArray();

        if (string.IsNullOrEmpty(expression.StringFormat))
        {
            return Join(arguments, expression, effectiveCulture);
        }

        try
        {
            return string.Format(effectiveCulture, expression.StringFormat, arguments);
        }
        catch (FormatException)
        {
            return !string.IsNullOrEmpty(expression.FallbackText)
                ? expression.FallbackText
                : Join(arguments, expression, effectiveCulture);
        }
    }

    /// <inheritdoc />
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        targetTypes.Select(_ => Binding.DoNothing).ToArray();

    private static bool IsUnavailable(object? value) =>
        value == DependencyProperty.UnsetValue || value == Binding.DoNothing;

    private static string Join(
        IEnumerable<object?> values,
        FrontedTextBindingExpression expression,
        CultureInfo culture)
    {
        return string.Join(
            expression.JoinSeparator ?? string.Empty,
            values.Select(value =>
            {
                if (IsUnavailable(value))
                {
                    return expression.NullText ?? string.Empty;
                }

                return value is null
                    ? expression.NullText ?? string.Empty
                    : System.Convert.ToString(value, culture) ?? expression.NullText ?? string.Empty;
            }));
    }
}
