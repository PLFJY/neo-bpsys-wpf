namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

public sealed class FrontedBehaviorDocument
{
    public int Version { get; set; } = 1;

    public string? WindowType { get; set; }

    public string? CanvasName { get; set; }

    public List<ControlBehaviorSet> ControlBehaviorSets { get; set; } = [];

    public ControlBehaviorSet? FindSet(Guid behaviorGuid) =>
        ControlBehaviorSets.FirstOrDefault(set => set.BehaviorGuid == behaviorGuid);

    public ControlBehaviorSet GetOrCreateSet(Guid behaviorGuid, string? displayName = null)
    {
        var existing = FindSet(behaviorGuid);
        if (existing is not null)
        {
            return existing;
        }

        var created = new ControlBehaviorSet
        {
            BehaviorGuid = behaviorGuid,
            DisplayName = displayName
        };
        ControlBehaviorSets.Add(created);
        return created;
    }

    public bool RemoveSet(Guid behaviorGuid)
    {
        var existing = FindSet(behaviorGuid);
        return existing is not null && ControlBehaviorSets.Remove(existing);
    }
}

