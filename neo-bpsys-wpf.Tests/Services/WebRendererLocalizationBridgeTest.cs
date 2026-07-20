using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Services;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>验证 Web Renderer 本地化桥接只发布主程序 Helper 的最终结果。</summary>
public sealed class WebRendererLocalizationBridgeTest
{
    /// <summary>任意宿主字典解析必须使用显式 culture，且快照保留 revision。</summary>
    [Theory]
    [InlineData("zh-CN")]
    [InlineData("en-US")]
    [InlineData("ja-JP")]
    public void SnapshotUsesExplicitCultureAndRevision(string cultureName)
    {
        var culture = System.Globalization.CultureInfo.GetCultureInfo(cultureName);
        var bridge = new WebRendererLocalizationBridge();
        var key = "GameProgressFree";
        var expected = I18nHelper.GetLocalizedStringFromAnyHostDictionary(key, culture);

        var state = bridge.ResolveLocalizedControl("control:test:free", key, null, culture);
        var snapshot = bridge.Create([new WebLocalizationRequest("control:test:free", key, null)], culture, 42);

        Assert.Equal(expected, state.DisplayText);
        Assert.Equal(42, snapshot.Revision);
        Assert.Equal(culture.Name, snapshot.Culture);
        Assert.Equal(expected, snapshot.StaticTexts["control:test:free"]);
    }

    /// <summary>地图、阵营和 GameProgress DTO 不重新实现翻译或业务文案。</summary>
    [Fact]
    public void DisplayProjectionsMatchAuthoritativeHelpers()
    {
        var culture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
        var bridge = new WebRendererLocalizationBridge();
        var map = Map.ArmsFactory;

        var mapState = bridge.ResolveMapName("control:test:map", map, null, culture);
        var parts = GameProgressDisplayHelper.GetParts(GameProgress.Game1FirstHalf, false, culture, GameProgressNumberStyle.Arabic);
        var progress = bridge.CreateGameProgress(GameProgress.Game1FirstHalf, false, LanguageKey.FollowApp, GameProgressNumberStyle.Arabic, culture);

        Assert.Equal(MapNameDisplayHelper.Format(map, null, culture), mapState.DisplayText);
        Assert.Equal(I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, Camp.Sur.ToString(), culture), bridge.ResolveCamp(Camp.Sur, culture));
        Assert.Equal(parts.FullText, progress.FullText);
        Assert.Equal(parts.GameText, progress.GameText);
        Assert.Equal(parts.HalfText, progress.HalfText);
        Assert.True(progress.IsValid);
    }

    /// <summary>缺失 key 有 fallback 时只显示后端 fallback。</summary>
    [Fact]
    public void MissingKeyUsesBackendFallback()
    {
        var bridge = new WebRendererLocalizationBridge();
        var state = bridge.ResolveLocalizedControl("control:test:missing", "WebRendererMissingKey", "fallback", System.Globalization.CultureInfo.GetCultureInfo("en-US"));

        Assert.Equal("fallback", state.DisplayText);
    }
}
