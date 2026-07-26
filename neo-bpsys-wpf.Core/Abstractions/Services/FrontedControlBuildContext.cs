using Microsoft.Extensions.Logging;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// v3 前台控件创建上下文。
/// </summary>
public class FrontedControlBuildContext
{
    /// <summary>
    /// DI 服务提供器。
    /// </summary>
    public required IServiceProvider Services { get; init; }

    /// <summary>
    /// 共享数据服务。
    /// </summary>
    public required ISharedDataService SharedDataService { get; init; }

    /// <summary>
    /// 前台资源解析器。
    /// </summary>
    public required IFrontedResourceResolver ResourceResolver { get; init; }

    /// <summary>
    /// 前台窗口 ID。
    /// </summary>
    public required string WindowId { get; init; }

    /// <summary>
    /// Canvas 名称。
    /// </summary>
    public required string CanvasName { get; init; }

    /// <summary>
    /// 经过运行时状态解析后生效的画布背景图片。
    /// </summary>
    public string? CanvasBackgroundImage { get; init; }

    /// <summary>
    /// Canvas 宽度。
    /// </summary>
    public double CanvasWidth { get; init; }

    /// <summary>
    /// Canvas 高度。
    /// </summary>
    public double CanvasHeight { get; init; }

    /// <summary>
    /// 指示控件是否为设计器预览而构建。
    /// </summary>
    public bool IsDesignerPreview { get; init; }

    /// <summary>
    /// 可选日志。
    /// </summary>
    public ILogger? Logger { get; init; }
}
