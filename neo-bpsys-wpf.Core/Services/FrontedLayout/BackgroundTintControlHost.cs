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
    private readonly double _strength;
    private readonly double _textureStrength;
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
        ILogger? logger)
    {
        _processor = processor;
        _source = source;
        _sourceKey = sourceKey;
        _mode = mode;
        _strength = strength;
        _textureStrength = textureStrength;
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

    public Image TintedImage { get; }

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
            _mode,
            _strength,
            _textureStrength,
            _logger);
    }

    private static void OnTintColorValueChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        ((BackgroundTintControlHost)dependencyObject).RefreshTint();
    }
}
