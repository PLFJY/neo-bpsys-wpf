namespace neo_bpsys_wpf.Core.Abstractions.Plugins;

/// <summary>
/// 插件基础接口 - 所有插件必须实现此接口
/// Base interface for all plugins
/// </summary>
public interface IPlugin
{
    /// <summary>
    /// 插件唯一标识符
    /// Unique plugin identifier
    /// </summary>
    string Id { get; }

    /// <summary>
    /// 插件名称
    /// Plugin name
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 插件描述
    /// Plugin description
    /// </summary>
    string Description { get; }

    /// <summary>
    /// 插件版本
    /// Plugin version
    /// </summary>
    Version Version { get; }

    /// <summary>
    /// 插件作者
    /// Plugin author
    /// </summary>
    string Author { get; }

    /// <summary>
    /// 插件初始化
    /// Initialize the plugin
    /// </summary>
    /// <param name="context">插件上下文 Plugin context</param>
    Task InitializeAsync(IPluginContext context);

    /// <summary>
    /// 插件启动
    /// Start the plugin
    /// </summary>
    Task StartAsync();

    /// <summary>
    /// 插件停止
    /// Stop the plugin
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// 插件卸载
    /// Dispose the plugin
    /// </summary>
    Task DisposeAsync();
}
