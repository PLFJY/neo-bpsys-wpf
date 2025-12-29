using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Plugins.Services;

namespace neo_bpsys_wpf.Plugins.Services;

/// <summary>
/// 插件日志记录器实现
/// </summary>
public class PluginLogger : IPluginLogger
{
    private readonly ILogger _logger;
    private readonly string _pluginId;

    public PluginLogger(ILogger logger, string pluginId)
    {
        _logger = logger;
        _pluginId = pluginId;
    }

    /// <inheritdoc/>
    public void Debug(string message)
    {
        _logger.LogDebug("[{PluginId}] {Message}", _pluginId, message);
    }

    /// <inheritdoc/>
    public void Debug(string message, params object[] args)
    {
        _logger.LogDebug($"[{_pluginId}] {message}", args);
    }

    /// <inheritdoc/>
    public void Info(string message)
    {
        _logger.LogInformation("[{PluginId}] {Message}", _pluginId, message);
    }

    /// <inheritdoc/>
    public void Info(string message, params object[] args)
    {
        _logger.LogInformation($"[{_pluginId}] {message}", args);
    }

    /// <inheritdoc/>
    public void Warning(string message)
    {
        _logger.LogWarning("[{PluginId}] {Message}", _pluginId, message);
    }

    /// <inheritdoc/>
    public void Warning(string message, params object[] args)
    {
        _logger.LogWarning($"[{_pluginId}] {message}", args);
    }

    /// <inheritdoc/>
    public void Error(string message)
    {
        _logger.LogError("[{PluginId}] {Message}", _pluginId, message);
    }

    /// <inheritdoc/>
    public void Error(string message, Exception exception)
    {
        _logger.LogError(exception, "[{PluginId}] {Message}", _pluginId, message);
    }

    /// <inheritdoc/>
    public void Error(string message, params object[] args)
    {
        _logger.LogError($"[{_pluginId}] {message}", args);
    }

    /// <inheritdoc/>
    public void Fatal(string message)
    {
        _logger.LogCritical("[{PluginId}] {Message}", _pluginId, message);
    }

    /// <inheritdoc/>
    public void Fatal(string message, Exception exception)
    {
        _logger.LogCritical(exception, "[{PluginId}] {Message}", _pluginId, message);
    }
}

/// <summary>
/// 插件日志工厂实现
/// </summary>
public class PluginLoggerFactory : IPluginLoggerFactory
{
    private readonly ILoggerFactory _loggerFactory;

    public PluginLoggerFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    /// <inheritdoc/>
    public IPluginLogger CreateLogger(string pluginId)
    {
        var logger = _loggerFactory.CreateLogger($"Plugin.{pluginId}");
        return new PluginLogger(logger, pluginId);
    }

    /// <inheritdoc/>
    public IPluginLogger CreateLogger<T>(string pluginId)
    {
        var logger = _loggerFactory.CreateLogger<T>();
        return new PluginLogger(logger, pluginId);
    }
}
