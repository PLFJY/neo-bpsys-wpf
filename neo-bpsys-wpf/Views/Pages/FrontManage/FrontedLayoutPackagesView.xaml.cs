using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using neo_bpsys_wpf.ViewModels.Pages;

namespace neo_bpsys_wpf.Views.Pages.FrontManage;

/// <summary>
/// FrontedLayoutPackagesView.xaml 的交互逻辑
/// </summary>
public partial class FrontedLayoutPackagesView : UserControl
{
    public FrontedLayoutPackagesView()
    {
        InitializeComponent();
    }

    private void PackageListBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is FrontManagePageViewModel viewModel
            && viewModel.ActivateSelectedPackageByDoubleClickCommand.CanExecute(null))
        {
            viewModel.ActivateSelectedPackageByDoubleClickCommand.Execute(null);
        }
    }

    private void PackageListBox_OnRequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
    {
        if (sender == PackageListBox && e.OriginalSource is ListBoxItem)
        {
            e.Handled = true;
        }
    }
}
