using System.Windows.Media;
using neo_bpsys_wpf.Core.Abstractions;

namespace neo_bpsys_wpf.GameStopwatch.ViewModels;

/// <summary>比赛秒表窗口视图模型。</summary>
public sealed class GameStopwatchWindowViewModel : ViewModelBase
{
    private readonly IGameStopwatchService _service;
    private Brush _textBrush;

    /// <summary>初始化窗口视图模型。</summary>
    /// <param name="service">秒表服务。</param>
    public GameStopwatchWindowViewModel(IGameStopwatchService service)
    {
        _service = service;
        _textBrush = CreateBrush(_service.TextColor);
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
    public Brush TextBrush => _textBrush;

    private void ServicePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IGameStopwatchService.TextColor))
        {
            _textBrush = CreateBrush(_service.TextColor);
        }

        OnPropertyChanged(e.PropertyName switch
        {
            nameof(IGameStopwatchService.TextColor) => nameof(TextBrush),
            nameof(IGameStopwatchService.FontFamilyName) => nameof(FontFamilyName),
            nameof(IGameStopwatchService.FontSize) => nameof(FontSize),
            nameof(IGameStopwatchService.WindowSize) => nameof(WindowSize),
            _ => nameof(DisplayText)
        });
    }

    private static Brush CreateBrush(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Brushes.White;
        }

        try
        {
            if (ColorConverter.ConvertFromString(value) is not Color color)
            {
                return Brushes.White;
            }

            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
        catch (Exception)
        {
            // 配置中的颜色可能来自旧版本或被手工修改；解析失败时回退到白色而不是中断渲染。
            return Brushes.White;
        }
    }
}
