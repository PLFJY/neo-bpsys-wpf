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
    public class ScoreToStringConverterInBg : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not Score score) return Binding.DoNothing;

            var team = parameter.ToString();

            if(team == "Main")
            {
                return $"{App.Services.GetRequiredService<ISharedDataService>().MainTeam.Name} W:{score.Win} D:{score.Tie} 小比分:{score.MinorPoints}";
            }
            else
            {
                return $"{App.Services.GetRequiredService<ISharedDataService>().AwayTeam.Name} W:{score.Win} D:{score.Tie} 小比分:{score.MinorPoints}";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
