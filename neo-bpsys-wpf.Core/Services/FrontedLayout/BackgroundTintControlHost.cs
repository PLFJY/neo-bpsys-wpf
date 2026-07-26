using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

public sealed class BackgroundTintControlHost : Grid
{
    public static readonly DependencyProperty TintColorValueProperty = DependencyProperty.Register(
        nameof(TintColorValue),
        typeof(string),
        typeof(BackgroundTintControlHost),
        new PropertyMetadata(ColorHelper.DefaultColorHex, OnTintColorValueChanged));

    private readonly BackgroundImageTintProcessor _processor;
    private readonly ImageSource _source;
    private readonly string? _sourceKey;
    private readonly BackgroundTintMode _mode;
    private double _strength;
    private double _textureStrength;
    private readonly BackgroundTintNormalizationMode _normalizationMode;
    private Rect _canvasRegion;
    private readonly double _canvasWidth;
    private readonly double _canvasHeight;
    private readonly IReadOnlyList<PolygonVertexConfig>? _polygonPoints;
    private readonly ILogger? _logger;

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
    }

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
            RefreshTint();
        }
    }

    /// <summary>获取或设置纹理保留强度。</summary>
    public double TextureStrengthValue
    {
        get => _textureStrength;
        set
        {
            _textureStrength = Math.Clamp(double.IsFinite(value) ? value : _textureStrength, 0D, 1D);
            RefreshTint();
        }
    }

    public Image TintedImage { get; }

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
        RefreshTint();
    }

    public void RefreshTint()
    {
        if (!ColorHelper.TryParseColor(TintColorValue, out var tint))
        {
            _logger?.LogWarning("Invalid background tint color: {Value}", TintColorValue);
            tint = Colors.White;
        }

        TintedImage.Source = _processor.CreateTinted(
            _source,
            _sourceKey,
            tint,
            new BackgroundTintProcessingOptions
            {
                Mode = _mode,
                TintStrength = _strength,
                TextureStrength = _textureStrength,
                NormalizationMode = _normalizationMode,
                CanvasRegion = _canvasRegion,
                CanvasWidth = _canvasWidth,
                CanvasHeight = _canvasHeight,
                PolygonPoints = _polygonPoints
            },
            _logger);
    }

    private static void OnTintColorValueChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        ((BackgroundTintControlHost)dependencyObject).RefreshTint();
    }
}
