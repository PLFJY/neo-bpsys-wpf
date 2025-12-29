using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Core.Plugins;
using neo_bpsys_wpf.Core.Plugins.Abstractions;
using neo_bpsys_wpf.Core.Plugins.Services;

namespace neo_bpsys_wpf.Plugins.Hosting;

/// <summary>
/// 插件上下文实现
/// </summary>
public class PluginContext : IPluginContext
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IPluginLogger _logger;

    public PluginContext(
        IPluginMetadata metadata,
        IServiceProvider serviceProvider,
        IPluginLoggerFactory loggerFactory)
    {
        Metadata = metadata;
        _serviceProvider = serviceProvider;
        _logger = loggerFactory.CreateLogger(metadata.Id);
    }

    /// <inheritdoc/>
    public IPluginMetadata Metadata { get; }

    /// <inheritdoc/>
    public IHostApplicationService HostApplication => _serviceProvider.GetRequiredService<IHostApplicationService>();

    /// <inheritdoc/>
    public IPluginManager PluginManager => _serviceProvider.GetRequiredService<IPluginManager>();

    /// <inheritdoc/>
    public Core.Plugins.Events.IPluginEventBus EventBus => _serviceProvider.GetRequiredService<Core.Plugins.Events.IPluginEventBus>();

    /// <inheritdoc/>
    public IUIExtensionService UIExtensions => _serviceProvider.GetRequiredService<IUIExtensionService>();

    /// <inheritdoc/>
    public IPluginConfigurationService Configuration => _serviceProvider.GetRequiredService<IPluginConfigurationService>();

    /// <inheritdoc/>
    public IPluginResourceService Resources => _serviceProvider.GetRequiredService<IPluginResourceService>();

    /// <inheritdoc/>
    public IPluginLogger Logger => _logger;

    /// <inheritdoc/>
    public IServiceProvider ServiceProvider => _serviceProvider;

    /// <inheritdoc/>
    public T GetService<T>() where T : class
    {
        return _serviceProvider.GetRequiredService<T>();
    }

    /// <inheritdoc/>
    public T? TryGetService<T>() where T : class
    {
        return _serviceProvider.GetService<T>();
    }
}

/// <summary>
/// 插件上下文工厂
/// </summary>
public class PluginContextFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IPluginLoggerFactory _loggerFactory;

    public PluginContextFactory(IServiceProvider serviceProvider, IPluginLoggerFactory loggerFactory)
    {
        _serviceProvider = serviceProvider;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// 创建插件上下文
    /// </summary>
    /// <param name="metadata">插件元数据</param>
    /// <returns>插件上下文</returns>
    public IPluginContext CreateContext(IPluginMetadata metadata)
    {
        return new PluginContext(metadata, _serviceProvider, _loggerFactory);
    }
}
