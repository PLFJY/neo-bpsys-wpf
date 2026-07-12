namespace neo_bpsys_wpf.ProductTour;

/// <summary>
/// 准备并清理隔离的游戏教程沙箱。
/// </summary>
public interface IGameTutorialSandboxService
{
    /// <summary>在引导流程开始前准备沙箱状态。</summary>
    /// <param name="cancellationToken">取消令牌。</param>
    Task PrepareAsync(CancellationToken cancellationToken = default);

    /// <summary>在完成、跳过或失败后清理沙箱状态。</summary>
    /// <param name="cancellationToken">取消令牌。</param>
    Task CleanupAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 空实现的沙箱，在宿主提供游戏特定行为之前使用。
/// </summary>
public sealed class NoOpGameTutorialSandboxService : IGameTutorialSandboxService
{
    /// <inheritdoc />
    public Task PrepareAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public Task CleanupAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
