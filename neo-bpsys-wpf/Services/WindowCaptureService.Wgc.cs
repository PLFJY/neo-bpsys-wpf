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
/// 窗口捕获服务的Windows Graphics Capture 逻辑。
/// </summary>
public partial class WindowCaptureService
{
    /// <summary>
    /// 通过窗口句柄创建捕获项并启动 WGC。
    /// </summary>
    /// <param name="hwnd">目标窗口句柄。</param>
    /// <returns>启动成功返回 <see langword="true"/>；失败返回 <see langword="false"/>。</returns>
    private bool StartWgcCaptureFromHwnd(HWND hwnd)
    {
        if (!IsWgcHwndInteropAvailable())
        {
            _ = MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "WindowCaptureWgcWindowCaptureRequires1903OrLaterUsePickerOn1803Or1809"));
            return false;
        }

        if (!IsWgcSupported())
        {
            _ = MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "WindowCaptureWgcNotSupportedOnCurrentSystem"));
            return false;
        }

        // 再次校验句柄有效性，防止 UI 侧缓存了已失效窗口句柄。
        if (!WindowEnumerationHelper.IsWindowValidForCapture(hwnd))
        {
            _ = MessageBoxHelper.ShowInfoAsync(I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "WindowCaptureSelectedWindowInvalidForCapture"));
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
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "WindowCaptureFailedToCaptureSelectedWindowFormat"),
                    ex.Message));
            return false;
        }

        if (item is null)
        {
            _ = MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "WindowCaptureGraphicsCaptureItemUnavailable"));
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
                    I18nHelper.GetLocalizedString(AppI18nDictionaries.Settings, "WindowCaptureFailedToStartCaptureFormat"),
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
            // 复用 staging 纹理：尺寸不变时跨帧复用，避免每帧分配新的 D3D staging 资源。
            // 尺寸变化时释放旧纹理并重建。
            if (_stagingTexture is null
                || _stagingWidth != contentSize.Width
                || _stagingHeight != contentSize.Height)
            {
                _stagingTexture?.Dispose();
                _stagingTexture = CreateCpuReadableTexture(sourceTexture.Description, contentSize.Width, contentSize.Height);
                _stagingWidth = contentSize.Width;
                _stagingHeight = contentSize.Height;
            }

            var stagingTexture = _stagingTexture;

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
            var requiredSize = stride * height;
            // 复用像素缓冲区：尺寸不变时跨帧复用，避免每帧在 LOH 分配约 8 MiB（1080p BGRA）。
            // BitmapSource.Create 会复制传入的像素数据，复用 buffer 是安全的。
            if (_stagingBuffer is null || _stagingBuffer.Length < requiredSize)
            {
                _stagingBuffer = new byte[requiredSize];
            }
            var pixels = _stagingBuffer;

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
    /// 在窗口选择器返回后，尝试根据捕获项尺寸反查目标窗口句柄。
    /// </summary>
    /// <remarks>
    /// <see cref="GraphicsCapturePicker"/> 返回的 <see cref="GraphicsCaptureItem"/> 不公开 HWND，
    /// 此处优先以 <see cref="GraphicsCaptureItem.DisplayName"/> 匹配窗口标题，再使用整窗或客户区尺寸消除同名歧义。
    /// 仅在存在唯一匹配时返回该窗口句柄；无匹配或存在歧义时返回 <see cref="HWND.Zero"/>，
    /// 此时标题栏裁剪会被跳过（回退为不裁剪的原有行为）。
    /// </remarks>
    /// <param name="item">窗口选择器返回的捕获项。</param>
    /// <returns>唯一匹配的窗口句柄；无匹配或存在歧义时返回 <see cref="HWND.Zero"/>。</returns>
    private HWND TryFindHwndForCaptureItem(GraphicsCaptureItem item)
    {
        var targetWidth = item.Size.Width;
        var targetHeight = item.Size.Height;
        if (targetWidth <= 0 || targetHeight <= 0)
        {
            return HWND.Zero;
        }

        // 收集本应用顶层窗口句柄，避免把选择器宿主、预览窗口等误判为捕获目标。
        var ownHwnds = new HashSet<nint>();
        if (Application.Current is not null)
        {
            foreach (Window w in Application.Current.Windows)
            {
                var hwnd = new WindowInteropHelper(w).Handle;
                if (hwnd != nint.Zero)
                {
                    ownHwnds.Add(hwnd);
                }
            }
        }

        var displayName = item.DisplayName?.Trim();
        if (string.IsNullOrEmpty(displayName))
        {
            return HWND.Zero;
        }

        var titleMatches = new List<HWND>();
        var sizeMatches = new List<HWND>();

        _ = Win32.EnumWindows((HWND hwnd, LPARAM _) =>
        {
            if (!WindowEnumerationHelper.IsWindowValidForCapture(hwnd))
            {
                return true;
            }

            // 排除本应用窗口。
            if (ownHwnds.Contains(hwnd))
            {
                return true;
            }

            if (!Win32.GetWindowRect(hwnd, out var rect))
            {
                return true;
            }

            if (!string.Equals(TryGetWindowTitle(hwnd), displayName, StringComparison.Ordinal))
            {
                return true;
            }

            titleMatches.Add(hwnd);

            // 不同窗口类型及 DPI 上下文下，WGC 返回的可能是整窗或客户区尺寸。
            // 两种尺寸都用于消除同名窗口的歧义；标题仍是必需条件，避免把显示器误判成窗口。
            var windowWidth = rect.right - rect.left;
            var windowHeight = rect.bottom - rect.top;
            var isSizeMatch = windowWidth == targetWidth && windowHeight == targetHeight;
            if (!isSizeMatch && Win32.GetClientRect(hwnd, out var clientRect))
            {
                isSizeMatch = clientRect.right - clientRect.left == targetWidth
                              && clientRect.bottom - clientRect.top == targetHeight;
            }

            if (isSizeMatch)
            {
                sizeMatches.Add(hwnd);
            }

            return true;
        }, default);

        // 标题唯一时，即使窗口在 Picker 关闭后发生了尺寸变化，仍可安全用于裁剪。
        if (titleMatches.Count == 1)
        {
            return titleMatches[0];
        }

        if (sizeMatches.Count == 1)
        {
            return sizeMatches[0];
        }

        if (titleMatches.Count > 1)
        {
            _logger.LogDebug(
                "Picker capture skipped title bar crop: found {TitleMatchCount} windows titled {DisplayName}, with {SizeMatchCount} matching capture size {Width}x{Height}.",
                titleMatches.Count, displayName, sizeMatches.Count, targetWidth, targetHeight);
        }

        return HWND.Zero;
    }
}
