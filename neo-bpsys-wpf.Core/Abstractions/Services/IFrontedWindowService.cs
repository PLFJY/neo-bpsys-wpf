using neo_bpsys_wpf.Core.Enums;
using System.Windows;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 前台窗口接口服务
/// </summary>
public interface IFrontedWindowService
{
    #region Properties

    /// <summary>
    /// 前台窗口列表
    /// </summary>
    Dictionary<string, Window> FrontedWindows { get; }

    /// <summary>
    /// 前台窗口状态列表
    /// </summary>
    Dictionary<string, bool> FrontedWindowStates { get; }

    #endregion

    #region Window Management

    /// <summary>
    /// 隐藏全部窗口
    /// </summary>
    void AllWindowHide();

    /// <summary>
    /// 显示全部窗口
    /// </summary>
    void AllWindowShow();

    /// <summary>
    /// 隐藏窗口
    /// </summary>
    /// <param name="windowType">窗口类型</param>
    void HideWindow(FrontedWindowType windowType);

    /// <summary>
    /// 隐藏窗口
    /// </summary>
    /// <param name="windowId">窗口 GUID</param>
    void HideWindow(string windowId);

    /// <summary>
    /// 显示窗口
    /// </summary>
    /// <param name="windowType">窗口类型</param>
    void ShowWindow(FrontedWindowType windowType);

    /// <summary>
    /// 显示窗口s
    /// </summary>
    /// <param name="windowId">窗口 GUID</param>
    void ShowWindow(string windowId);

    /// <summary>
    /// Reloads v3 layouts in currently registered frontend windows when they support it.
    /// </summary>
    Task ReloadFrontedLayoutsAsync();

    /// <summary>
    /// Applies the stored window background color to a registered fronted window immediately.
    /// </summary>
    /// <param name="fullWindowType">Window layout identity, such as <c>BpWindow</c>.</param>
    bool ApplyWindowBackgroundColor(string fullWindowType);

    /// <summary>
    /// Applies the stored window width and height to a registered fronted window immediately.
    /// </summary>
    /// <param name="fullWindowType">Window layout identity, such as <c>BpWindow</c>.</param>
    bool ApplyWindowSize(string fullWindowType);

    /// <summary>
    /// Gets the current width and height of a registered fronted window, or <c>null</c> if the window is not open.
    /// </summary>
    /// <param name="fullWindowType">Window layout identity, such as <c>BpWindow</c>.</param>
    /// <returns>A tuple of (Width, Height), or <c>null</c> when the window is not found.</returns>
    (double Width, double Height)? GetWindowSize(string fullWindowType);

    #endregion

    #region Window Registration

    /// <summary>
    /// 注册窗口
    /// </summary>
    /// <param name="windowId">窗口 GUID</param>
    /// <param name="window">窗口</param>
    /// <param name="canvasNames">旧版调用保留参数，window-centric v3 不再使用。</param>
    void RegisterFrontedWindowAndCanvas(string windowId, Window window, string[]? canvasNames = null);

    #endregion

    #region Window Information

    /// <summary>
    /// 获取窗口名称
    /// </summary>
    /// <param name="windowType"></param>
    /// <returns></returns>
    string? GetWindowName(FrontedWindowType windowType);

    /// <summary>
    /// 获取窗口名称
    /// </summary>
    /// <param name="windowId">窗口 GUID</param>
    /// <returns></returns>
    string? GetWindowName(string windowId);

    #endregion
}
