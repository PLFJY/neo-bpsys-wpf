namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>Exposes SmartBP automatic-recognition state to the host application.</summary>
public interface ISmartBpAutoRecognitionGlobalControl
{
    /// <summary>Gets whether SmartBP automatic recognition is running.</summary>
    bool IsRunning { get; }

    /// <summary>Occurs when the running state changes.</summary>
    event EventHandler? StateChanged;

    /// <summary>Stops SmartBP automatic recognition.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes after recognition has stopped.</returns>
    Task StopAsync(CancellationToken cancellationToken = default);
}

/// <summary>Allows the SmartBP module to publish automatic-recognition state to the host bridge.</summary>
public interface ISmartBpAutoRecognitionGlobalControlSink
{
    /// <summary>Updates the running state and stop callback.</summary>
    /// <param name="isRunning">Whether recognition is running.</param>
    /// <param name="stop">Module-owned stop callback.</param>
    void Update(bool isRunning, Func<CancellationToken, Task>? stop = null);
}
