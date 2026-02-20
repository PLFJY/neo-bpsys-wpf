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
/// 窗口捕获服务。
/// 该服务统一负责窗口枚举、WGC 捕获生命周期管理、最新帧缓存以及预览窗口展示。
/// </summary>
public partial class WindowCaptureService(ILogger<WindowCaptureService> logger, INavigationService navigationService) : IWindowCaptureService
{
    private const int WgcMinimumBuild = 17134; // Windows 10 1803
    private const int WgcHwndInteropMinimumBuild = 18362; // Windows 10 1903

    private readonly ILogger<WindowCaptureService> _logger = logger;
    private readonly INavigationService _navigationService = navigationService;

    // 捕获线程会写入 _currentFrame，UI 线程会读取 _currentFrame。
    // 使用 Lock 保证读写一致性，避免帧对象在替换瞬间出现竞争。
    private readonly Lock _frameLock = new();

    // WinRT 设备：WGC 的 FramePool/CreateCaptureSession 依赖它。
    private IDirect3DDevice? _winrtDevice;

    // SharpDX 设备：负责 CopyResource + MapSubresource，将 GPU 纹理读回 CPU。
    private Device? _d3dDevice;

    // 帧池：持续产出捕获帧。
    private Direct3D11CaptureFramePool? _framePool;

    // 捕获会话：启动/停止的核心对象。
    private GraphicsCaptureSession? _captureSession;

    // 当前捕获目标（窗口/显示器）对应的 GraphicsCaptureItem。
    private GraphicsCaptureItem? _captureItem;

    // 用于检测尺寸变化，尺寸变化后要重建 FramePool。
    private SizeInt32 _currentCaptureSize;

    // 缓存“最近一帧”；外部通过 GetCurrentFrame() 读取。
    private BitmapSource? _currentFrame;

    // 预览窗口相关状态。
    private Window? _previewWindow;
    private Image? _previewImage;
    private DispatcherTimer? _previewTimer;

    // BitBlt 捕获相关状态。
    // _bitbltTimer: 周期性拉取窗口图像的驱动器（和 WGC 的事件回调作用类似）。
    private DispatcherTimer? _bitbltTimer;

    // _bitbltTargetHwnd: 当前 BitBlt 模式下的目标窗口句柄。
    private HWND _bitbltTargetHwnd = HWND.Zero;

    // _captureTargetHwnd: 当前捕获目标窗口句柄（用于在 WGC 帧上裁掉标题栏/边框）。
    private HWND _captureTargetHwnd = HWND.Zero;

    /// <summary>
    /// 当前是否正在捕获。
    /// </summary>
    public bool IsCapturing { get; private set; }

