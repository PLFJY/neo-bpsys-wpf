using System.Windows;

namespace neo_bpsys_wpf.ProductTour;

/// <summary>
/// Determines whether a tutorial owner is currently active for automatic page tutorial runs.
/// </summary>
public interface ITutorialOwnerActivationService
{
    /// <summary>
    /// Determines whether the specified owner is still the active owner for the page key.
    /// </summary>
    /// <param name="owner">Tutorial owner element.</param>
    /// <param name="pageKey">Tutorial page key.</param>
    /// <returns><see langword="true" /> when automatic tutorial runs may start for the owner; otherwise, <see langword="false" />.</returns>
    bool IsOwnerActive(FrameworkElement owner, string pageKey);
}

/// <summary>
/// Default owner activation service used when an application does not provide active owner semantics.
/// </summary>
public sealed class AlwaysActiveTutorialOwnerActivationService : ITutorialOwnerActivationService
{
    /// <inheritdoc />
    public bool IsOwnerActive(FrameworkElement owner, string pageKey)
    {
        _ = owner;
        _ = pageKey;
        return true;
    }
}
