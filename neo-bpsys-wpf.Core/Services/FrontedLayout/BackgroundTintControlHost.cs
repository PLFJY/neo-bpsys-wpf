using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 承载局部背景染色结果的宿主控件。
/// </summary>
/// <remarks>
/// <para>
/// 染色属于 CPU 像素处理，不应当阻塞前台窗口的 UI 线程。运行时宿主首次渲染同步完成以
/// 避免初始空白，后续颜色/强度/尺寸变化通过调度器合并，并在后台计算后以 latest-wins
/// 方式回到 UI 线程应用；Designer 预览与单元测试默认保持同步，避免破坏既有交互时序。
/// </para>
/// <para>
/// 控件不可见期间只记录脏状态，恢复可见后补算一次，避免被遮挡或最小化的前台窗口持续消耗 CPU。
/// </para>
/// </remarks>
public sealed class BackgroundTintControlHost : Grid
{
    /// <summary>染色颜色值依赖属性。</summary>
    public static readonly DependencyProperty TintColorValueProperty = DependencyProperty.Register(
        nameof(TintColorValue),
        typeof(string),
        typeof(BackgroundTintControlHost),
        new PropertyMetadata(ColorHelper.DefaultColorHex, OnTintColorValueChanged));

    private readonly BackgroundImageTintProcessor _processor;
    private readonly ImageSource _source;
    private readonly string? _sourceKey;
    private readonly BackgroundTintMode _mode;
    private readonly BackgroundTintNormalizationMode _normalizationMode;
    private readonly IReadOnlyList<PolygonVertexConfig>? _polygonPoints;
    private readonly ILogger? _logger;
    private readonly bool _useAsyncTint;
    private double _strength;
    private double _textureStrength;
    private Rect _canvasRegion;
    private readonly double _canvasWidth;
    private readonly double _canvasHeight;
    private bool _hasRendered;
    private bool _dirty;
    private bool _rendering;

    /// <summary>
    /// 初始化同步渲染的染色控件宿主。
    /// </summary>
    /// <param name="processor">染色处理器。</param>
    /// <param name="source">Canvas 背景图源。</param>
    /// <param name="sourceKey">背景图资源键，用于缓存键。</param>
    /// <param name="mode">染色模式。</param>
    /// <param name="strength">染色强度。</param>
    /// <param name="textureStrength">纹理保留强度。</param>
    /// <param name="canvasWidth">Canvas 宽度。</param>
    /// <param name="canvasHeight">Canvas 高度。</param>
    /// <param name="left">可见区域左边距。</param>
    /// <param name="top">可见区域上边距。</param>
    /// <param name="maskWidth">可见区域宽度。</param>
    /// <param name="maskHeight">可见区域高度。</param>
    /// <param name="normalizationMode">可见区域归一化模式。</param>
    /// <param name="polygonPoints">可选的归一化多边形。</param>
    /// <param name="logger">可选日志记录器。</param>
    public BackgroundTintControlHost(
        BackgroundImageTintProcessor processor,
        ImageSource source,
        string? sourceKey,
        BackgroundTintMode mode,
        double strength,
        double textureStrength,
        double canvasWidth,
        double canvasHeight,
        double left,
        double top,
        double maskWidth,
        double maskHeight,
        BackgroundTintNormalizationMode normalizationMode,
        IReadOnlyList<PolygonVertexConfig>? polygonPoints,
        ILogger? logger)
        : this(
            processor, source, sourceKey, mode, strength, textureStrength, canvasWidth, canvasHeight,
            left, top, maskWidth, maskHeight, normalizationMode, polygonPoints, logger, false)
    {
    }

