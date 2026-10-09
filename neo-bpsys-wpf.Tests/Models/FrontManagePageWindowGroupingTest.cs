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

    /// <summary>窗口分组使用注册来源，管理项保留 canonical ID。</summary>
    /// <param name="id">窗口 ID。</param>
    /// <param name="isBuiltIn">是否内置。</param>
    /// <param name="packageId">插件包 ID。</param>
    /// <param name="expectedGroup">预期来源分组。</param>
    [Theory]
    [InlineData("BpWindow", true, null, "BuiltIn")]
    [InlineData("plugin:test.plugin/Overlay", false, "test.plugin", "Plugin")]
    [InlineData("ExternalOverlay", false, null, "External")]
    public void Registration_IsGroupedByOrigin(string id, bool isBuiltIn, string? packageId, string expectedGroup)
    {
        var registration = CreateV3Registration(id, isBuiltIn, packageId);
        var groups = FrontedWindowManageGroup.FromRegistrations([registration]);

        Assert.Contains(groups.Single(group => group.GroupKey == expectedGroup).Windows, item => item.WindowId == id);
        Assert.Contains(groups, group => group.GroupKey == "Custom");
        Assert.Equal(id, FrontedWindowManageItem.FromRegistration(registration).WindowId);
    }

    [Fact]
    public void CustomGroupIsAlwaysPresentBetweenBuiltInAndPluginGroups()
    {
        FrontedWindowRegistration[] registrations =
        [
            CreateV3Registration("BpWindow", isBuiltIn: true, packageId: null),
            CreateV3Registration("ExternalOverlay", isBuiltIn: false, packageId: null),
            CreateV3Registration("plugin:test.plugin/Overlay", isBuiltIn: false, packageId: "test.plugin")
        ];

        var groups = FrontedWindowManageGroup.FromRegistrations(registrations);

        Assert.Equal(["BuiltIn", "Custom", "Plugin", "External"], groups.Select(group => group.GroupKey));
    }

    [Fact]
    public void EmptyCustomGroupContainsOnlyCreatePlaceholder()
    {
        var groups = FrontedWindowManageGroup.FromRegistrations(
            [CreateV3Registration("BpWindow", isBuiltIn: true, packageId: null)]);

        var customGroup = groups.Single(group => group.GroupKey == "Custom");

        var placeholder = Assert.Single(customGroup.Windows);
        Assert.True(customGroup.IsCustom);
        Assert.True(placeholder.IsCreatePlaceholder);
        Assert.True(placeholder.IsCustom);
    }

    [Fact]
    public void CustomRegistrationIsFollowedByCreatePlaceholder()
    {
        var custom = new FrontedCustomV3LayoutWindowRegistration
        {
            Id = "custom:user-layout/custom-window",
            LocalId = "custom-window",
            PackageScopeId = "user-layout",
            IsBuiltIn = false,
            DisplayName = "Custom Window"
        };

        var groups = FrontedWindowManageGroup.FromRegistrations([custom]);
        var customGroup = groups.Single(group => group.GroupKey == "Custom");

        Assert.Equal(2, customGroup.Windows.Count);
        Assert.False(customGroup.Windows[0].IsCreatePlaceholder);
        Assert.True(customGroup.Windows[0].IsCustom);
        Assert.True(customGroup.Windows[1].IsCreatePlaceholder);
        Assert.True(customGroup.Windows[1].IsCustom);
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
