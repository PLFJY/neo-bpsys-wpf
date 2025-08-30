using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.Themes;
using neo_bpsys_wpf.ViewModels.Pages;
using neo_bpsys_wpf.ViewModels.Windows;
using neo_bpsys_wpf.Views.Pages;
using neo_bpsys_wpf.Views.Windows;
using Serilog;
using System.IO;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Helpers;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.DependencyInjection;

namespace neo_bpsys_wpf;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static readonly IHost _host = Host.CreateDefaultBuilder()
        .UseSerilog((_, loggerConfiguration) =>
        {
            if (!Directory.Exists(AppConstants.LogPath))
                Directory.CreateDirectory(AppConstants.LogPath);

            loggerConfiguration
                .WriteTo.Console()
                .WriteTo.File(
                    path: Path.Combine(AppConstants.LogPath, "log-.txt"), // 使用日期滚动的文件名格式
                    rollingInterval: RollingInterval.Day, // 每天创建一个新文件
                    retainedFileCountLimit: 3, // 只保留最近3天的日志文件
                    outputTemplate:
                    "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                    encoding: Encoding.UTF8
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
            services.AddSingleton<ScoreGlobalWindow>(sp => new ScoreGlobalWindow()
            {
                DataContext = sp.GetRequiredService<ScoreWindowViewModel>(),
            });
            services.AddSingleton<ScoreSurWindow>(sp => new ScoreSurWindow()
            {
                DataContext = sp.GetRequiredService<ScoreWindowViewModel>(),
            });
            services.AddSingleton<ScoreHunWindow>(sp => new ScoreHunWindow()
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
                Owner = Current.MainWindow
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

    /// <summary>
    /// Gets services.
    /// </summary>
    public static IServiceProvider Services => _host.Services;

    /// <summary>
    /// 互斥锁
    /// </summary>
    private static Mutex _mutex = null;

    bool createdNew;

    protected override async void OnStartup(StartupEventArgs e)
    {
        Console.OutputEncoding=Encoding.UTF8;
        _mutex = new Mutex(true, AppConstants.AppName, out createdNew);

        if (!createdNew)
        {
            MessageBox.Show("程序已运行", "警告");
            Current.Shutdown();
        }

        base.OnStartup(e);
        Timeline.DesiredFrameRateProperty.OverrideMetadata(
            typeof(Timeline),
            new FrameworkPropertyMetadata { DefaultValue = 100 }
        );
        await _host.StartAsync();
        var _logger = _host.Services.GetRequiredService<ILogger<App>>();
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

        var settingsHostService = _host.Services.GetRequiredService<ISettingsHostService>();
        settingsHostService.LoadConfig();
        Current.Resources["scoreGlobal_surIcon"] = ImageHelper.GetUiImageSource(
            settingsHostService.Settings.ScoreWindowSettings.IsCampIconBlackVerEnabled
                ? "surIcon_black"
                : "surIcon");
        Current.Resources["scoreGlobal_hunIcon"] = ImageHelper.GetUiImageSource(
            settingsHostService.Settings.ScoreWindowSettings.IsCampIconBlackVerEnabled
                ? "hunIcon_black"
                : "hunIcon");
        Current.Resources["mapBpV2_surIcon"] = ImageHelper.GetUiImageSource(
            settingsHostService.Settings.ScoreWindowSettings.IsCampIconBlackVerEnabled
                ? "surIcon_black"
                : "surIcon");
        Current.Resources["mapBpV2_hunIcon"] = ImageHelper.GetUiImageSource(
            settingsHostService.Settings.ScoreWindowSettings.IsCampIconBlackVerEnabled
                ? "hunIcon_black"
                : "hunIcon");
        ApplicationThemeManager.Changed += (currentApplicationTheme, _) =>
        {
            foreach (var dict in Current.Resources.MergedDictionaries)
            {
                if (dict is not IconThemesDictionary iconThemesDictionary) continue;
                iconThemesDictionary.Theme = currentApplicationTheme;
                break;
            }
        };
        ApplicationThemeManager.Apply(ApplicationTheme.Dark);
#if !DEBUG
            _logger.LogInformation("Update checking on start up");
            await _host.Services.GetRequiredService<IUpdaterService>().UpdateCheck(true);
#endif
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
        var _logger = _host.Services.GetRequiredService<ILogger<App>>();
        _logger.LogInformation("Application Closed");
        await _host.StopAsync();
        _host.Dispose();
    }

    /// <summary>
    /// 重启程序
    /// </summary>
    public static void Restart()
    {
        // 释放互斥锁
        _mutex.Close();
        // 重启应用程序
        System.Diagnostics.Process.Start(ResourceAssembly.Location.Replace(".dll", ".exe"));
        Current.Shutdown();
    }

    /// <summary>
    /// Occurs when an exception is thrown by an application but not handled.
    /// </summary>
    private void OnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e
    )
    {
        var logger = _host.Services.GetRequiredService<ILogger<App>>();
        logger.LogError("Application crashed unexpectedly");
#if !DEBUG
        MessageBox.Show($"出现了些在意料之外的错误，请带着\n{AppConstants.LogPath}\n处的日志文件联系开发者解决", "错误");
#endif
        // For more info see https://docs.microsoft.com/en-us/dotnet/api/system.windows.application.dispatcherunhandledexception?view=windowsdesktop-6.0
    }
}