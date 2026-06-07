using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Phase 2 UI metadata for behavior trigger event selection.
/// </summary>
public sealed class FrontedBehaviorEventCatalog
{
    private readonly IReadOnlyList<FrontedBehaviorEventDescriptor> _events;

    public FrontedBehaviorEventCatalog()
    {
        _events =
        [
            Event("WindowShown", "Window"),
            Event("WindowHidden", "Window"),
            Event("CanvasLoaded", "Canvas"),
            Event("CanvasStateChanged", "Canvas"),
            Event("ControlLoaded", "Control"),
            Event("GameProgressChanged", "Game"),
            Event(
                "CharacterPicked",
                "Game",
                Field("Event.Camp"),
                Field("Event.Team"),
                Field("Event.SlotIndex", "int"),
                Field("Event.CharacterId"),
                Field("Event.CharacterName")),
            Event(
                "CharacterBanned",
                "Game",
                Field("Event.Camp"),
                Field("Event.Team"),
                Field("Event.SlotIndex", "int"),
                Field("Event.CharacterId"),
                Field("Event.CharacterName")),
            Event(
                "CurrentPickingSlotChanged",
                "Game",
                Field("Event.Camp"),
                Field("Event.SlotIndex", "int"),
                Field("Event.IsActive", "bool")),
            Event(
                "ScoreChanged",
                "Game",
                Field("Event.Team"),
                Field("Event.ScoreType"),
                Field("Event.Value", "int")),
            Event("TeamChanged", "Game"),
            Event("TeamSwapped", "Game"),
            Event("MapChanged", "Game"),
            Event("TimerStarted", "Timer"),
            Event("TimerStopped", "Timer"),
            Event("TimerReached", "Timer", Field("Event.RemainingSeconds", "int")),
            Event("ManualTrigger", "Manual"),
            Event(
                "PluginEvent",
                "Plugin",
                Field("Event.PluginId"),
                Field("Event.EventName"))
        ];
    }

    public IReadOnlyList<FrontedBehaviorEventDescriptor> Events => _events;

    public FrontedBehaviorEventDescriptor? Find(string eventType)
    {
        return _events.FirstOrDefault(item => string.Equals(item.EventType, eventType, StringComparison.Ordinal));
    }

    private static FrontedBehaviorEventDescriptor Event(
        string eventType,
        string category,
        params FrontedBehaviorEventPayloadField[] fields)
    {
        return new FrontedBehaviorEventDescriptor
        {
            EventType = eventType,
            DisplayNameKey = $"Designer.Behaviors.Event.{eventType}",
            DescriptionKey = $"Designer.Behaviors.Event.{eventType}.Description",
            Category = category,
            PayloadFields = [.. fields]
        };
    }

    private static FrontedBehaviorEventPayloadField Field(string path, string typeName = "string")
    {
        return new FrontedBehaviorEventPayloadField
        {
            Path = path,
            DisplayNameKey = $"Designer.Behaviors.Payload.{path}",
            TypeName = typeName,
            IsCommonFilterTarget = true
        };
    }
}
