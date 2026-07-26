using System.ComponentModel;
using System.Security.Principal;
using System.Windows;
using neo_bpsys_wpf.Core.Abstractions.Services;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// <see cref="IElevationService"/> 的实现，通过 WindowsIdentity / WindowsPrincipal 检测当前进程的 UAC 提升状态。
/// </summary>
public sealed class ElevationService : IElevationService
{
    /// <inheritdoc />
    public bool IsCurrentProcessElevated { get; }

    /// <summary>
    /// 创建实例并立即检测当前进程的权限级别。
    /// </summary>
    public ElevationService()
    {
        IsCurrentProcessElevated = DetectElevation();
    }

    /// <inheritdoc />
    public bool RestartAsAdmin()
    {
        // 已提权则无需重启
        if (IsCurrentProcessElevated) return true;

        if (Application.Current is not App app)
        {
            return false;
        }

        try
        {
            app.RestartAsAdmin();
            return true;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223 /* ERROR_CANCELLED */)
        {
            // 用户在 UAC 提示中拒绝了提权
            return false;
        }
    }

    private static bool DetectElevation()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            // 任何异常（如无法获取当前身份）都保守地视为未提权
            return false;
        }
    }
}