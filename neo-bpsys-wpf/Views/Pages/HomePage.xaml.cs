using System.Security.Cryptography.X509Certificates;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Core.Messages;
using neo_bpsys_wpf.ViewModels.Pages;
using Wpf.Ui;

namespace neo_bpsys_wpf.Views.Pages;

/// <summary>
/// HomePage.xaml 的交互逻辑
/// </summary>
public partial class HomePage : Page
{
    public SnackbarService SnbService { get; init; }
    public HomePage()
    {
        InitializeComponent();
    }

    private void SnbPre_OnLoaded(object sender, RoutedEventArgs e)
    {
        // ((HomePageViewModel)DataContext).
        SnbService.SetSnackbarPresenter(SnbPre);
        
        WeakReferenceMessenger.Default.Send(new SnackbarInitializedMessage(true));
        // ((HomePageViewModel)DataContext).LoadedCommand.Execute(null);
    }
}