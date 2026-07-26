using System.ComponentModel;

namespace neo_bpsys_wpf.FocusKeeper;

/// <summary>
/// 提供游戏窗口焦点保持能力：拦截 WM_KILLFOCUS / WM_ACTIVATE / WM_ACTIVATEAPP
/// 等消息，使游戏在失去前台焦点时继续运行而不暂停或静音。
/// </summary>
public interface IFocusKeeperService : INotifyPropertyChanged, IDisposable
{
    /// <summary>获取当前是否已注入到目标进程。</summary>
    bool IsInstalled { get; }

    /// <summary>
    /// 获取主程序当前是否以管理员权限运行。
    /// </summary>
    /// <remarks>
    /// 若主程序未提权而目标游戏以管理员权限运行，
    /// <c>SetWindowsHookEx</c> 跨进程注入将因 <c>ERROR_ACCESS_DENIED</c> 失败。
    /// </remarks>
    bool IsCurrentProcessElevated { get; }

    /// <summary>
    /// 以管理员权限（UAC 提升）重启主程序。
    /// </summary>
    /// <returns>是否成功发起重启流程；用户在 UAC 提示中拒绝提权时返回 <c>false</c>。</returns>
    bool RestartAsAdmin();

    /// <summary>获取或设置焦点保持是否处于激活状态（仅在已注入时有效）。</summary>
    bool IsEnabled { get; set; }

    /// <summary>获取当前目标进程的名称（已注入时非空）。</summary>
    string? TargetProcessName { get; }

    /// <summary>获取当前目标进程的 PID（已注入时非空）。</summary>
    int? TargetProcessId { get; }

    /// <summary>获取最近一次错误信息（安装失败等）。</summary>
    string? ErrorMessage { get; }

    /// <summary>
    /// 枚举系统中可能为游戏的可见顶层窗口，供用户选择注入目标。
    /// </summary>
    /// <returns>窗口句柄与进程信息的列表。</returns>
    IReadOnlyList<GameWindowInfo> EnumerateGameWindows();

    /// <summary>
    /// 自动查找第五人格游戏窗口并注入。
    /// </summary>
    /// <returns>是否成功安装。</returns>
    bool FindAndInstall();

    /// <summary>
    /// 注入到指定窗口所属的进程。
    /// </summary>
    /// <param name="windowHandle">目标窗口句柄。</param>
    /// <returns>是否成功安装。</returns>
    bool Install(IntPtr windowHandle);

    /// <summary>
    /// 卸载钩子并清理所有 subclass，解除对目标进程的影响。
    /// </summary>
    void Uninstall();
}
