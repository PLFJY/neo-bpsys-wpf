using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Controls;

namespace neo_bpsys_wpf.GameStopwatch.Views;

/// <summary>比赛秒表前台窗口。</summary>
[FrontedWindowInfo("A6B4CB0B-354B-4B66-8AB8-2E94C3F0E5D2", "比赛秒表")]
public partial class GameStopwatchWindow : FrontedWindowBase
{
    /// <summary>初始化窗口。</summary>
    public GameStopwatchWindow() => InitializeComponent();
}
