using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
    private CancellationTokenSource _tutorialLifetime = new();

    /// <summary>Gets the lifetime token for this child tutorial owner.</summary>
    internal CancellationToken TutorialLifetimeToken => _tutorialLifetime.Token;

    /// <summary>
    /// Initializes a new instance of the <see cref="FrontedLayoutPackagesView"/> class.
    /// </summary>
    public FrontedLayoutPackagesView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (_tutorialLifetime.IsCancellationRequested)
            {
                _tutorialLifetime.Dispose();
                _tutorialLifetime = new CancellationTokenSource();
            }
        };
        Unloaded += (_, _) => _tutorialLifetime.Cancel();
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
