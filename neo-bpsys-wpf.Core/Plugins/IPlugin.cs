using Microsoft.Extensions.DependencyInjection;

namespace neo_bpsys_wpf.Core.Plugins;

/// <summary>
/// 插件基础接口，所有插件必须实现此接口
/// </summary>
public interface IPlugin
{
    /// <summary>
    /// 插件唯一标识符
    /// </summary>
    string Id { get; }

    /// <summary>
    /// 插件名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 插件版本
    /// </summary>
    Version Version { get; }

    /// <summary>
    /// 插件描述
    /// </summary>
    string Description { get; }

    /// <summary>
    /// 插件作者
    /// </summary>
    string Author { get; }

    /// <summary>
    /// 插件依赖的其他插件ID列表
    /// </summary>
    IReadOnlyList<string> Dependencies { get; }

    /// <summary>
    /// 配置插件服务，在依赖注入容器构建之前调用
    /// </summary>
    /// <param name="services">服务集合</param>
    void ConfigureServices(IServiceCollection services);

    /// <summary>
    /// 初始化插件，在依赖注入容器构建之后调用
    /// </summary>
    /// <param name="serviceProvider">服务提供者</param>
    /// <returns>异步任务</returns>
    Task InitializeAsync(IServiceProvider serviceProvider);

    /// <summary>
    /// 启用插件
    /// </summary>
    /// <returns>异步任务</returns>
    Task EnableAsync();

    /// <summary>
    /// 禁用插件
    /// </summary>
    /// <returns>异步任务</returns>
    Task DisableAsync();

    /// <summary>
    /// 卸载插件，释放资源
    /// </summary>
    /// <returns>异步任务</returns>
    Task UnloadAsync();
}
