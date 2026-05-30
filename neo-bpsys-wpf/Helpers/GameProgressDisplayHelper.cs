using neo_bpsys_wpf.Core.Enums;
using System.Globalization;

namespace neo_bpsys_wpf.Helpers;

/// <summary>
/// 集中生成前台可见的对局进度文本。
/// </summary>
public static class GameProgressDisplayHelper
{
    public static string Format(GameProgress progress, bool isBo3Mode, bool useLineBreak = false)
    {
        return (int)progress switch
        {
            -1 => I18nHelper.GetLocalizedString("GameProgressFree"),
            0 => FormatHalf(1, isOvertime: false, "FirstHalf", useLineBreak),
            1 => FormatHalf(1, isOvertime: false, "SecondHalf", useLineBreak),
            2 => FormatHalf(2, isOvertime: false, "FirstHalf", useLineBreak),
            3 => FormatHalf(2, isOvertime: false, "SecondHalf", useLineBreak),
            4 => FormatHalf(3, isOvertime: false, "FirstHalf", useLineBreak),
            5 => FormatHalf(3, isOvertime: false, "SecondHalf", useLineBreak),
            6 => isBo3Mode
                ? FormatHalf(3, isOvertime: true, "FirstHalf", useLineBreak)
                : FormatHalf(4, isOvertime: false, "FirstHalf", useLineBreak),
            7 => isBo3Mode
                ? FormatHalf(3, isOvertime: true, "SecondHalf", useLineBreak)
                : FormatHalf(4, isOvertime: false, "SecondHalf", useLineBreak),
            8 => FormatHalf(5, isOvertime: false, "FirstHalf", useLineBreak),
            9 => FormatHalf(5, isOvertime: false, "SecondHalf", useLineBreak),
            10 => FormatHalf(5, isOvertime: true, "FirstHalf", useLineBreak),
            11 => FormatHalf(5, isOvertime: true, "SecondHalf", useLineBreak),
            _ => string.Empty
        };
    }

    private static string FormatHalf(int gameNumber, bool isOvertime, string halfKey, bool useLineBreak)
    {
        var halfText = I18nHelper.GetLocalizedString(halfKey);

        if (useLineBreak)
        {
            var gameText = isOvertime
                ? string.Format(
                    CultureInfo.CurrentUICulture,
                    I18nHelper.GetLocalizedString("GameProgressGameOvertimeOnlyFormat"),
                    gameNumber)
                : string.Format(
                    CultureInfo.CurrentUICulture,
                    I18nHelper.GetLocalizedString("GameProgressGameOnlyFormat"),
                    gameNumber);

            return $"{gameText}\n{halfText}";
        }

        var formatKey = isOvertime
            ? "GameProgressGameOvertimeHalfFormat"
            : "GameProgressGameHalfFormat";

        return string.Format(
            CultureInfo.CurrentUICulture,
            I18nHelper.GetLocalizedString(formatKey),
            gameNumber,
            halfText);
    }
}
