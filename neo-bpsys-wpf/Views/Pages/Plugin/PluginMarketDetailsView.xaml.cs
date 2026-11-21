using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Navigation;
using neo_bpsys_wpf.Models.Plugins;

namespace neo_bpsys_wpf.Views.Pages.Plugin;

/// <summary>呈现插件说明、安装状态和下载入口，并在退场期间保留当前内容。</summary>
public partial class PluginMarketDetailsView : UserControl
{
    /// <summary>创建插件详情视图。</summary>
    public PluginMarketDetailsView()
    {
        InitializeComponent();
        Readme.AddHandler(Hyperlink.RequestNavigateEvent, new RequestNavigateEventHandler(OnRequestNavigate));
    }

    internal void Present(PluginMarketItem? plugin) => PluginContent.DataContext = plugin;

    private static void OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        e.Handled = true;
        if (e.Uri is not { IsAbsoluteUri: true } uri || uri.Scheme is not ("https" or "http")) return;
        try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        { Trace.WriteLine($"Cannot open plugin documentation link: {ex.Message}"); }
    }
}
