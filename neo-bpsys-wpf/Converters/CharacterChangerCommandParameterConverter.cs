using neo_bpsys_wpf.CustomControls;
using System.Globalization;
using System.Windows.Data;

namespace neo_bpsys_wpf.Converters
{
    public class CharacterChangerCommandParameterConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var characterChanger = value as CharacterChanger;
            var contentConverter = parameter as IndexToButtonContentConverter;
            if (characterChanger != null && contentConverter != null)
            {
                int index = characterChanger.Index;
                int buttonContent = (int)contentConverter.Convert(index, typeof(int), parameter, culture);
                return new CharacterChangerCommandParameter(index, buttonContent - 1);
            }
            return Binding.DoNothing;
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
