using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Plugins.Events;
using System.IO;

namespace neo_bpsys_wpf.Core.Plugins;

/// <summary>
/// 插件系统宿主服务，用于在应用程序启动时加载插件
/// </summary>
public sealed class PluginHostService : IHostedService
{
    private readonly ILogger<PluginHostService> _logger;
    private readonly IPluginManager _pluginManager;
    private readonly IPluginEventBus _eventBus;
    private readonly PluginSystemOptions _options;

    /// <summary>
    /// 创建插件系统宿主服务
    /// </summary>
    public PluginHostService(
        ILogger<PluginHostService> logger,
        IPluginManager pluginManager,
        IPluginEventBus eventBus,
        PluginSystemOptions? options = null)
    {
        _logger = logger;
        _pluginManager = pluginManager;
        _eventBus = eventBus;
        _options = options ?? new PluginSystemOptions();
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Plugin system starting...");

        try
        {
            // 确保插件目录存在
            if (!Directory.Exists(_options.PluginDirectory))
            {
                Directory.CreateDirectory(_options.PluginDirectory);
                _logger.LogInformation("Created plugin directory: {Directory}", _options.PluginDirectory);
            }

            if (_options.AutoLoadPlugins)
            {
                _logger.LogInformation("Auto-loading plugins from: {Directory}", _options.PluginDirectory);

                var results = await _pluginManager.LoadPluginsAsync(_options.PluginDirectory);

                var successCount = results.Count(r => r.Success);
                var failCount = results.Count(r => !r.Success);

                _logger.LogInformation("Plugin loading completed. Success: {SuccessCount}, Failed: {FailCount}",
                    successCount, failCount);

                foreach (var result in results.Where(r => !r.Success))
                {
                    _logger.LogWarning("Failed to load plugin: {Error}", result.ErrorMessage);
                }
            }

            // 发布应用程序启动事件
            await _eventBus.PublishAsync(new ApplicationStartedEvent());

            _logger.LogInformation("Plugin system started successfully. {Count} plugin(s) loaded.",
                _pluginManager.LoadedPlugins.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting plugin system");

            if (!_options.ContinueOnLoadError)
            {
                throw;
            }
        }
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Plugin system stopping...");

        try
        {
            // 发布应用程序关闭事件
            await _eventBus.PublishAsync(new ApplicationShuttingDownEvent());

            // 卸载所有插件
            var pluginIds = _pluginManager.LoadedPlugins.Select(p => p.Id).ToList();

            foreach (var pluginId in pluginIds)
            {
                try
                {
                    await _pluginManager.UnloadPluginAsync(pluginId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error unloading plugin: {PluginId}", pluginId);
                }
            }

            _logger.LogInformation("Plugin system stopped.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping plugin system");
        }
    }
}
