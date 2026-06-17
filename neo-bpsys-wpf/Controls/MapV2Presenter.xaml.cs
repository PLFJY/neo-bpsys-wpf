using neo_bpsys_wpf.Core.Helpers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MapV2 = neo_bpsys_wpf.Core.Models.MapV2;

namespace neo_bpsys_wpf.Controls;

/// <summary>
/// MapV2Presenter.xaml 的交互逻辑
/// </summary>
public partial class MapV2Presenter : UserControl
{
    public MapV2Presenter()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 获取地图 BP v2 选图边框动画目标元素。
    /// </summary>
    public FrameworkElement PickingBorderAnimationTarget => PickingBorder;

    /// <summary>
    /// 获取或设置要展示的地图数据。
    /// </summary>
    public MapV2? Map
    {
        get => (MapV2)GetValue(MapProperty);
        set => SetValue(MapProperty, value);
    }

    /// <summary>
    /// <see cref="Map"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty MapProperty =
        DependencyProperty.Register(nameof(Map), typeof(MapV2), typeof(MapV2Presenter), new PropertyMetadata(null));

    /// <summary>
    /// 获取或设置地图名称的前景色。
    /// </summary>
    public Brush MapNameForeground
    {
        get => (Brush)GetValue(MapNameForegroundProperty);
        set => SetValue(MapNameForegroundProperty, value);
    }

    /// <summary>
    /// <see cref="MapNameForeground"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty MapNameForegroundProperty =
        DependencyProperty.Register(nameof(MapNameForeground), typeof(Brush), typeof(MapV2Presenter), new PropertyMetadata(ColorHelper.HexToBrush("#FFFFFF")));

    /// <summary>
    /// 获取或设置地图名称的字体。
    /// </summary>
    public FontFamily MapNameFontFamily
    {
        get => (FontFamily)GetValue(MapNameFontFamilyProperty);
        set => SetValue(MapNameFontFamilyProperty, value);
    }

    /// <summary>
    /// <see cref="MapNameFontFamily"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty MapNameFontFamilyProperty =
        DependencyProperty.Register(nameof(MapNameFontFamily), typeof(FontFamily), typeof(MapV2Presenter), new PropertyMetadata(new FontFamily(new Uri("pack://application:,,,/Assets/Fonts/"), "./#汉仪第五人格体简")));

    /// <summary>
    /// 获取或设置地图名称的字体大小。
    /// </summary>
    public double MapNameFontSize
    {
        get => (double)GetValue(MapNameFontSizeProperty);
        set => SetValue(MapNameFontSizeProperty, value);
    }

    /// <summary>
    /// <see cref="MapNameFontSize"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty MapNameFontSizeProperty =
        DependencyProperty.Register(nameof(MapNameFontSize), typeof(double), typeof(MapV2Presenter), new PropertyMetadata(14.0));

    /// <summary>
    /// 获取或设置地图名称的字体粗细。
    /// </summary>
    public FontWeight MapNameFontWeight
    {
        get => (FontWeight)GetValue(MapNameFontWeightProperty);
        set => SetValue(MapNameFontWeightProperty, value);
    }

    /// <summary>
    /// <see cref="MapNameFontWeight"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty MapNameFontWeightProperty =
        DependencyProperty.Register(nameof(MapNameFontWeight), typeof(FontWeight), typeof(MapV2Presenter), new PropertyMetadata(FontWeights.Regular));

    /// <summary>
    /// 获取或设置队伍名称的前景色。
    /// </summary>
    public Brush TeamNameForeground
    {
        get => (Brush)GetValue(TeamNameForegroundProperty);
        set => SetValue(TeamNameForegroundProperty, value);
    }

    /// <summary>
    /// <see cref="TeamNameForeground"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty TeamNameForegroundProperty =
        DependencyProperty.Register(nameof(TeamNameForeground), typeof(Brush), typeof(MapV2Presenter), new PropertyMetadata(ColorHelper.HexToBrush("#FFFFFF")));

    /// <summary>
    /// 获取或设置队伍名称的字体。
    /// </summary>
    public FontFamily TeamNameFontFamily
    {
        get => (FontFamily)GetValue(TeamNameFontFamilyProperty);
        set => SetValue(TeamNameFontFamilyProperty, value);
    }

    /// <summary>
    /// <see cref="TeamNameFontFamily"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty TeamNameFontFamilyProperty =
        DependencyProperty.Register(nameof(TeamNameFontFamily), typeof(FontFamily), typeof(MapV2Presenter), new PropertyMetadata(new FontFamily("Arial")));

    /// <summary>
    /// 获取或设置队伍名称的字体大小。
    /// </summary>
    public double TeamNameFontSize
    {
        get => (double)GetValue(TeamNameFontSizeProperty);
        set => SetValue(TeamNameFontSizeProperty, value);
    }

    /// <summary>
    /// <see cref="TeamNameFontSize"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty TeamNameFontSizeProperty =
        DependencyProperty.Register(nameof(TeamNameFontSize), typeof(double), typeof(MapV2Presenter), new PropertyMetadata(16.0));

