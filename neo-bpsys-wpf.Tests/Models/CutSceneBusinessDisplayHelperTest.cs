using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Helpers;
using System.Globalization;
using System.Text.Json;
using WPFLocalizeExtension.Engine;
using Xunit;

namespace neo_bpsys_wpf.Tests.Models;

public class CutSceneBusinessDisplayHelperTest
{
    [Theory]
    [InlineData(GameProgress.Game1FirstHalf, false, "GAME 1 FIRST HALF")]
    [InlineData(GameProgress.Game1SecondHalf, false, "GAME 1 SECOND HALF")]
    [InlineData(GameProgress.Game4FirstHalf, true, "GAME 3 OVERTIME FIRST HALF")]
    [InlineData(GameProgress.Game4FirstHalf, false, "GAME 4 FIRST HALF")]
    [InlineData(GameProgress.Game5OvertimeSecondHalf, false, "GAME 5 OVERTIME SECOND HALF")]
    public void GameProgressDisplayHelperFormatsKnownProgress(
        GameProgress progress,
        bool isBo3Mode,
        string expected)
    {
        UseEnglishCulture();

        Assert.Equal(expected, GameProgressDisplayHelper.Format(progress, isBo3Mode));
    }

    [Fact]
    public void MapNameDisplayHelperLocalizesKnownMapKey()
    {
        var text = MapNameDisplayHelper.Format(Map.ChinaTown);

        Assert.False(string.IsNullOrWhiteSpace(text));
    }

    [Fact]
    public void MapNameDisplayHelperUsesEmptyTextWhenMapIsNull()
    {
        Assert.Equal("-", MapNameDisplayHelper.Format(null, "-"));
    }

    [Fact]
    public void GetParts_Game1FirstHalf_ReturnsGameNumber1AndFirstHalf()
    {
        UseEnglishCulture();

        var parts = GameProgressDisplayHelper.GetParts(
            GameProgress.Game1FirstHalf, isBo3Mode: false);

        Assert.Equal(1, parts.GameNumber);
        Assert.False(parts.IsOvertime);
        Assert.Equal(GameProgressHalf.First, parts.Half);
        Assert.Equal("GAME 1", parts.GameText);
        Assert.Equal("FIRST HALF", parts.HalfText);
        Assert.Equal("GAME 1 FIRST HALF", parts.FullText);
        Assert.False(parts.IsFree);
    }

    [Fact]
    public void GetParts_Bo3Overtime_UsesGame3Overtime()
    {
        UseEnglishCulture();

        var parts = GameProgressDisplayHelper.GetParts(
            GameProgress.Game4FirstHalf, isBo3Mode: true);

        Assert.Equal(3, parts.GameNumber);
        Assert.True(parts.IsOvertime);
        Assert.Equal(GameProgressHalf.First, parts.Half);
        Assert.Equal("GAME 3 OVERTIME", parts.GameText);
    }

    [Fact]
    public void GetParts_Bo5Game4_WhenNotBo3()
    {
        UseEnglishCulture();

        var parts = GameProgressDisplayHelper.GetParts(
            GameProgress.Game4FirstHalf, isBo3Mode: false);

        Assert.Equal(4, parts.GameNumber);
        Assert.False(parts.IsOvertime);
        Assert.Equal(GameProgressHalf.First, parts.Half);
        Assert.Equal("GAME 4", parts.GameText);
    }

    [Fact]
    public void GetParts_Free_ReturnsFree()
    {
        UseEnglishCulture();

        var parts = GameProgressDisplayHelper.GetParts(
            GameProgress.Free, isBo3Mode: false);

        Assert.True(parts.IsFree);
        Assert.Equal("FREE GAME", parts.FullText);
        Assert.Null(parts.GameNumber);
        Assert.Null(parts.Half);
    }

    [Fact]
    public void CjkNumberStyle_FormatsChineseNumeral()
    {
        var cjkCulture = CultureInfo.GetCultureInfo("zh-CN");

        var parts = GameProgressDisplayHelper.GetParts(
            GameProgress.Game1FirstHalf, isBo3Mode: false,
            culture: cjkCulture, numberStyle: GameProgressNumberStyle.CjkNumeral);

        Assert.Equal("第一局", parts.GameText);
    }

    [Fact]
    public void EnglishNumberStyle_UsesArabicNumber()
    {
        UseEnglishCulture();

        var parts = GameProgressDisplayHelper.GetParts(
            GameProgress.Game1FirstHalf, isBo3Mode: false,
            culture: CultureInfo.GetCultureInfo("en-US"),
            numberStyle: GameProgressNumberStyle.Arabic);

        Assert.Equal("GAME 1", parts.GameText);
    }

