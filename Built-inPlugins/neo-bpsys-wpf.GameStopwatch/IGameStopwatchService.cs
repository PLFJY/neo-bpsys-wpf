using System.ComponentModel;

namespace neo_bpsys_wpf.GameStopwatch;

/// <summary>
/// 提供比赛秒表的状态、控制和显示设置。
/// </summary>
public interface IGameStopwatchService : INotifyPropertyChanged
{
    /// <summary>获取当前显示的秒表文本。</summary>
    string DisplayText { get; }

    /// <summary>获取秒表是否正在运行。</summary>
    bool IsRunning { get; }

    /// <summary>获取或设置秒表文字颜色的 ARGB 十六进制值。</summary>
    string TextColor { get; set; }

    /// <summary>获取或设置秒表使用的字体族名称。</summary>
    string FontFamilyName { get; set; }

    /// <summary>获取或设置秒表字号。</summary>
    double FontSize { get; set; }

    /// <summary>获取或设置前台窗口固定的宽度。</summary>
    double WindowSize { get; set; }

    /// <summary>开始或继续计时。</summary>
    void Start();

    /// <summary>暂停计时。</summary>
    void Pause();

    /// <summary>将秒表归零并暂停。</summary>
    void Reset();
}
