using System.Windows.Controls;
using neo_bpsys_wpf.Core.Attributes;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.ViewModels.Pages;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Views.Pages;

/// <summary>
/// FrontManagePage.xaml 的交互逻辑
/// </summary>
[BackendPageInfo("0532C747-A9E8-44E7-A8FB-7416DF8FC4C6",
    "FrontendManagement",
    SymbolRegular.ShareScreenStart24,
    BackendPageCategory.External)]
public partial class FrontManagePage : Page
{
    public FrontManagePage()
    {
        InitializeComponent();
    }

    private void PackageListBox_OnMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is FrontManagePageViewModel viewModel
            && viewModel.ActivateSelectedPackageByDoubleClickCommand.CanExecute(null))
        {
            viewModel.ActivateSelectedPackageByDoubleClickCommand.Execute(null);
        }
    }
}
