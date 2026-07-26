using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using System.Windows;
using System.Windows.Controls;

namespace neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

/// <summary>
/// 用于将行为运行时附加到前台窗口的上下文。
/// </summary>
public sealed class FrontedBehaviorRuntimeContext
{
    /// <summary>
    /// 前台窗口标识。
    /// </summary>
    public required string WindowId { get; init; }

    /// <summary>
    /// 前台窗口类型名称，例如 "BpWindow"。
    /// </summary>
    public required string WindowType { get; init; }

    /// <summary>
    /// 窗口内部的画布名称。以窗口为中心的 v3 布局始终使用 <c>BaseCanvas</c>。
    /// </summary>
    public string CanvasName { get; init; } = FrontedLayoutConstants.BaseCanvasName;

    /// <summary>
    /// 已渲染的 Canvas 根元素。
    /// </summary>
    public required Canvas RootCanvas { get; init; }

    /// <summary>
    /// 当前渲染所使用的窗口布局配置。
    /// </summary>
    public required FrontedWindowConfig WindowConfig { get; init; }

    /// <summary>
    /// 应用程序的共享数据服务实例。
    /// </summary>
    public required ISharedDataService SharedDataService { get; init; }

    /// <summary>
    /// 指示此是否为设计器预览上下文（而非真实前台窗口）。
    /// </summary>
    public bool IsDesignerPreview { get; init; }

    /// <summary>
    /// 可选的日志记录器。
    /// </summary>
    public Microsoft.Extensions.Logging.ILogger? Logger { get; init; }
}
