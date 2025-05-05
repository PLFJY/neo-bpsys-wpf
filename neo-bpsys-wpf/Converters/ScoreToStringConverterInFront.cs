using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Models;
using neo_bpsys_wpf.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace neo_bpsys_wpf.Converters
{
    public class ScoreToStringConverterInFront : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not Score score) return Binding.DoNothing;

            return $"W:{score.Win} D:{score.Tie}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
