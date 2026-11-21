using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;
using neo_bpsys_wpf.Models.Plugins;
using neo_bpsys_wpf.ViewModels.Pages;

namespace neo_bpsys_wpf.Views.Pages.Plugin;

/// <summary>具有有界虚拟化列表和加载反馈的市场目录。</summary>
public partial class PluginMarketBrowseView : UserControl
{
    /// <summary>创建市场目录。</summary>
    public PluginMarketBrowseView() => InitializeComponent();

    internal void FocusCatalog() => Catalog.Focus();

    private void OnCatalogMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source
            && ItemsControl.ContainerFromElement(Catalog, source) is ListBoxItem { DataContext: PluginMarketItem item })
            Open(item);
    }

    private void OnCatalogKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || Catalog.SelectedItem is not PluginMarketItem item) return;
        Open(item);
        e.Handled = true;
    }

    private void Open(PluginMarketItem item)
    {
        if (DataContext is PluginPageViewModel viewModel) viewModel.OpenMarketPluginDetailsCommand.Execute(item);
    }
}
