using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.GameStopwatch.Views;

namespace neo_bpsys_wpf.GameStopwatch.ViewModels;

/// <summary>比赛秒表设置页视图模型。</summary>
public sealed partial class GameStopwatchSettingsPageViewModel : ViewModelBase
{
    private readonly IGameStopwatchService _service;
    private readonly IFrontedWindowService _frontedWindowService;

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
        SelectedFontFamily = FontFamilies.FirstOrDefault(x => string.Equals(x.Source, _service.FontFamilyName, StringComparison.OrdinalIgnoreCase))
            ?? FontFamilies.FirstOrDefault();
        _service.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IGameStopwatchService.TextColor)) OnPropertyChanged(nameof(TextColor));
            if (e.PropertyName == nameof(IGameStopwatchService.FontSize)) OnPropertyChanged(nameof(FontSize));
            if (e.PropertyName == nameof(IGameStopwatchService.WindowSize)) OnPropertyChanged(nameof(WindowSize));
        };
    }

    /// <summary>获取可选字体。</summary>
    public ObservableCollection<FontFamily> FontFamilies { get; }

    /// <summary>获取或设置文字颜色。</summary>
    public Color TextColor
    {
        get => ParseColor(_service.TextColor);
        set => _service.TextColor = value.ToString();
    }

    /// <summary>获取或设置字号。</summary>
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

    /// <summary>获取或设置字体。</summary>
    public FontFamily? SelectedFontFamily
    {
        get => field;
        set
        {
            if (SetProperty(ref field, value) && value is not null) _service.FontFamilyName = value.Source;
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
        var window = _frontedWindowService.EnsureWindowCreated(GameStopwatchWindowContributor.WindowId);
        if (window is null)
        {
            return;
        }

        _frontedWindowService.ShowWindow(GameStopwatchWindowContributor.WindowId);
        window.Activate();
    }

    private static Color ParseColor(string value)
    {
        try { return (Color)ColorConverter.ConvertFromString(value)!; }
        catch (FormatException) { return Colors.White; }
    }
}
