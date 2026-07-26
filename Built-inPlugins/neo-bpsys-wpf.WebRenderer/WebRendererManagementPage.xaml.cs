using System.Windows.Controls;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Enums;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.WebRenderer;

/// <summary>Web Renderer 的实验性后台管理页。</summary>
[BackendPageInfo("218C9320-8545-4429-A9F4-E2B87AFB864E", "Web 前台", SymbolRegular.Globe24, BackendPageCategory.External)]
public partial class WebRendererManagementPage : Page
{
    /// <summary>初始化管理页。</summary>
    public WebRendererManagementPage()
    {
        InitializeComponent();
    }
}
