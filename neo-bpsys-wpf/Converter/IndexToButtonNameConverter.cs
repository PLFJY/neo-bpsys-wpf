using System;
using System.Globalization;
using System.Windows.Data;

namespace neo_bpsys_wpf.Converter
{
    /// <summary>
    /// 使CharacterChanger的Index转换为Button的名称，
    ///  <para>当Index = 0，三个按钮的Name分别叫做：Button1、Button2、Button3，</para>
    ///  <para>当Index = 1，三个按钮的Name分别叫做：Button0、Button2、Button3，</para>
    ///  <para>当Index = 2，三个按钮的Name分别叫做：Button0、Button1、Button3，</para>
    ///  <para>当Index = 3，三个按钮的Name分别叫做：Button0、Button1、Button2；</para>
    /// </summary>
    public class IndexToButtonNameConverter : IValueConverter
    {
        public int ButtonIndex { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int index = (int)value;
            switch (index)
            {
                case 0:
                    return $"Button{ButtonIndex}";
                case 1:
                    return ButtonIndex == 1 ? "Button0" : $"Button{ButtonIndex}";
                case 2:
                    return ButtonIndex == 3 ? "Button3" : $"Button{ButtonIndex - 1}";
                case 3:
                    return $"Button{ButtonIndex - 1}";
                default:
                    return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}