namespace neo_bpsys_wpf.WebRenderer.Services;

/// <summary>串行化 Web Renderer 的管理生命周期操作。</summary>
public sealed class WebRendererLifecycleOperationCoordinator
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>操作状态发生变化时触发。</summary>
    public event EventHandler? StateChanged;

    /// <summary>获取是否存在正在运行的生命周期操作。</summary>
    public bool IsLifecycleOperationRunning { get; private set; }

    /// <summary>获取当前操作名称。</summary>
    public string? CurrentOperation { get; private set; }

    /// <summary>以总超时运行互斥操作。</summary>
    public async Task RunAsync(string operationName, TimeSpan timeout, Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            IsLifecycleOperationRunning = true; CurrentOperation = operationName; StateChanged?.Invoke(this, EventArgs.Empty);
            using var timeoutSource = new CancellationTokenSource(timeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
            await operation(linked.Token);
        }
        finally
        {
            IsLifecycleOperationRunning = false; CurrentOperation = null; StateChanged?.Invoke(this, EventArgs.Empty);
            _gate.Release();
        }
    }
}
