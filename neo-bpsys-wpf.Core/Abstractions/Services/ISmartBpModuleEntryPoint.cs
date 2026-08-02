using neo_bpsys_wpf.Core.Models.SmartBpModule;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 由 SmartBP 运行时模块实现的入口点。
/// </summary>
public interface ISmartBpModuleEntryPoint
{
    /// <summary>
    /// 创建实际的 SmartBP 页面内容。
    /// </summary>
    /// <param name="hostServices">宿主服务提供程序。</param>
    /// <returns>WPF 内容对象。</returns>
    object CreateSmartBpContent(IServiceProvider hostServices);

    /// <summary>
    /// 获取该模块公开的功能命令。
    /// </summary>
    /// <returns>功能命令列表。</returns>
    IReadOnlyList<SmartBpFeatureCommand> GetFeatureCommands();

    /// <summary>
    /// 获取赛后数据识别进度源；模块未提供时返回 null。
    /// </summary>
    /// <returns>进度源实例，或 null。</returns>
    ISmartBpPostGameRecognitionProgressSource? GetPostGameRecognitionProgressSource();
}
