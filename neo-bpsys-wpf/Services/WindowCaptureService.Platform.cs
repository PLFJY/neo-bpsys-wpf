using Composition.WindowsRuntimeHelpers;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Views.Pages;
using SharpDX.Direct3D11;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Wpf.Ui;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 窗口捕获服务的平台检测与窗口辅助逻辑。
/// </summary>
public partial class WindowCaptureService
{
    /// <summary>
    /// 获取目标窗口客户区在“整窗坐标系”中的采集区域。
    /// </summary>
    /// <param name="hwnd">目标窗口句柄。</param>
    /// <param name="region">输出采集区域。</param>
    /// <returns>成功返回 <see langword="true"/>，失败返回 <see langword="false"/>。</returns>
    private static bool IsWgcApiAvailable() => OperatingSystem.IsWindowsVersionAtLeast(10, 0, WgcMinimumBuild);

    private static bool IsWgcHwndInteropAvailable() => OperatingSystem.IsWindowsVersionAtLeast(10, 0, WgcHwndInteropMinimumBuild);

    private static bool IsWgcSupported()
    {
        if (!IsWgcApiAvailable())
        {
            return false;
        }

        return GraphicsCaptureSession.IsSupported();
    }

    private static bool TryGetClientCaptureRegion(HWND hwnd, out System.Drawing.Rectangle region)
    {
        region = default;

        if (!Win32.GetWindowRect(hwnd, out var windowRect))
        {
            return false;
        }

        if (!Win32.GetClientRect(hwnd, out var clientRect))
        {
            return false;
        }

        var width = clientRect.right - clientRect.left;
        var height = clientRect.bottom - clientRect.top;
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        // 客户区左上角是相对客户区(0,0)，通过 ClientToScreen 转到屏幕坐标，
        // 再减去 windowRect 左上角得到相对整窗的偏移。
        var clientTopLeft = new System.Drawing.Point { X = 0, Y = 0 };
        if (!Win32.ClientToScreen(hwnd, ref clientTopLeft))
        {
            return false;
        }

        var x = clientTopLeft.X - windowRect.left;
        var y = clientTopLeft.Y - windowRect.top;

        region = new System.Drawing.Rectangle(x, y, width, height);
        return true;
    }

    /// <summary>
    /// 判断是否为需要置顶显示的 dwrg 进程。
    /// </summary>
    /// <param name="processName">进程名。</param>
    /// <returns>是 dwrg 或 dwrg.exe 返回 <see langword="true"/>。</returns>
    private static bool IsDwrgProcess(string? processName)
    {
        return string.Equals(processName, "dwrg", StringComparison.OrdinalIgnoreCase)
               || string.Equals(processName, "dwrg.exe", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 判断是否为 Airplayer 白名单进程。
    /// </summary>
    /// <param name="processName">进程名。</param>
    /// <returns>匹配 Airplayer 返回 <see langword="true"/>。</returns>
    private static bool IsAirplayerProcess(string? processName)
    {
        return string.Equals(processName, "Airplayer", StringComparison.OrdinalIgnoreCase)
               || string.Equals(processName, "Airplayer.exe", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 判断窗口标题是否命中 Airplayer 白名单标识。
    /// </summary>
    /// <param name="title">窗口标题。</param>
    /// <returns>标题为 Airplayer 返回 <see langword="true"/>。</returns>
    private static bool IsAirplayerTitle(string? title)
    {
        return string.Equals(title?.Trim(), "Airplayer", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 读取窗口标题。
    /// </summary>
    /// <param name="hwnd">窗口句柄。</param>
    /// <returns>窗口标题；无标题时返回空字符串。</returns>
    private static string TryGetWindowTitle(HWND hwnd)
    {
        // 长度为 0 直接返回，避免不必要的缓冲区分配。
        var titleLength = Win32.GetWindowTextLength(hwnd);
        if (titleLength <= 0)
        {
            return string.Empty;
        }

        unsafe
        {
            fixed (char* pBuffer = new char[titleLength + 1])
            {
                _ = Win32.GetWindowText(hwnd, pBuffer, titleLength + 1);
                return new string(pBuffer);
            }
        }
    }

    // SelectObject 失败时的返回值（GDI 约定）。
    private static readonly nint HGDI_ERROR = -1;

    private readonly record struct WindowCandidate(nint Hwnd, string Title, string ProcessName, uint Pid);
}
