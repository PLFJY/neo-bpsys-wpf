using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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

        if (IsWithinOverlay(source))
        {
            return;
        }

        if (viewModel.ClosePluginMarketOverlaysCommand.CanExecute(null))
        {
            viewModel.ClosePluginMarketOverlaysCommand.Execute(null);
        }
    }

    /// <summary>
    /// 判断点击位置是否仍属于任一浮层的交互区域。
    /// 这里除了普通面板本身，还会把 ComboBox 的下拉项弹层算作浮层内部，
    /// 避免用户在切换插件源或 Ghproxy 时因为点中了弹出的列表项而被误判为“点击外部关闭”。
    /// </summary>
    private bool IsWithinOverlay(DependencyObject source)
    {
        return IsWithinPanel(source, DownloadQueuePanel)
               || IsWithinPanel(source, PluginDetailsPanel)
               || IsWithinPanel(source, PluginMarketSettingsPanel)
               || IsWithinOverlayPopup(source);
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

    /// <summary>
    /// 判断点击位置是否来自浮层内部控件弹出的 Popup。
    /// 主要用于识别插件市场设置面板中的 ComboBox 下拉项。
    /// </summary>
    private bool IsWithinOverlayPopup(DependencyObject source)
    {
        var current = source;
        while (current != null)
        {
            if (current is Popup popup
                && popup.PlacementTarget is DependencyObject placementTarget
                && IsWithinOverlayPlacementTarget(placementTarget))
            {
                return true;
            }

            if (current is ComboBoxItem comboBoxItem)
            {
                var owner = ItemsControl.ItemsControlFromItemContainer(comboBoxItem);
                if (owner != null && IsWithinOverlayPlacementTarget(owner))
                {
                    return true;
                }
            }

            if (current is FrameworkElement frameworkElement)
            {
                if (frameworkElement.TemplatedParent is DependencyObject templatedParent
                    && IsWithinOverlayPlacementTarget(templatedParent))
                {
                    return true;
                }

                current = frameworkElement.Parent
                          ?? frameworkElement.TemplatedParent
                          ?? LogicalTreeHelper.GetParent(frameworkElement);
                continue;
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

    /// <summary>
    /// 判断某个 Popup 的归属控件是否位于插件市场浮层内部。
    /// </summary>
    private bool IsWithinOverlayPlacementTarget(DependencyObject placementTarget)
    {
        return IsWithinPanel(placementTarget, DownloadQueuePanel)
               || IsWithinPanel(placementTarget, PluginDetailsPanel)
               || IsWithinPanel(placementTarget, PluginMarketSettingsPanel);
    }
}
