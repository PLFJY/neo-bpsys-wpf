using Microsoft.Extensions.DependencyInjection;

namespace neo_bpsys_wpf.Core.Plugins.Abstractions;

/// <summary>
/// 插件主接口，所有插件必须实现此接口
/// </summary>
public interface IPlugin : IDisposable
{
    /// <summary>
    /// 获取插件元数据
    /// </summary>
    IPluginMetadata Metadata { get; }

    /// <summary>
    /// 获取插件当前状态
    /// </summary>
    PluginState State { get; }

    /// <summary>
    /// 配置插件服务，在此方法中注册插件的服务到DI容器
    /// </summary>
    /// <param name="services">服务集合</param>
    void ConfigureServices(IServiceCollection services);

    /// <summary>
    /// 初始化插件，在服务容器构建完成后调用
    /// </summary>
    /// <param name="serviceProvider">服务提供者</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default);

    /// <summary>
    /// 启动插件
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止插件
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task StopAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 插件状态枚举
/// </summary>
public enum PluginState
{
    /// <summary>
    /// 未加载
    /// </summary>
    NotLoaded,

    /// <summary>
    /// 已加载但未初始化
    /// </summary>
    Loaded,

    /// <summary>
    /// 已初始化
    /// </summary>
    Initialized,

    /// <summary>
    /// 运行中
    /// </summary>
    Running,

    /// <summary>
    /// 已停止
    /// </summary>
    Stopped,

    /// <summary>
    /// 已禁用
    /// </summary>
    Disabled,

    /// <summary>
    /// 错误状态
    /// </summary>
    Error
}
