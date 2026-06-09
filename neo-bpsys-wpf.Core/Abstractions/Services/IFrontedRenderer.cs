using neo_bpsys_wpf.Core.Models.FrontedLayout;
using System.Windows.Controls;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// v3 前台 Canvas 渲染器。
/// </summary>
public interface IFrontedRenderer
{
    /// <summary>
    /// Renders a window-centric v3 layout to the internal BaseCanvas.
    /// </summary>
    /// <param name="canvas">The internal BaseCanvas.</param>
    /// <param name="config">The window-centric config.</param>
    /// <param name="context">The render context.</param>
    void RenderToCanvas(Canvas canvas, FrontedWindowConfig config, FrontedRenderContext context);

    /// <summary>
    /// Renders a v3 control layout to the internal BaseCanvas.
    /// </summary>
    /// <param name="canvas">The internal BaseCanvas.</param>
    /// <param name="canvasSettings">The canvas settings.</param>
    /// <param name="controlLayout">The control layout.</param>
    /// <param name="context">The render context.</param>
    void RenderToCanvas(
        Canvas canvas,
        FrontedCanvasSettings canvasSettings,
        FrontedControlLayout controlLayout,
        FrontedRenderContext context);

    /// <summary>
    /// Renders a legacy canvas-centric v3 config for conversion and transitional helpers.
    /// </summary>
    void RenderToCanvas(Canvas canvas, FrontedCanvasConfig config, FrontedRenderContext context);
}
