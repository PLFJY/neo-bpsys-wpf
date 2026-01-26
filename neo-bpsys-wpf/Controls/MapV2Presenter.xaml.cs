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

    public MapV2? Map
    {
        get => (MapV2)GetValue(MapProperty);
        set => SetValue(MapProperty, value);
    }

    // Using a DependencyProperty as the backing store for Map.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty MapProperty =
        DependencyProperty.Register(nameof(Map), typeof(MapV2), typeof(MapV2Presenter), new PropertyMetadata(null));

    public Brush MapNameForeground
    {
        get => (Brush)GetValue(MapNameForegroundProperty);
        set => SetValue(MapNameForegroundProperty, value);
    }

    // Using a DependencyProperty as the backing store for MapNameForeground.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty MapNameForegroundProperty =
        DependencyProperty.Register(nameof(MapNameForeground), typeof(Brush), typeof(MapV2Presenter), new PropertyMetadata(ColorHelper.HexToBrush("#FFFFFF")));

    public FontFamily MapNameFontFamily
    {
        get => (FontFamily)GetValue(MapNameFontFamilyProperty);
        set => SetValue(MapNameFontFamilyProperty, value);
    }

    // Using a DependencyProperty as the backing store for MapNameFontFamily.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty MapNameFontFamilyProperty =
        DependencyProperty.Register(nameof(MapNameFontFamily), typeof(FontFamily), typeof(MapV2Presenter), new PropertyMetadata(new FontFamily(new Uri("pack://application:,,,/Assets/Fonts/"), "./#汉仪第五人格体简")));

    public double MapNameFontSize
    {
        get => (double)GetValue(MapNameFontSizeProperty);
        set => SetValue(MapNameFontSizeProperty, value);
    }

    // Using a DependencyProperty as the backing store for MapNameFontSize.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty MapNameFontSizeProperty =
        DependencyProperty.Register(nameof(MapNameFontSize), typeof(double), typeof(MapV2Presenter), new PropertyMetadata(14.0));

    public FontWeight MapNameFontWeight
    {
        get => (FontWeight)GetValue(MapNameFontWeightProperty);
        set => SetValue(MapNameFontWeightProperty, value);
    }

    // Using a DependencyProperty as the backing store for MapNameFontWeight.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty MapNameFontWeightProperty =
        DependencyProperty.Register(nameof(MapNameFontWeight), typeof(FontWeight), typeof(MapV2Presenter), new PropertyMetadata(FontWeights.Regular));

    public Brush TeamNameForeground
    {
        get => (Brush)GetValue(TeamNameForegroundProperty);
        set => SetValue(TeamNameForegroundProperty, value);
    }

    // Using a DependencyProperty as the backing store for TeamNameForeground.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty TeamNameForegroundProperty =
        DependencyProperty.Register(nameof(TeamNameForeground), typeof(Brush), typeof(MapV2Presenter), new PropertyMetadata(ColorHelper.HexToBrush("#FFFFFF")));

    public FontFamily TeamNameFontFamily
    {
        get => (FontFamily)GetValue(TeamNameFontFamilyProperty);
        set => SetValue(TeamNameFontFamilyProperty, value);
    }

    // Using a DependencyProperty as the backing store for TeamNameFontFamily.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty TeamNameFontFamilyProperty =
        DependencyProperty.Register(nameof(TeamNameFontFamily), typeof(FontFamily), typeof(MapV2Presenter), new PropertyMetadata(new FontFamily("Arial")));

    public double TeamNameFontSize
    {
        get => (double)GetValue(TeamNameFontSizeProperty);
        set => SetValue(TeamNameFontSizeProperty, value);
    }

    // Using a DependencyProperty as the backing store for TeamNameFontSize.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty TeamNameFontSizeProperty =
        DependencyProperty.Register(nameof(TeamNameFontSize), typeof(double), typeof(MapV2Presenter), new PropertyMetadata(16.0));

    public FontWeight TeamNameFontWeight
    {
        get => (FontWeight)GetValue(TeamNameFontWeightProperty);
        set => SetValue(TeamNameFontWeightProperty, value);
    }

    // Using a DependencyProperty as the backing store for TeamNameFontWeight.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty TeamNameFontWeightProperty =
        DependencyProperty.Register(nameof(TeamNameFontWeight), typeof(FontWeight), typeof(MapV2Presenter), new PropertyMetadata(FontWeights.Regular));

    public Brush CampNameForeground
    {
        get => (Brush)GetValue(CampNameForegroundProperty);
        set => SetValue(CampNameForegroundProperty, value);
    }

    // Using a DependencyProperty as the backing store for CampNameForeground.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty CampNameForegroundProperty =
        DependencyProperty.Register(nameof(CampNameForeground), typeof(Brush), typeof(MapV2Presenter), new PropertyMetadata(ColorHelper.HexToBrush("#FFFFFF")));

    public FontFamily CampNameFontFamily
    {
        get => (FontFamily)GetValue(CampNameFontFamilyProperty);
        set => SetValue(CampNameFontFamilyProperty, value);
    }

    // Using a DependencyProperty as the backing store for CampNameFontFamily.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty CampNameFontFamilyProperty =
        DependencyProperty.Register(nameof(CampNameFontFamily), typeof(FontFamily), typeof(MapV2Presenter), new PropertyMetadata(new FontFamily("Arial")));

    public double CampNameFontSize
    {
        get => (double)GetValue(CampNameFontSizeProperty);
        set => SetValue(CampNameFontSizeProperty, value);
    }

    // Using a DependencyProperty as the backing store for CampNameFontSize.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty CampNameFontSizeProperty =
        DependencyProperty.Register(nameof(CampNameFontSize), typeof(double), typeof(MapV2Presenter), new PropertyMetadata(20.0));

    public FontWeight CampNameFontWeight
    {
        get => (FontWeight)GetValue(CampNameFontWeightProperty);
        set => SetValue(CampNameFontWeightProperty, value);
    }

    // Using a DependencyProperty as the backing store for CampNameFontWeight.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty CampNameFontWeightProperty =
        DependencyProperty.Register(nameof(CampNameFontWeight), typeof(FontWeight), typeof(MapV2Presenter), new PropertyMetadata(FontWeights.Regular));

    public Brush PickingBorderBrush
    {
        get => (Brush)GetValue(PickingBorderBrushProperty);
        set => SetValue(PickingBorderBrushProperty, value);
    }

    // Using a DependencyProperty as the backing store for PickingBorderBrush.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty PickingBorderBrushProperty =
        DependencyProperty.Register(nameof(PickingBorderBrush), typeof(Brush), typeof(MapV2Presenter), new PropertyMetadata(new SolidColorBrush(Colors.White)));

    public ImageSource PickingBorderImage
    {
        get => (ImageSource)GetValue(PickingBorderImageProperty);
        set => SetValue(PickingBorderImageProperty, value);
    }

    // Using a DependencyProperty as the backing store for PickingBorderImage.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty PickingBorderImageProperty =
        DependencyProperty.Register(nameof(PickingBorderImage), typeof(ImageSource), typeof(MapV2Presenter), new PropertyMetadata(null));
}