extern alias smartbp;

using System;
using System.Globalization;
using System.Windows;
using neo_bpsys_wpf.Tests.Controls;
using Xunit;
using SmartBpLocalizationProvider = smartbp::neo_bpsys_wpf.Helpers.SmartBpLocalizationProvider;

namespace neo_bpsys_wpf.Tests.Services;

[Collection(WpfUiCollectionDefinition.Name)]
public sealed class SmartBpModuleLocalizationProviderTest
{
    [Theory]
    [InlineData("zh-CN", "智慧 BP", "窗口捕获设置")]
    [InlineData("en-US", "Smart BP", "Window Capture Settings")]
    [InlineData("ja-JP", "スマート BP", "ウィンドウキャプチャ設定")]
    public void ResolvesResourcesFromExactModuleAssembly(
        string cultureName,
        string expectedTitle,
        string expectedCaptureSettings)
    {
        var provider = SmartBpLocalizationProvider.Instance;
        var target = new DependencyObject();
        var culture = CultureInfo.GetCultureInfo(cultureName);

        Assert.Equal(expectedTitle, provider.GetLocalizedObject("SmartBp", target, culture));
        Assert.Equal(
            expectedCaptureSettings,
            provider.GetLocalizedObject("SmartBpWindowCaptureSettings", target, culture));
    }

    [Theory]
    [InlineData("SmartBpCaptureMethodWgc")]
    [InlineData("SmartBpRecommendedProviderFormat")]
    [InlineData("SmartBpOcrStatusMissing")]
    [InlineData("SmartBpCurrentOcrModelDisabled")]
    [InlineData("SmartBpOcrModelZhCnV5MobileDisplayName")]
    public void ResolvesDynamicModuleDisplayKeys(string key)
    {
        var provider = SmartBpLocalizationProvider.Instance;
        var localized = provider.GetLocalizedObject(
            key,
            new DependencyObject(),
            CultureInfo.GetCultureInfo("zh-CN"))?.ToString();

        Assert.False(string.IsNullOrWhiteSpace(localized));
        Assert.NotEqual(key, localized);
        Assert.False(localized!.StartsWith("Key:", StringComparison.Ordinal));
    }
}
