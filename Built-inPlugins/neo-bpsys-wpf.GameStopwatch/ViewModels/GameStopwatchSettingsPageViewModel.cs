using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;

namespace neo_bpsys_wpf.GameStopwatch.ViewModels;

/// <summary>比赛秒表设置页视图模型。</summary>
public sealed partial class GameStopwatchSettingsPageViewModel : ViewModelBase
{
    private const string WindowCanonicalId = "plugin:neo_bpsys_wpf.GameStopwatch/A6B4CB0B-354B-4B66-8AB8-2E94C3F0E5D2";

    private readonly IGameStopwatchService _service;
    private readonly IFrontedWindowService _frontedWindowService;
    private Color _textColor;
    private FontFamily? _selectedFontFamily;

    /// <summary>初始化设置页视图模型。</summary>
    /// <param name="service">秒表服务。</param>
    /// <param name="frontedWindowService">前台窗口服务。</param>
    public GameStopwatchSettingsPageViewModel(
        IGameStopwatchService service,
        IFrontedWindowService frontedWindowService)
    {
        _service = service;
        _frontedWindowService = frontedWindowService;
        FontFamilies = new ObservableCollection<FontFamily>(Fonts.SystemFontFamilies.OrderBy(x => x.Source, StringComparer.CurrentCultureIgnoreCase));
        _textColor = ParseColor(_service.TextColor);
        SelectedFontFamily = FindFontFamily(_service.FontFamilyName) ?? FontFamilies.FirstOrDefault();
        _service.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IGameStopwatchService.TextColor))
            {
                _textColor = ParseColor(_service.TextColor);
                OnPropertyChanged(nameof(TextColor));
            }

            if (e.PropertyName == nameof(IGameStopwatchService.FontFamilyName))
            {
                SyncSelectedFontFamily();
            }

            if (e.PropertyName == nameof(IGameStopwatchService.FontSize)) OnPropertyChanged(nameof(FontSize));
            if (e.PropertyName == nameof(IGameStopwatchService.WindowSize)) OnPropertyChanged(nameof(WindowSize));
        };
    }

    /// <summary>获取可选字体。</summary>
    public ObservableCollection<FontFamily> FontFamilies { get; }

    /// <summary>获取或设置文字颜色。</summary>
    public Color TextColor
    {
        get => _textColor;
        set => _service.TextColor = value.ToString();
    }

    /// <summary>获取或设置字号。</summary>
    /// <remarks>非法范围由 <see cref="IGameStopwatchService"/> 统一钳制。</remarks>
    public double FontSize
    {
        get => _service.FontSize;
        set => _service.FontSize = value;
    }

    /// <summary>获取或设置前台窗口固定的宽度。</summary>
    public double WindowSize
    {
        get => _service.WindowSize;
        set => _service.WindowSize = value;
    }

    /// <summary>获取或设置字体。设置为 <see langword="null"/> 时保持当前字体不变。</summary>
    public FontFamily? SelectedFontFamily
    {
        get => _selectedFontFamily;
        set
        {
            // 下拉框清空选择时不应把服务置为无效状态；保持当前字体并忽略 null。
            if (value is null)
            {
                return;
            }

            if (SetProperty(ref _selectedFontFamily, value)) _service.FontFamilyName = value.Source;
        }
    }

    /// <summary>开始或继续秒表。</summary>
    [RelayCommand]
    private void Start() => _service.Start();

    /// <summary>暂停秒表。</summary>
    [RelayCommand]
    private void Pause() => _service.Pause();

    /// <summary>重置秒表。</summary>
    [RelayCommand]
    private void Reset() => _service.Reset();

    /// <summary>打开秒表前台窗口。</summary>
    [RelayCommand]
    private void OpenWindow()
    {
        var window = _frontedWindowService.EnsureWindowCreated(WindowCanonicalId);
        if (window is null)
        {
            return;
        }

        _frontedWindowService.ShowWindow(WindowCanonicalId);
        window.Activate();
    }

    private void SyncSelectedFontFamily()
    {
        var match = FindFontFamily(_service.FontFamilyName);
        if (match is null || string.Equals(match.Source, SelectedFontFamily?.Source, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        SetProperty(ref _selectedFontFamily, match, nameof(SelectedFontFamily));
    }

    private FontFamily? FindFontFamily(string? familyName)
    {
        if (string.IsNullOrWhiteSpace(familyName))
        {
            return null;
        }

        return FontFamilies.FirstOrDefault(
            x => string.Equals(x.Source, familyName, StringComparison.OrdinalIgnoreCase));
    }

    private static Color ParseColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Colors.White;
        }

        try
        {
            return ColorConverter.ConvertFromString(value) is Color color ? color : Colors.White;
        }
        catch (Exception)
        {
            // 颜色配置可能来自旧版本或被手工修改；解析失败时回退到白色。
            return Colors.White;
        }
    }
}
