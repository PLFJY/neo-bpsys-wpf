using System;
using System.Globalization;
using System.Windows.Data;

namespace neo_bpsys_wpf.Converters
{
    /// <summary>
    /// 将Index转换为CharacterChanger的Button Content，对应Button的Nmae的数字-1
    /// </summary>
    public class IndexToButtonContentConverter : IValueConverter
    {
        public int ButtonIndex { get; set; }

        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var buttonName = new IndexToButtonNameConverter { ButtonIndex = ButtonIndex }.Convert(value, targetType, parameter, culture).ToString();
            if (buttonName == null) return null;
            int number = int.Parse(buttonName.Substring(6));
            return number + 1;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}