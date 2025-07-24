using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace neo_bpsys_wpf.Controls;

/// <summary>
/// TextSettingsEditControl.xaml 的交互逻辑
/// </summary>
[ObservableObject]
public partial class TextSettingsEditControl : UserControl
{
    private readonly TextSettings? _textSettings;
    private readonly Action _saveAction;
    private readonly Action _closeAction;

    public TextSettingsEditControl(List<FontFamily> fontList, TextSettings? textSettings,
        Action saveAction, Action cancelAction)
    {
        InitializeComponent();
        DataContext = this;
        FontList = fontList;
        _textSettings = textSettings;
        _saveAction = saveAction;
        _closeAction = cancelAction;
        if (textSettings == null) return;
        SelectedColor = textSettings.Color.ToColor();
        SelectedFontFamily = textSettings.FontFamily;
        SelectedFontSize = textSettings.FontSize.ToString();
        SelectedFontWeight = textSettings.FontWeight;
    }

    [ObservableProperty] private List<FontFamily> _fontList;

    [ObservableProperty] private Color _selectedColor = Color.FromArgb(255, 255, 255, 255);

    [ObservableProperty] private FontFamily _selectedFontFamily = new("Arial");

    [ObservableProperty] private string _selectedFontSize = "16.0";

    [ObservableProperty] private FontWeight _selectedFontWeight = FontWeights.Normal;

    [RelayCommand]
    private void Apply()
    {
        if (_textSettings == null)
        {
            _closeAction?.Invoke();
            return;
        }

        if (double.TryParse(SelectedFontSize, out var fontsize))
            _textSettings.FontSize = fontsize;
        _textSettings.FontFamily = SelectedFontFamily;
        _textSettings.Color = SelectedColor.ToArgbHexString();
        _textSettings.FontWeight = SelectedFontWeight;
    }

    [RelayCommand]
    private void Save()
    {
        if (_textSettings == null)
        {
            _closeAction?.Invoke();
            return;
        }

        if (double.TryParse(SelectedFontSize, out var fontsize))
            _textSettings.FontSize = fontsize;
        _textSettings.FontFamily = SelectedFontFamily;
        _textSettings.Color = SelectedColor.ToArgbHexString();
        _textSettings.FontWeight = SelectedFontWeight;
        _saveAction?.Invoke();
    }

    [RelayCommand]
    private void Close()
    {
        _closeAction?.Invoke();
    }

    public List<FontWeight> FontWeightList { get; } =
    [
        FontWeights.Thin,
        FontWeights.ExtraLight,
        FontWeights.Light,
        FontWeights.Normal,
        FontWeights.Medium,
        FontWeights.SemiBold,
        FontWeights.Bold,
        FontWeights.ExtraBold, FontWeights.Black
    ];
}