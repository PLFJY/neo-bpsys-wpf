using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace neo_bpsys_wpf.FocusKeeper;

/// <summary>
/// 监听应用停止事件，确保主程序退出时卸载 FocusKeeper 钩子。
/// </summary>
/// <remarks>
/// Generic Host 在 <c>StopAsync</c> 时触发 <c>ApplicationStopping</c>，
/// 此托管服务在该回调中调用 <see cref="IFocusKeeperService.Dispose"/>，
/// 保证目标进程中的 subclass 与 IAT hook 被完整清理（含 <c>FocusKeeper_Finalize</c>），
/// 避免目标进程因钩子 DLL 被强制卸载而崩溃。
/// </remarks>
public sealed class FocusKeeperShutdownHostedService : IHostedService, IDisposable
{
    private readonly IFocusKeeperService _service;
    private readonly ILogger<FocusKeeperShutdownHostedService>? _logger;
    private readonly IHostApplicationLifetime _lifetime;
    private CancellationTokenRegistration _stoppingRegistration;
    private int _disposed;

    /// <summary>
    /// 创建实例。
    /// </summary>
    /// <param name="service">焦点保持服务。</param>
    /// <param name="lifetime">应用生命周期。</param>
    /// <param name="logger">日志记录器。</param>
    /// <exception cref="ArgumentNullException"><paramref name="service"/> 或 <paramref name="lifetime"/> 为 <see langword="null"/>。</exception>
    public FocusKeeperShutdownHostedService(
        IFocusKeeperService service,
        IHostApplicationLifetime lifetime,
        ILogger<FocusKeeperShutdownHostedService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(lifetime);
        _service = service;
        _lifetime = lifetime;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // 保存注册句柄，保证 StopAsync/Dispose 时注销回调，避免应用生命周期对象长期持有本服务。
        _stoppingRegistration = _lifetime.ApplicationStopping.Register(OnApplicationStopping);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        Dispose();
        return Task.CompletedTask;
    }

    /// <summary>注销生命周期回调并释放焦点保持服务；重复调用安全。</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            _stoppingRegistration.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // 应用生命周期已释放时忽略。
        }

        try
        {
            if (_service is IDisposable disposable)
            {
                _logger?.LogInformation("Application stopping: disposing FocusKeeperService to uninstall hooks.");
                disposable.Dispose();
            }
        }
        catch (Exception ex)
        {
            // 退出阶段吞掉异常，避免影响主程序关闭
            _logger?.LogError(ex, "Failed to dispose FocusKeeperService on application stopping.");
        }
    }

    private void OnApplicationStopping() => Dispose();
}
