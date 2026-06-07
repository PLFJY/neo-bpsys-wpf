namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// Creates stable identifiers for Designer v3 behavior targets.
/// </summary>
public static class FrontedBehaviorGuidHelper
{
    /// <summary>
    /// Creates a non-empty GUID for behavior-system identities.
    /// </summary>
    public static Guid NewGuid()
    {
        var guid = Guid.CreateVersion7();
        return guid == Guid.Empty ? Guid.NewGuid() : guid;
    }
}

