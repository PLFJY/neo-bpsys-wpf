using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.ViewModels.Pages;
using neo_bpsys_wpf.ViewModels.Windows;
using neo_bpsys_wpf.Views.Pages;
using neo_bpsys_wpf.Views.Windows;
using System.Windows;
using System.Windows.Threading;
using Wpf.Ui;
using Wpf.Ui.DependencyInjection;

namespace neo_bpsys_wpf
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static readonly IHost _host = Host.CreateDefaultBuilder()
            .ConfigureServices(
                services =>
                {
                    services.AddNavigationViewPageProvider();

                    //App Host
                    services.AddHostedService<ApplicationHostService>();

                    // Theme manipulation
                    services.AddSingleton<IThemeService, ThemeService>();

                    // TaskBar manipulation
                    services.AddSingleton<ITaskBarService, TaskBarService>();

                    // Service containing navigation, same as INavigationWindow... but without window
                    services.AddSingleton<INavigationService, NavigationService>();

                    //SharedDataService
                    services.AddSingleton<ISharedDataService, SharedDataService>();

                    // Main window with navigation
                    services.AddSingleton<INavigationWindow, MainWindow>(sp => new MainWindow(sp.GetRequiredService<INavigationService>())
                    {
                        DataContext = sp.GetRequiredService<MainWindowViewModel>(),
                    });
                    services.AddSingleton<MainWindowViewModel>();

                    //Views and ViewModels
                    //Window

                    //Page
                    services.AddSingleton<MapBpPage>();
                    services.AddSingleton<BanHunPage>(sp => new BanHunPage()
                    {
                        DataContext = sp.GetRequiredService<BanHunPageViewModel>()
                    });
                    services.AddSingleton<BanHunPageViewModel>();
                    services.AddSingleton<BanSurPage>(sp => new BanSurPage()
                    {
                        DataContext = sp.GetRequiredService<BanSurPageViewModel>()
                    });
                    services.AddSingleton<BanSurPageViewModel>();
                    services.AddSingleton<ExtensionPage>();
                    services.AddSingleton<GameDataPage>();
                    services.AddSingleton<HomePage>();
                    services.AddSingleton<MapBpPage>(sp => new MapBpPage()
                    {
                        DataContext = sp.GetRequiredService<MapBpPageViewModel>()
                    });
                    services.AddSingleton<MapBpPageViewModel>();
                    services.AddSingleton<PickPage>(sp => new PickPage()
                    {
                        DataContext = sp.GetRequiredService<PickPageViewModel>()
                    });
                    services.AddSingleton<PickPageViewModel>();
                    services.AddSingleton<ScorePage>();
                    services.AddSingleton<SettingPage>();
                    services.AddSingleton<TalentPage>();
                    services.AddSingleton<TeamInfoPage>(sp => new TeamInfoPage()
                    {
                        DataContext = sp.GetRequiredService<TeamInfoPageViewModel>()
                    });
                    services.AddSingleton<TeamInfoPageViewModel>();
                    services.AddSingleton<FrontManagePage>();

                }
            )
            .Build();


        /// <summary>
        /// Gets services.
        /// </summary>
        public static IServiceProvider Services
        {
            get { return _host.Services; }
        }

        /// <summary>
        /// Occurs when the application is loading.
        /// </summary>
        private async void OnStartup(object sender, StartupEventArgs e)
        {
            await _host.StartAsync();
        }

        /// <summary>
        /// Occurs when the application is closing.
        /// </summary>
        private async void OnExit(object sender, ExitEventArgs e)
        {
            await _host.StopAsync();

            _host.Dispose();
        }

        /// <summary>
        /// Occurs when an exception is thrown by an application but not handled.
        /// </summary>
        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // For more info see https://docs.microsoft.com/en-us/dotnet/api/system.windows.application.dispatcherunhandledexception?view=windowsdesktop-6.0
        }
    }
}
