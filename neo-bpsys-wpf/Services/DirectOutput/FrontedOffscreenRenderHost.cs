using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace neo_bpsys_wpf.Services.DirectOutput;

/// <summary>
/// 在不可见 PresentationSource 中复用真实 v3 控件树，输出 96 DPI Pbgra32 帧。
/// 不创建 Window，也不读取 HWND 的屏幕内容。所有成员均须在创建它的 Dispatcher 上调用。
/// </summary>
internal sealed class FrontedOffscreenRenderHost : IDisposable
{
    private readonly HwndSource _source;
    private readonly Border _root;
    private readonly Viewbox _viewbox;
    private readonly IFrontedRenderer _renderer;
    private readonly ISharedDataService _sharedData;
    private readonly FrontedBehaviorRuntimeHostManager _behaviors;
    private readonly string _windowType;
    private readonly string _instanceId;
    private readonly RenderTargetBitmap _bitmap;
    private bool _disposed;

    /// <summary>创建无 WS_VISIBLE、无激活能力的 HWND 和固定尺寸的离屏树。</summary>
    /// <param name="windowType">布局的 Canonical ID。</param>
    /// <param name="config">真实窗口布局。</param>
    /// <param name="renderer">现有 v3 渲染器。</param>
    /// <param name="sharedData">应用共享状态。</param>
    /// <param name="behaviors">现有行为管理器。</param>
    /// <exception cref="ArgumentOutOfRangeException">输出尺寸不是有限正数或超过 8192。</exception>
    public FrontedOffscreenRenderHost(string windowType, FrontedWindowConfig config,
        IFrontedRenderer renderer, ISharedDataService sharedData, FrontedBehaviorRuntimeHostManager behaviors)
    {
        _windowType = windowType;
        _instanceId = $"direct-output:{windowType}";
        _renderer = renderer;
        _sharedData = sharedData;
        _behaviors = behaviors;
        Width = PixelSize(config.WindowSettings.WindowWidth);
        Height = PixelSize(config.WindowSettings.WindowHeight);
        Canvas = new Canvas { Name = FrontedLayoutConstants.BaseCanvasName };
        _viewbox = new Viewbox { Child = Canvas, Stretch = config.WindowSettings.ViewboxStretch };
        _root = new Border
        {
            Width = config.WindowSettings.WindowWidth,
            Height = config.WindowSettings.WindowHeight,
            ClipToBounds = true,
            Child = _viewbox
        };
        foreach (var element in new FrameworkElement[] { _root, _viewbox, Canvas })
        {
            element.UseLayoutRounding = true;
            element.SnapsToDevicePixels = true;
            TextOptions.SetTextFormattingMode(element, TextFormattingMode.Display);
            // ClearType requires an opaque destination; grayscale preserves alpha at glyph edges.
            TextOptions.SetTextRenderingMode(element, TextRenderingMode.Grayscale);
            RenderOptions.SetBitmapScalingMode(element, BitmapScalingMode.HighQuality);
        }
        _bitmap = new RenderTargetBitmap(Width, Height, 96, 96, PixelFormats.Pbgra32);
        _source = new HwndSource(new HwndSourceParameters("neo-bpsys-wpf Direct Output")
        {
            WindowStyle = unchecked((int)0x80000000), // WS_POPUP, deliberately no WS_VISIBLE
            ExtendedWindowStyle = 0x08000080, // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW
            Width = Width,
            Height = Height
        }) { SizeToContent = SizeToContent.WidthAndHeight };
        _source.CompositionTarget.BackgroundColor = Colors.Transparent;
        _source.RootVisual = _root;
    }

    /// <summary>获取输出像素宽度，独立于 Canvas 设计坐标。</summary>
    public int Width { get; }

    /// <summary>获取输出像素高度。</summary>
    public int Height { get; }

    /// <summary>获取持久复用的真实布局画布。</summary>
    public Canvas Canvas { get; }

    /// <summary>获取离屏运行实例的行为 ID，避免与 Window Capture 的宿主冲突。</summary>
    public string InstanceId => _instanceId;

    /// <summary>渲染布局并附加真实行为；只在开始或显式布局/BO 状态变化时重建控件。</summary>
    /// <param name="config">活动包的布局。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>Loaded、绑定和行为附加完成的任务。</returns>
    public async Task LoadAsync(FrontedWindowConfig config, CancellationToken token)
    {
        _root.Dispatcher.VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
        _behaviors.DetachHost(_instanceId);
        _viewbox.Stretch = config.WindowSettings.ViewboxStretch;
        _root.Background = string.IsNullOrWhiteSpace(config.WindowSettings.BackgroundColor)
            ? Brushes.Transparent
            : (Brush)new BrushConverter().ConvertFromInvariantString(config.WindowSettings.BackgroundColor)!;
        _renderer.RenderToCanvas(Canvas, config, new FrontedRenderContext
        {
            WindowId = _instanceId,
            WindowTypeName = _windowType,
            CanvasName = FrontedLayoutConstants.BaseCanvasName
        });
        Layout();
        // Let real PresentationSource Loaded/Unloaded and data binding callbacks run before behavior parts resolve.
        await _root.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle, token);
        token.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _behaviors.AttachHostAsync(new FrontedBehaviorRuntimeContext
        {
            WindowId = _instanceId,
            WindowType = _windowType,
            RootCanvas = Canvas,
            WindowConfig = config,
            SharedDataService = _sharedData
        }, token);
    }

    /// <summary>从同一控件树生成帧，清除上一帧以保留透明动画与移除效果。</summary>
    /// <param name="pixels">接收紧密排列、从上到下的 Pbgra32 像素，长度至少为 Width × Height × 4。</param>
    /// <exception cref="ObjectDisposedException">宿主已释放。</exception>
    public void CopyFrame(byte[] pixels)
    {
        _root.Dispatcher.VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
        Layout();
        _bitmap.Clear();
        _bitmap.Render(_root);
        _bitmap.CopyPixels(pixels, checked(Width * 4), 0);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _root.Dispatcher.VerifyAccess();
        if (_disposed) return;
        _disposed = true;
        try { _behaviors.DetachHost(_instanceId); }
        finally
        {
            _source.RootVisual = null;
            _source.Dispose();
            Canvas.Children.Clear();
            Canvas.Background = null;
            _root.Child = null;
        }
    }

    private void Layout()
    {
        var size = new Size(_root.Width, _root.Height);
        _root.Measure(size);
        _root.Arrange(new Rect(size));
        _root.UpdateLayout();
    }

    private static int PixelSize(double value)
    {
        if (!double.IsFinite(value) || value <= 0 || value > 8192)
            throw new ArgumentOutOfRangeException(nameof(value), value, "Direct Output dimensions must be within (0, 8192].");
        return checked((int)Math.Ceiling(value));
    }
}