    [Theory]
    [InlineData("zh-CN", true)]
    [InlineData("ja-JP", true)]
    [InlineData("ko-KR", true)]
    [InlineData("en-US", false)]
    [InlineData("fr-FR", false)]
    public void IsCjkCulture_DetectsCorrectly(string cultureName, bool expected)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        Assert.Equal(expected, GameProgressDisplayHelper.IsCjkCulture(culture));
    }

    [Fact]
    public void GetCjkNumber_ReturnsCorrectNumerals()
    {
        Assert.Equal("一", GameProgressDisplayHelper.GetCjkNumber(1));
        Assert.Equal("二", GameProgressDisplayHelper.GetCjkNumber(2));
        Assert.Equal("三", GameProgressDisplayHelper.GetCjkNumber(3));
        Assert.Equal("四", GameProgressDisplayHelper.GetCjkNumber(4));
        Assert.Equal("五", GameProgressDisplayHelper.GetCjkNumber(5));
    }

    [Fact]
    public void GetCjkNumber_OutOfRange_FallsBackToArabic()
    {
        Assert.Equal("6", GameProgressDisplayHelper.GetCjkNumber(6));
    }

    [Fact]
    public void GameProgressTextConfig_NewProperties_RoundTrip()
    {
        var config = new GameProgressTextControlConfig
        {
            DisplayMode = GameProgressTextDisplayMode.VerticalGameAndHalf,
            VerticalLanguageMode = GameProgressVerticalLanguageMode.Upright,
            LatinVerticalMode = GameProgressLatinVerticalMode.StackCharacters,
            NumberStyle = GameProgressNumberStyle.CjkNumeral,
            DisplayLanguage = LanguageKey.zh_Hans,
            VerticalTextSpacing = 4,
            GroupSpacing = 12,
            ShowSeparator = true,
            SeparatorThickness = 2,
            SeparatorColor = "#FF000000",
            BackgroundColor = "#FF333333",
            PaddingLeft = 4,
            PaddingTop = 4,
            PaddingRight = 4,
            PaddingBottom = 4
        };

        var json = JsonSerializer.Serialize(config);
        var deserialized = JsonSerializer.Deserialize<GameProgressTextControlConfig>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(GameProgressTextDisplayMode.VerticalGameAndHalf, deserialized!.DisplayMode);
        Assert.Equal(GameProgressVerticalLanguageMode.Upright, deserialized.VerticalLanguageMode);
        Assert.Equal(GameProgressLatinVerticalMode.StackCharacters, deserialized.LatinVerticalMode);
        Assert.Equal(GameProgressNumberStyle.CjkNumeral, deserialized.NumberStyle);
        Assert.Equal(LanguageKey.zh_Hans, deserialized.DisplayLanguage);
        Assert.Equal(4, deserialized.VerticalTextSpacing);
        Assert.Equal(12, deserialized.GroupSpacing);
        Assert.True(deserialized.ShowSeparator);
        Assert.Equal(2, deserialized.SeparatorThickness);
        Assert.Equal("#FF000000", deserialized.SeparatorColor);
        Assert.Equal("#FF333333", deserialized.BackgroundColor);
    }

    [Fact]
    public void GameProgressTextConfig_VerticalDirection_RoundTrip()
    {
        var config = new GameProgressTextControlConfig
        {
            VerticalDirection = GameProgressVerticalDirection.FacingRight
        };

        var json = JsonSerializer.Serialize(config);
        var deserialized = JsonSerializer.Deserialize<GameProgressTextControlConfig>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(GameProgressVerticalDirection.FacingRight, deserialized!.VerticalDirection);
    }

    [Fact]
    public void GameProgressTextConfig_DisplayLanguage_RoundTrip()
    {
        var config = new GameProgressTextControlConfig
        {
            DisplayLanguage = LanguageKey.ja_JP
        };

        var json = JsonSerializer.Serialize(config);
        var deserialized = JsonSerializer.Deserialize<GameProgressTextControlConfig>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(LanguageKey.ja_JP, deserialized!.DisplayLanguage);
    }

    [Theory]
    [InlineData(LanguageKey.zh_Hans, "zh-CN")]
    [InlineData(LanguageKey.en_US, "en-US")]
    [InlineData(LanguageKey.ja_JP, "ja-JP")]
    [InlineData(LanguageKey.FollowApp, null)]
    public void ResolveCulture_MapsToCorrectCulture(
        LanguageKey language, string expectedName)
    {
        if (expectedName is null)
        {
            // FollowApp should return CurrentUICulture
            var culture = GameProgressDisplayHelper.ResolveCulture(language);
            Assert.Equal(CultureInfo.CurrentUICulture.Name, culture.Name);
        }
        else
        {
            var culture = GameProgressDisplayHelper.ResolveCulture(language);
            Assert.Equal(expectedName, culture.Name);
        }
    }

    private static void UseEnglishCulture()
    {
        LocalizeDictionary.Instance.Culture = CultureInfo.GetCultureInfo("en-US");
    }
}
