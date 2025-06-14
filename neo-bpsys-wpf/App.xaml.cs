using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.Theme;
using neo_bpsys_wpf.ViewModels.Pages;
using neo_bpsys_wpf.ViewModels.Windows;
using neo_bpsys_wpf.Views.Pages;
using neo_bpsys_wpf.Views.Windows;
using System.Windows;
using System.Windows.Threading;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui.DependencyInjection;

namespace neo_bpsys_wpf
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static readonly IHost _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddNavigationViewPageProvider();

                //App Host
                services.AddHostedService<ApplicationHostService>();

                // Theme manipulation
                services.AddSingleton<IThemeService, ThemeService>();

                // TaskBar manipulation
                services.AddSingleton<ITaskBarService, TaskBarService>();

                //UpdaterService
                services.AddSingleton<IUpdaterService, UpdaterService>();

                // Service containing navigation, same as INavigationWindow... but without window
                services.AddSingleton<INavigationService, NavigationService>();

                //_sharedDataService
                services.AddSingleton<ISharedDataService, SharedDataService>();

                // Main window with navigation
                services.AddSingleton<INavigationWindow, MainWindow>(sp => new MainWindow(
                    sp.GetRequiredService<INavigationService>(),
                    sp.GetRequiredService<IMessageBoxService>(),
                    sp.GetRequiredService<IInfoBarService>()
                )
                {
                    DataContext = sp.GetRequiredService<MainWindowViewModel>(),
                });
                services.AddSingleton<MainWindowViewModel>();

                //FrontService
                services.AddSingleton<IFrontService, FrontService>();

                //Tool Services
                services.AddSingleton<IFilePickerService, FilePickerService>();
                services.AddSingleton<IMessageBoxService, MessageBoxService>();
                services.AddSingleton<IInfoBarService, InfoBarService>();
                services.AddSingleton<IGameGuidanceService, GameGuidanceService>();

                //Views and ViewModels
                //Window
                services.AddSingleton<BpWindow>(sp => new BpWindow()
                {
                    DataContext = sp.GetRequiredService<BpWindowViewModel>(),
                });
                services.AddSingleton<BpWindowViewModel>();
                services.AddSingleton<InterludeWindow>(sp => new InterludeWindow()
                {
                    DataContext = sp.GetRequiredService<InterludeWindowViewModel>(),
                });
                services.AddSingleton<InterludeWindowViewModel>();
                services.AddSingleton<ScoreWindow>(sp => new ScoreWindow()
                {
                    DataContext = sp.GetRequiredService<ScoreWindowViewModel>(),
                });
                services.AddSingleton<ScoreWindowViewModel>();
                services.AddSingleton<GameDataWindow>(sp => new GameDataWindow()
                {
                    DataContext = sp.GetRequiredService<BpWindowViewModel>(),
                });
                services.AddSingleton<GameDataWindowViewModel>();
                services.AddSingleton<WidgetsWindow>(sp => new WidgetsWindow()
                {
                    DataContext = sp.GetRequiredService<WidgetsWindowViewModel>(),
                });
                services.AddSingleton<WidgetsWindowViewModel>();
                services.AddSingleton<ScoreManualWindow>(sp => new ScoreManualWindow()
                {
                    DataContext = sp.GetRequiredService<ScoreManualWindowViewModel>()
                });
                services.AddSingleton<ScoreManualWindowViewModel>();
                services.AddSingleton<MapBpWindow>(sp => new MapBpWindow()
                {
                    DataContext = sp.GetRequiredService<MapBpWindowViewModel>()
                });
                services.AddSingleton<MapBpWindowViewModel>();

                //Page
                services.AddSingleton<HomePage>();

                services.AddSingleton<TeamInfoPage>(sp => new TeamInfoPage()
                {
                    DataContext = sp.GetRequiredService<TeamInfoPageViewModel>(),
                });
                services.AddSingleton<TeamInfoPageViewModel>();

                services.AddSingleton<MapBpPage>(sp => new MapBpPage()
                {
                    DataContext = sp.GetRequiredService<MapBpPageViewModel>(),
                });
                services.AddSingleton<MapBpPageViewModel>();

                services.AddSingleton<BanHunPage>(sp => new BanHunPage()
                {
                    DataContext = sp.GetRequiredService<BanHunPageViewModel>(),
                });
                services.AddSingleton<BanHunPageViewModel>();

                services.AddSingleton<BanSurPage>(sp => new BanSurPage()
                {
                    DataContext = sp.GetRequiredService<BanSurPageViewModel>(),
                });
                services.AddSingleton<BanSurPageViewModel>();

                services.AddSingleton<PickPage>(sp => new PickPage()
                {
                    DataContext = sp.GetRequiredService<PickPageViewModel>(),
                });
                services.AddSingleton<PickPageViewModel>();

                services.AddSingleton<TalentPage>(sp => new TalentPage()
                {
                    DataContext = sp.GetRequiredService<TalentPageViewModel>(),
                });
                services.AddSingleton<TalentPageViewModel>();

                services.AddSingleton<ScorePage>(sp => new ScorePage()
                {
                    DataContext = sp.GetRequiredService<ScorePageViewModel>(),
                });
                services.AddSingleton<ScorePageViewModel>();

                services.AddSingleton<GameDataPage>(sp => new GameDataPage()
                {
                    DataContext = sp.GetRequiredService<GameDataPageViewModel>(),
                });
                services.AddSingleton<GameDataPageViewModel>();

                services.AddSingleton<FrontManagePage>(sp => new FrontManagePage()
                {
                    DataContext = sp.GetRequiredService<FrontManagePageViewModel>(),
                });
                services.AddSingleton<FrontManagePageViewModel>();

                services.AddSingleton<ExtensionPage>(sp => new ExtensionPage()
                {
                    DataContext = sp.GetRequiredService<ExtensionPageViewModel>(),
                });
                services.AddSingleton<ExtensionPageViewModel>();

                services.AddSingleton<SettingPage>(sp => new SettingPage()
                {
                    DataContext = sp.GetRequiredService<SettingPageViewModel>()
                });
                services.AddSingleton<SettingPageViewModel>();

            })
            .Build();

        /// <summary>
        /// Gets services.
        /// </summary>
        public static IServiceProvider Services => _host.Services;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            await _host.StartAsync();
            Application.Current.Resources["surIcon"] = ImageHelper.GetUiImageSource("surIcon");
            Application.Current.Resources["hunIcon"] = ImageHelper.GetUiImageSource("hunIcon");
            ApplicationThemeManager.Changed += (currentApplicationTheme, systemAccent) =>
            {
                foreach (ResourceDictionary dict in Application.Current.Resources.MergedDictionaries)
                {
                    if (dict is IconThemesDictionary iconThemesDictionary)
                    {
                        iconThemesDictionary.Theme = currentApplicationTheme;
                        break;
                    }
                }
            };
            ApplicationThemeManager.Apply(ApplicationTheme.Dark, WindowBackdropType.Mica, true);
#if !DEBUG
            await _host.Services.GetRequiredService<IUpdaterService>().UpdateCheck(true);
#endif
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
            await _host.StopAsync();

            _host.Dispose();
        }

        /// <summary>
        /// Occurs when an exception is thrown by an application but not handled.
        /// </summary>
        private void OnDispatcherUnhandledException(
            object sender,
            DispatcherUnhandledExceptionEventArgs e
        )
        {
            // For more info see https://docs.microsoft.com/en-us/dotnet/api/system.windows.application.dispatcherunhandledexception?view=windowsdesktop-6.0
        }
    }
}
