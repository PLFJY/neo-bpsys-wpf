#nullable enable

using neo_bpsys_wpf.Core.Services.FrontedLayout;
using System.Linq;
using Xunit;

namespace neo_bpsys_wpf.Tests.Models;

public class FrontedBehaviorEventCatalogTest
{
    [Fact]
    public void EventCatalog_ContainsRequiredEvents()
    {
        var catalog = new FrontedBehaviorEventCatalog();
        string[] required =
        [
            "WindowShown",
            "WindowHidden",
            "CanvasLoaded",
            "CanvasStateChanged",
            "ControlLoaded",
            "GameProgressChanged",
            "CharacterPicked",
            "CharacterBanned",
            "CurrentPickingSlotChanged",
            "ScoreChanged",
            "TeamChanged",
            "TeamSwapped",
            "MapChanged",
            "TimerStarted",
            "TimerStopped",
            "TimerReached",
            "ManualTrigger",
            "PluginEvent"
        ];

        foreach (var eventType in required)
        {
            Assert.NotNull(catalog.Find(eventType));
        }
    }

    [Fact]
    public void EventCatalog_CharacterPicked_HasExpectedPayloadFields()
    {
        var catalog = new FrontedBehaviorEventCatalog();

        var descriptor = catalog.Find("CharacterPicked");

        Assert.NotNull(descriptor);
        var paths = descriptor.PayloadFields.Select(field => field.Path).ToArray();
        Assert.Contains("Event.Camp", paths);
        Assert.Contains("Event.Team", paths);
        Assert.Contains("Event.SlotIndex", paths);
        Assert.Contains("Event.CharacterId", paths);
        Assert.Contains("Event.CharacterName", paths);
    }
}