    /// <summary>
    /// 获取或设置队伍名称的字体粗细。
    /// </summary>
    public FontWeight TeamNameFontWeight
    {
        get => (FontWeight)GetValue(TeamNameFontWeightProperty);
        set => SetValue(TeamNameFontWeightProperty, value);
    }

    /// <summary>
    /// <see cref="TeamNameFontWeight"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty TeamNameFontWeightProperty =
        DependencyProperty.Register(nameof(TeamNameFontWeight), typeof(FontWeight), typeof(MapV2Presenter), new PropertyMetadata(FontWeights.Regular));

    /// <summary>
    /// 获取或设置阵营名称的前景色。
    /// </summary>
    public Brush CampNameForeground
    {
        get => (Brush)GetValue(CampNameForegroundProperty);
        set => SetValue(CampNameForegroundProperty, value);
    }

    /// <summary>
    /// <see cref="CampNameForeground"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty CampNameForegroundProperty =
        DependencyProperty.Register(nameof(CampNameForeground), typeof(Brush), typeof(MapV2Presenter), new PropertyMetadata(ColorHelper.HexToBrush("#FFFFFF")));

    /// <summary>
    /// 获取或设置阵营名称的字体。
    /// </summary>
    public FontFamily CampNameFontFamily
    {
        get => (FontFamily)GetValue(CampNameFontFamilyProperty);
        set => SetValue(CampNameFontFamilyProperty, value);
    }

    /// <summary>
    /// <see cref="CampNameFontFamily"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty CampNameFontFamilyProperty =
        DependencyProperty.Register(nameof(CampNameFontFamily), typeof(FontFamily), typeof(MapV2Presenter), new PropertyMetadata(new FontFamily("Arial")));

    /// <summary>
    /// 获取或设置阵营名称的字体大小。
    /// </summary>
    public double CampNameFontSize
    {
        get => (double)GetValue(CampNameFontSizeProperty);
        set => SetValue(CampNameFontSizeProperty, value);
    }

    /// <summary>
    /// <see cref="CampNameFontSize"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty CampNameFontSizeProperty =
        DependencyProperty.Register(nameof(CampNameFontSize), typeof(double), typeof(MapV2Presenter), new PropertyMetadata(20.0));

    /// <summary>
    /// 获取或设置阵营名称的字体粗细。
    /// </summary>
    public FontWeight CampNameFontWeight
    {
        get => (FontWeight)GetValue(CampNameFontWeightProperty);
        set => SetValue(CampNameFontWeightProperty, value);
    }

    /// <summary>
    /// <see cref="CampNameFontWeight"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty CampNameFontWeightProperty =
        DependencyProperty.Register(nameof(CampNameFontWeight), typeof(FontWeight), typeof(MapV2Presenter), new PropertyMetadata(FontWeights.Regular));

    /// <summary>
    /// 获取或设置地图边框正常状态的画刷。
    /// </summary>
    public Brush MapBorderNormalBrush
    {
        get => (Brush)GetValue(MapBorderNormalBrushProperty);
        set => SetValue(MapBorderNormalBrushProperty, value);
    }

    /// <summary>
    /// <see cref="MapBorderNormalBrush"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty MapBorderNormalBrushProperty =
        DependencyProperty.Register(nameof(MapBorderNormalBrush), typeof(Brush), typeof(MapV2Presenter), new PropertyMetadata(ColorHelper.HexToBrush("#2B483B")));

    /// <summary>
    /// 获取或设置地图边框被禁用状态的画刷。
    /// </summary>
    public Brush MapBorderBannedBrush
    {
        get => (Brush)GetValue(MapBorderBannedBrushProperty);
        set => SetValue(MapBorderBannedBrushProperty, value);
    }

    /// <summary>
    /// <see cref="MapBorderBannedBrush"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty MapBorderBannedBrushProperty =
        DependencyProperty.Register(nameof(MapBorderBannedBrush), typeof(Brush), typeof(MapV2Presenter), new PropertyMetadata(ColorHelper.HexToBrush("#9C3E2F")));

    /// <summary>
    /// 获取或设置正在选择状态下的边框画刷。
    /// </summary>
    public Brush PickingBorderBrush
    {
        get => (Brush)GetValue(PickingBorderBrushProperty);
        set => SetValue(PickingBorderBrushProperty, value);
    }

    /// <summary>
    /// <see cref="PickingBorderBrush"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty PickingBorderBrushProperty =
        DependencyProperty.Register(nameof(PickingBorderBrush), typeof(Brush), typeof(MapV2Presenter), new PropertyMetadata(new SolidColorBrush(Colors.White)));

    /// <summary>
    /// 获取或设置正在选择状态下的边框图片。
    /// </summary>
    public ImageSource PickingBorderImage
    {
        get => (ImageSource)GetValue(PickingBorderImageProperty);
        set => SetValue(PickingBorderImageProperty, value);
    }

    /// <summary>
    /// <see cref="PickingBorderImage"/> 依赖属性的标识符。
    /// </summary>
    public static readonly DependencyProperty PickingBorderImageProperty =
        DependencyProperty.Register(nameof(PickingBorderImage), typeof(ImageSource), typeof(MapV2Presenter), new PropertyMetadata(null));
}
