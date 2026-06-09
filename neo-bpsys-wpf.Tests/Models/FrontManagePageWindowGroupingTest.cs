#nullable enable

using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.ViewModels.Pages;
using System;
using System.Linq;
using Xunit;

namespace neo_bpsys_wpf.Tests.Models;

public class FrontManagePageWindowGroupingTest
{
    [Fact]
    public void GroupsFollowRegistryOrderAndDoNotExposeCanvas()
    {
        IFrontedWindowDescriptor[] descriptors =
        [
            CreateDescriptor("score-sur", "ScoreSurWindow", "Score", 100),
            CreateDescriptor("score-hun", "ScoreHunWindow", "Score", 110),
            CreateDescriptor("bp", "BpWindow", "Bp", 10)
        ];

        var groups = FrontedWindowManageGroup.FromDescriptors(descriptors);

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
        IFrontedWindowDescriptor[] descriptors =
        [
            CreateDescriptor("builtin", "BpWindow", null, null),
            new FrontedPluginWindowDescriptor
            {
                PackageId = "top.plfjy.test",
                WindowId = Guid.NewGuid().ToString(),
                WindowTypeName = "Overlay",
                DisplayName = "Overlay",
                Kind = FrontedWindowKind.PluginLayout,
                AllowBlankDefaultLayout = true
            }
        ];

        var groups = FrontedWindowManageGroup.FromDescriptors(descriptors);

        Assert.Equal(["BuiltIn", "Plugin"], groups.Select(group => group.GroupKey));
    }

    private static FrontedBuiltInWindowDescriptor CreateDescriptor(
        string windowId,
        string windowTypeName,
        string? groupKey,
        int? displayOrder)
    {
        return new FrontedBuiltInWindowDescriptor
        {
            WindowId = windowId,
            WindowTypeName = windowTypeName,
            DisplayName = windowTypeName,
            GroupKey = groupKey,
            DisplayOrder = displayOrder,
            IsV3LayoutWindow = true,
            Customizable = true
        };
    }
}
