using neo_bpsys_wpf.Core.Services.FrontedLayout;
using System;
using Xunit;

namespace neo_bpsys_wpf.Tests.Models;

public class FrontedV3LayoutWindowIdentityTest
{
    [Fact]
    public void BuildCanonicalId_BuiltIn_ReturnsLocalId()
    {
        Assert.Equal(
            "BpWindow",
            FrontedV3LayoutWindowIdentity.BuildCanonicalId("BpWindow", packageId: null, isBuiltIn: true));
    }

    [Fact]
    public void BuildCanonicalId_PluginA_PrefixedWithPluginAndPackage()
    {
        Assert.Equal(
            "plugin:a/Overlay",
            FrontedV3LayoutWindowIdentity.BuildCanonicalId("Overlay", "a", isBuiltIn: false));
    }

    [Fact]
    public void BuildCanonicalId_PluginB_PrefixedWithPluginAndPackage()
    {
        Assert.Equal(
            "plugin:b/Overlay",
            FrontedV3LayoutWindowIdentity.BuildCanonicalId("Overlay", "b", isBuiltIn: false));
    }

    [Fact]
    public void BuildCanonicalId_BuiltInIgnoresPackageIdEvenWhenProvided()
    {
        Assert.Equal(
            "BpWindow",
            FrontedV3LayoutWindowIdentity.BuildCanonicalId("BpWindow", "some-package", isBuiltIn: true));
    }

    [Fact]
    public void BuildCanonicalId_NullPackageIdForNonBuiltIn_ReturnsLocalId()
    {
        Assert.Equal(
            "Overlay",
            FrontedV3LayoutWindowIdentity.BuildCanonicalId("Overlay", packageId: null, isBuiltIn: false));
    }

    [Theory]
    [InlineData("plugin:x/y")]
    [InlineData("../escape")]
    [InlineData("a.b")]
    [InlineData("a\\b")]
    [InlineData("a:b")]
    [InlineData("")]
    public void EnsureValidLocalWindowId_RejectsInvalidIds(string localWindowId)
    {
        Assert.False(FrontedV3LayoutWindowIdValidator.IsValidLocalWindowId(localWindowId));
        Assert.Throws<ArgumentException>(
            () => FrontedV3LayoutWindowIdValidator.EnsureValidLocalWindowId(localWindowId));
    }

    [Fact]
    public void EnsureValidLocalWindowId_RejectsNull()
    {
        Assert.False(FrontedV3LayoutWindowIdValidator.IsValidLocalWindowId(null!));
        Assert.Throws<ArgumentException>(
            () => FrontedV3LayoutWindowIdValidator.EnsureValidLocalWindowId(null!));
    }

    [Theory]
    [InlineData("BpWindow")]
    [InlineData("ExampleOverlay")]
    [InlineData("a-b_c")]
    public void EnsureValidLocalWindowId_AcceptsValidIds(string localWindowId)
    {
        Assert.True(FrontedV3LayoutWindowIdValidator.IsValidLocalWindowId(localWindowId));
        FrontedV3LayoutWindowIdValidator.EnsureValidLocalWindowId(localWindowId);
    }

    [Fact]
    public void EnsureValidLocalWindowId_ExceptionMessageContainsValueAndReason()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => FrontedV3LayoutWindowIdValidator.EnsureValidLocalWindowId("a.b"));

        Assert.Contains("a.b", exception.Message, StringComparison.Ordinal);
        Assert.Contains(".", exception.Message, StringComparison.Ordinal);
    }
}
