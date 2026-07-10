using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;
using neo_bpsys_wpf.ViewModels.Pages;
using neo_bpsys_wpf.Views.Pages;

namespace neo_bpsys_wpf.Views.Pages.FrontManage;

/// <summary>
/// FrontedLayoutPackagesView.xaml 的交互逻辑
/// </summary>
public partial class FrontedLayoutPackagesView : UserControl
{
    /// <summary>Layout packages view tutorial key.</summary>
    public const string TutorialPageKey = "Page.FrontManage.LayoutPackages";

    private CancellationTokenSource _tutorialLifetime = new();
    private Task<TutorialRunResult>? _tutorialTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="FrontedLayoutPackagesView"/> class.
    /// </summary>
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
        if (sender == LayoutPackageList && e.OriginalSource is ListBoxItem)
        {
            e.Handled = true;
        }
    }
}
