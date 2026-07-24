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
            Assert.Equal("BuiltIn", registration.GroupKey);
        }
    }

    [Fact]
    public void GroupsFollowRegistryOrderAndDoNotExposeCanvas()
    {
        FrontedWindowRegistration[] registrations =
        [
            CreateV3Registration("score-sur", "ScoreSurWindow", "Score", 100),
            CreateV3Registration("score-hun", "ScoreHunWindow", "Score", 110),
            CreateV3Registration("bp", "BpWindow", "Bp", 10)
        ];

        var groups = FrontedWindowManageGroup.FromRegistrations(registrations);

        Assert.Collection(
            groups,
            group =>
            {
                Assert.Equal("Score", group.GroupKey);
                Assert.Equal(["ScoreSurWindow", "ScoreHunWindow"], group.Windows.Select(window => window.DisplayName));
            },
            group =>
            {
                Assert.Equal("Bp", group.GroupKey);
                Assert.Equal(["BpWindow"], group.Windows.Select(window => window.DisplayName));
            });

        Assert.All(groups.SelectMany(group => group.Windows), window =>
        {
            Assert.DoesNotContain("BaseCanvas", window.DisplayName, StringComparison.Ordinal);
            Assert.DoesNotContain("Canvas", window.DisplayName, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void MissingGroupKeyUsesStableFallback()
    {
        FrontedWindowRegistration[] registrations =
        [
            CreateV3Registration("BpWindow", "BpWindow", null, null, isBuiltIn: true),
            CreateV3Registration("plugin:top.plfjy.test/Overlay", "Overlay", null, null, isBuiltIn: false)
        ];

        var groups = FrontedWindowManageGroup.FromRegistrations(registrations);

        Assert.Equal(["BuiltIn", "Plugin"], groups.Select(group => group.GroupKey));
    }

    [Fact]
    public void V3RegistrationReportsV3LayoutKind()
    {
        var registration = CreateV3Registration("Overlay", "Overlay", null, null, isBuiltIn: false);

        Assert.Equal(FrontedWindowRegistrationKind.V3Layout, registration.Kind);
    }

    private static FrontedV3LayoutWindowRegistration CreateV3Registration(
        string id,
        string displayName,
        string? groupKey,
        int? displayOrder,
        bool isBuiltIn = true)
    {
        return new FrontedV3LayoutWindowRegistration
        {
            Id = id,
            LocalId = id,
            IsBuiltIn = isBuiltIn,
            DisplayName = displayName,
            GroupKey = groupKey,
            DisplayOrder = displayOrder
        };
    }
}
