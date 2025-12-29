using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Plugins.Events;
using neo_bpsys_wpf.Core.Plugins.Services;
using neo_bpsys_wpf.Plugins.Services;

namespace neo_bpsys_wpf.Plugins.Hosting;

/// <summary>
/// 插件系统托管服务，负责在应用启动时加载插件
/// </summary>
public class PluginHostedService : IHostedService
{
    private readonly IPluginManager _pluginManager;
    private readonly IPluginEventBus _eventBus;
    private readonly IPluginConfigurationService _configurationService;
    private readonly PluginSystemOptions _options;
    private readonly ILogger<PluginHostedService> _logger;

    public PluginHostedService(
        IPluginManager pluginManager,
        IPluginEventBus eventBus,
        IPluginConfigurationService configurationService,
        PluginSystemOptions options,
        ILogger<PluginHostedService> logger)
    {
        _pluginManager = pluginManager;
        _eventBus = eventBus;
        _configurationService = configurationService;
        _options = options;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Plugin system starting...");

        try
        {
            // 加载插件配置
            await _configurationService.LoadAsync(cancellationToken);

            // 自动加载插件
            if (_options.AutoLoadPlugins && _pluginManager is PluginManager manager)
            {
                await manager.LoadAllPluginsAsync(cancellationToken);
            }

            // 发布应用程序启动事件
            await _eventBus.PublishAsync(new ApplicationStartedEvent(), cancellationToken);

            _logger.LogInformation("Plugin system started. Loaded {Count} plugins.", 
                _pluginManager.LoadedPlugins.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting plugin system");
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Plugin system stopping...");

        try
        {
            // 发布应用程序关闭事件
            await _eventBus.PublishAsync(new ApplicationShuttingDownEvent(), cancellationToken);

            // 停止所有插件
            foreach (var plugin in _pluginManager.LoadedPlugins.ToList())
            {
                try
                {
                    await _pluginManager.UnloadPluginAsync(plugin.Metadata.Id, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error unloading plugin {PluginId}", plugin.Metadata.Id);
                }
            }

            // 保存插件配置
            await _configurationService.SaveAsync(cancellationToken);

            _logger.LogInformation("Plugin system stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping plugin system");
        }
    }
}
