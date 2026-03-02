using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Extensions.Registry;
using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.ViewModels.Pages;
using neo_bpsys_wpf.ViewModels.Windows;
using neo_bpsys_wpf.Views.Pages;
using neo_bpsys_wpf.Views.Windows;
using Wpf.Ui;
using Wpf.Ui.DependencyInjection;
using ISnackbarService = neo_bpsys_wpf.Core.Abstractions.Services.ISnackbarService;
using SnackbarService = neo_bpsys_wpf.Services.SnackbarService;


namespace neo_bpsys_wpf;

public partial class App
{
    private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
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

        // HomeTeam window with navigation
        services.AddSingleton<INavigationWindow, MainWindow>(sp => new MainWindow(
            sp.GetRequiredService<INavigationService>(),
            sp.GetRequiredService<IInfoBarService>(),
            sp.GetRequiredService<ISnackbarService>(),
            sp.GetRequiredService<ISettingsHostService>(),
            sp.GetRequiredService<ILogger<MainWindow>>()
        )
        {
            DataContext = sp.GetRequiredService<MainWindowViewModel>(),
        });
        services.AddSingleton<MainWindowViewModel>();

        //FrontedWindowService
        services.AddSingleton<IFrontedWindowService, FrontedWindowService>();

        // 角色选择动画服务（支持插件覆写）
        services.AddSingleton<IAnimationService, AnimationService>();

        // 角色选择服务
        services.AddSingleton<ICharacterSelectionService, CharacterSelectionService>();

        //Tool Services
        services.AddSingleton<IFilePickerService, FilePickerService>();
        services.AddSingleton<IInfoBarService, InfoBarService>();
        services.AddSingleton<ISnackbarService, SnackbarService>();
        services.AddSingleton<IWindowCaptureService, WindowCaptureService>();
        // 场景定义先注册，配置服务会在构造时按 SceneKey 解析规则。
        services.AddSingleton<ISmartBpSceneDefinition, SmartBpGameDataSceneDefinition>();
        services.AddSingleton<ISmartBpRegionConfigService, SmartBpRegionConfigService>();

        //Additional Feature Services
        services.AddSingleton<IGameGuidanceService, GameGuidanceService>();
        services.AddSingleton<ISmartBpService, SmartBpService>();
        services.AddSingleton<IOcrService, OcrService>();
        services.AddSingleton<ISettingsHostService, SettingsHostService>();
        services.AddSingleton<ITextSettingsNavigationService, TextSettingsNavigationService>();
        services.AddSingleton<IPluginService, PluginService>();

        //Views and ViewModels
        //Windows
        services.AddFrontedWindow<BpWindow, BpWindowViewModel>();
        services.AddFrontedWindow<CutSceneWindow, CutSceneWindowViewModel>();
        services.AddFrontedWindow<ScoreGlobalWindow, ScoreWindowViewModel>();
        services.AddFrontedWindow<ScoreSurWindow, ScoreWindowViewModel>();
        services.AddFrontedWindow<ScoreHunWindow, ScoreWindowViewModel>();
        services.AddFrontedWindow<GameDataWindow, GameDataWindowViewModel>();
        services.AddFrontedWindow<WidgetsWindow, WidgetsWindowViewModel>();
        services.AddTransient<ScoreManualWindow>(sp => new ScoreManualWindow
        {
            DataContext = sp.GetRequiredService<ScoreManualWindowViewModel>(),
            Owner = Current.MainWindow
        });
        services.AddSingleton<ScoreManualWindowViewModel>();

        //Pages
        //Internal
        services.AddBackendPage<HomePage, HomePageViewModel>();
        services.AddBackendPage<TeamInfoPage, TeamInfoPageViewModel>();
        services.AddBackendPage<MapBpPage, MapBpPageViewModel>();
        services.AddBackendPage<BanHunPage, BanHunPageViewModel>();
        services.AddBackendPage<BanSurPage, BanSurPageViewModel>();
        services.AddBackendPage<PickPage, PickPageViewModel>();
        services.AddBackendPage<TalentPage, TalentPageViewModel>();
        services.AddBackendPage<ScorePage, ScorePageViewModel>();
        services.AddBackendPage<GameDataPage, GameDataPageViewModel>();
        //External
        services.AddBackendPage<SettingPage, SettingPageViewModel>();
        services.AddBackendPage<FrontManagePage, FrontManagePageViewModel>();
        services.AddBackendPage<PluginPage, PluginPageViewModel>();
        services.AddBackendPage<SmartBpPage, SmartBpPageViewModel>();

        PluginService.InitializePlugins(context, services);
    }
}
