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

        //Additional Feature Services
        services.AddSingleton<IGameGuidanceService, GameGuidanceService>();
        services.AddSingleton<ISettingsHostService, SettingsHostService>();
        services.AddSingleton<ITextSettingsNavigationService, TextSettingsNavigationService>();
        services.AddSingleton<IPluginService, PluginService>();

        //Views and ViewModels
        //Window
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

        //Page
        services.AddSingleton<HomePage>(sp=> new HomePage
        {
            DataContext = sp.GetRequiredService<HomePageViewModel>(),
        });
        services.AddSingleton<HomePageViewModel>();
        services.AddSingleton<TeamInfoPage>(sp => new TeamInfoPage
        {
            DataContext = sp.GetRequiredService<TeamInfoPageViewModel>(),
        });
        services.AddSingleton<TeamInfoPageViewModel>();

        services.AddSingleton<MapBpPage>(sp => new MapBpPage
        {
            DataContext = sp.GetRequiredService<MapBpPageViewModel>(),
        });
        services.AddSingleton<MapBpPageViewModel>();

        services.AddSingleton<BanHunPage>(sp => new BanHunPage
        {
            DataContext = sp.GetRequiredService<BanHunPageViewModel>(),
        });
        services.AddSingleton<BanHunPageViewModel>();

        services.AddSingleton<BanSurPage>(sp => new BanSurPage
        {
            DataContext = sp.GetRequiredService<BanSurPageViewModel>(),
        });
        services.AddSingleton<BanSurPageViewModel>();

        services.AddSingleton<PickPage>(sp => new PickPage
        {
            DataContext = sp.GetRequiredService<PickPageViewModel>(),
        });
        services.AddSingleton<PickPageViewModel>();

        services.AddSingleton<TalentPage>(sp => new TalentPage
        {
            DataContext = sp.GetRequiredService<TalentPageViewModel>(),
        });
        services.AddSingleton<TalentPageViewModel>();

        services.AddSingleton<ScorePage>(sp => new ScorePage
        {
            DataContext = sp.GetRequiredService<ScorePageViewModel>(),
        });
        services.AddSingleton<ScorePageViewModel>();

        services.AddSingleton<GameDataPage>(sp => new GameDataPage
        {
            DataContext = sp.GetRequiredService<GameDataPageViewModel>(),
        });
        services.AddSingleton<GameDataPageViewModel>();

        services.AddSingleton<FrontManagePage>(sp => new FrontManagePage
        {
            DataContext = sp.GetRequiredService<FrontManagePageViewModel>(),
        });
        services.AddSingleton<FrontManagePageViewModel>();

        services.AddSingleton<PluginPage>(sp => new PluginPage
        {
            DataContext = sp.GetRequiredService<PluginPageViewModel>(),
        });
        services.AddSingleton<PluginPageViewModel>();

        services.AddSingleton<SettingPage>(sp =>
            new SettingPage(sp.GetRequiredService<ITextSettingsNavigationService>())
            {
                DataContext = sp.GetRequiredService<SettingPageViewModel>()
            });
        services.AddSingleton<SettingPageViewModel>();

        PluginService.InitializePlugins(context, services);
    }
}