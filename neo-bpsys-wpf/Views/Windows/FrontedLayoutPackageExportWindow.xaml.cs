using neo_bpsys_wpf.Core.Models.FrontedLayout.Packages;
using neo_bpsys_wpf.ViewModels.Windows;
using System.Windows;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Views.Windows;

public partial class FrontedLayoutPackageExportWindow : FluentWindow
{
    public FrontedLayoutPackageExportWindow()
    {
        InitializeComponent();
    }

    public FrontedLayoutPackageExportRequest? ExportRequest { get; private set; }

    private FrontedLayoutPackageExportWindowViewModel? ViewModel =>
        DataContext as FrontedLayoutPackageExportWindowViewModel;

    private void Ok_OnClick(object sender, RoutedEventArgs e)
    {
        var request = ViewModel?.CreateRequest();
        if (request is null)
        {
            return;
        }

        ExportRequest = request;
        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
