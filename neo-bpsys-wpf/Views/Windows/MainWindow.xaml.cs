using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.ViewModels.Windows;
using System.Windows;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Views.Windows
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : FluentWindow, INavigationWindow
    {
        public MainWindow(INavigationService navigationService)
        {
            InitializeComponent();
            navigationService.SetNavigationControl(RootNavigation);
        }

        public INavigationView GetNavigation() => RootNavigation;

        public void CloseWindow() => Close();

        public void ShowWindow() => Show();

        public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);

        public void SetPageService(INavigationViewPageProvider navigationViewPageProvider) =>
            RootNavigation.SetPageProviderService(navigationViewPageProvider);

        INavigationView INavigationWindow.GetNavigation()
        {
            throw new NotImplementedException();
        }

        public void SetServiceProvider(IServiceProvider serviceProvider)
        {
            throw new NotImplementedException();
        }
    }
}