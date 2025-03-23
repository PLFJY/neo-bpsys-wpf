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
        public MainWindow(MainWindowViewModel viewModel, INavigationService navigationService)
        {
            DataContext = viewModel;
            InitializeComponent();
            navigationService.SetNavigationControl(RootNavigation);
        }

        public INavigationView GetNavigation() => RootNavigation;

        public void CloseWindow() => Close();

        public void ShowWindow() => Show();

        public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);

        public void SetPageService(INavigationViewPageProvider navigationViewPageProvider) =>
            RootNavigation.SetPageProviderService(navigationViewPageProvider);

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // Make sure that closing this window will begin the process of closing the application.
            Application.Current.Shutdown();
        }


        INavigationView INavigationWindow.GetNavigation()
        {
            throw new NotImplementedException();
        }

        public void SetServiceProvider(IServiceProvider serviceProvider)
        {
            throw new NotImplementedException();
        }

        private void ThemeChange_Click(object sender, RoutedEventArgs e)
        {
            ApplicationThemeManager.Apply(
                ApplicationThemeManager.GetAppTheme() == ApplicationTheme.Light ?
                ApplicationTheme.Dark :
                ApplicationTheme.Light);
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void ThemeSwitch_Click(object sender, RoutedEventArgs e)
        {
            ApplicationThemeManager.Apply(
                ApplicationThemeManager.GetAppTheme() == ApplicationTheme.Light ?
                ApplicationTheme.Dark :
                ApplicationTheme.Light);
        }


        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Normal ? WindowState.Maximized : WindowState.Normal;
        }

        private async void Close_Click(object sender, RoutedEventArgs e)
        {
            var messageBox = new Wpf.Ui.Controls.MessageBox()
            {
                Title = "退出确认",
                Content = "是否退出程序",
                PrimaryButtonText = "退出",
                PrimaryButtonIcon = new SymbolIcon() { Symbol = SymbolRegular.ArrowExit20 },
                CloseButtonIcon = new SymbolIcon() { Symbol = SymbolRegular.Prohibited20 },
                CloseButtonText = "取消",
            };
            var result = await messageBox.ShowDialogAsync();

            if(result == Wpf.Ui.Controls.MessageBoxResult.Primary) Application.Current.Shutdown();
        }

        private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            DragMove();
        }
    }
}