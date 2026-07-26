using neo_bpsys_wpf.Core.Enums;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace neo_bpsys_wpf.Controls;

/// <summary>
/// 全局比分展示控件，显示比分文本和阵营图标。
/// </summary>
public class GlobalScorePresenter : Control
{
    /// <summary>
    /// 初始化全局比分展示控件。
    /// </summary>
    public GlobalScorePresenter()
    {
        Loaded += (_, _) => UpdateTintedCampIcons();
    }

    /// <summary>
    /// 获取或设置显示的比分文本。
    /// </summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>
    /// <see cref="Text"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(GlobalScorePresenter), new FrameworkPropertyMetadata("-", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>
    /// 获取或设置一个值，指示是否显示为监管者阵营图标。
    /// </summary>
    public bool IsHunIcon
    {
        get => (bool)GetValue(IsHunIconProperty);
        set => SetValue(IsHunIconProperty, value);
    }

    /// <summary>
    /// <see cref="IsHunIcon"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty IsHunIconProperty =
        DependencyProperty.Register(nameof(IsHunIcon), typeof(bool), typeof(GlobalScorePresenter), new PropertyMetadata(false));

    /// <summary>
    /// 获取或设置一个值，指示阵营图标是否可见。
    /// </summary>
    public bool IsCampVisible
    {
        get => (bool)GetValue(IsCampVisibleProperty);
        set => SetValue(IsCampVisibleProperty, value);
    }

    /// <summary>
    /// <see cref="IsCampVisible"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty IsCampVisibleProperty =
        DependencyProperty.Register(nameof(IsCampVisible), typeof(bool), typeof(GlobalScorePresenter), new PropertyMetadata(false));

    /// <summary>
    /// 获取或设置阵营图标填充颜色。
    /// </summary>
    public GlobalScoreCampIconColor CampIconColor
    {
        get => (GlobalScoreCampIconColor)GetValue(CampIconColorProperty);
        set => SetValue(CampIconColorProperty, value);
    }

    /// <summary>
    /// <see cref="CampIconColor"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty CampIconColorProperty =
        DependencyProperty.Register(
            nameof(CampIconColor),
            typeof(GlobalScoreCampIconColor),
            typeof(GlobalScorePresenter),
            new PropertyMetadata(GlobalScoreCampIconColor.White, OnCampIconColorChanged));

    /// <summary>
    /// 获取填充后的求生者阵营图标。
    /// </summary>
    public ImageSource? TintedSurIcon
    {
        get => (ImageSource?)GetValue(TintedSurIconProperty);
        private set => SetValue(TintedSurIconPropertyKey, value);
    }

    private static readonly DependencyPropertyKey TintedSurIconPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(TintedSurIcon),
            typeof(ImageSource),
            typeof(GlobalScorePresenter),
            new PropertyMetadata(null));

    /// <summary>
    /// <see cref="TintedSurIcon"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty TintedSurIconProperty = TintedSurIconPropertyKey.DependencyProperty;

    /// <summary>
    /// 获取填充后的监管者阵营图标。
    /// </summary>
    public ImageSource? TintedHunIcon
    {
        get => (ImageSource?)GetValue(TintedHunIconProperty);
        private set => SetValue(TintedHunIconPropertyKey, value);
    }

    private static readonly DependencyPropertyKey TintedHunIconPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(TintedHunIcon),
            typeof(ImageSource),
            typeof(GlobalScorePresenter),
            new PropertyMetadata(null));

    /// <summary>
    /// <see cref="TintedHunIcon"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty TintedHunIconProperty = TintedHunIconPropertyKey.DependencyProperty;

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        UpdateTintedCampIcons();
    }

    private static void OnCampIconColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GlobalScorePresenter presenter)
        {
            presenter.UpdateTintedCampIcons();
        }
    }

    private void UpdateTintedCampIcons()
    {
        TintedSurIcon = CreateTintedIcon(TryFindResource("scoreGlobal_surIcon") as ImageSource, CampIconColor);
        TintedHunIcon = CreateTintedIcon(TryFindResource("scoreGlobal_hunIcon") as ImageSource, CampIconColor);
    }

    private static ImageSource? CreateTintedIcon(ImageSource? source, GlobalScoreCampIconColor color)
    {
        if (source is not BitmapSource bitmap)
        {
            return source;
        }

        var formatted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
        var stride = formatted.PixelWidth * 4;
        var pixels = new byte[stride * formatted.PixelHeight];
        formatted.CopyPixels(pixels, stride, 0);

        var fill = color == GlobalScoreCampIconColor.Black ? (byte)0 : (byte)255;
        for (var i = 0; i < pixels.Length; i += 4)
        {
            var alpha = pixels[i + 3];
            pixels[i] = fill;
            pixels[i + 1] = fill;
            pixels[i + 2] = fill;
            pixels[i + 3] = alpha;
        }

        var tinted = BitmapSource.Create(
            formatted.PixelWidth,
            formatted.PixelHeight,
            formatted.DpiX,
            formatted.DpiY,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);
        tinted.Freeze();
        return tinted;
    }
}
