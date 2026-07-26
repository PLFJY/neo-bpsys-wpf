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
    /// 前台窗口列表（只读视图）。键为窗口 Canonical ID，使用
    /// <see cref="StringComparer.OrdinalIgnoreCase"/> 与注册表的比较语义保持一致。
    /// </summary>
    /// <remarks>
    /// 公开为 <see cref="IReadOnlyDictionary{TKey, TValue}"/> 以防止外部消费者直接修改缓存。
    /// 内部可变字典保持 private。需要修改窗口缓存必须通过服务方法（如
    /// <see cref="EnsureWindowCreated"/>、<see cref="ShowWindow(string)"/>、
    /// <see cref="HideWindow(string)"/> 等）。
    /// </remarks>
    IReadOnlyDictionary<string, Window> FrontedWindows { get; }

    /// <summary>
    /// 前台窗口状态列表（只读视图）。键为窗口 Canonical ID，值为窗口是否可见。
    /// 比较语义与 <see cref="FrontedWindows"/> 一致。
    /// </summary>
    /// <remarks>
    /// 公开为 <see cref="IReadOnlyDictionary{TKey, TValue}"/> 以防止外部消费者直接修改状态缓存。
    /// 内部可变字典保持 private。需要修改窗口状态必须通过服务方法。
    /// </remarks>
    IReadOnlyDictionary<string, bool> FrontedWindowStates { get; }

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
    /// 在当前已注册的前台窗口支持时，重新加载其 v3 布局。
    /// </summary>
    Task ReloadFrontedLayoutsAsync();

    /// <summary>
    /// 将已存在的 v3 前台窗口布局标记为脏，但不创建该窗口。
    /// </summary>
    /// <param name="windowIdOrFullWindowType">运行时窗口 ID 或完整的布局窗口类型。</param>
    void MarkWindowLayoutDirty(string windowIdOrFullWindowType);

    /// <summary>
    /// 将存储的窗口背景色立即应用到已注册的前台窗口。
    /// </summary>
    /// <param name="fullWindowType">窗口布局标识，例如 <c>BpWindow</c>。</param>
    /// <returns>找到并更新已注册窗口时返回 <see langword="true"/>。</returns>
    Task<bool> ApplyWindowBackgroundColorAsync(string fullWindowType);

    /// <summary>
    /// 将存储的窗口宽高立即应用到已注册的前台窗口。
    /// </summary>
    /// <param name="fullWindowType">窗口布局标识，例如 <c>BpWindow</c>。</param>
    /// <returns>找到并更新已注册窗口时返回 <see langword="true"/>。</returns>
    Task<bool> ApplyWindowSizeAsync(string fullWindowType);

    /// <summary>
    /// 重启已创建的前台窗口，以便影响源的透明度设置生效。
    /// </summary>
    /// <param name="fullWindowType">窗口布局标识，例如 <c>BpWindow</c>。</param>
    /// <returns>
    /// 当已存在的窗口实例被移除或重启时返回 <see langword="true"/>；
    /// 当窗口未注册或尚未创建时返回 <see langword="false"/>。
    /// </returns>
    Task<bool> RestartWindowForTransparencyChangeAsync(string fullWindowType);

    /// <summary>
    /// 获取已注册前台窗口的当前宽高，窗口未打开时返回 <c>null</c>。
    /// </summary>
    /// <param name="fullWindowType">窗口布局标识，例如 <c>BpWindow</c>。</param>
    /// <returns>由 (Width, Height) 组成的元组，窗口未找到时返回 <c>null</c>。</returns>
    (double Width, double Height)? GetWindowSize(string fullWindowType);

    #endregion

    #region Window Registration

    /// <summary>
    /// 确保存在一个前台窗口实例，且不创建任何其他前台窗口。
    /// </summary>
    /// <param name="windowId">窗口 GUID</param>
    /// <returns>已存在或新建的窗口；当 ID 未注册时返回 <see langword="null"/>。</returns>
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
