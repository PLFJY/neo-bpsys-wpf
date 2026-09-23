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

    // 复用的 staging 纹理和像素缓冲区：尺寸不变时跨帧复用，避免每帧分配约 8 MiB（1080p BGRA）。
    // 仅在 OnFrameArrived（捕获线程）中访问，无需跨线程同步。
    private Texture2D? _stagingTexture;
    private byte[]? _stagingBuffer;
    private int _stagingWidth;
    private int _stagingHeight;

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
            _ = MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "WindowCapturePleaseSelectWindowFirst"));
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
            _ = MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "WindowCaptureWgcRequires1803OrLater"));
            return false;
        }

        if (!IsWgcSupported())
        {
            _ = MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "WindowCaptureWgcNotSupportedOnCurrentSystem"));
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

            StopCapture();
            // Picker 返回的 GraphicsCaptureItem 不直接暴露 HWND；
            // 通过显示名和尺寸反查目标窗口，用于裁掉标题栏/边框。
            _captureTargetHwnd = TryFindHwndForCaptureItem(item);
            return StartWgcCapture(item);
        }
        catch (Exception ex)
        {
            // 这里捕获异常是为了保护 UI 主流程，避免未处理异常中断应用。
            _logger.LogError(ex, "Failed to start capture from picker.");
            _ = MessageBoxHelper.ShowErrorAsync(
                string.Format(
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "WindowCaptureFailedToStartPickerCaptureFormat"),
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
            // GetCurrentFrame 可能在后台线程被调用，所有 UI 访问必须切回 Dispatcher。
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                _logger.LogWarning("PleaseStartWindowCaptureFirst: Application dispatcher is null.");
                return null;
            }

            _ = dispatcher.BeginInvoke(() =>
            {
                _ = MessageBoxHelper.ShowErrorAsync(
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "PleaseStartWindowCaptureFirst"),
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Error"),
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Close"));
                _navigationService.Navigate(typeof(SmartBpPage));
            });
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
            _ = MessageBoxHelper.ShowInfoAsync(I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "WindowCaptureCaptureNotStarted"));
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
            Title = I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "WindowCapturePreviewWindowTitle"),
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
            if (!IsCapturing) _previewWindow.Close();
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
        _previewWindow?.Close();
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

        // 释放复用的 staging 资源，避免停止捕获后仍持有 D3D 纹理和大缓冲区。
        _stagingTexture?.Dispose();
        _stagingTexture = null;
        _stagingBuffer = null;
        _stagingWidth = 0;
        _stagingHeight = 0;

        IsCapturing = false;
    }
}
