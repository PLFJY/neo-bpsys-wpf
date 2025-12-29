using Microsoft.Extensions.DependencyInjection;

namespace neo_bpsys_wpf.Core.Plugins;

/// <summary>
/// 插件基类，提供IPlugin接口的默认实现
/// </summary>
public abstract class PluginBase : IPlugin
{
    /// <inheritdoc/>
    public abstract string Id { get; }

    /// <inheritdoc/>
    public abstract string Name { get; }

    /// <inheritdoc/>
    public abstract Version Version { get; }

    /// <inheritdoc/>
    public virtual string Description => string.Empty;

    /// <inheritdoc/>
    public virtual string Author => "Unknown";

    /// <inheritdoc/>
    public virtual IReadOnlyList<string> Dependencies => Array.Empty<string>();

    /// <summary>
    /// 服务提供者，在InitializeAsync后可用
    /// </summary>
    protected IServiceProvider? ServiceProvider { get; private set; }

    /// <summary>
    /// 插件是否已启用
    /// </summary>
    public bool IsEnabled { get; private set; }

    /// <summary>
    /// 插件是否已初始化
    /// </summary>
    public bool IsInitialized { get; private set; }

    /// <inheritdoc/>
    public virtual void ConfigureServices(IServiceCollection services)
    {
        // 默认不添加任何服务
    }

    /// <inheritdoc/>
    public virtual Task InitializeAsync(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
        IsInitialized = true;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public virtual Task EnableAsync()
    {
        IsEnabled = true;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public virtual Task DisableAsync()
    {
        IsEnabled = false;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public virtual Task UnloadAsync()
    {
        IsEnabled = false;
        IsInitialized = false;
        ServiceProvider = null;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 从服务提供者获取服务
    /// </summary>
    /// <typeparam name="T">服务类型</typeparam>
    /// <returns>服务实例</returns>
    /// <exception cref="InvalidOperationException">插件未初始化时抛出</exception>
    protected T GetRequiredService<T>() where T : notnull
    {
        if (ServiceProvider == null)
            throw new InvalidOperationException("Plugin has not been initialized. Cannot get services before InitializeAsync is called.");

        return ServiceProvider.GetRequiredService<T>();
    }

    /// <summary>
    /// 从服务提供者获取服务（可能为null）
    /// </summary>
    /// <typeparam name="T">服务类型</typeparam>
    /// <returns>服务实例或null</returns>
    protected T? GetService<T>() where T : class
    {
        return ServiceProvider?.GetService<T>();
    }
}
