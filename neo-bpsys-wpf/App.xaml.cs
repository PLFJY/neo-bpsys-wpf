﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Abstractions.Services;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.Themes;
using neo_bpsys_wpf.ViewModels.Pages;
using neo_bpsys_wpf.ViewModels.Windows;
using neo_bpsys_wpf.Views.Pages;
using neo_bpsys_wpf.Views.Windows;
using Serilog;
using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui.DependencyInjection;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace neo_bpsys_wpf
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static readonly IHost _host = Host.CreateDefaultBuilder()
            .UseSerilog((hostingContext, loggerConfiguration) =>
            {
                var loggingDir = Path.Combine(Environment.GetFolderPath
                        (Environment.SpecialFolder.ApplicationData),
                    "neo-bpsys-wpf",
                    "Log");
                if (!Directory.Exists(loggingDir))
                    Directory.CreateDirectory(loggingDir);

                loggerConfiguration
                    .WriteTo.Console()
                    .WriteTo.File(
                        path: Path.Combine(loggingDir, "log-.txt"), // 使用日期滚动的文件名格式
                        rollingInterval: RollingInterval.Day, // 每天创建一个新文件
                        retainedFileCountLimit: 3, // 只保留最近3天的日志文件
                        outputTemplate:
                        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
                    )
                    .Enrich.FromLogContext()
                    .MinimumLevel.Debug();
            })
            .ConfigureLogging(loggingBuilder =>
            {
                loggingBuilder.ClearProviders();
                loggingBuilder.AddSerilog(dispose: true);
            })
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

                // MainTeam window with navigation
                services.AddSingleton<INavigationWindow, MainWindow>(sp => new MainWindow(
                    sp.GetRequiredService<INavigationService>(),
                    sp.GetRequiredService<IMessageBoxService>(),
                    sp.GetRequiredService<IInfoBarService>(),
                    sp.GetRequiredService<ILogger<MainWindow>>()
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

                //Additional Feature Services
                services.AddSingleton<IGameGuidanceService, GameGuidanceService>();
                services.AddSingleton<ISettingsHostService, SettingsHostService>();
                services.AddSingleton<ITextSettingsNavigationService, TextSettingsNavigationService>();

                //Views and ViewModels
                //Window
                services.AddSingleton<BpWindow>(sp => new BpWindow()
                {
                    DataContext = sp.GetRequiredService<BpWindowViewModel>(),
                });
                services.AddSingleton<BpWindowViewModel>();
                services.AddSingleton<CutSceneWindow>(sp => new CutSceneWindow()
                {
                    DataContext = sp.GetRequiredService<CutSceneWindowViewModel>(),
                });
                services.AddSingleton<CutSceneWindowViewModel>();
                services.AddSingleton<ScoreWindow>(sp => new ScoreWindow()
                {
                    DataContext = sp.GetRequiredService<ScoreWindowViewModel>(),
                });
                services.AddSingleton<ScoreWindowViewModel>();
                services.AddSingleton<GameDataWindow>(sp => new GameDataWindow()
                {
                    DataContext = sp.GetRequiredService<GameDataWindowViewModel>(),
                });
                services.AddSingleton<GameDataWindowViewModel>();
                services.AddSingleton<WidgetsWindow>(sp => new WidgetsWindow()
                {
                    DataContext = sp.GetRequiredService<WidgetsWindowViewModel>(),
                });
                services.AddSingleton<WidgetsWindowViewModel>();
                services.AddTransient<ScoreManualWindow>(sp => new ScoreManualWindow()
                {
                    DataContext = sp.GetRequiredService<ScoreManualWindowViewModel>(),
                    Owner = App.Current.MainWindow
                });
                services.AddSingleton<ScoreManualWindowViewModel>();

                //Page
                services.AddTransient<HomePage>();

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

                services.AddSingleton<SettingPage>(sp =>
                    new SettingPage(sp.GetRequiredService<ITextSettingsNavigationService>())
                    {
                        DataContext = sp.GetRequiredService<SettingPageViewModel>()
                    });
                services.AddSingleton<SettingPageViewModel>();
            })
            .Build();

        private ILogger<App> _logger;

        /// <summary>
        /// Gets services.
        /// </summary>
        public static IServiceProvider Services => _host.Services;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            await _host.StartAsync();
            _logger = _host.Services.GetRequiredService<ILogger<App>>();
            _logger.LogInformation("Application Started");
            _logger.LogInformation("""
                                   
                                   ==============================================================================
                                                           _                                                __ 
                                                          | |                                              / _|
                                    _ __   ___  ___ ______| |__  _ __  ___ _   _ ___ ________      ___ __ | |_ 
                                   | '_ \ / _ \/ _ \______| '_ \| '_ \/ __| | | / __|______\ \ /\ / / '_ \|  _|
                                   | | | |  __/ (_) |     | |_) | |_) \__ \ |_| \__ \       \ V  V /| |_) | |  
                                   |_| |_|\___|\___/      |_.__/| .__/|___/\__, |___/        \_/\_/ | .__/|_|  
                                                                | |         __/ |                   | |        
                                                                |_|        |___/                    |_|        
                                                                                ______ _     ______ _____   __ 
                                                                                | ___ \ |    |  ___|_  \ \ / / 
                                                                   ______ ______| |_/ / |    | |_    | |\ V /  
                                                                  |______|______|  __/| |    |  _|   | | \ /   
                                                                                | |   | |____| | /\__/ / | |   
                                                                                \_|   \_____/\_| \____/  \_/   
                                   ==============================================================================
                                   """);
            Application.Current.Resources["surIcon"] = ImageHelper.GetUiImageSource("surIcon");
            Application.Current.Resources["hunIcon"] = ImageHelper.GetUiImageSource("hunIcon");
            ApplicationThemeManager.Changed += (currentApplicationTheme, systemAccent) =>
            {
                foreach (var dict in Application.Current.Resources.MergedDictionaries)
                {
                    if (dict is not IconThemesDictionary iconThemesDictionary) continue;
                    iconThemesDictionary.Theme = currentApplicationTheme;
                    break;
                }
            };
            ApplicationThemeManager.Apply(ApplicationTheme.Dark, WindowBackdropType.Mica, true);
#if !DEBUG
            _logger.LogInformation("Update checking on start up");
            await _host.Services.GetRequiredService<IUpdaterService>().UpdateCheck(true);
#endif
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
            _logger.LogInformation("Application Closed");
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
            _logger.LogError("Application crashed unexpectedly");
            // For more info see https://docs.microsoft.com/en-us/dotnet/api/system.windows.application.dispatcherunhandledexception?view=windowsdesktop-6.0
        }
    }
}