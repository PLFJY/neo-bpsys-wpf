using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models.Legacy;
using System.Text.Json;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>保护启动版本判定及旧配置格式。</summary>
public class SettingsConfigVersionHelperTest
{
    /// <summary>仅缺失或 null 版本进入 legacy 迁移。</summary>
    /// <param name="json">配置内容。</param>
    /// <param name="hasVersion">是否包含版本。</param>
    /// <param name="isNull">版本是否为 null。</param>
    /// <param name="version">版本值。</param>
    /// <param name="isLegacy">是否应迁移。</param>
    [Theory]
    [InlineData("{}", false, false, null, true)]
    [InlineData("{\"Version\":null}", true, true, null, true)]
    [InlineData("{\"Version\":1}", true, false, 1, false)]
    [InlineData("{\"Version\":2}", true, false, 2, false)]
    [InlineData("{\"Version\":3}", true, false, 3, false)]
    public void OnlyMissingOrNullVersionIsLegacy(string json, bool hasVersion, bool isNull, int? version, bool isLegacy)
    {
        var info = SettingsConfigVersionHelper.InspectJson(json);
        Assert.Equal(hasVersion, info.HasVersion);
        Assert.Equal(isNull, info.IsNullVersion);
        Assert.Equal(version, info.Version);
        Assert.Equal(isLegacy, info.IsLegacy);
    }

    /// <summary>旧字体与颜色数据仍可反序列化。</summary>
    [Fact]
    public void LegacySettingsDeserializesOldFrontendTextSettings()
    {
        var legacy = JsonSerializer.Deserialize<LegacySettings>(
            """
            { "BpWindowSettings": { "TextSettings": { "Timer": {
              "Color": "#FF112233", "FontFamilySite": "Arial", "FontWeight": "Bold", "FontSize": 58
            } } } }
            """,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new neo_bpsys_wpf.Converters.FontWeightJsonConverter() }
            });
        var timer = legacy?.BpWindowSettings?.TextSettings?.Timer;
        Assert.NotNull(timer);
        Assert.Equal("#FF112233", timer.Color);
        Assert.Equal("Arial", timer.FontFamilySite);
        Assert.Equal(58, timer.FontSize);
    }
}
