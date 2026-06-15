using neo_bpsys_wpf.Core.Enums;
using System.Globalization;
using WPFLocalizeExtension.Engine;

namespace neo_bpsys_wpf.Helpers;

/// <summary>
/// 集中生成前台可见的对局进度文本。
/// </summary>
public static class GameProgressDisplayHelper
{
    internal static Func<CultureInfo?> CurrentAppCultureProvider { get; set; } =
        () => LocalizeDictionary.CurrentCulture;

    /// <summary>
    /// CJK 数字映射表。
    /// </summary>
    private static readonly string[] CjkNumerals = ["一", "二", "三", "四", "五"];

    /// <summary>
    /// 格式化对局进度为完整的单行文本（向后兼容）。
    /// </summary>
    /// <param name="progress">对局进度枚举值。</param>
    /// <param name="isBo3Mode">是否为 BO3 模式。</param>
    /// <returns>格式化后的对局进度文本。</returns>
    public static string Format(GameProgress progress, bool isBo3Mode)
    {
        if ((int)progress < -1 || (int)progress > 11)
        {
            return string.Empty;
        }

        return GetParts(
                progress,
                isBo3Mode,
                ResolveCulture(LanguageKey.FollowApp),
                GameProgressNumberStyle.Arabic)
            .FullText;
    }

    /// <param name="language">语言键。</param>
    /// <returns>对应的文化信息。</returns>
    /// <summary>
    /// 根据 <see cref="LanguageKey"/> 解析为目标 <see cref="CultureInfo"/>。
    /// </summary>
    public static CultureInfo ResolveCulture(LanguageKey language)
    {
        return ResolveCulture(language, CurrentAppCultureProvider());
    }

    /// <summary>
    /// 根据 <see cref="LanguageKey"/> 和当前应用文化解析为目标 <see cref="CultureInfo"/>。
    /// </summary>
    /// <param name="language">语言键。</param>
    /// <param name="appCulture">当前应用文化；<see cref="LanguageKey.FollowApp"/> 时优先使用。</param>
    /// <returns>对应的文化信息。</returns>
    public static CultureInfo ResolveCulture(LanguageKey language, CultureInfo? appCulture)
    {
        return language switch
        {
            LanguageKey.zh_Hans => CultureInfo.GetCultureInfo("zh-CN"),
            LanguageKey.en_US => CultureInfo.GetCultureInfo("en-US"),
            LanguageKey.ja_JP => CultureInfo.GetCultureInfo("ja-JP"),
            _ => appCulture ?? CultureInfo.CurrentUICulture
        };
    }

    /// <summary>
    /// 获取对局进度的结构化部件。
    /// </summary>
    /// <param name="progress">对局进度枚举。</param>
    /// <param name="isBo3Mode">是否为 BO3 模式。</param>
    /// <param name="culture">目标文化。为 null 时使用当前 UI 文化。</param>
    /// <param name="numberStyle">数字风格。为 Auto 时根据文化自动选择。</param>
    /// <returns>结构化部件。</returns>
    public static GameProgressDisplayParts GetParts(
        GameProgress progress,
        bool isBo3Mode,
        CultureInfo? culture = null,
        GameProgressNumberStyle numberStyle = GameProgressNumberStyle.Auto)
    {
        culture ??= CultureInfo.CurrentUICulture;
        numberStyle = ResolveNumberStyle(numberStyle, culture);

        if ((int)progress == -1)
        {
            var freeText = I18nHelper.GetLocalizedString("GameProgressFree", culture);
            return new GameProgressDisplayParts
            {
                Progress = progress,
                IsFree = true,
                FullText = freeText
            };
        }

        var (gameNumber, isOvertime, half) = GetGameInfo(progress, isBo3Mode);
        var gameNumberStr = GetNumberText(gameNumber, numberStyle);

        var gameText = isOvertime
            ? string.Format(
                culture,
                I18nHelper.GetLocalizedString("GameProgressGameOvertimeOnlyFormat", culture),
                gameNumberStr)
            : string.Format(
                culture,
                I18nHelper.GetLocalizedString("GameProgressGameOnlyFormat", culture),
                gameNumberStr);

        var halfKey = half == GameProgressHalf.First ? "FirstHalf" : "SecondHalf";
        var halfText = GetHalfText(halfKey, culture);

        var fullText = isOvertime
            ? string.Format(
                culture,
                I18nHelper.GetLocalizedString("GameProgressGameOvertimeHalfFormat", culture),
                gameNumberStr,
                halfText)
            : string.Format(
                culture,
                I18nHelper.GetLocalizedString("GameProgressGameHalfFormat", culture),
                gameNumberStr,
                halfText);

        return new GameProgressDisplayParts
        {
            Progress = progress,
            GameNumber = gameNumber,
            IsOvertime = isOvertime,
            Half = half,
            GameText = gameText,
            HalfText = halfText,
            FullText = fullText
        };
    }

