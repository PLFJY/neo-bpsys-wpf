using neo_bpsys_wpf.Core.Enums;
using System.Globalization;
using System.Windows.Data;

namespace neo_bpsys_wpf.Converters;

/// <summary>
/// 对局进度到文字转换器<br/>
/// 比如：<see cref="GameProgress.Game1FirstHalf"/> 会变为 GAME1 FIRST HALF<br/>
/// 若传入了其它参数比如endl表示输出换行版则是 GAME1\nFIRST HALF，同时利用Bo3Mode（MultiBinding的第二个Value）来判断是输出
/// </summary>
public class GameProgressToStringConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values[0] is not GameProgress gameProgress || values[1] is not bool isBo3Mode) return Binding.DoNothing;
        string? para = null;
        if (parameter != null)
            para = parameter.ToString();

        return para == "endl"
            ? gameProgress switch
            {
                GameProgress.Free => "FREE GAME",
                GameProgress.Game1FirstHalf => "GAME 1\nFIRST HALF",
                GameProgress.Game1SecondHalf => "GAME 1\nSECOND HALF",
                GameProgress.Game2FirstHalf => "GAME 2\nFIRST HALF",
                GameProgress.Game2SecondHalf => "GAME 2\nSECOND HALF",
                GameProgress.Game3FirstHalf => "GAME 3\nFIRST HALF",
                GameProgress.Game3SecondHalf => "GAME 3\nSECOND HALF",
                GameProgress.Game4FirstHalf => isBo3Mode ? "GAME 3 EXTRA\nFIRST HALF" : "GAME 4\nFIRST HALF",
                GameProgress.Game4SecondHalf => isBo3Mode ? "GAME 3 EXTRA\nSECOND HALF" : "GAME 4\nSECOND HALF",
                GameProgress.Game5FirstHalf => "GAME 5\nFIRST HALF",
                GameProgress.Game5SecondHalf => "GAME 5\nSECOND HALF",
                GameProgress.Game5ExtraFirstHalf => "GAME 5 EXTRA\nFIRST HALF",
                GameProgress.Game5ExtraSecondHalf => "GAME 5 EXTRA\nSECOND HALF",
                _ => Binding.DoNothing,
            }
            : gameProgress switch
            {
                GameProgress.Free => "FREE GAME",
                GameProgress.Game1FirstHalf => "GAME 1 FIRST HALF",
                GameProgress.Game1SecondHalf => "GAME 1 SECOND HALF",
                GameProgress.Game2FirstHalf => "GAME 2 FIRST HALF",
                GameProgress.Game2SecondHalf => "GAME 2 SECOND HALF",
                GameProgress.Game3FirstHalf => "GAME 3 FIRST HALF",
                GameProgress.Game3SecondHalf => "GAME 3 SECOND HALF",
                GameProgress.Game4FirstHalf => isBo3Mode ? "GAME 3 EXTRA FIRST HALF" : "GAME 4 FIRST HALF",
                GameProgress.Game4SecondHalf => isBo3Mode ? "GAME 3 EXTRA SECOND HALF" : "GAME 4 SECOND HALF",
                GameProgress.Game5FirstHalf => "GAME 5 FIRST HALF",
                GameProgress.Game5SecondHalf => "GAME 5 SECOND HALF",
                GameProgress.Game5ExtraFirstHalf => "GAME 5 EXTRA FIRST HALF",
                GameProgress.Game5ExtraSecondHalf => "GAME 5 EXTRA SECOND HALF",
                _ => Binding.DoNothing,
            };
    }

    public object[] ConvertBack(object value, Type[] targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}