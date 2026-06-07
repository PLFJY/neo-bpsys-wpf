namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// Handles fronted behavior data owned by a fronted control identity.
/// </summary>
public interface IFrontedBehaviorService
{
    /// <summary>
    /// Removes behavior data attached directly to the specified behavior target.
    /// </summary>
    void RemoveBehaviors(Guid behaviorGuid);
}

