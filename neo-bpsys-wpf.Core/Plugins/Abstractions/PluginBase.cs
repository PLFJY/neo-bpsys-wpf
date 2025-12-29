using Microsoft.Extensions.DependencyInjection;

namespace neo_bpsys_wpf.Core.Plugins.Abstractions;

/// <summary>
/// 插件基类，提供默认实现
/// </summary>
public abstract class PluginBase : IPlugin
{
    private PluginState _state = PluginState.NotLoaded;
    private IServiceProvider? _serviceProvider;
    private bool _disposed;

    /// <inheritdoc/>
    public abstract IPluginMetadata Metadata { get; }

    /// <inheritdoc/>
    public PluginState State
    {
        get => _state;
        protected set => _state = value;
    }

    /// <summary>
    /// 获取服务提供者
    /// </summary>
    protected IServiceProvider ServiceProvider => _serviceProvider 
        ?? throw new InvalidOperationException("Plugin has not been initialized yet.");

    /// <inheritdoc/>
    public virtual void ConfigureServices(IServiceCollection services)
    {
        // 默认不注册任何服务，子类可以重写此方法
    }

    /// <inheritdoc/>
    public virtual Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        _serviceProvider = serviceProvider;
        State = PluginState.Initialized;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public virtual Task StartAsync(CancellationToken cancellationToken = default)
    {
        State = PluginState.Running;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public virtual Task StopAsync(CancellationToken cancellationToken = default)
    {
        State = PluginState.Stopped;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    /// <param name="disposing">是否释放托管资源</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // 释放托管资源
            }
            _disposed = true;
        }
    }
}
