using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.ViewModels.Windows;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Views.Windows;

public partial class FrontedBindingBrowserWindow : FluentWindow
{
    public FrontedBindingBrowserWindow()
    {
        InitializeComponent();
    }

    public string? SelectedBindingPath { get; private set; }

    private FrontedBindingBrowserWindowViewModel? ViewModel =>
        DataContext as FrontedBindingBrowserWindowViewModel;

    public void InitializeSelection(string? initialPath) =>
        ViewModel?.InitializeSelection(initialPath);

    private void BindingTree_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (ViewModel is not null && e.NewValue is FrontedBindingTreeNode node)
        {
            ViewModel.SelectedNode = node;
        }
    }

    private void BindingSearch_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is not null && e.AddedItems.Count > 0 && e.AddedItems[0] is FrontedBindingTreeNode node)
        {
            ViewModel.SelectedNode = node;
        }
    }

    private void UseSelected_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ViewModel?.SelectedPath))
        {
            return;
        }

        SelectedBindingPath = ViewModel.SelectedPath;
        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
