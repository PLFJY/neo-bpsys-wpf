using neo_bpsys_wpf.Core.Models.FrontedLayout;
using System.Windows.Controls;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// v3 前台 Canvas 渲染器。
/// </summary>
public interface IFrontedRenderer
{
    /// <summary>
    /// 将以窗口为中心的 v3 布局渲染到内部的 BaseCanvas。
    /// </summary>
    /// <param name="canvas">内部的 BaseCanvas。</param>
    /// <param name="config">以窗口为中心的配置。</param>
    /// <param name="context">渲染上下文。</param>
    void RenderToCanvas(Canvas canvas, FrontedWindowConfig config, FrontedRenderContext context);

    /// <summary>
    /// 将 v3 控件布局渲染到内部的 BaseCanvas。
    /// </summary>
    /// <param name="canvas">内部的 BaseCanvas。</param>
    /// <param name="canvasSettings">画布设置。</param>
    /// <param name="controlLayout">控件布局。</param>
    /// <param name="context">渲染上下文。</param>
    void RenderToCanvas(
        Canvas canvas,
        FrontedCanvasSettings canvasSettings,
        FrontedControlLayout controlLayout,
        FrontedRenderContext context);

    /// <summary>
    /// 渲染以画布为中心的旧版 v3 配置，用于转换和过渡辅助。
    /// </summary>
    void RenderToCanvas(Canvas canvas, FrontedCanvasConfig config, FrontedRenderContext context);
}
