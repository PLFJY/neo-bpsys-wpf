namespace neo_bpsys_wpf.ProductTour;

/// <summary>
/// Observes tutorial package and step execution for diagnostics and integration tests.
/// </summary>
public interface ITutorialRunObserver
{
    /// <summary>Called when a pending package was selected for execution.</summary>
    /// <param name="packageId">Package id.</param>
    /// <param name="pageKey">Page key.</param>
    /// <param name="triggerMode">Trigger mode.</param>
    void OnPackageRunRequested(string packageId, string pageKey, TutorialTriggerMode triggerMode);

    /// <summary>Called when a package starts running.</summary>
    /// <param name="packageId">Package id.</param>
    /// <param name="pageKey">Page key.</param>
    /// <param name="triggerMode">Trigger mode.</param>
    void OnPackageStarted(string packageId, string pageKey, TutorialTriggerMode triggerMode);

    /// <summary>Called when a step is actually shown.</summary>
    /// <param name="packageId">Package id.</param>
    /// <param name="targetName">Target name, if any.</param>
    /// <param name="title">Step title.</param>
    void OnStepShown(string packageId, string? targetName, string title);

    /// <summary>Called when a package finishes.</summary>
    /// <param name="packageId">Package id.</param>
    /// <param name="result">Run result.</param>
    void OnPackageCompleted(string packageId, TutorialRunResult result);

    /// <summary>Called when no package is pending for a page key.</summary>
    /// <param name="pageKey">Page key.</param>
    void OnPackageNotPending(string pageKey);

    /// <summary>
    /// Called when a package run is suppressed because another tutorial is active.
    /// Suppressed is terminal for the current auto-run request and is not retried by TutorialPageLoader.
    /// </summary>
    /// <param name="pageKey">Page key.</param>
    void OnPackageSuppressed(string pageKey);

    /// <summary>Called when a package target is missing.</summary>
    /// <param name="packageId">Package id.</param>
    void OnPackageTargetMissing(string packageId);
}

/// <summary>
/// Default no-op tutorial run observer.
/// </summary>
public sealed class NoOpTutorialRunObserver : ITutorialRunObserver
{
    /// <inheritdoc />
    public void OnPackageRunRequested(string packageId, string pageKey, TutorialTriggerMode triggerMode)
    {
    }

    /// <inheritdoc />
    public void OnPackageStarted(string packageId, string pageKey, TutorialTriggerMode triggerMode)
    {
    }

    /// <inheritdoc />
    public void OnStepShown(string packageId, string? targetName, string title)
    {
    }

    /// <inheritdoc />
    public void OnPackageCompleted(string packageId, TutorialRunResult result)
    {
    }

    /// <inheritdoc />
    public void OnPackageNotPending(string pageKey)
    {
    }

    /// <inheritdoc />
    public void OnPackageSuppressed(string pageKey)
    {
    }

    /// <inheritdoc />
    public void OnPackageTargetMissing(string packageId)
    {
    }
}
