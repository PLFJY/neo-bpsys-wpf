using neo_bpsys_wpf.Core.Models.FrontedLayout.Registrations;
using neo_bpsys_wpf.Core.Services.Registry;
using System;
using System.Collections.Generic;
using System.Windows;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// 验证 <see cref="FrontedWindowRegistryService"/> 在构造时对重复 Canonical ID 的 fail-fast 行为。
/// </summary>
public class FrontedWindowRegistryServiceTest
{
    [Fact]
    public void Constructor_DuplicateCanonicalId_ThrowsInvalidOperationExceptionWithConflictingId()
    {
        const string duplicateId = "plugin:top.plfjy.test/Overlay";
        var first = CreateV3Registration(duplicateId, "FirstWindow");
        var second = CreateV3Registration(duplicateId, "SecondWindow");

        var registrations = new List<FrontedWindowRegistration> { first, second };

        var ex = Assert.Throws<InvalidOperationException>(
            () => new FrontedWindowRegistryService(registrations));

        Assert.Contains(duplicateId, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_DuplicateCanonicalIdAcrossKinds_ThrowsInvalidOperationExceptionWithConflictingId()
    {
        const string duplicateId = "BpWindow";
        var first = CreateV3Registration(duplicateId, "BpWindow");
        var second = CreateXamlRegistration(duplicateId, "BpWindowXaml");

        var registrations = new List<FrontedWindowRegistration> { first, second };

        var ex = Assert.Throws<InvalidOperationException>(
            () => new FrontedWindowRegistryService(registrations));

        Assert.Contains(duplicateId, ex.Message, StringComparison.Ordinal);
    }

    private static FrontedV3LayoutWindowRegistration CreateV3Registration(string id, string displayName)
    {
        return new FrontedV3LayoutWindowRegistration
        {
            Id = id,
            LocalId = id,
            IsBuiltIn = false,
            DisplayName = displayName
        };
    }

    private static FrontedXamlWindowRegistration CreateXamlRegistration(string id, string displayName)
    {
        return new FrontedXamlWindowRegistration
        {
            Id = id,
            LocalId = id,
            IsBuiltIn = false,
            DisplayName = displayName,
            WindowType = typeof(Window)
        };
    }
}
