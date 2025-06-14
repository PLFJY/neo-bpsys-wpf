using neo_bpsys_wpf.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace neo_bpsys_wpf.Converters
{
    public class GameProgressToStringConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[0] is not GameProgress gameProgress) return Binding.DoNothing;
            if (values[1] is not bool isBo3Mode) return Binding.DoNothing;
            string? para = null;
            if (parameter != null)
                para = parameter.ToString();

            return para == "endl"
                ? gameProgress switch
                {
                    GameProgress.Free => "自由对局",
                    GameProgress.Game1FirstHalf => "GAME1\nFIRST HALF",
                    GameProgress.Game1SecondHalf => "GAME1\nSECOND HALF",
                    GameProgress.Game2FirstHalf => "GAME2\nFIRST HALF",
                    GameProgress.Game2SecondHalf => "GAME2\nSECOND HALF",
                    GameProgress.Game3FirstHalf => "GAME3\nFIRST HALF",
                    GameProgress.Game3SecondHalf => "GAME3\nSECOND HALF",
                    GameProgress.Game4FirstHalf => isBo3Mode ? "GAME3 EXTRA\nFIRST HALF" : "GAME4\nFIRST HALF",
                    GameProgress.Game4SecondHalf => isBo3Mode ? "GAME3 EXTRA\nSECOND HALF" : "GAME4\nSECOND HALF",
                    GameProgress.Game5FirstHalf => "GAME5\nFIRST HALF",
                    GameProgress.Game5SecondHalf => "GAME5\nSECOND HALF",
                    GameProgress.Game5ExtraFirstHalf => "GAME5 EXTRA\nFIRST HALF",
                    GameProgress.Game5ExtraSecondHalf => "GAME5 EXTRA\nSECOND HALF",
                    _ => Binding.DoNothing,
                }
                : gameProgress switch
            {
                GameProgress.Free => "自由对局",
                GameProgress.Game1FirstHalf => "GAME1 FIRST HALF",
                GameProgress.Game1SecondHalf => "GAME1 SECOND HALF",
                GameProgress.Game2FirstHalf => "GAME2 FIRST HALF",
                GameProgress.Game2SecondHalf => "GAME2 SECOND HALF",
                GameProgress.Game3FirstHalf => "GAME3 FIRST HALF",
                GameProgress.Game3SecondHalf => "GAME3 SECOND HALF",
                GameProgress.Game4FirstHalf => isBo3Mode ? "GAME3 EXTRA FIRST HALF" : "GAME4 FIRST HALF",
                GameProgress.Game4SecondHalf => isBo3Mode ? "GAME3 EXTRA SECOND HALF" : "GAME4 SECOND HALF",
                GameProgress.Game5FirstHalf => "GAME5 FIRST HALF",
                GameProgress.Game5SecondHalf => "GAME5 SECOND HALF",
                GameProgress.Game5ExtraFirstHalf => "GAME5 EXTRA FIRST HALF",
                GameProgress.Game5ExtraSecondHalf => "GAME5 EXTRA SECOND HALF",
                _ => Binding.DoNothing,
            };
        }

        public object[] ConvertBack(object value, Type[] targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
