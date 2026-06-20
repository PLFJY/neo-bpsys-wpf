using neo_bpsys_wpf.Core.Abstractions.Services;

namespace neo_bpsys_wpf.Services.SmartBpModule;

/// <summary>Bridges SmartBP module running state into the host process.</summary>
public sealed class SmartBpAutoRecognitionGlobalControl : ISmartBpAutoRecognitionGlobalControl, ISmartBpAutoRecognitionGlobalControlSink
{
    private readonly Lock _sync = new();
    private Func<CancellationToken, Task>? _stop;
    private bool _isRunning;

    /// <inheritdoc />
    public bool IsRunning { get { lock (_sync) return _isRunning; } }

    /// <inheritdoc />
    public event EventHandler? StateChanged;

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        Func<CancellationToken, Task>? stop;
        lock (_sync) stop = _stop;
        return stop?.Invoke(cancellationToken) ?? Task.CompletedTask;
    }

    /// <summary>Updates the module-owned stop callback and running state.</summary>
    /// <param name="isRunning">Whether recognition is running.</param>
    /// <param name="stop">Callback used to stop recognition.</param>
    public void Update(bool isRunning, Func<CancellationToken, Task>? stop = null)
    {
        var changed = false;
        lock (_sync)
        {
            if (stop is not null) _stop = stop;
            changed = _isRunning != isRunning;
            _isRunning = isRunning;
        }
        if (changed) StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
