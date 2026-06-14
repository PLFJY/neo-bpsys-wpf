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
    /// Marks an existing v3 fronted window layout dirty without creating the window.
    /// </summary>
    /// <param name="windowIdOrFullWindowType">The runtime window id or full layout window type.</param>
    void MarkWindowLayoutDirty(string windowIdOrFullWindowType);

    /// <summary>
    /// Applies the stored window background color to a registered fronted window immediately.
    /// </summary>
    /// <param name="fullWindowType">Window layout identity, such as <c>BpWindow</c>.</param>
    /// <returns><see langword="true"/> when a registered window was found and updated.</returns>
    Task<bool> ApplyWindowBackgroundColorAsync(string fullWindowType);

    /// <summary>
    /// Applies the stored window width and height to a registered fronted window immediately.
    /// </summary>
    /// <param name="fullWindowType">Window layout identity, such as <c>BpWindow</c>.</param>
    /// <returns><see langword="true"/> when a registered window was found and updated.</returns>
    Task<bool> ApplyWindowSizeAsync(string fullWindowType);

    /// <summary>
    /// Restarts an already-created fronted window so source-affecting transparency settings can take effect.
    /// </summary>
    /// <param name="fullWindowType">Window layout identity, such as <c>BpWindow</c>.</param>
    /// <returns>
    /// <see langword="true"/> when an existing window instance was removed or restarted;
    /// <see langword="false"/> when the window is not registered or has not been created.
    /// </returns>
    Task<bool> RestartWindowForTransparencyChangeAsync(string fullWindowType);

    /// <summary>
    /// Gets the current width and height of a registered fronted window, or <c>null</c> if the window is not open.
    /// </summary>
    /// <param name="fullWindowType">Window layout identity, such as <c>BpWindow</c>.</param>
    /// <returns>A tuple of (Width, Height), or <c>null</c> when the window is not found.</returns>
    (double Width, double Height)? GetWindowSize(string fullWindowType);

    #endregion

    #region Window Registration

    /// <summary>
    /// Ensures one fronted window instance exists without creating any other fronted windows.
    /// </summary>
    /// <param name="windowId">窗口 GUID</param>
    /// <returns>The existing or newly created window, or <see langword="null"/> when the id is not registered.</returns>
    Window? EnsureWindowCreated(string windowId);

    #endregion

    #region Window Information

    /// <summary>
    /// 获取窗口名称
    /// </summary>
    /// <param name="windowType">窗口类型</param>
    /// <returns>窗口名称，如果未找到则返回 <c>null</c></returns>
    string? GetWindowName(FrontedWindowType windowType);

    /// <summary>
    /// 获取窗口名称
    /// </summary>
    /// <param name="windowId">窗口 GUID</param>
    /// <returns>窗口名称，如果未找到则返回 <c>null</c></returns>
    string? GetWindowName(string windowId);

    #endregion
}