    /// <summary>
    /// 判断指定文化是否为 CJK 文化（中文、日文、韩文）。
    /// </summary>
    public static bool IsCjkCulture(CultureInfo culture)
    {
        var twoLetter = culture.TwoLetterISOLanguageName;
        return twoLetter is "zh" or "ja" or "ko";
    }

    /// <summary>
    /// 将数字转换为 CJK 数字（一、二、三、四、五）。
    /// </summary>
    /// <param name="number">要转换的数字。</param>
    /// <returns>CJK 数字字符串，超过范围则返回阿拉伯数字字符串。</returns>
    public static string GetCjkNumber(int number)
    {
        if (number >= 1 && number <= 5)
        {
            return CjkNumerals[number - 1];
        }

        return number.ToString(CultureInfo.InvariantCulture);
    }

    private static string GetHalfText(string halfKey, CultureInfo culture)
    {
        var halfText = I18nHelper.GetLocalizedString(halfKey, culture);
        if (halfText != halfKey)
        {
            return halfText;
        }

        return halfKey switch
        {
            "FirstHalf" => "FIRST HALF",
            "SecondHalf" => "SECOND HALF",
            _ => halfText
        };
    }

    private static (int gameNumber, bool isOvertime, GameProgressHalf half) GetGameInfo(
        GameProgress progress, bool isBo3Mode)
    {
        return (int)progress switch
        {
            0 => (1, false, GameProgressHalf.First),
            1 => (1, false, GameProgressHalf.Second),
            2 => (2, false, GameProgressHalf.First),
            3 => (2, false, GameProgressHalf.Second),
            4 => (3, false, GameProgressHalf.First),
            5 => (3, false, GameProgressHalf.Second),
            6 => isBo3Mode ? (3, true, GameProgressHalf.First) : (4, false, GameProgressHalf.First),
            7 => isBo3Mode ? (3, true, GameProgressHalf.Second) : (4, false, GameProgressHalf.Second),
            8 => (5, false, GameProgressHalf.First),
            9 => (5, false, GameProgressHalf.Second),
            10 => (5, true, GameProgressHalf.First),
            11 => (5, true, GameProgressHalf.Second),
            _ => (0, false, GameProgressHalf.First)
        };
    }

    private static GameProgressNumberStyle ResolveNumberStyle(
        GameProgressNumberStyle style, CultureInfo culture)
    {
        if (style != GameProgressNumberStyle.Auto)
        {
            return style;
        }

        return IsCjkCulture(culture)
            ? GameProgressNumberStyle.CjkNumeral
            : GameProgressNumberStyle.Arabic;
    }

    private static string GetNumberText(int number, GameProgressNumberStyle style)
    {
        return style switch
        {
            GameProgressNumberStyle.CjkNumeral => GetCjkNumber(number),
            _ => number.ToString(CultureInfo.InvariantCulture)
        };
    }
}
