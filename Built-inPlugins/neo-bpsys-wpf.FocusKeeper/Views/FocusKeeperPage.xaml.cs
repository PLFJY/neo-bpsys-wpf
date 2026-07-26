using neo_bpsys_wpf.Core.Attributes;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.FocusKeeper.Views;

/// <summary>焦点保持后台管理页。</summary>
[BackendPageInfo("F7C1E2A4-3D5B-4A8E-9F01-2B6C7D8E9A0B", "焦点保持", SymbolRegular.EyeTracking24)]
public partial class FocusKeeperPage : Page
{
    /// <summary>初始化焦点保持页面。</summary>
    public FocusKeeperPage() => InitializeComponent();
}
