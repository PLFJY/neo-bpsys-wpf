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
/// 窗口捕获服务的BitBlt 捕获逻辑。
/// </summary>
public partial class WindowCaptureService
{
    /// <summary>
    /// 通过窗口句柄启动 BitBlt 捕获。
    /// </summary>
    /// <param name="hwnd">目标窗口句柄。</param>
    /// <returns>启动成功返回 <see langword="true"/>；失败返回 <see langword="false"/>。</returns>
    private bool StartBitbltCaptureFromHwnd(HWND hwnd)
    {
        // 验证窗口句柄有效性。
        if (!WindowEnumerationHelper.IsWindowValidForCapture(hwnd))
        {
            _ = MessageBoxHelper.ShowInfoAsync(I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "WindowCaptureSelectedWindowInvalidForCapture"));
            return false;
        }

        try
        {
            // 保持单会话模型，启动新捕获前清理旧状态。
            StopCapture();
            _bitbltTargetHwnd = hwnd;
            _captureTargetHwnd = hwnd;

            // 先抓第一帧，确保目标窗口可被 BitBlt 实际采集到。
            if (!TryCaptureBitbltFrame(hwnd))
            {
                _ = MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "WindowCaptureFailedToCaptureFirstFrameWithBitblt"));
                StopCapture();
                return false;
            }

            _bitbltTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                // 与预览节奏一致，约 15 FPS。
                Interval = TimeSpan.FromMilliseconds(66)
            };
            // 使用定时拉帧而不是阻塞循环，避免占用 UI 线程导致界面卡顿。
            _bitbltTimer.Tick += OnBitbltTimerTick;
            _bitbltTimer.Start();

            IsCapturing = true;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Bitblt capture.");
            _ = MessageBoxHelper.ShowErrorAsync(
                string.Format(
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "WindowCaptureFailedToStartBitbltCaptureFormat"),
                    ex.Message));
            StopCapture();
            return false;
        }
    }

    /// <summary>
    /// BitBlt 定时抓帧回调。
    /// </summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="args">事件参数。</param>
    private void OnBitbltTimerTick(object? sender, EventArgs args)
    {
        // 句柄被重置（例如 StopCapture 后）时直接退出当前 tick。
        if (_bitbltTargetHwnd == HWND.Zero)
        {
            return;
        }

        // 窗口无效时直接结束捕获，避免持续无效调用。
        if (!WindowEnumerationHelper.IsWindowValidForCapture((nint)_bitbltTargetHwnd))
        {
            StopCapture();
            return;
        }

        // 单次抓帧失败不立即打断会话，交由下一次 tick 继续尝试。
        _ = TryCaptureBitbltFrame(_bitbltTargetHwnd);
    }

    /// <summary>
    /// 使用 BitBlt/PrintWindow 抓取当前窗口画面并更新最新帧缓存。
    /// </summary>
    /// <param name="hwnd">目标窗口句柄。</param>
    /// <returns>抓帧成功返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    private bool TryCaptureBitbltFrame(HWND hwnd)
    {
        if (hwnd == 0)
        {
            return false;
        }

        // 优先只抓客户区，避免把 DWM 非客户区边框（移动/缩放时更明显）带入结果。
        var captureClientOnly = false;
        var width = 0;
        var height = 0;
        if (Win32.GetClientRect(hwnd, out var clientRect))
        {
            width = clientRect.right - clientRect.left;
            height = clientRect.bottom - clientRect.top;
            captureClientOnly = width > 0 && height > 0;
        }

        // 客户区不可用时回退整窗捕获，保证功能可用性。
        if (!captureClientOnly)
        {
            if (!Win32.GetWindowRect(hwnd, out var windowRect))
            {
                return false;
            }

            width = windowRect.right - windowRect.left;
            height = windowRect.bottom - windowRect.top;
            if (width <= 0 || height <= 0)
            {
                return false;
            }
        }

        HDC windowDc = HDC.Zero;
        HDC memoryDc = HDC.Zero;
        HBITMAP bitmap = HBITMAP.Zero;
        HGDIOBJ oldObject = HGDIOBJ.Zero;

        try
        {
            // 获取目标窗口 DC（设备上下文）。后续复制像素需要以它为源。
            windowDc = captureClientOnly ? Win32.GetDC(hwnd) : Win32.GetWindowDC(hwnd);
            if (windowDc == HDC.Zero)
            {
                return false;
            }

            // 创建兼容内存 DC，作为离屏缓冲的承载对象。
            memoryDc = Win32.CreateCompatibleDC(windowDc);
            if (memoryDc == HDC.Zero)
            {
                return false;
            }

            // 根据目标大小创建兼容位图，作为帧像素容器。
            bitmap = Win32.CreateCompatibleBitmap(windowDc, width, height);
            if (bitmap == HBITMAP.Zero)
            {
                return false;
            }

            // 把位图选入内存 DC，后续绘制/复制结果都会落到 bitmap。
            oldObject = Win32.SelectObject(memoryDc, bitmap);
            if (oldObject == HGDIOBJ.Zero || oldObject == HGDI_ERROR)
            {
                return false;
            }

            bool copied;
            if (captureClientOnly)
            {
                // 客户区路径固定使用 BitBlt，减少 PrintWindow/BitBlt 交替导致的视觉抖动。
                copied = Win32.BitBlt(
                    memoryDc,
                    0,
                    0,
                    width,
                    height,
                    windowDc,
                    0,
                    0,
                    ROP_CODE.SRCCOPY);
            }
            else
            {
                // 整窗路径优先 PrintWindow；失败时再回退 BitBlt 提高兼容性。
                copied = Win32.PrintWindow(hwnd, memoryDc, 0);
                if (!copied)
                {
                    copied = Win32.BitBlt(
                        memoryDc,
                        0,
                        0,
                        width,
                        height,
                        windowDc,
                        0,
                        0,
                        ROP_CODE.SRCCOPY | ROP_CODE.CAPTUREBLT);
                }
            }

            if (!copied)
            {
                return false;
            }

            var frame = Imaging.CreateBitmapSourceFromHBitmap(
                bitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            // 设为 Freeze，便于跨线程安全读取并减少后续 WPF 开销。
            frame.Freeze();

            lock (_frameLock)
            {
                _currentFrame = frame;
            }

            return true;
        }
        finally
        {
            // GDI 资源释放顺序很关键：先把旧对象选回去，再销毁位图/DC。
            if (oldObject != 0 && oldObject != HGDI_ERROR && memoryDc != 0)
            {
                _ = Win32.SelectObject(memoryDc, oldObject);
            }

            if (bitmap != 0)
            {
                _ = Win32.DeleteObject(bitmap);
            }

            if (memoryDc != 0)
            {
                _ = Win32.DeleteDC(memoryDc);
            }

            if (windowDc != 0)
            {
                _ = Win32.ReleaseDC(hwnd, windowDc);
            }
        }
    }
}
