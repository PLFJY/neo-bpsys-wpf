using neo_bpsys_wpf.Core.Models.FrontedLayout;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// v3 前台布局配置读写服务。
/// </summary>
public interface IFrontedLayoutService
{
    /// <summary>
    /// 加载 Canvas 配置。
    /// </summary>
    Task<FrontedCanvasConfig?> LoadCanvasConfigAsync(
        string windowTypeName,
        string canvasName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 保存 Canvas 配置到用户布局路径。
    /// </summary>
    Task SaveCanvasConfigAsync(
        string windowTypeName,
        string canvasName,
        FrontedCanvasConfig config,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取用户布局路径。
    /// </summary>
    string GetUserLayoutPath(string windowTypeName, string canvasName);

    /// <summary>
    /// 获取内置默认布局路径。
    /// </summary>
    string GetBuiltInDefaultLayoutPath(string windowTypeName, string canvasName);

    /// <summary>
    /// 获取插件默认布局路径。
    /// </summary>
    string GetPluginDefaultLayoutPath(string pluginFolder, string windowTypeName, string canvasName);
}
