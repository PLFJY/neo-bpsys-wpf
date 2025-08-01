using System.Globalization;
using System.Windows.Data;
using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Converters;

/// <summary>
/// 阵营枚举转换成中文，用于TeamInfoPage
/// </summary>
public class CampToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not Camp camp)
            return string.Empty;

        var campWord = camp == Camp.Sur ? "求生者" : "监管者";

        return campWord;
    }

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture
    )
    {
        throw new NotImplementedException();
    }
}