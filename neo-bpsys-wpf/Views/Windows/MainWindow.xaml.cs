using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Controls;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.Tutorial;
using neo_bpsys_wpf.Views.Pages;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Controls;
using neo_bpsys_wpf.Helpers;
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
    private readonly IOnboardingCoordinator _onboardingCoordinator;
    private readonly ITutorialRunner _tutorialRunner;
    private bool _firstRunWelcomeAttempted;

    internal bool ForceCloseForTest { get; set; }

    public MainWindow(
        INavigationService navigationService,
        IInfoBarService infoBarService,
        ISnackbarService snackbarService,
        ISettingsHostService settingsHostService,
        IOnboardingCoordinator onboardingCoordinator,
        ITutorialRunner tutorialRunner,
        ILogger<MainWindow> logger
    )
    {
        _logger = logger;
        _onboardingCoordinator = onboardingCoordinator;
        _tutorialRunner = tutorialRunner;
        InitializeComponent();
        navigationService.SetNavigationControl(RootNavigation);
        if (navigationService is neo_bpsys_wpf.Services.NavigationService neoNavigationService)
        {
            neoNavigationService.PageChanged += OnNavigationPageChanged;
        }

        infoBarService.SetInfoBarControl(InfoBar);
        snackbarService.SetSnackbarPresenter(SnbPre);
        if (settingsHostService.Settings.ShowAfterUpdateTip)
            Loaded += async (s, e) =>
            {
                await Task.Delay(5500);
                snackbarService.Show(I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "Notification"),
                    new HyperLinkSnackbarContent(
                        I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "AfterUpdateTip"),
                        I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "DontRemindMeAgainUntilTheNextUpdate"),
                        () =>
                        {
                            settingsHostService.Settings.ShowAfterUpdateTip = false;
                            settingsHostService.SaveConfigAsync();
                            snackbarService.Hide();
                        }
                    ),
                    ControlAppearance.Secondary,
                    new SymbolIcon(SymbolRegular.Info24, 24D)
                    {
                        Margin = new Thickness(0, 0, 5, 0)
                    }, TimeSpan.FromSeconds(10), true
                );
            };
        if (Resources["StartupLoading"] is Storyboard startupLoading)
        {
            startupLoading.Completed += async (_, _) => await TryShowFirstRunWelcomeAsync();
        }
    }

    private async Task TryShowFirstRunWelcomeAsync()
    {
        if (_firstRunWelcomeAttempted)
        {
            return;
        }

        _firstRunWelcomeAttempted = true;
        await _onboardingCoordinator.ShowFirstRunWelcomeAsync(this);
    }

#if !Release
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.F12)
        {
            try
            {
                var service = IAppHost.Host!.Services.GetRequiredService<ISharedDataService>();
                var win = new DebugSharedDataWindow(service);
                win.Show();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open DebugSharedDataWindow.");
                System.Windows.MessageBox.Show(ex.ToString());
            }
        }
    }
#endif

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (ForceCloseForTest)
        {
            return;
        }

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
            Title = I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Warning"),
            Content = I18nHelper.GetLocalizedString(AppI18nDictionaries.Shell, "AreYouSureYouWantToExit"),
            PrimaryButtonText = I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Confirm"),
            PrimaryButtonIcon = new SymbolIcon() { Symbol = SymbolRegular.ArrowExit20 },
            CloseButtonIcon = new SymbolIcon() { Symbol = SymbolRegular.Prohibited20 },
            CloseButtonText = I18nHelper.GetLocalizedString(AppI18nDictionaries.Common, "Cancel"),
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
        return RootNavigation.Navigate(pageType);
    }

    public void SetPageService(INavigationViewPageProvider navigationViewPageProvider) =>
        RootNavigation.SetPageProviderService(navigationViewPageProvider);

    INavigationView INavigationWindow.GetNavigation() => RootNavigation;

    public void SetServiceProvider(IServiceProvider serviceProvider)
    {
        RootNavigation.SetServiceProvider(serviceProvider);
    }

    private void OnNavigationPageChanged(object? sender, NavigationPageChangedEventArgs e)
    {
        if (e.PageType == typeof(FrontManagePage)
            && e.PageContent is FrontManagePage frontManagePage)
        {
            ScheduleNavigationPageTutorial(
                frontManagePage,
                TutorialPageKeys.FrontManage,
                "NavigationPageChanged");
            return;
        }

        if (e.PageType == typeof(SmartBpPage)
            && e.PageContent is SmartBpPage smartBpPage)
        {
            ScheduleNavigationPageTutorial(
                smartBpPage,
                TutorialPageKeys.SmartBp,
                "NavigationPageChanged");
            return;
        }

        if (e.PageType == typeof(TeamInfoPage)
            && e.PageContent is TeamInfoPage teamInfoPage)
        {
            ScheduleNavigationPageTutorial(
                teamInfoPage,
                TutorialPageKeys.TeamInfo,
                "NavigationPageChanged");
            return;
        }

        if (e.PageType == typeof(ScorePage)
            && e.PageContent is ScorePage scorePage)
        {
            ScheduleNavigationPageTutorial(
                scorePage,
                TutorialPageKeys.Score,
                "NavigationPageChanged");
            return;
        }

        if (e.PageType == typeof(PickPage)
            && e.PageContent is PickPage pickPage)
        {
            ScheduleNavigationPageTutorial(
                pickPage,
                PickPage.TutorialPageKey,
                "NavigationPageChanged");
            return;
        }

        if (e.PageType == typeof(BanSurPage)
            && e.PageContent is BanSurPage banSurPage)
        {
            ScheduleNavigationPageTutorial(
                banSurPage,
                BanSurPage.TutorialPageKey,
                "NavigationPageChanged");
            return;
        }

        if (e.PageType == typeof(BanHunPage)
            && e.PageContent is BanHunPage banHunPage)
        {
            ScheduleNavigationPageTutorial(
                banHunPage,
                BanHunPage.TutorialPageKey,
                "NavigationPageChanged");
        }
    }

    private void ScheduleNavigationPageTutorial(FrameworkElement owner, string pageKey, string reason)
    {
        _ = reason;
        owner.Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(async () => await _tutorialRunner.RunSequenceAsync(owner, pageKey, TutorialOwnerLifetime.GetToken(owner))));
    }
}
