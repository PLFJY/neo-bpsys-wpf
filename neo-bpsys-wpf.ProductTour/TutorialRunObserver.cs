namespace neo_bpsys_wpf.ProductTour;

/// <summary>
/// 观察教程包和步骤的执行情况，用于诊断和集成测试。
/// </summary>
public interface ITutorialRunObserver
{
    /// <summary>当请求自动运行页面或窗口教程时调用。</summary>
    /// <param name="ownerType">宿主元素类型名称。</param>
    /// <param name="pageKey">页面键。</param>
    /// <param name="reason">UI 事件或调用方原因。</param>
    void OnAutoRunRequested(string ownerType, string pageKey, string reason);

    /// <summary>当自动运行页面或窗口教程完成时调用。</summary>
    /// <param name="ownerType">宿主元素类型名称。</param>
    /// <param name="pageKey">页面键。</param>
    /// <param name="result">最终运行结果。</param>
    void OnAutoRunCompleted(string ownerType, string pageKey, TutorialRunResult result);

    /// <summary>当某个待执行的包被选中执行时调用。</summary>
    /// <param name="packageId">包 id。</param>
    /// <param name="pageKey">页面键。</param>
    /// <param name="triggerMode">触发模式。</param>
    void OnPackageRunRequested(string packageId, string pageKey, TutorialTriggerMode triggerMode);

    /// <summary>当某个包开始运行时调用。</summary>
    /// <param name="packageId">包 id。</param>
    /// <param name="pageKey">页面键。</param>
    /// <param name="triggerMode">触发模式。</param>
    void OnPackageStarted(string packageId, string pageKey, TutorialTriggerMode triggerMode);

    /// <summary>当某个步骤实际显示时调用。</summary>
    /// <param name="packageId">包 id。</param>
    /// <param name="targetName">目标名称（如有）。</param>
    /// <param name="title">步骤标题。</param>
    void OnStepShown(string packageId, string? targetName, string title);

    /// <summary>当某个包完成时调用。</summary>
    /// <param name="packageId">包 id。</param>
    /// <param name="result">运行结果。</param>
    void OnPackageCompleted(string packageId, TutorialRunResult result);

    /// <summary>当某个页面键没有待执行的包时调用。</summary>
    /// <param name="pageKey">页面键。</param>
    void OnPackageNotPending(string pageKey);

    /// <summary>当某个包因完成状态已覆盖而跳过时调用。</summary>
    /// <param name="packageId">包 id。</param>
    /// <param name="completionKind">已记录的完成类型。</param>
    /// <param name="recordedVersion">已记录的包版本。</param>
    /// <param name="currentVersion">当前包版本。</param>
    void OnPackageSkippedByState(
        string packageId,
        TutorialCompletionKind completionKind,
        int recordedVersion,
        int currentVersion);

    /// <summary>当某个包因当前未就绪而无法运行时调用。</summary>
    /// <param name="packageId">包 id。</param>
    /// <param name="pageKey">页面键。</param>
    void OnPackageNotReady(string packageId, string pageKey);

    /// <summary>当为某次运行解析出页面序列时调用。</summary>
    /// <param name="pageKey">页面键。</param>
    /// <param name="packageIds">解析得到的包 id 列表。</param>
    void OnSequenceResolved(
        string pageKey,
        IReadOnlyList<string> packageIds);

    /// <summary>当某个包的目标缺失时调用。</summary>
    /// <param name="packageId">包 id。</param>
    void OnPackageTargetMissing(string packageId);
}

/// <summary>
/// 默认的空实现教程运行观察器。
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
    public void OnPackageNotReady(string packageId, string pageKey)
    {
    }

    /// <inheritdoc />
    public void OnSequenceResolved(
        string pageKey,
        IReadOnlyList<string> packageIds)
    {
    }

    /// <inheritdoc />
    public void OnPackageTargetMissing(string packageId)
    {
    }
}