    /// <summary>
    /// 列出当前可用于捕获的窗口。
    /// </summary>
    /// <returns>窗口信息列表；若没有可捕获窗口则返回空列表。</returns>
    public List<WindowInfo> ListActiveWindows()
    {
        var candidates = new List<WindowCandidate>();

        // 通过 EnumWindows 枚举系统顶层窗口。
        // 只要回调返回 true，枚举就会继续。
        _ = Win32.EnumWindows((HWND hwnd, LPARAM _) =>
        {
            // 统一走同一套“可捕获窗口”规则，避免 UI 展示列表和真正捕获标准不一致。
            if (!WindowEnumerationHelper.IsWindowValidForCapture(hwnd))
            {
                return true;
            }

            unsafe
            {
                uint pid = 0;
                // 取 PID 的目的：
                // 1) 展示进程名给用户看
                // 2) 支持将特定进程（dwrg）置顶排序
                Win32.GetWindowThreadProcessId(hwnd, &pid);

                string processName;
                try
                {
                    // 进程名比 PID 更可读，便于用户快速识别目标窗口。
                    using var process = System.Diagnostics.Process.GetProcessById((int)pid);
                    processName = process.ProcessName;
                }
                catch
                {
                    // 枚举到一半进程可能退出；这里兜底为 Unknown，不让列表生成失败。
                    processName = "Unknown";
                }

                // 标题读取统一走 helper，避免“长度判断 + 空白判断”分散在多处。
                var title = TryGetWindowTitle(hwnd);
                candidates.Add(new WindowCandidate(hwnd, title, processName, pid));
            }

            return true;
        }, default);

        if (candidates.Count == 0)
        {
            return [];
        }

        // Airplayer 白名单：
        // 1) 进程名匹配 Airplayer
        // 2) 任意窗口标题为 Airplayer
        // 满足条件的 PID，其全部窗口都保留（不会被标题规则剔除）。
        var airplayerWhitelistedPids = candidates
            .Where(x => IsAirplayerProcess(x.ProcessName) || IsAirplayerTitle(x.Title))
            .Select(x => x.Pid)
            .ToHashSet();

        // 非白名单进程仍要求标题非空白，以降低噪音窗口数量。
        var windows = candidates
            .Where(x =>
                (airplayerWhitelistedPids.Contains(x.Pid) || !string.IsNullOrWhiteSpace(x.Title))
                && Win32.IsWindowVisible(x.Hwnd))
            .Select(x => new WindowInfo(x.Hwnd, x.Title, x.ProcessName, x.Pid))
            .ToList();

        if (windows.Count == 0)
        {
            return windows;
        }

        // dwrg 相关窗口放在列表最上方。
        // 这里采用“稳定重排”：只把匹配项前置，不打乱各分组内部顺序，降低用户认知负担。
        var prioritized = windows
            .Where(window => IsDwrgProcess(window.ProcessName))
            .Concat(windows.Where(window => !IsDwrgProcess(window.ProcessName)))
            .ToList();

        return prioritized;
    }

    /// <summary>
    /// 使用指定方式启动窗口捕获。
    /// </summary>
    /// <param name="window">目标窗口。</param>
    /// <param name="captureMethod">捕获方式。</param>
    /// <returns>启动成功返回 <see langword="true"/>；失败返回 <see langword="false"/>。</returns>
    public bool StartCapture(WindowInfo? window, CaptureMethod captureMethod)
    {
        // 没有选中窗口时直接失败，比后续空引用更可控、更易理解。
        if (window is null)
        {
            _ = MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString("WindowCapturePleaseSelectWindowFirst"));
            return false;
        }

