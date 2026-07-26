namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 进程权限（UAC 提升级别）检测服务。
/// </summary>
/// <remarks>
/// 用于判断当前主程序是否以管理员权限运行。
/// 跨进程 API Hook（如 <c>SetWindowsHookEx</c> 注入到更高权限的进程）
/// 要求钩子进程与目标进程具有相同的完整性级别，
/// 若主程序未提权而目标游戏已提权，注入将失败（<c>ERROR_ACCESS_DENIED</c>）。
/// </remarks>
public interface IElevationService
{
    /// <summary>
    /// 获取当前进程是否以管理员权限运行（已提升 UAC）。
    /// </summary>
    /// <value>已提升权限返回 <c>true</c>；普通用户返回 <c>false</c>。</value>
    /// <remarks>
    /// 权限级别在进程启动时确定，运行期间不会变化，因此此值在进程生命周期内保持不变。
    /// </remarks>
    bool IsCurrentProcessElevated { get; }

    /// <summary>
    /// 以管理员权限（UAC 提升）重启主程序。
    /// </summary>
    /// <returns>是否成功发起重启流程；用户在 UAC 提示中拒绝提权时返回 <c>false</c>。</returns>
    /// <remarks>
    /// 调用此方法后，当前进程会在释放单实例互斥锁并启动新进程后退出。
    /// 仅在 <see cref="IsCurrentProcessElevated"/> 为 <c>false</c> 时调用才有意义；
    /// 已提权时调用此方法应直接返回 <c>true</c> 而无需重启。
    /// </remarks>
    bool RestartAsAdmin();
}
