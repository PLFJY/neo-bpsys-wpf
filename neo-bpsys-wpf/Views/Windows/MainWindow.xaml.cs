using neo_bpsys_wpf.Services;
using System.Windows;
using System.Windows.Input;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Controls;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using MessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;

namespace neo_bpsys_wpf.Views.Windows
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : FluentWindow, INavigationWindow
    {
        public MainWindow(
            INavigationService navigationService,
            IMessageBoxService messageBoxService,
            IInfoBarService infoBarService
        )
        {
            InitializeComponent();
            navigationService.SetNavigationControl(RootNavigation);
            infoBarService.SetInfoBarControl(InfoBar);
            this.Closing += MainWindow_Closing;
            TitleBar.MouseDown += TitleBar_MouseDown;
            WindowIcon.MouseDown += WindowIcon_MouseDown;
            MaximizeButton.Click += MaximizeButton_Click;
            MinimizeButton.Click += MinimizeButton_Click;
            ExitButton.Click += ExitButton_Click;
        }

        private async void MainWindow_Closing(
            object? sender,
            System.ComponentModel.CancelEventArgs e
        )
        {
            e.Cancel = true;
            await ConfirmToExitAsync();
        }

        private async void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            await ConfirmToExitAsync();
        }

        private static async Task ConfirmToExitAsync()
        {
            var messageBox = new MessageBox()
            {
                Title = "退出确认",
                Content = "是否退出",
                PrimaryButtonText = "退出",
                PrimaryButtonIcon = new SymbolIcon() { Symbol = SymbolRegular.ArrowExit20 },
                CloseButtonIcon = new SymbolIcon() { Symbol = SymbolRegular.Prohibited20 },
                CloseButtonText = "取消",
            };
            var result = await messageBox.ShowDialogAsync();

            if (result == MessageBoxResult.Primary)
            {
                App.Current.Shutdown();
            }
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState =
                this.WindowState == WindowState.Normal ? WindowState.Maximized : WindowState.Normal;
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void WindowIcon_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SystemCommands.ShowSystemMenu(this, this.PointToScreen(e.GetPosition(this)));
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && e.ChangedButton == MouseButton.Left)
                MaximizeButton_Click(sender, e);

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }

            if (e.ChangedButton == MouseButton.Right)
            {
                SystemCommands.ShowSystemMenu(this, this.PointToScreen(e.GetPosition(this)));
            }
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
