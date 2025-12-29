namespace neo_bpsys_wpf.Core.Abstractions.Plugins;

/// <summary>
/// 插件基类 - 提供插件的基础实现
/// Base plugin class - Provides basic plugin implementation
/// </summary>
public abstract class PluginBase : IPlugin
{
    /// <summary>
    /// 插件上下文
    /// Plugin context
    /// </summary>
    protected IPluginContext? Context { get; private set; }

    /// <inheritdoc />
    public abstract string Id { get; }

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public abstract string Description { get; }

    /// <inheritdoc />
    public abstract Version Version { get; }

    /// <inheritdoc />
    public abstract string Author { get; }

    /// <inheritdoc />
    public virtual async Task InitializeAsync(IPluginContext context)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        await OnInitializeAsync(context);
    }

    /// <inheritdoc />
    public virtual async Task StartAsync()
    {
        await OnStartAsync();
    }

    /// <inheritdoc />
    public virtual async Task StopAsync()
    {
        await OnStopAsync();
    }

    /// <inheritdoc />
    public virtual async Task DisposeAsync()
    {
        await OnDisposeAsync();
        Context = null;
    }

    /// <summary>
    /// 插件初始化时调用
    /// Called when plugin is initialized
    /// </summary>
    protected virtual Task OnInitializeAsync(IPluginContext context)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 插件启动时调用
    /// Called when plugin is started
    /// </summary>
    protected virtual Task OnStartAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 插件停止时调用
    /// Called when plugin is stopped
    /// </summary>
    protected virtual Task OnStopAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 插件销毁时调用
    /// Called when plugin is disposed
    /// </summary>
    protected virtual Task OnDisposeAsync()
    {
        return Task.CompletedTask;
    }
}
