#nullable enable

using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

public class FrontedBehaviorServiceTest
{
    [Fact]
    public async Task BehaviorDocument_SaveLoad_RoundTrip()
    {
        var root = CreateTempRoot();
        try
        {
            var service = CreateService(root);
            var behaviorGuid = Guid.NewGuid();
            var document = new FrontedBehaviorDocument
            {
                WindowType = "BpWindow",
                CanvasName = "BaseCanvas"
            };
            document.GetOrCreateSet(behaviorGuid, "Title").Behaviors.Add(new FrontedBehavior
            {
                Name = "Fade",
                Trigger = new TriggerDescriptor
                {
                    EventType = "CharacterPicked",
                    Filters =
                    {
                        new TriggerFilter
                        {
                            Left = "Event.Camp",
                            Operator = TriggerFilterOperator.NotEquals,
                            Right = "Hun",
                            RightValueKind = TriggerFilterValueKind.EventPath
                        }
                    }
                }
            });

            await service.SaveDocumentAsync(document, TestContext.Current.CancellationToken);
            var loaded = await service.LoadDocumentAsync("BpWindow", TestContext.Current.CancellationToken);

            var loadedBehavior = Assert.Single(loaded.FindSet(behaviorGuid)!.Behaviors);
            Assert.Equal("Fade", loadedBehavior.Name);
            var filter = Assert.Single(loadedBehavior.Trigger!.Filters);
            Assert.Equal(TriggerFilterOperator.NotEquals, filter.Operator);
            Assert.Equal(TriggerFilterValueKind.EventPath, filter.RightValueKind);
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [Fact]
    public async Task BehaviorService_MissingFile_ReturnsEmptyDocument()
    {
        var root = CreateTempRoot();
        try
        {
            var service = CreateService(root);

            var document = await service.LoadDocumentAsync("BpWindow", TestContext.Current.CancellationToken);

            Assert.Equal(1, document.Version);
            Assert.Equal("BpWindow", document.WindowType);
            Assert.Equal("BaseCanvas", document.CanvasName);
            Assert.Empty(document.ControlBehaviorSets);
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [Fact]
    public async Task BehaviorService_RemoveBehaviors_RemovesSet()
    {
        var root = CreateTempRoot();
        try
        {
            var service = CreateService(root);
            var removedGuid = Guid.NewGuid();
            var keptGuid = Guid.NewGuid();
            var document = new FrontedBehaviorDocument
            {
                WindowType = "BpWindow",
                CanvasName = "BaseCanvas"
            };
            document.GetOrCreateSet(removedGuid, "Removed").Behaviors.Add(new FrontedBehavior { Name = "Removed" });
            document.GetOrCreateSet(keptGuid, "Kept").Behaviors.Add(new FrontedBehavior { Name = "Kept" });
            await service.SaveDocumentAsync(document, TestContext.Current.CancellationToken);

            var loaded = await service.LoadDocumentAsync("BpWindow", TestContext.Current.CancellationToken);
            service.RemoveBehaviors(removedGuid);
            await service.SaveDocumentAsync(loaded, TestContext.Current.CancellationToken);
            var roundTrip = await service.LoadDocumentAsync("BpWindow", TestContext.Current.CancellationToken);

            Assert.Null(roundTrip.FindSet(removedGuid));
            Assert.NotNull(roundTrip.FindSet(keptGuid));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [Fact]
    public void TriggerFilter_RoundTrip_PreservesOperatorAndValueKind()
    {
        var filter = new TriggerFilter
        {
            Left = "Event.Team",
            Operator = TriggerFilterOperator.Contains,
            Right = "HomeTeam",
            RightValueKind = TriggerFilterValueKind.BindingPath
        };

        var roundTrip = JsonSerializer.Deserialize<TriggerFilter>(JsonSerializer.Serialize(filter));

        Assert.NotNull(roundTrip);
        Assert.Equal(TriggerFilterOperator.Contains, roundTrip.Operator);
        Assert.Equal(TriggerFilterValueKind.BindingPath, roundTrip.RightValueKind);
    }

    private static FrontedBehaviorService CreateService(string root)
    {
        return new FrontedBehaviorService(new FrontedUserLayoutStore(root), NullLogger<FrontedBehaviorService>.Instance);
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "neo-bpsys-behavior-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteTempRoot(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
