using neo_bpsys_wpf.Core.Models.FrontedLayout;
using System.Windows.Controls;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// v3 前台 Canvas 渲染器。
/// </summary>
public interface IFrontedRenderer
{
    /// <summary>
    /// 按 v3 配置渲染 Canvas。
    /// </summary>
    void RenderToCanvas(Canvas canvas, FrontedCanvasConfig config, FrontedRenderContext context);
}
