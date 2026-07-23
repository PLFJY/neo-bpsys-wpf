using neo_bpsys_wpf.Core.Attributes;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.GameStopwatch.Views;

/// <summary>比赛秒表设置页。</summary>
[BackendPageInfo("B4A6C6B0-5D54-4F43-9E6F-1F5D4BDA7F38", "比赛秒表", SymbolRegular.Timer24)]
public partial class GameStopwatchSettingsPage : Page
{
    /// <summary>初始化设置页。</summary>
    public GameStopwatchSettingsPage() => InitializeComponent();
}
