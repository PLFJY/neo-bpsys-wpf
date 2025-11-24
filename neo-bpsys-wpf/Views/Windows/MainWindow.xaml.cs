using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Controls;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Services;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Controls;
using ISnackbarService = neo_bpsys_wpf.Core.Abstractions.Services.ISnackbarService;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using MessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;
using Lang = neo_bpsys_wpf.Assets.Locales.Lang;

namespace neo_bpsys_wpf.Views.Windows;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : FluentWindow, INavigationWindow
{
    private readonly ILogger<MainWindow> _logger;

    public MainWindow(
        INavigationService navigationService,
        IInfoBarService infoBarService,
        ISnackbarService snackbarService,
        ISettingsHostService settingsHostService,
        ILogger<MainWindow> logger
    )
    {
        _logger = logger;
        InitializeComponent();
        navigationService.SetNavigationControl(RootNavigation);
        infoBarService.SetInfoBarControl(InfoBar);
        snackbarService.SetSnackbarPresenter(SnbPre);
        if (settingsHostService.Settings.ShowTip)
            Loaded += async (s, e) =>
            {
                await Task.Delay(5500);
                snackbarService.Show("提示",
                    new HyperLinkSnackbarContent(
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "bpui"),
                        "软件安装目录下有无作者名字版本的前台UI awa",
                        () =>
                        {
                            settingsHostService.Settings.ShowTip = false;
                            settingsHostService.SaveConfig();
                            snackbarService.Hide();
                        }
                    ),
                    ControlAppearance.Secondary,
                    new SymbolIcon(SymbolRegular.Info24, 24D)
                    {
                        Margin = new Thickness(0, 0, 5, 0)
                    }, TimeSpan.FromSeconds(5), true
                );
            };
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
            Title = Lang.Warning,
            Content = Lang.AreYouSureYouWantToExit,
            PrimaryButtonText = Lang.Confirm,
            PrimaryButtonIcon = new SymbolIcon() { Symbol = SymbolRegular.ArrowExit20 },
            CloseButtonIcon = new SymbolIcon() { Symbol = SymbolRegular.Prohibited20 },
            CloseButtonText = Lang.Cancel,
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
            WindowState == WindowState.Normal ? WindowState.Maximized : WindowState.Normal;
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