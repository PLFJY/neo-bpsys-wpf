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

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int controlIndex = (int)value;
            var buttonName = GetButtonName(controlIndex);
            if (buttonName == null)
                return Binding.DoNothing;
            return buttonName;
        }

        private int? GetButtonName(int controlIndex)
        {
            switch (controlIndex)
            {
                case 0:
                    if (ButtonIndex == 1)
                        return 2;
                    else if (ButtonIndex == 2)
                        return 3;
                    else if (ButtonIndex == 3)
                        return 4;
                    else
                        return null;
                case 1:
                    if (ButtonIndex == 1)
                        return 1;
                    else if (ButtonIndex == 2)
                        return 3;
                    else if (ButtonIndex == 3)
                        return 4;
                    else
                        return null;
                case 2:
                    if (ButtonIndex == 1)
                        return 1;
                    else if (ButtonIndex == 2)
                        return 2;
                    else if (ButtonIndex == 3)
                        return 4;
                    else
                        return null;
                case 3:
                    if (ButtonIndex == 1)
                        return 1;
                    else if (ButtonIndex == 2)
                        return 2;
                    else if (ButtonIndex == 3)
                        return 3;
                    else
                        return null;
                default:
                    return null;
            }
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
}
