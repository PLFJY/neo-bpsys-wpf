using System.Windows.Media;
using neo_bpsys_wpf.Core.Abstractions;

namespace neo_bpsys_wpf.GameStopwatch.ViewModels;

/// <summary>比赛秒表窗口视图模型。</summary>
public sealed class GameStopwatchWindowViewModel : ViewModelBase
{
    private readonly IGameStopwatchService _service;

    /// <summary>初始化窗口视图模型。</summary>
    /// <param name="service">秒表服务。</param>
    public GameStopwatchWindowViewModel(IGameStopwatchService service)
    {
        _service = service;
        _service.PropertyChanged += ServicePropertyChanged;
    }

    /// <summary>获取显示文本。</summary>
    public string DisplayText => _service.DisplayText;

    /// <summary>获取当前字体族名称。</summary>
    public string FontFamilyName => _service.FontFamilyName;

    /// <summary>获取当前字号。</summary>
    public double FontSize => _service.FontSize;

    /// <summary>获取前台窗口固定的宽度。</summary>
    public double WindowSize => _service.WindowSize;

    /// <summary>获取当前文字画刷。</summary>
    public Brush TextBrush => CreateBrush(_service.TextColor);

    private void ServicePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        OnPropertyChanged(e.PropertyName switch
        {
            nameof(IGameStopwatchService.TextColor) => nameof(TextBrush),
            nameof(IGameStopwatchService.FontFamilyName) => nameof(FontFamilyName),
            nameof(IGameStopwatchService.FontSize) => nameof(FontSize),
            nameof(IGameStopwatchService.WindowSize) => nameof(WindowSize),
            _ => nameof(DisplayText)
        });
    }

    private static Brush CreateBrush(string value)
    {
        try
        {
            var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(value)!;
            brush.Freeze();
            return brush;
        }
        catch (FormatException)
        {
            return Brushes.White;
        }
    }
}
