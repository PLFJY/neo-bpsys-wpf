namespace neo_bpsys_wpf.ProductTour;

/// <summary>
/// Observes tutorial package and step execution for diagnostics and integration tests.
/// </summary>
public interface ITutorialRunObserver
{
    /// <summary>Called when an automatic page or window tutorial run is requested.</summary>
    /// <param name="ownerType">Owner element type name.</param>
    /// <param name="pageKey">Page key.</param>
    /// <param name="reason">UI event or caller reason.</param>
    void OnAutoRunRequested(string ownerType, string pageKey, string reason);

    /// <summary>Called when an automatic page or window tutorial run finishes.</summary>
    /// <param name="ownerType">Owner element type name.</param>
    /// <param name="pageKey">Page key.</param>
    /// <param name="result">Final run result.</param>
    void OnAutoRunCompleted(string ownerType, string pageKey, TutorialRunResult result);

    /// <summary>Called when an automatic page or window tutorial run is rejected because the owner is inactive.</summary>
    /// <param name="ownerType">Owner element type name.</param>
    /// <param name="pageKey">Page key.</param>
    /// <param name="reason">Diagnostic reason.</param>
    void OnAutoRunRejectedInactiveOwner(string ownerType, string pageKey, string reason);

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

    /// <summary>Called when a package is skipped because completion state already covers it.</summary>
    /// <param name="packageId">Package id.</param>
    /// <param name="completionKind">Recorded completion kind.</param>
    /// <param name="recordedVersion">Recorded package version.</param>
    /// <param name="currentVersion">Current package version.</param>
    void OnPackageSkippedByState(
        string packageId,
        TutorialCompletionKind completionKind,
        int recordedVersion,
        int currentVersion);

    /// <summary>Called when a package is skipped because its CanRun condition returned false.</summary>
    /// <param name="packageId">Package id.</param>
    /// <param name="pageKey">Page key.</param>
    void OnPackageSkippedByCanRun(string packageId, string pageKey);

    /// <summary>Called when a page sequence has been resolved for a run.</summary>
    /// <param name="pageKey">Page key.</param>
    /// <param name="packageIds">Resolved package ids.</param>
    /// <param name="strategy">Automatic run strategy.</param>
    void OnSequenceResolved(
        string pageKey,
        IReadOnlyList<string> packageIds,
        TutorialAutoRunStrategy strategy);

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
    public void OnAutoRunRequested(string ownerType, string pageKey, string reason)
    {
    }

    /// <inheritdoc />
    public void OnAutoRunCompleted(string ownerType, string pageKey, TutorialRunResult result)
    {
    }

    /// <inheritdoc />
    public void OnAutoRunRejectedInactiveOwner(string ownerType, string pageKey, string reason)
    {
    }

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
    public void OnPackageSkippedByState(
        string packageId,
        TutorialCompletionKind completionKind,
        int recordedVersion,
        int currentVersion)
    {
    }

    /// <inheritdoc />
    public void OnPackageSkippedByCanRun(string packageId, string pageKey)
    {
    }

    /// <inheritdoc />
    public void OnSequenceResolved(
        string pageKey,
        IReadOnlyList<string> packageIds,
        TutorialAutoRunStrategy strategy)
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
