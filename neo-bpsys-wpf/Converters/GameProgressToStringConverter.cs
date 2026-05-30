using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Helpers;
using System.Globalization;
using System.Windows.Data;

namespace neo_bpsys_wpf.Converters;

/// <summary>
/// 对局进度到文字转换器。
/// </summary>
public class GameProgressToStringConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values[0] is not GameProgress gameProgress || values[1] is not bool isBo3Mode)
        {
            return Binding.DoNothing;
        }

        var text = GameProgressDisplayHelper.Format(
            gameProgress,
            isBo3Mode,
            parameter?.ToString() == "endl");

        return string.IsNullOrEmpty(text) ? Binding.DoNothing : text;
    }

    public object[] ConvertBack(object value, Type[] targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
