using neo_bpsys_wpf.Core.Abstractions.Services;

namespace neo_bpsys_wpf.Services.SmartBpModule;

/// <summary>将 SmartBP 模块的运行状态桥接到宿主进程。</summary>
public sealed class SmartBpAutoRecognitionGlobalControl : ISmartBpAutoRecognitionGlobalControl, ISmartBpAutoRecognitionGlobalControlSink
{
    private readonly Lock _sync = new();
    private Func<CancellationToken, Task>? _stop;
    private Func<CancellationToken, Task>? _forceSyncGameState;
    private bool _isRunning;

    /// <inheritdoc />
    public bool IsRunning { get { lock (_sync) return _isRunning; } }

    /// <inheritdoc />
    public event EventHandler? StateChanged;

    /// <inheritdoc />
    public Task ForceSyncGameStateAsync(CancellationToken cancellationToken = default)
    {
        Func<CancellationToken, Task>? forceSyncGameState;
        lock (_sync) forceSyncGameState = _forceSyncGameState;
        return forceSyncGameState?.Invoke(cancellationToken) ?? Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        Func<CancellationToken, Task>? stop;
        lock (_sync) stop = _stop;
        return stop?.Invoke(cancellationToken) ?? Task.CompletedTask;
    }

    /// <summary>更新模块拥有的停止回调和运行状态（兼容旧版模块的二进制契约）。</summary>
    /// <param name="isRunning">识别是否正在运行。</param>
    /// <param name="stop">用于停止识别的回调。</param>
    public void Update(bool isRunning, Func<CancellationToken, Task>? stop = null)
        => Update(isRunning, stop, null);

    /// <summary>更新模块拥有的回调和运行状态。</summary>
    /// <param name="isRunning">识别是否正在运行。</param>
    /// <param name="stop">用于停止识别的回调。</param>
    /// <param name="forceSyncGameState">用于强制同步对局状态的回调。</param>
    public void Update(bool isRunning, Func<CancellationToken, Task>? stop, Func<CancellationToken, Task>? forceSyncGameState)
    {
        var changed = false;
        lock (_sync)
        {
            if (stop is not null) _stop = stop;
            if (forceSyncGameState is not null) _forceSyncGameState = forceSyncGameState;
            changed = _isRunning != isRunning;
            _isRunning = isRunning;
        }
        if (changed) StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
