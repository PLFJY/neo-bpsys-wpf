using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Plugins;
using neo_bpsys_wpf.Core.Abstractions.Services;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 插件导航服务实现
/// Plugin navigation service implementation
/// </summary>
public class PluginNavigationService : IPluginNavigationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PluginNavigationService> _logger;
    private readonly ConcurrentDictionary<string, List<PluginPageDescriptor>> _pluginPages = new();

    public PluginNavigationService(
        IServiceProvider serviceProvider,
        ILogger<PluginNavigationService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public event EventHandler<PluginPagesRegisteredEventArgs>? PluginPagesRegistered;

    /// <inheritdoc />
    public event EventHandler<PluginPagesUnregisteredEventArgs>? PluginPagesUnregistered;

    /// <inheritdoc />
    public void RegisterPluginPages(IUIPlugin plugin)
    {
        if (plugin == null) throw new ArgumentNullException(nameof(plugin));

        _logger.LogInformation("Registering pages for plugin: {PluginId}", plugin.Id);

        var pages = plugin.GetPages()?.ToList() ?? new List<PluginPageDescriptor>();
        
        if (pages.Count == 0)
        {
            _logger.LogInformation("Plugin {PluginId} has no pages to register", plugin.Id);
            return;
        }

        // 配置插件服务到DI容器
        var serviceCollection = new ServiceCollection();
        foreach (var service in _serviceProvider.GetServices<object>())
        {
            // 这里简化处理，实际应该从现有容器复制服务描述符
        }
        
        plugin.ConfigureServices(serviceCollection);

        // 存储页面描述符
        _pluginPages[plugin.Id] = pages;

        _logger.LogInformation("Registered {Count} pages for plugin: {PluginId}", pages.Count, plugin.Id);

        // 触发事件
        PluginPagesRegistered?.Invoke(this, new PluginPagesRegisteredEventArgs
        {
            PluginId = plugin.Id,
            Pages = pages
        });
    }

    /// <inheritdoc />
    public void UnregisterPluginPages(string pluginId)
    {
        if (string.IsNullOrEmpty(pluginId))
            throw new ArgumentException("Plugin ID cannot be null or empty", nameof(pluginId));

        _logger.LogInformation("Unregistering pages for plugin: {PluginId}", pluginId);

        if (_pluginPages.TryRemove(pluginId, out var pages))
        {
            _logger.LogInformation("Unregistered {Count} pages for plugin: {PluginId}", 
                pages.Count, pluginId);

            // 触发事件
            PluginPagesUnregistered?.Invoke(this, new PluginPagesUnregisteredEventArgs
            {
                PluginId = pluginId
            });
        }
        else
        {
            _logger.LogWarning("No pages found for plugin: {PluginId}", pluginId);
        }
    }

    /// <inheritdoc />
    public IEnumerable<PluginPageDescriptor> GetPluginPages()
    {
        return _pluginPages.Values.SelectMany(pages => pages).OrderBy(p => p.Priority);
    }
}