    /// <summary>
    /// 初始化染色控件宿主。
    /// </summary>
    /// <param name="processor">染色处理器。</param>
    /// <param name="source">Canvas 背景图源。</param>
    /// <param name="sourceKey">背景图资源键，用于缓存键。</param>
    /// <param name="mode">染色模式。</param>
    /// <param name="strength">染色强度。</param>
    /// <param name="textureStrength">纹理保留强度。</param>
    /// <param name="canvasWidth">Canvas 宽度。</param>
    /// <param name="canvasHeight">Canvas 高度。</param>
    /// <param name="left">可见区域左边距。</param>
    /// <param name="top">可见区域上边距。</param>
    /// <param name="maskWidth">可见区域宽度。</param>
    /// <param name="maskHeight">可见区域高度。</param>
    /// <param name="normalizationMode">可见区域归一化模式。</param>
    /// <param name="polygonPoints">可选的归一化多边形。</param>
    /// <param name="logger">可选日志记录器。</param>
    /// <param name="useAsyncTint">是否在首次渲染后把后续染色放入后台线程。</param>
    public BackgroundTintControlHost(
        BackgroundImageTintProcessor processor,
        ImageSource source,
        string? sourceKey,
        BackgroundTintMode mode,
        double strength,
        double textureStrength,
        double canvasWidth,
        double canvasHeight,
        double left,
        double top,
        double maskWidth,
        double maskHeight,
        BackgroundTintNormalizationMode normalizationMode,
        IReadOnlyList<PolygonVertexConfig>? polygonPoints,
        ILogger? logger,
        bool useAsyncTint)
    {
        _processor = processor;
        _source = source;
        _sourceKey = sourceKey;
        _mode = mode;
        _strength = strength;
        _textureStrength = textureStrength;
        _normalizationMode = normalizationMode;
        _canvasRegion = new Rect(left, top, maskWidth, maskHeight);
        _canvasWidth = canvasWidth;
        _canvasHeight = canvasHeight;
        _polygonPoints = polygonPoints;
        _logger = logger;
        _useAsyncTint = useAsyncTint;
        ClipToBounds = true;

        TintedImage = new Image
        {
            Width = canvasWidth,
            Height = canvasHeight,
            Stretch = Stretch.Fill,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(-left, -top, 0, 0),
            IsHitTestVisible = false
        };
        Children.Add(TintedImage);
        IsVisibleChanged += OnIsVisibleChanged;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_dirty)
        {
            RequestRefresh();
        }
    }

    /// <summary>获取或设置绑定到共享数据的染色颜色。</summary>
    public string? TintColorValue
    {
        get => (string?)GetValue(TintColorValueProperty);
        set => SetValue(TintColorValueProperty, value);
    }

    /// <summary>获取或设置染色强度。</summary>
    public double TintStrengthValue
    {
        get => _strength;
        set
        {
            _strength = Math.Clamp(double.IsFinite(value) ? value : _strength, 0D, 1D);
            RequestRefresh();
        }
    }

    /// <summary>获取或设置纹理保留强度。</summary>
    public double TextureStrengthValue
    {
        get => _textureStrength;
        set
        {
            _textureStrength = Math.Clamp(double.IsFinite(value) ? value : _textureStrength, 0D, 1D);
            RequestRefresh();
        }
    }

    /// <summary>获取承载染色结果的图片元素。</summary>
    public Image TintedImage { get; }

    /// <summary>
    /// 更新可见遮罩尺寸。
    /// </summary>
    /// <param name="width">新宽度。</param>
    /// <param name="height">新高度。</param>
    public void UpdateMaskSize(double width, double height)
    {
        if (!double.IsFinite(width)
            || !double.IsFinite(height)
            || width <= 0D
            || height <= 0D
            || (_canvasRegion.Width.Equals(width) && _canvasRegion.Height.Equals(height)))
        {
            return;
        }

        _canvasRegion = new Rect(_canvasRegion.X, _canvasRegion.Y, width, height);
        RequestRefresh();
    }

    /// <summary>
    /// 同步重新生成染色图。
    /// </summary>
    /// <remarks>
    /// 保留同步入口给首次创建与 Designer/测试使用；运行时绑定变化应走内部调度路径。
    /// </remarks>
    public void RefreshTint()
    {
        if (!TryCreateTintOptions(out var tint, out var options))
        {
            return;
        }

        try
        {
            TintedImage.Source = _processor.CreateTinted(_source, _sourceKey, tint, options, _logger);
            _hasRendered = true;
            _dirty = false;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Background tint rendering failed synchronously.");
        }
    }

    private void RequestRefresh()
    {
        if (!IsLoaded)
        {
            // 尚未进入可视树：记录脏状态，Loaded 后由工厂的首次同步渲染或 OnLoaded 补算。
            _dirty = true;
            return;
        }

        if (!IsVisible)
        {
            _dirty = true;
            return;
        }

        if (!_useAsyncTint || !_hasRendered)
        {
            RefreshTint();
            return;
        }

        _dirty = true;
        if (!_rendering)
        {
            _ = RenderLatestAsync();
        }
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible && _dirty)
        {
            RequestRefresh();
        }
    }

    private async Task RenderLatestAsync()
    {
        _rendering = true;
        try
        {
            while (_dirty)
            {
                _dirty = false;
                if (!TryCreateTintOptions(out var tint, out var options))
                {
                    continue;
                }

                var source = _source;
                var sourceKey = _sourceKey;
                var logger = _logger;
                BitmapSource? result = null;
                try
                {
                    // CreateTinted 只读取冻结的源位图与不可变选项，缓存内部自带锁，可安全在后台执行。
                    result = await Task.Run(() => _processor.CreateTinted(source, sourceKey, tint, options, logger));
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "Background tint rendering failed asynchronously.");
                }

                if (result is not null)
                {
                    if (!IsVisible)
                    {
                        // 窗口在计算期间被隐藏：保留脏标记，恢复可见时再应用，避免隐藏窗口占用渲染资源。
                        _dirty = true;
                        break;
                    }

                    TintedImage.Source = result;
                    _hasRendered = true;
                }
            }
        }
        finally
        {
            _rendering = false;
            if (_dirty && IsVisible)
            {
                _ = RenderLatestAsync();
            }
        }
    }

    private bool TryCreateTintOptions(out Color tint, out BackgroundTintProcessingOptions options)
    {
        if (!ColorHelper.TryParseColor(TintColorValue, out tint))
        {
            _logger?.LogWarning("Invalid background tint color: {Value}", TintColorValue);
            tint = Colors.White;
        }

        options = new BackgroundTintProcessingOptions
        {
            Mode = _mode,
            TintStrength = _strength,
            TextureStrength = _textureStrength,
            NormalizationMode = _normalizationMode,
            CanvasRegion = _canvasRegion,
            CanvasWidth = _canvasWidth,
            CanvasHeight = _canvasHeight,
            PolygonPoints = _polygonPoints,
            // 宿主始终把染色结果裁剪到可见区域；BaseColorWithTexture 模式只处理该区域即可。
            RestrictProcessingToRegion = true
        };
        return true;
    }

    private static void OnTintColorValueChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        var host = (BackgroundTintControlHost)dependencyObject;
        if (host.IsLoaded && host._useAsyncTint && host._hasRendered)
        {
            // 同一帧内多次颜色变化只保留最后一次，避免拖动色盘时产生重复后台任务。
            FrontedRenderScheduler.Schedule(host, host.RequestRefresh);
            return;
        }

        host.RequestRefresh();
    }
}
