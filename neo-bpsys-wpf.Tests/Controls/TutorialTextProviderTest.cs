using System.Globalization;
using neo_bpsys_wpf.ProductTour;
using WPFLocalizeExtension.Engine;
using Xunit;

namespace neo_bpsys_wpf.Tests.Controls;

[Collection(WpfUiCollectionDefinition.Name)]
public sealed class TutorialTextProviderTest
{
    [Theory]
    [InlineData("zh-CN", "完成", "跳过首次导览？")]
    [InlineData("en-US", "Finish", "Skip the first-run tour?")]
    public void ResolvesProductTourTextFromItsOwnAssembly(
        string cultureName,
        string expectedFinish,
        string expectedSkipTitle)
    {
        var previousCulture = LocalizeDictionary.Instance.Culture;

        try
        {
            LocalizeDictionary.Instance.Culture = CultureInfo.GetCultureInfo(cultureName);
            var provider = new DefaultTutorialTextProvider();

            Assert.Equal(expectedFinish, provider.Finish);
            Assert.Equal(expectedSkipTitle, provider.SkipConfirmTitle);
            Assert.DoesNotContain("Confirm", provider.SkipConfirmDescription);
        }
        finally
        {
            LocalizeDictionary.Instance.Culture = previousCulture;
        }
    }
}
