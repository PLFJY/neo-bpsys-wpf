using neo_bpsys_wpf.Services;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

public sealed class FrontedDesignerI18nLocalizationServiceTest
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyLocalizationKeyReturnsFallback(string key)
    {
        var service = new FrontedDesignerI18nLocalizationService();

        var result = service.GetDesignerText(key, "Fallback text");

        Assert.Equal("Fallback text", result);
    }
}
