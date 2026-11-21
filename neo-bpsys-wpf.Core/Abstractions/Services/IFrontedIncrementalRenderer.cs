using neo_bpsys_wpf.Core.Models.FrontedLayout;
using System;
using System.Collections.Generic;
using System.Windows.Controls;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 可选的前台画布增量渲染能力。
/// </summary>
/// <remarks>
/// <para>
/// 适合属性编辑、单个控件增删等局部变更；调用方应在局部刷新失败时回退
/// <see cref="IFrontedRenderer"/> 的完整渲染。完整重建仍适用于画布/窗口切换、
/// 布局整体替换和需要重建名称作用域的场景。
/// </para>
/// <para>
/// 所有方法必须在拥有 <c>canvas</c> 的 WPF Dispatcher 线程上调用；实现应校验
/// <c>canvas</c>、<c>config</c>、<c>context</c> 非空。批量方法的 <c>controlNames</c>
/// 只枚举一次，重复名称按一个处理；返回数量小于去重后的名称数量表示部分失败。
/// </para>
/// </remarks>
public interface IFrontedIncrementalRenderer
{
    /// <summary>
    /// 刷新指定控件的可视化，保留画布、背景和其他控件。
    /// </summary>
    /// <param name="canvas">目标画布。</param>
    /// <param name="config">当前画布配置。</param>
    /// <param name="context">渲染上下文。</param>
    /// <param name="controlName">控件名称。</param>
    /// <returns>成功刷新时为 <see langword="true"/>。</returns>
    bool RefreshControl(
        Canvas canvas,
        FrontedCanvasConfig config,
        FrontedRenderContext context,
        string controlName);

    /// <summary>
    /// 批量刷新指定控件，只解析一次画布运行时状态。
    /// </summary>
    /// <param name="canvas">目标画布。</param>
    /// <param name="config">当前画布配置。</param>
    /// <param name="context">渲染上下文。</param>
    /// <param name="controlNames">控件名称集合；只枚举一次。</param>
    /// <returns>成功刷新的数量；小于去重后的名称数量表示部分失败。</returns>
    int RefreshControls(
        Canvas canvas,
        FrontedCanvasConfig config,
        FrontedRenderContext context,
        IEnumerable<string> controlNames);

    /// <summary>
    /// 向现有画布增量添加一个控件。
    /// </summary>
    /// <param name="canvas">目标画布。</param>
    /// <param name="config">当前画布配置。</param>
    /// <param name="context">渲染上下文。</param>
    /// <param name="controlName">控件名称。</param>
    /// <returns>成功添加时为 <see langword="true"/>。</returns>
    bool AddControl(
        Canvas canvas,
        FrontedCanvasConfig config,
        FrontedRenderContext context,
        string controlName);

    /// <summary>
    /// 批量向现有画布增量添加控件，只解析一次画布运行时状态。
    /// </summary>
    /// <param name="canvas">目标画布。</param>
    /// <param name="config">当前画布配置。</param>
    /// <param name="context">渲染上下文。</param>
    /// <param name="controlNames">控件名称集合；只枚举一次。</param>
    /// <returns>成功添加的数量；小于去重后的名称数量表示部分失败。</returns>
    int AddControls(
        Canvas canvas,
        FrontedCanvasConfig config,
        FrontedRenderContext context,
        IEnumerable<string> controlNames);

    /// <summary>
    /// 从现有画布增量移除一个控件。
    /// </summary>
    /// <param name="canvas">目标画布。</param>
    /// <param name="controlName">控件名称。</param>
    /// <returns>找到并移除控件时为 <see langword="true"/>。</returns>
    bool RemoveControl(Canvas canvas, string controlName);

    /// <summary>
    /// 批量从现有画布增量移除控件。
    /// </summary>
    /// <param name="canvas">目标画布。</param>
    /// <param name="controlNames">控件名称集合；只枚举一次。</param>
    /// <returns>实际移除的数量；小于去重后的名称数量表示部分失败。</returns>
    int RemoveControls(Canvas canvas, IEnumerable<string> controlNames);
}
