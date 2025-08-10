using neo_bpsys_wpf.Controls;
using System.Globalization;
using System.Windows.Data;

namespace neo_bpsys_wpf.Converters;

/// <summary>
/// 用于转换角色切换器的Command参数，
/// </summary>
public class CharacterChangerCommandParameterConverter : IValueConverter
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="value">CharacterChanger控件</param>
    /// <param name="targetType"></param>
    /// <param name="parameter">指定了的将自身Index转为ButtonContent的Converter，用于判断自身Index和目标Index</param>
    /// <param name="culture"></param>
    /// <returns>返回一个CharacterChangerCommandParameter对象，Source是自身index，Target是点击的按钮所在的Index，但是由于我们无法为按钮直接设置Name，所以通过判断其Content的Converter来判断目标的Index</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not CharacterChanger characterChanger || parameter is not IndexToButtonContentConverter contentConverter) return Binding.DoNothing;
        var index = characterChanger.Index;
        var buttonContent = (int)contentConverter.Convert(index, typeof(int), parameter, culture);
        return new CharacterChangerCommandParameter(index, buttonContent - 1);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}