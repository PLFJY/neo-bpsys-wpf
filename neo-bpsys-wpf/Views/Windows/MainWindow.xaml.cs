using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using neo_bpsys_wpf.Core.Abstractions.Services;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Controls;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using MessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;

namespace neo_bpsys_wpf.Views.Windows;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : FluentWindow, INavigationWindow
{
    private readonly ILogger<MainWindow> _logger;

    public MainWindow(
        INavigationService navigationService,
        IMessageBoxService messageBoxService,
        IInfoBarService infoBarService,
        ILogger<MainWindow> logger
    )
    {
        _logger = logger;
        InitializeComponent();
        navigationService.SetNavigationControl(RootNavigation);
        infoBarService.SetInfoBarControl(InfoBar);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        e.Cancel = true;
        _ = ConfirmToExitAsync();
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        _ = ConfirmToExitAsync();
    }

    private async Task ConfirmToExitAsync()
    {
        var messageBox = new MessageBox()
        {
            Title = "退出确认",
            Content = "是否退出",
            PrimaryButtonText = "退出",
            PrimaryButtonIcon = new SymbolIcon() { Symbol = SymbolRegular.ArrowExit20 },
            CloseButtonIcon = new SymbolIcon() { Symbol = SymbolRegular.Prohibited20 },
            CloseButtonText = "取消",
            Owner = App.Current.MainWindow,
        };
        var result = await messageBox.ShowDialogAsync();

        if (result == MessageBoxResult.Primary)
        {
            _logger.LogInformation("Application Closing");
            Application.Current.Shutdown();
        }
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState =
            WindowState == WindowState.Normal ?
                WindowState.Maximized : WindowState.Normal;
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void WindowIcon_MouseDown(object sender, MouseButtonEventArgs e)
    {
        SystemCommands.ShowSystemMenu(this, PointToScreen(e.GetPosition(this)));
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Right)
        {
            SystemCommands.ShowSystemMenu(this, PointToScreen(e.GetPosition(this)));
        }

        if (e.ClickCount == 2 && e.ChangedButton == MouseButton.Left)
        {
            MaximizeButton_Click(sender, e);
            return;
        }

        if (e.ChangedButton == MouseButton.Left && e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    public INavigationView GetNavigation() => RootNavigation;

    public void CloseWindow() => Close();

    public void ShowWindow() => Show();

    public bool Navigate(Type pageType)
    {
        _logger.LogInformation("Navigate to {PageType}", pageType);
        return RootNavigation.Navigate(pageType);
    }

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