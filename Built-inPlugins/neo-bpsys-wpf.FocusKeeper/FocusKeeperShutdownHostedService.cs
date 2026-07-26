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
public sealed class FocusKeeperShutdownHostedService : IHostedService
{
    private readonly IFocusKeeperService _service;
    private readonly ILogger<FocusKeeperShutdownHostedService>? _logger;
    private readonly IHostApplicationLifetime _lifetime;

    /// <summary>
    /// 创建实例。
    /// </summary>
    /// <param name="service">焦点保持服务。</param>
    /// <param name="lifetime">应用生命周期。</param>
    /// <param name="logger">日志记录器。</param>
    public FocusKeeperShutdownHostedService(
        IFocusKeeperService service,
        IHostApplicationLifetime lifetime,
        ILogger<FocusKeeperShutdownHostedService>? logger = null)
    {
        _service = service;
        _lifetime = lifetime;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _lifetime.ApplicationStopping.Register(OnApplicationStopping);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void OnApplicationStopping()
    {
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
}
