using neo_bpsys_wpf.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace neo_bpsys_wpf.Converters
{
    public class BooleanToTraitConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return Binding.DoNothing;

            if (parameter is not Trait trait)
                return Binding.DoNothing;

            return value.Equals(trait.TraitName);
        }

        public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b && parameter is Enums.Trait trait)
                return new Trait(trait);

            return null;
        }
    }
}
