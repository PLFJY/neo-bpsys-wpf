using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using neo_bpsys_wpf.Controls;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Services;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Controls;
using ISnackbarService = neo_bpsys_wpf.Core.Abstractions.Services.ISnackbarService;
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

        Loaded += async (s, e) =>
        {
            await Task.Delay(800);
            var asgService = App.Services.GetRequiredService<IASGService>();
            var settings = settingsHostService.Settings;
            if (!asgService.IsLoggedIn)
            {
                if (!string.IsNullOrWhiteSpace(settings.AsgEmail) && !string.IsNullOrWhiteSpace(settings.AsgPassword))
                {
                    var ok = await asgService.LoginAsync(settings.AsgEmail!, settings.AsgPassword!);
                    if (ok) return;
                }

                var emailTextBox = new Wpf.Ui.Controls.TextBox
                {
                    Width = 240,
                    PlaceholderText = "邮箱",
                    Text = settings.AsgEmail ?? string.Empty
                };
                var passwordTextBox = new Wpf.Ui.Controls.TextBox
                {
                    Width = 180,
                    Margin = new Thickness(10, 0, 0, 0),
                    PlaceholderText = "密码",
                    Text = settings.AsgPassword ?? string.Empty
                };
                var stackPanel = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
                stackPanel.Children.Add(emailTextBox);
                stackPanel.Children.Add(passwordTextBox);

                var messageBox = new MessageBox()
                {
                    Title = "登录提示",
                    Content = stackPanel,
                    PrimaryButtonText = "登录",
                    PrimaryButtonIcon = new SymbolIcon() { Symbol = SymbolRegular.ArrowImport24 },
                    CloseButtonIcon = new SymbolIcon() { Symbol = SymbolRegular.Dismiss24 },
                    CloseButtonText = "取消",
                    Owner = App.Current.MainWindow,
                };
                var result = await messageBox.ShowDialogAsync();
                if (result == MessageBoxResult.Primary)
                {
                    var ok = await asgService.LoginAsync(emailTextBox.Text, passwordTextBox.Text);
                    if (ok)
                    {
                        settings.AsgEmail = emailTextBox.Text;
                        settings.AsgPassword = passwordTextBox.Text;
                        settingsHostService.SaveConfig();
                    }
                    else
                    {
                        await App.Services.GetRequiredService<IMessageBoxService>()
                            .ShowErrorAsync("登录失败，请稍后在设置中重试", "提示");
                    }
                }
            }
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
