using neo_bpsys_wpf.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using static neo_bpsys_wpf.Services.FrontService;

namespace neo_bpsys_wpf.Converters
{
    public class WindowResolutionToStringConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not List<WindowResolution> windowResolutionsList) return null;

            var windowResolutionStringList  = windowResolutionsList.Select(resolution => resolution.Width == 1440 && resolution.Height == 810 ?
            $"{resolution.Width}x{resolution.Height}(默认)" :
            $"{resolution.Width}x{resolution.Height}");

            return windowResolutionStringList;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
