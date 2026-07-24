#nullable enable

using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Extensions.Registry;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Registrations;
using neo_bpsys_wpf.Core.Services.Registry;
using neo_bpsys_wpf.ViewModels.Pages;
using System;
using System.Linq;
using Xunit;

namespace neo_bpsys_wpf.Tests.Models;

public class FrontManagePageWindowGroupingTest
{
    [Fact]
    public void BuiltInDesignerV3WindowsUseStableV3Registrations()
    {
        var services = new ServiceCollection();
        var expectedWindows = new[]
        {
            FrontedWindowType.BpWindow,
            FrontedWindowType.CutSceneWindow,
            FrontedWindowType.ScoreSurWindow,
            FrontedWindowType.ScoreHunWindow,
            FrontedWindowType.ScoreGlobalWindow,
            FrontedWindowType.GameDataWindow,
            FrontedWindowType.BpOverviewWindow,
            FrontedWindowType.MapV2Window
        };

        foreach (var windowType in expectedWindows)
        {
            services.AddFrontedV3LayoutWindow(
                FrontedWindowHelper.GetFrontedWindowCanonicalId(windowType),
                isBuiltIn: true);
        }

        services.AddSingleton<IFrontedWindowRegistry, FrontedWindowRegistryService>();
        var registry = services.BuildServiceProvider().GetRequiredService<IFrontedWindowRegistry>();

        foreach (var windowType in expectedWindows)
        {
            var canonicalId = FrontedWindowHelper.GetFrontedWindowCanonicalId(windowType);
            Assert.True(registry.TryGet(canonicalId, out var registration));
            Assert.Equal(canonicalId, registration.Id);
            Assert.Equal(canonicalId, registration.LocalId);
            Assert.True(registration.IsBuiltIn);
            Assert.Equal(FrontedWindowRegistrationKind.V3Layout, registration.Kind);
        }
    }

    [Fact]
    public void BuiltInRegistration_GoesToBuiltInGroup()
    {
        FrontedWindowRegistration[] registrations =
        [
            CreateV3Registration("BpWindow", isBuiltIn: true, packageId: null)
        ];

        var groups = FrontedWindowManageGroup.FromRegistrations(registrations);

        Assert.Single(groups);
        Assert.Equal("BuiltIn", groups[0].GroupKey);
    }

    [Fact]
    public void PluginRegistration_GoesToPluginGroup()
    {
        FrontedWindowRegistration[] registrations =
        [
            CreateV3Registration("plugin:test.plugin/Overlay", isBuiltIn: false, packageId: "test.plugin")
        ];

        var groups = FrontedWindowManageGroup.FromRegistrations(registrations);

        Assert.Single(groups);
        Assert.Equal("Plugin", groups[0].GroupKey);
    }

    [Fact]
    public void HostNonBuiltInRegistration_GoesToExternalGroup()
    {
        FrontedWindowRegistration[] registrations =
        [
            CreateV3Registration("ExternalOverlay", isBuiltIn: false, packageId: null)
        ];

        var groups = FrontedWindowManageGroup.FromRegistrations(registrations);

        Assert.Single(groups);
        Assert.Equal("External", groups[0].GroupKey);
    }

    [Fact]
    public void ThreeSourceGroupsAreEmittedInRegistrationOrder()
    {
        FrontedWindowRegistration[] registrations =
        [
            CreateV3Registration("BpWindow", isBuiltIn: true, packageId: null),
            CreateV3Registration("ExternalOverlay", isBuiltIn: false, packageId: null),
            CreateV3Registration("plugin:test.plugin/Overlay", isBuiltIn: false, packageId: "test.plugin")
        ];

        var groups = FrontedWindowManageGroup.FromRegistrations(registrations);

        Assert.Equal(["BuiltIn", "External", "Plugin"], groups.Select(group => group.GroupKey));
    }

    [Fact]
    public void KindDisplay_IsIndependentFromSourceGroup()
    {
        FrontedWindowRegistration[] registrations =
        [
            CreateV3Registration("BpWindow", isBuiltIn: true, packageId: null),
            CreateV3Registration("plugin:test.plugin/Overlay", isBuiltIn: false, packageId: "test.plugin"),
            CreateXamlRegistration("XamlBuiltIn", isBuiltIn: true, packageId: null),
            CreateXamlRegistration("plugin:test.plugin/XamlOverlay", isBuiltIn: false, packageId: "test.plugin")
        ];

        var items = registrations
            .Select(registration => FrontedWindowManageItem.FromRegistration(registration))
            .ToArray();

        // V3Layout registrations share the same KindDisplay regardless of source group.
        Assert.Equal(items[0].KindDisplay, items[1].KindDisplay);

        // XAML registrations share the same KindDisplay regardless of source group.
        Assert.Equal(items[2].KindDisplay, items[3].KindDisplay);

        // V3Layout and XAML have different KindDisplay.
        Assert.NotEqual(items[0].KindDisplay, items[2].KindDisplay);
    }

    [Fact]
    public void V3RegistrationReportsV3LayoutKind()
    {
        var registration = CreateV3Registration("Overlay", isBuiltIn: false, packageId: null);

        Assert.Equal(FrontedWindowRegistrationKind.V3Layout, registration.Kind);
    }

    [Fact]
    public void ManageItemDoesNotExposeDuplicateFullWindowType()
    {
        var registration = CreateV3Registration("BpWindow", isBuiltIn: true, packageId: null);

        var item = FrontedWindowManageItem.FromRegistration(registration);

        // WindowId is the single Canonical ID property; FullWindowType has been removed.
        Assert.Equal(registration.Id, item.WindowId);
        Assert.DoesNotContain("FullWindowType", item.GetType().GetProperties().Select(property => property.Name));
    }

    private static FrontedV3LayoutWindowRegistration CreateV3Registration(
        string id,
        bool isBuiltIn,
        string? packageId)
    {
        return new FrontedV3LayoutWindowRegistration
        {
            Id = id,
            LocalId = id,
            IsBuiltIn = isBuiltIn,
            PackageId = packageId,
            DisplayName = id
        };
    }

    private static FrontedXamlWindowRegistration CreateXamlRegistration(
        string id,
        bool isBuiltIn,
        string? packageId)
    {
        return new FrontedXamlWindowRegistration
        {
            Id = id,
            LocalId = id,
            IsBuiltIn = isBuiltIn,
            PackageId = packageId,
            DisplayName = id,
            WindowType = typeof(object)
        };
    }
}