        switch (captureMethod)
        {
            case CaptureMethod.Bitblt:
                return StartBitbltCaptureFromHwnd(window.Hwnd);
            case CaptureMethod.WGC:
                return StartWgcCaptureFromHwnd(window.Hwnd);
            default:
                _logger.LogWarning("Unsupported capture method: {CaptureMethod}", captureMethod);
                return false;
        }
    }

    /// <summary>
    /// 打开系统窗口选择器并启动捕获。
    /// </summary>
    /// <returns>启动成功返回 <see langword="true"/>；用户取消或失败返回 <see langword="false"/>。</returns>
    public async Task<bool> StartCaptureWithPickerAsync()
    {
        // WGC 是系统能力，先判断支持性，避免后续调用抛平台异常。
        if (!IsWgcApiAvailable())
        {
            _ = MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString("WindowCaptureWgcRequires1803OrLater"));
            return false;
        }

        if (!IsWgcSupported())
        {
            _ = MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString("WindowCaptureWgcNotSupportedOnCurrentSystem"));
            return false;
        }

        try
        {
            var picker = new GraphicsCapturePicker();
            var ownerWindow = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                              ?? Application.Current?.MainWindow;

            // 给 Picker 绑定宿主窗口可以保证焦点与 Z 序正确（不容易被主窗口遮挡）。
            if (ownerWindow is not null)
                picker.SetWindow(new WindowInteropHelper(ownerWindow).Handle);

            // 系统 UI：等待用户选择目标窗口。
            var item = await picker.PickSingleItemAsync();

            // 用户点击取消属于正常流程，不作为异常处理。
            if (item == null)
                return false;

            // Picker 路径无法稳定映射回 HWND，这里不做标题栏裁剪。
            StopCapture();
            _captureTargetHwnd = HWND.Zero;
            return StartWgcCapture(item);
        }
        catch (Exception ex)
        {
            // 这里捕获异常是为了保护 UI 主流程，避免未处理异常中断应用。
            _logger.LogError(ex, "Failed to start capture from picker.");
            _ = MessageBoxHelper.ShowErrorAsync(
                string.Format(
                    I18nHelper.GetLocalizedString("WindowCaptureFailedToStartPickerCaptureFormat"),
                    ex.Message));
            return false;
        }
    }

    /// <summary>
    /// 获取当前缓存的最新一帧。
    /// </summary>
    /// <returns>最新帧；若尚未产生有效帧则返回 <see langword="null"/>。</returns>
    public BitmapSource? GetCurrentFrame()
    {
        if (!IsCapturing)
        {
            _ = MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString("PleaseStartWindowCaptureFirst"),
                I18nHelper.GetLocalizedString("Error"), I18nHelper.GetLocalizedString("Close"));
            _navigationService.Navigate(typeof(SmartBpPage));
            return null;
        }
        // 与 OnFrameArrived 中的写入共用同一把锁，确保读取到一致快照。
        lock (_frameLock)
        {
            return _currentFrame;
        }
    }

    /// <summary>
    /// 打开捕获预览窗口。
    /// </summary>
    public void OpenPreviewWindow()
    {
        // 预览依赖捕获源；未开始捕获时直接提示用户。
        if (!IsCapturing)
        {
            _ = MessageBoxHelper.ShowInfoAsync(I18nHelper.GetLocalizedString("WindowCaptureCaptureNotStarted"));
            return;
        }

        // 保持单实例预览窗口：避免重复创建多个窗口导致资源浪费。
        if (_previewWindow != null)
        {
            _previewWindow.Activate();
            return;
        }

        _previewImage = new Image
        {
            Stretch = Stretch.Uniform
        };

        _previewWindow = new Window
        {
            Title = I18nHelper.GetLocalizedString("WindowCapturePreviewWindowTitle"),
            Width = 960,
            Height = 540,
            Content = _previewImage
        };

        // UI 渲染优先级的定时器，更新预览图像时与界面绘制节奏更一致。
        _previewTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            // 约 15 FPS：足够观察效果，同时降低 UI 线程负担。
            Interval = TimeSpan.FromMilliseconds(66)
        };
        _previewTimer.Tick += (_, _) =>
        {
            var frame = GetCurrentFrame();
            if (frame != null && _previewImage != null)
            {
                // 每次 Tick 都拿最新缓存帧覆盖展示。
                _previewImage.Source = frame;
            }
        };
        _previewTimer.Start();

        _previewWindow.Closed += (_, _) =>
        {
            // 关闭窗口时及时解绑/回收，避免定时器继续引用 UI 对象造成泄漏。
            if (_previewTimer is not null)
            {
                _previewTimer.Stop();
                _previewTimer = null;
            }

            _previewImage = null;
            _previewWindow = null;
        };

        _previewWindow.Show();
    }

    /// <summary>
    /// 停止捕获并释放会话资源。
    /// </summary>
    public void StopCapture()
    {
        if (_bitbltTimer != null)
        {
            // 停止并解绑 BitBlt 定时器，防止停止后仍持续抓帧。
            _bitbltTimer.Stop();
            _bitbltTimer.Tick -= OnBitbltTimerTick;
            _bitbltTimer = null;
        }

        _bitbltTargetHwnd = HWND.Zero;
        _captureTargetHwnd = HWND.Zero;

        if (_framePool != null)
        {
            // 先解订阅事件，避免对象释放后仍触发回调。
            _framePool.FrameArrived -= OnFrameArrived;
        }

        // 捕获相关对象内部持有原生资源，显式释放可更快归还系统资源。
        _captureSession?.Dispose();
        _captureSession = null;

        _framePool?.Dispose();
        _framePool = null;

        _captureItem = null;

        IsCapturing = false;
    }

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
            _ = MessageBoxHelper.ShowInfoAsync(I18nHelper.GetLocalizedString("WindowCaptureSelectedWindowInvalidForCapture"));
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
                _ = MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString("WindowCaptureFailedToCaptureFirstFrameWithBitblt"));
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
                    I18nHelper.GetLocalizedString("WindowCaptureFailedToStartBitbltCaptureFormat"),
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

    /// <summary>
    /// 通过窗口句柄创建捕获项并启动 WGC。
    /// </summary>
    /// <param name="hwnd">目标窗口句柄。</param>
    /// <returns>启动成功返回 <see langword="true"/>；失败返回 <see langword="false"/>。</returns>
    private bool StartWgcCaptureFromHwnd(HWND hwnd)
    {
        if (!IsWgcHwndInteropAvailable())
        {
            _ = MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString("WindowCaptureWgcWindowCaptureRequires1903OrLaterUsePickerOn1803Or1809"));
            return false;
        }

        if (!IsWgcSupported())
        {
            _ = MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString("WindowCaptureWgcNotSupportedOnCurrentSystem"));
            return false;
        }

        // 再次校验句柄有效性，防止 UI 侧缓存了已失效窗口句柄。
        if (!WindowEnumerationHelper.IsWindowValidForCapture(hwnd))
        {
            _ = MessageBoxHelper.ShowInfoAsync(I18nHelper.GetLocalizedString("WindowCaptureSelectedWindowInvalidForCapture"));
            return false;
        }

        // 句柄 -> GraphicsCaptureItem 是进入 WGC 捕获流程的关键转换。
        GraphicsCaptureItem? item;
        try
        {
            item = CaptureHelper.CreateItemForWindow(hwnd);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create GraphicsCaptureItem from HWND.");
            _ = MessageBoxHelper.ShowErrorAsync(
                string.Format(
                    I18nHelper.GetLocalizedString("WindowCaptureFailedToCaptureSelectedWindowFormat"),
                    ex.Message));
            return false;
        }

        if (item is null)
        {
            _ = MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString("WindowCaptureGraphicsCaptureItemUnavailable"));
            return false;
        }

        StopCapture();
        _captureTargetHwnd = hwnd;
        return StartWgcCapture(item);
    }

    /// <summary>
    /// 使用指定捕获项启动 WGC 会话。
    /// </summary>
    /// <param name="item">捕获项。</param>
    /// <returns>启动成功返回 <see langword="true"/>；失败返回 <see langword="false"/>。</returns>
    private bool StartWgcCapture(GraphicsCaptureItem item)
    {
        try
        {
            EnsureCaptureDevice();

            _captureItem = item;
            _currentCaptureSize = item.Size;

            // 使用 BGRA8 便于后续直接转换为 WPF Bgra32。
            _framePool = Direct3D11CaptureFramePool.Create(
                _winrtDevice!,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                2,
                item.Size);

            _captureSession = _framePool.CreateCaptureSession(item);

            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
            {
                //隐藏鼠标指针
                _captureSession.IsCursorCaptureEnabled = false;
            }
            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 20348))
            {
                //隐藏边框
                _captureSession.IsBorderRequired = false;
            }
            // 通过 FrameArrived 事件拉取每一帧。
            _framePool.FrameArrived += OnFrameArrived;
            _captureSession.StartCapture();
            IsCapturing = true;

            return true;
        }
        catch (Exception ex)
        {
            // 启动失败时回滚状态，保证服务回到可再次启动的干净状态。
            _logger.LogError(ex, "Failed to start WGC capture.");
            _ = MessageBoxHelper.ShowErrorAsync(
                string.Format(
                    I18nHelper.GetLocalizedString("WindowCaptureFailedToStartCaptureFormat"),
                    ex.Message));
            StopCapture();
            return false;
        }
    }

    /// <summary>
    /// 确保捕获设备已创建。
    /// </summary>
    private void EnsureCaptureDevice()
    {
        // 设备是重资源对象，可跨会话复用；避免重复创建带来的性能和资源压力。
        if (_winrtDevice is not null && _d3dDevice is not null)
        {
            return;
        }

        // WinRT 设备供 WGC 使用，SharpDX 设备供图像读回处理使用。
        _winrtDevice = Direct3D11Helper.CreateDevice();
        _d3dDevice = Direct3D11Helper.CreateSharpDXDevice(_winrtDevice);
    }

    /// <summary>
    /// 处理捕获帧到达事件：读取帧、转换位图、更新缓存。
    /// </summary>
    /// <param name="sender">帧池对象。</param>
    /// <param name="args">事件参数（当前未使用）。</param>
    private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        // 防御性检查：设备异常时本帧直接跳过，避免连锁异常。
        if (_d3dDevice is null || _winrtDevice is null)
        {
            return;
        }

        // 尺寸变化时需要重建 FramePool；先记录，再在 finally 里统一处理。
        var shouldRecreate = false;

        try
        {
            // 读取当前帧。using 确保帧对象及时释放。
            using var frame = sender.TryGetNextFrame();
            var contentSize = frame.ContentSize;
            if (contentSize.Width <= 0 || contentSize.Height <= 0)
            {
                return;
            }

            // 目标窗口大小变化时，原 FramePool 尺寸不再匹配，需要重建。
            if (contentSize.Width != _currentCaptureSize.Width || contentSize.Height != _currentCaptureSize.Height)
            {
                _currentCaptureSize = contentSize;
                shouldRecreate = true;
            }

            // 将 WinRT surface 包装为 SharpDX texture，便于执行 Direct3D 复制。
            using var sourceTexture = Direct3D11Helper.CreateSharpDXTexture2D(frame.Surface);
            // 创建 CPU 可读 staging 纹理，后续通过 MapSubresource 读取像素。
            using var stagingTexture =
                CreateCpuReadableTexture(sourceTexture.Description, contentSize.Width, contentSize.Height);

            // 先把帧数据复制到 staging 资源。
            _d3dDevice.ImmediateContext.CopyResource(sourceTexture, stagingTexture);

            // 再将纹理转换为 WPF 可直接显示的 BitmapSource。
            var frameBitmap = CreateBitmapSourceFromTexture(stagingTexture, contentSize.Width, contentSize.Height);
            // WGC 采集窗口时可能包含非客户区；若有 HWND 上下文则裁到客户区。
            frameBitmap = CropToClientAreaIfNeeded(frameBitmap);
            if (frameBitmap is not null)
            {
                // 写缓存和读取缓存共用锁，保证并发安全。
                lock (_frameLock)
                {
                    _currentFrame = frameBitmap;
                }
            }
        }
        catch (Exception ex)
        {
            // 单帧异常不终止整个捕获会话，只记录调试日志继续下一帧。
            _logger.LogDebug(ex, "Failed to process a captured frame.");
        }
        finally
        {
            if (shouldRecreate && _framePool is not null)
            {
                try
                {
                    // 尺寸变化后重建帧池，确保后续帧尺寸与资源匹配。
                    _framePool.Recreate(
                        _winrtDevice,
                        DirectXPixelFormat.B8G8R8A8UIntNormalized,
                        2,
                        _currentCaptureSize);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to recreate frame pool on size change.");
                }
            }
        }
    }

    /// <summary>
    /// 创建 CPU 可读的 staging 纹理。
    /// </summary>
    /// <param name="sourceDescription">源纹理描述（主要复用格式信息）。</param>
    /// <param name="width">目标宽度。</param>
    /// <param name="height">目标高度。</param>
    /// <returns>可供 CPU 读取的纹理。</returns>
    private Texture2D CreateCpuReadableTexture(Texture2DDescription sourceDescription, int width, int height)
    {
        // 必须是 Staging + CpuAccessFlags.Read，MapSubresource 才能读取。
        var description = new Texture2DDescription
        {
            Width = width,
            Height = height,
            MipLevels = 1,
            ArraySize = 1,
            Format = sourceDescription.Format,
            SampleDescription = new SharpDX.DXGI.SampleDescription(1, 0),
            Usage = ResourceUsage.Staging,
            BindFlags = BindFlags.None,
            CpuAccessFlags = CpuAccessFlags.Read,
            OptionFlags = ResourceOptionFlags.None
        };

        return new Texture2D(_d3dDevice!, description);
    }

    /// <summary>
    /// 将 D3D 纹理转换为 WPF 位图。
    /// </summary>
    /// <param name="texture">源纹理。</param>
    /// <param name="width">宽度。</param>
    /// <param name="height">高度。</param>
    /// <returns>转换得到的位图，失败时返回 <see langword="null"/>。</returns>
    private BitmapSource? CreateBitmapSourceFromTexture(Texture2D texture, int width, int height)
    {
        if (_d3dDevice is null)
        {
            return null;
        }

        var context = _d3dDevice.ImmediateContext;
        // 获取 CPU 可读指针。注意一定要和 Unmap 成对调用。
        var dataBox = context.MapSubresource(texture, 0, MapMode.Read, MapFlags.None);

        try
        {
            // BGRA8 每像素 4 字节。
            var stride = width * 4;
            var pixels = new byte[stride * height];

            for (var y = 0; y < height; y++)
            {
                // RowPitch 可能大于 stride（行对齐），逐行拷贝可避免错位。
                Marshal.Copy(nint.Add(dataBox.DataPointer, y * dataBox.RowPitch), pixels, y * stride, stride);
            }

            var bitmap = BitmapSource.Create(
                width,
                height,
                96,
                96,
                PixelFormats.Bgra32,
                null,
                pixels,
                stride);
            // Freeze 后可跨线程安全访问，适合缓存给 UI 层读取。
            bitmap.Freeze();

            return bitmap;
        }
        finally
        {
            // 即使发生异常也必须 Unmap，避免 D3D 资源处于不一致状态。
            context.UnmapSubresource(texture, 0);
        }
    }

    /// <summary>
    /// 根据当前捕获窗口的客户区信息，对帧进行裁剪（移除标题栏/边框）。
    /// </summary>
    /// <param name="frame">原始帧。</param>
    /// <returns>裁剪后的帧；若无法裁剪则返回原始帧。</returns>
    private BitmapSource? CropToClientAreaIfNeeded(BitmapSource? frame)
    {
        if (frame is null)
        {
            return null;
        }

        if (_captureTargetHwnd == 0)
        {
            return frame;
        }

        if (!TryGetClientCaptureRegion(_captureTargetHwnd, out var region))
        {
            return frame;
        }

        // 防御性裁剪：限制区域在当前帧范围内，避免因尺寸瞬时变化导致越界。
        var x = Math.Clamp(region.X, 0, frame.PixelWidth - 1);
        var y = Math.Clamp(region.Y, 0, frame.PixelHeight - 1);
        var width = Math.Min(region.Width, frame.PixelWidth - x);
        var height = Math.Min(region.Height, frame.PixelHeight - y);
        if (width <= 0 || height <= 0)
        {
            return frame;
        }

        // 已经是完整客户区时无需再生成新对象。
        if (x == 0 && y == 0 && width == frame.PixelWidth && height == frame.PixelHeight)
        {
            return frame;
        }

        var cropped = new CroppedBitmap(frame, new Int32Rect(x, y, width, height));
        cropped.Freeze();
        return cropped;
    }

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
