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
    /// Canvas 名称。
    /// </summary>
    public required string CanvasName { get; init; }
}
