#nullable enable

using System.Globalization;
using System;
using System.Collections.Generic;
using System.Text.Json;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Registrations;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Core.Services.Registry;
using Xunit;

namespace neo_bpsys_wpf.Tests.Models;

public sealed class CustomV3WindowContractTest
{
    [Fact]
    public void CustomCanonicalIdRoundTripsThroughLayoutPath()
    {
        const string canonicalId = "custom:example-layout/custom-window";

        var relativePath = FrontedV3LayoutWindowPathHelper.GetLayoutRelativePath(canonicalId);

        Assert.Equal("custom/example-layout/custom-window.json", relativePath.Replace('\\', '/'));
        Assert.Equal(
            canonicalId,
            FrontedV3LayoutWindowPathHelper.ToCanonicalWindowIdFromLayoutRelativePath(relativePath));
    }

    [Theory]
    [InlineData("v3.0.7+git-hash", "v3.0.7", 0)]
    [InlineData("V3.0.8-preview.1", "v3.0.7", 1)]
    [InlineData("v3.0.6-beta", "3.0.7+git-hash", -1)]
    public void CreatedVersionComparisonUsesNumericVersionOnly(
        string left,
        string right,
        int expectedSign)
    {
        var actual = Math.Sign(FrontedPackageVersionComparer.CompareNumeric(left, right));

        Assert.Equal(expectedSign, actual);
    }

    [Fact]
    public void DisplayNamesSerializeAtLayoutRootAndUseRequiredFallbackOrder()
    {
        var config = new FrontedWindowConfig
        {
            DisplayNames = new()
            {
                ["en_US"] = "Custom Window",
                ["ja_JP"] = "カスタムウィンドウ"
            }
        };

        var json = JsonSerializer.Serialize(config);
        var restored = JsonSerializer.Deserialize<FrontedWindowConfig>(json)!;

        Assert.Equal(
            "Custom Window",
            FrontedWindowDisplayNameResolver.ResolveDisplayName(
                restored.DisplayNames,
                LanguageKey.zh_Hans,
                CultureInfo.GetCultureInfo("zh-CN"),
                "custom-window"));
        Assert.Contains("DisplayNames", json, StringComparison.Ordinal);
    }

    [Fact]
    public void RegistryCanReplaceOnlyDynamicCustomWindowSet()
    {
        var builtIn = new FrontedV3LayoutWindowRegistration
        {
            Id = "BpWindow",
            LocalId = "BpWindow",
            IsBuiltIn = true,
            DisplayName = "BP"
        };
        var custom = new FrontedCustomV3LayoutWindowRegistration
        {
            Id = "custom:example-layout/custom-window",
            LocalId = "custom-window",
            PackageScopeId = "example-layout",
            IsBuiltIn = false,
            DisplayName = "Custom Window"
        };
        var registry = new FrontedWindowRegistryService([builtIn]);

        registry.ReplaceCustomWindows([custom]);

        Assert.True(registry.TryGet(custom.Id, out var registration));
        Assert.Same(custom, registration);
        Assert.Single(registry.GetV3LayoutWindows(), item => item.Id == custom.Id);

        registry.ReplaceCustomWindows([]);

        Assert.False(registry.TryGet(custom.Id, out _));
        Assert.True(registry.TryGet(builtIn.Id, out _));
    }

    [Fact]
    public void CustomRegistrationDisplayNameUsesRequestedLanguage()
    {
        var registration = new FrontedCustomV3LayoutWindowRegistration
        {
            Id = "custom:example-layout/custom-window",
            LocalId = "custom-window",
            PackageScopeId = "example-layout",
            IsBuiltIn = false,
            DisplayName = "自定义窗口",
            DisplayNames = new Dictionary<string, string>
            {
                ["zh_Hans"] = "自定义窗口",
                ["en_US"] = "Custom Window"
            }
        };

        var displayName = FrontedWindowDisplayNameResolver.ResolveDisplayName(
            registration,
            LanguageKey.en_US,
            CultureInfo.GetCultureInfo("en-US"));

        Assert.Equal("Custom Window", displayName);
    }

    [Theory]
    [InlineData(".")]
    [InlineData("CON")]
    [InlineData("window.")]
    public void UnsafeWindowsPathSegmentsAreRejected(string windowId)
    {
        Assert.False(FrontedV3LayoutWindowPathHelper.IsSafePathSegment(windowId));
        Assert.Throws<ArgumentException>(() =>
            FrontedWindowIdentity.BuildCustomCanonicalId("example-layout", windowId));
    }
}
