using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.ViewModels.Windows;
using neo_bpsys_wpf.Views.Pages;
using neo_bpsys_wpf.Views.Windows;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
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
                    services.AddSingleton<INavigationWindow, MainWindow>();
                    services.AddSingleton<MainWindowViewModel>();

                    //Views and ViewModels
                    //Window

                    //Page
                    services.AddSingleton<HomePage>();
                    services.AddSingleton<MapBpPage>();
                    services.AddSingleton<BanHunPage>();
                    services.AddSingleton<BanSurPage>();
                    services.AddSingleton<ExtensionPage>();
                    services.AddSingleton<GameDataPage>();
                    services.AddSingleton<MapBpPage>();
                    services.AddSingleton<PickPage>();
                    services.AddSingleton<ScorePage>();
                    services.AddSingleton<SettingPage>();
                    services.AddSingleton<TalentPage>();
                    services.AddSingleton<TeamInfoPage>();
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
