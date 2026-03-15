using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Navigation;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Enums;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Views.Pages;

/// <summary>
/// PluginPage.xaml 的交互逻辑
/// </summary>
[BackendPageInfo("94ABE666-EB81-4244-BDA2-A5E6486FB091",
    "Plugins",
    SymbolRegular.AppsAddIn24,
    BackendPageCategory.External)]
public partial class PluginPage : Page
{
    public PluginPage()
    {
        InitializeComponent();
        PluginReadmeMarkdownViewer.AddHandler(
            Hyperlink.RequestNavigateEvent,
            new RequestNavigateEventHandler(PluginReadmeMarkdownViewer_OnRequestNavigate));
    }

    private static void PluginReadmeMarkdownViewer_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        if (e.Uri == null)
        {
            return;
        }

        e.Handled = true;
    }
}
