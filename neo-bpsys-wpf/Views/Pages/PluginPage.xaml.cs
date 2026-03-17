using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.ViewModels.Pages;
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

    private void RootGrid_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not PluginPageViewModel viewModel || !viewModel.HasPluginMarketOverlay)
        {
            return;
        }

        if (e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        if (IsWithinPanel(source, DownloadQueuePanel)
            || IsWithinPanel(source, PluginDetailsPanel)
            || IsWithinPanel(source, PluginMarketSettingsPanel))
        {
            return;
        }

        if (viewModel.ClosePluginMarketOverlaysCommand.CanExecute(null))
        {
            viewModel.ClosePluginMarketOverlaysCommand.Execute(null);
        }
    }

    /// <summary>
    /// 判断点击位置是否位于指定浮层内部。
    /// </summary>
    private static bool IsWithinPanel(DependencyObject source, FrameworkElement? panel)
    {
        if (panel == null || panel.Visibility != Visibility.Visible)
        {
            return false;
        }

        var current = source;
        while (current != null)
        {
            if (ReferenceEquals(current, panel))
            {
                return true;
            }

            current = current switch
            {
                Visual visual => VisualTreeHelper.GetParent(visual),
                Visual3D visual3D => VisualTreeHelper.GetParent(visual3D),
                _ => LogicalTreeHelper.GetParent(current)
            };
        }

        return false;
    }
}
