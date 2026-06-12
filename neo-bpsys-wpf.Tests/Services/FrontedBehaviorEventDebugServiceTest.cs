using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.ViewModels.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

public class FrontedBehaviorEventDebugServiceTest
{
    [Fact]
    public void DebugService_RecordsGlobalEventsWithoutRuntimeHost()
    {
        var bus = new FrontedEventBus();
        using var service = new FrontedBehaviorEventDebugService(bus);

        bus.Publish(new FrontedBehaviorEvent
        {
            EventType = "Guidance.StepChanged",
            Payload = new Dictionary<string, object?>
            {
                ["Action"] = GameAction.PickSur,
                ["IndexesText"] = "[0]"
            }
        });

        var record = Assert.Single(service.Records);
        Assert.Equal("Guidance.StepChanged", record.EventType);
        Assert.Contains(record.Payload, entry => entry.Key == "Action" && entry.FilterText == "PickSur");
        Assert.Contains(record.Payload, entry => entry.Key == "IndexesText" && entry.FilterText == "[0]");
    }

    [Fact]
    public void DebugService_MaxRecords_KeepsNewestRecords()
    {
        var bus = new FrontedEventBus();
        using var service = new FrontedBehaviorEventDebugService(bus)
        {
            MaxRecords = 2
        };

        bus.Publish(new FrontedBehaviorEvent { EventType = "one" });
        bus.Publish(new FrontedBehaviorEvent { EventType = "two" });
        bus.Publish(new FrontedBehaviorEvent { EventType = "three" });

        Assert.Equal(["two", "three"], service.Records.Select(record => record.EventType));
    }

    [Fact]
    public void DebugService_Pause_IgnoresNewEvents()
    {
        var bus = new FrontedEventBus();
        using var service = new FrontedBehaviorEventDebugService(bus)
        {
            IsPaused = true
        };

        bus.Publish(new FrontedBehaviorEvent { EventType = "ignored" });

        Assert.Empty(service.Records);
    }

    [Fact]
    public void DebuggerCopyHelpers_CreateFilterStrings()
    {
        var actionEntry = new FrontedBehaviorPayloadDebugEntry
        {
            Key = "Action",
            FilterText = "PickSur"
        };
        var indexesEntry = new FrontedBehaviorPayloadDebugEntry
        {
            Key = "IndexesText",
            FilterText = "[0]"
        };

        Assert.Equal("Event.Action Equals PickSur", FrontedBehaviorEventDebuggerViewModel.CreateEqualsFilter(actionEntry));
        Assert.Equal("Event.IndexesText Contains 0", FrontedBehaviorEventDebuggerViewModel.CreateContainsFilter(indexesEntry));
    }
}
