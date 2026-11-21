using Microsoft.Extensions.Logging;

namespace neo_bpsys_wpf.WebRenderer.Services;

/// <summary>串行化 Web Renderer 的管理生命周期操作。</summary>
public sealed class WebRendererLifecycleOperationCoordinator : IDisposable
{
    private static readonly TimeSpan MaxTimeout = TimeSpan.FromMinutes(10);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ILogger<WebRendererLifecycleOperationCoordinator>? _logger;
    private int _isRunning;
    private string? _currentOperation;
    private int _disposed;

    /// <summary>初始化协调器。</summary>
    /// <param name="logger">可选日志记录器，用于记录超时与失败信息。</param>
    public WebRendererLifecycleOperationCoordinator(
        ILogger<WebRendererLifecycleOperationCoordinator>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>操作状态发生变化时触发。订阅者必须只做通知类工作，异常会被隔离。</summary>
    public event EventHandler? StateChanged;

    /// <summary>获取是否存在正在运行的生命周期操作。</summary>
    public bool IsLifecycleOperationRunning => Volatile.Read(ref _isRunning) != 0;

    /// <summary>获取当前操作名称。</summary>
    public string? CurrentOperation => Volatile.Read(ref _currentOperation);

    /// <summary>以总超时运行互斥操作。超时覆盖等待前一个操作的阶段和操作执行阶段。</summary>
    /// <param name="operationName">操作名称，用于状态与超时诊断。</param>
    /// <param name="timeout">总超时；支持 <see cref="Timeout.InfiniteTimeSpan"/>，其余必须大于零且不超过 10 分钟。</param>
    /// <param name="operation">要执行的生命周期操作。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>操作完成后返回的任务。</returns>
    /// <exception cref="ArgumentException">操作名称为空。</exception>
    /// <exception cref="ArgumentNullException"><paramref name="operation"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="ArgumentOutOfRangeException">超时不在允许范围内。</exception>
    /// <exception cref="ObjectDisposedException">协调器已被释放。</exception>
    /// <exception cref="TimeoutException">等待或执行超过总超时。</exception>
    /// <exception cref="OperationCanceledException">调用方取消。</exception>
    public async Task RunAsync(string operationName, TimeSpan timeout, Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentNullException.ThrowIfNull(operation);
        ValidateTimeout(timeout);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        using var timeoutSource = timeout == Timeout.InfiniteTimeSpan
            ? null
            : new CancellationTokenSource(timeout);
        using var linked = timeoutSource is null
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);

        try
        {
            await _gate.WaitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutSource?.IsCancellationRequested == true
                                                  && !cancellationToken.IsCancellationRequested)
        {
            _logger?.LogWarning(
                "Web Renderer lifecycle operation {Operation} timed out after {Timeout} while waiting for the previous operation.",
                operationName, timeout);
            throw new TimeoutException(
                $"Web Renderer lifecycle operation '{operationName}' timed out after {timeout} while waiting for the previous operation.");
        }

        try
        {
            Volatile.Write(ref _currentOperation, operationName);
            Volatile.Write(ref _isRunning, 1);
            RaiseStateChanged();

            try
            {
                await operation(linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeoutSource?.IsCancellationRequested == true
                                                      && !cancellationToken.IsCancellationRequested)
            {
                _logger?.LogWarning(
                    "Web Renderer lifecycle operation {Operation} timed out after {Timeout}.",
                    operationName, timeout);
                throw new TimeoutException(
                    $"Web Renderer lifecycle operation '{operationName}' timed out after {timeout}.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger?.LogError(ex, "Web Renderer lifecycle operation {Operation} failed.", operationName);
                throw;
            }
        }
        finally
        {
            Volatile.Write(ref _isRunning, 0);
            Volatile.Write(ref _currentOperation, null);
            try
            {
                _gate.Release();
            }
            catch (ObjectDisposedException)
            {
                // 协调器在操作进行中被释放（应用退出）：串行化保证已不再需要。
            }

            RaiseStateChanged();
        }
    }

    /// <summary>释放协调器占用的信号量。</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        _gate.Dispose();
    }

    private static void ValidateTimeout(TimeSpan timeout)
    {
        if (timeout == Timeout.InfiniteTimeSpan)
            return;
        if (timeout <= TimeSpan.Zero || timeout > MaxTimeout)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout,
                "Timeout must be positive (or infinite) and must not exceed 10 minutes.");
        }
    }

    /// <summary>
    /// 触发状态变更通知。订阅者异常不得阻止信号量释放或掩盖原始操作异常。
    /// </summary>
    private void RaiseStateChanged()
    {
        try
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            // 状态通知是尽力而为；协调器只保证生命周期操作串行化。
        }
    }
}
