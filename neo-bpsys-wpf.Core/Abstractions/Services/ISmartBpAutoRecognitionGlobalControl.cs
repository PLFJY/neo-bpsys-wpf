namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>向宿主应用公开 SmartBP 自动识别状态。</summary>
public interface ISmartBpAutoRecognitionGlobalControl
{
    /// <summary>获取 SmartBP 自动识别是否正在运行。</summary>
    bool IsRunning { get; }

    /// <summary>运行状态变化时触发。</summary>
    event EventHandler? StateChanged;

    /// <summary>停止 SmartBP 自动识别。</summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>识别停止后完成的任务。</returns>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 强制同步对局状态
    /// </summary>
    /// <returns>执行强制同步后完成的任务</returns>
    Task ForceSyncGameStateAsync(CancellationToken cancellationToken = default);
}

/// <summary>允许 SmartBP 模块向宿主桥接发布自动识别状态。</summary>
public interface ISmartBpAutoRecognitionGlobalControlSink
{
    /// <summary>更新运行状态和回调。</summary>
    /// <param name="isRunning">识别是否正在运行。</param>
    /// <param name="stop">由模块拥有的停止回调。</param>
    /// <param name="forceSyncGameState">由模块拥有的强制同步对局状态回调。</param>
    void Update(bool isRunning, Func<CancellationToken, Task>? stop = null, Func<CancellationToken, Task>? forceSyncGameState = null);
}
