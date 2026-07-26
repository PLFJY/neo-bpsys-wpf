namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// v3 前台 Canvas 渲染上下文。
/// </summary>
public class FrontedRenderContext
{
    /// <summary>
    /// 前台窗口 ID。
    /// </summary>
    public required string WindowId { get; init; }

    /// <summary>
    /// 前台窗口类型名（已知时）。
    /// </summary>
    public string? WindowTypeName { get; init; }

    /// <summary>
    /// Canvas 名称。
    /// </summary>
    public required string CanvasName { get; init; }

    /// <summary>
    /// 可选的共享数据覆盖，用于隔离的预览渲染。
    /// </summary>
    public ISharedDataService? SharedDataServiceOverride { get; init; }

    /// <summary>
    /// 指示缺失的插件控件是否应渲染为仅设计器可用的占位符。
    /// </summary>
    public bool RenderMissingPluginPlaceholders { get; init; }

    /// <summary>
    /// 指示本次渲染是否由设计器预览承载。
    /// </summary>
    public bool IsDesignerPreview { get; init; }
}
