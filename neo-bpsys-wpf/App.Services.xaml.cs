using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Extensions.Registry;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Services.Archives;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Controls.FrontedLayout;
using neo_bpsys_wpf.Services.Abstractions;
using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.Services.FrontedDesigner;
using neo_bpsys_wpf.Services.SmartBpModule;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;
using neo_bpsys_wpf.ViewModels.Pages;
using neo_bpsys_wpf.ViewModels.Windows;
using neo_bpsys_wpf.Views.Pages;
using neo_bpsys_wpf.Views.Windows;
using Wpf.Ui;
using Wpf.Ui.DependencyInjection;
using IContentDialogService = neo_bpsys_wpf.Core.Abstractions.Services.IContentDialogService;
using ISnackbarService = neo_bpsys_wpf.Core.Abstractions.Services.ISnackbarService;
using ContentDialogService = neo_bpsys_wpf.Services.ContentDialogService;
using SnackbarService = neo_bpsys_wpf.Services.SnackbarService;


namespace neo_bpsys_wpf;

public partial class App
{
    private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        services.AddNavigationViewPageProvider();
        services.AddProductTour();
        services.AddSingleton<ITutorialLanguageService, NeoBpsysTutorialLanguageService>();
        services.AddSingleton<ITutorialTextProvider, NeoBpsysTutorialTextProvider>();
        services.AddSingleton<ITutorialAvatarProvider, AliceTutorialAvatarProvider>();
        services.AddSingleton<ITutorialContentResolver, NeoBpsysTutorialContentResolver>();

        //App Host
        services.AddHostedService<ApplicationHostService>();

        // Theme manipulation
        services.AddSingleton<IThemeService, ThemeService>();

        // TaskBar manipulation
        services.AddSingleton<ITaskBarService, TaskBarService>();

        //UpdaterService
        services.AddSingleton<IUpdaterService, UpdaterService>();
        services.AddSingleton<IGlobalRestartService, GlobalRestartService>();

        // Service containing navigation, same as INavigationWindow... but without window
        services.AddSingleton<Services.NavigationService>();
        services.AddSingleton<INavigationService>(sp => sp.GetRequiredService<Services.NavigationService>());

        // SharedDataServices
        services.AddSingleton<ISharedDataService, SharedDataService>();

        //MatchScoreService
        services.AddSingleton<IMatchScoreService, MatchScoreService>();

        // HomeTeam window with navigation
        services.AddSingleton<INavigationWindow, MainWindow>(sp => new MainWindow(
            sp.GetRequiredService<INavigationService>(),
            sp.GetRequiredService<IInfoBarService>(),
            sp.GetRequiredService<ISnackbarService>(),
            sp.GetRequiredService<IContentDialogService>(),
            sp.GetRequiredService<ISettingsHostService>(),
            sp.GetRequiredService<IOnboardingCoordinator>(),
            sp.GetRequiredService<ITutorialRunner>(),
            sp.GetRequiredService<ILogger<MainWindow>>()
        )
        {
            DataContext = sp.GetRequiredService<MainWindowViewModel>(),
        });
        services.AddSingleton<MainWindowViewModel>();
        // ClassicMode Window
        services.AddSingleton<ClassicBackendWindow>();

        //FrontedWindowService
        services.AddSingleton<IFrontedWindowService, FrontedWindowService>();

        // 角色选择服务
        services.AddSingleton<ICharacterSelectionService, CharacterSelectionService>();

        //Tool Services
        services.AddSingleton<IFilePickerService, FilePickerService>();
        services.AddSingleton<IGitHubDownloadUrlResolver, GitHubDownloadUrlResolver>();
        services.AddSingleton<IArchiveService, SevenZipArchiveService>();
        services.AddSingleton<IInfoBarService, InfoBarService>();
        services.AddSingleton<ISnackbarService, SnackbarService>();
        services.AddSingleton<IContentDialogService, ContentDialogService>();
        services.AddSingleton<IWindowCaptureService, WindowCaptureService>();
        services.AddSingleton<SmartBpModuleManager>();
        services.AddSingleton<ISmartBpFeatureService, SmartBpFeatureService>();
        services.AddSingleton<ISmartBpModuleStorageProvider, SmartBpModuleStorageProvider>();
        services.AddSingleton<ISmartBpOcrModelPathProvider, SmartBpOcrModelPathProvider>();
        services.AddSingleton<SmartBpAutoRecognitionGlobalControl>();
        services.AddSingleton<ISmartBpAutoRecognitionGlobalControl>(sp =>
            sp.GetRequiredService<SmartBpAutoRecognitionGlobalControl>());
        services.AddSingleton<ISmartBpAutoRecognitionGlobalControlSink>(sp =>
            sp.GetRequiredService<SmartBpAutoRecognitionGlobalControl>());

        //Additional Feature Services
        services.AddSingleton<IGameGuidanceService, GameGuidanceService>();
        services.AddSingleton<ISettingsMigrationService, SettingsMigrationService>();
        services.AddSingleton<ILegacyV2ConfigDetector, LegacyV2ConfigDetector>();
        services.AddSingleton<FrontedLayoutPackageLegacyConverter>();
        services.AddSingleton<ILegacyV2StartupMigrationService, LegacyV2StartupMigrationService>();
        services.AddSingleton<ISettingsHostService, SettingsHostService>();
        services.AddSingleton<IWebLocalizationProvider, WebRendererLocalizationBridge>();
        services.AddSingleton<IWebGameProgressProvider, WebGameProgressProvider>();
        services.AddSingleton<IFrontedImageSafetyService, FrontedImageSafetyService>();
        services.AddSingleton<IFrontedResourceResolver, FrontedResourceResolver>();
        services.AddSingleton<IFrontedLocalResourceStore, FrontedLocalResourceStore>();
        services.AddSingleton<IFrontedUserLayoutStore, FrontedUserLayoutStore>();
        services.AddSingleton<IFrontedWindowLayoutOptionsService, FrontedWindowLayoutOptionsService>();
        services.AddSingleton<IFrontedWindowRegistry, neo_bpsys_wpf.Core.Services.Registry.FrontedWindowRegistryService>();
        services.AddSingleton<IFrontedLayoutPackageManager>(sp => new FrontedLayoutPackageManager(
            sp.GetRequiredService<ILogger<FrontedLayoutPackageManager>>(),
            key => Helpers.I18nHelper.GetLocalizedString(Helpers.AppI18nDictionaries.FrontManage, key)));
        services.AddSingleton<IFrontedLayoutPackageExporter, FrontedLayoutPackageExporter>();
        services.AddSingleton<IFrontedLayoutPackageImporter, FrontedLayoutPackageImporter>();
        services.AddSingleton<IFrontedLayoutPackageLegacyConverter>(sp =>
            sp.GetRequiredService<FrontedLayoutPackageLegacyConverter>());
        LegacyConvertMessageHelper.LocalizeTemplate = key => Helpers.I18nHelper.GetLocalizedString(Helpers.AppI18nDictionaries.FrontManage, key);
        services.AddSingleton<IFrontedPluginMetadataProvider, FrontedPluginMetadataProvider>();
        services.AddSingleton<FrontedBehaviorEventCatalog>();
        services.AddSingleton<IFrontedBehaviorService, FrontedBehaviorService>();
        services.AddSingleton<IFrontedBehaviorClipboard, FrontedBehaviorClipboard>();
        services.AddSingleton<IFrontedBehaviorControlSemanticResolver, FrontedBehaviorControlSemanticResolver>();
        services.AddSingleton<FrontedBehaviorCopyPasteService>();
        services.AddSingleton<IFrontedEventBus, FrontedEventBus>();
        services.AddSingleton<IFrontedBehaviorEventDebugService, FrontedBehaviorEventDebugService>();
        services.AddSingleton<FrontedBehaviorTriggerEvaluator>();
        services.AddSingleton<FrontedBehaviorRuntimeHostManager>();
        services.AddSingleton<IFrontedBehaviorRuntime, FrontedBehaviorRuntime>();
        services.AddSingleton<IFrontedTransitionOrchestrator, FrontedTransitionOrchestrator>();
        // SharedData bridge: creates once, subscribes to attributed events on startup
        services.AddSingleton<FrontedSharedDataBehaviorEventBridge>(sp =>
        {
            var bridge = new FrontedSharedDataBehaviorEventBridge(
                sp.GetRequiredService<ISharedDataService>(),
                sp.GetRequiredService<IFrontedEventBus>(),
                sp.GetRequiredService<ILogger<FrontedSharedDataBehaviorEventBridge>>(),
                sp.GetRequiredService<IGameGuidanceService>(),
                sp.GetRequiredService<ICharacterSelectionService>());
            bridge.Start();
            return bridge;
        });
        services.AddSingleton<FrontedNodeCatalog>();
        services.AddSingleton<FrontedNodeGraphValidator>();
        services.AddSingleton<IFrontedGraphDelayProvider, FrontedGraphDelayProvider>();
        services.AddSingleton<IFrontedNodeGraphRuntime, FrontedNodeGraphRuntime>();
        services.AddSingleton<IFrontedAnimationTargetResolver, FrontedAnimationTargetResolver>();
        services.AddSingleton<IAnimatablePropertyAdapter, BackgroundTintAnimatablePropertyAdapter>();
        services.AddSingleton<IAnimatablePropertyAdapter, ShapeAnimatablePropertyAdapter>();
        services.AddSingleton<IAnimatablePropertyAdapter, TextAnimatablePropertyAdapter>();
        services.AddSingleton<IAnimatablePropertyAdapter, GaussianBlurAnimatablePropertyAdapter>();
        services.AddSingleton<IAnimatablePropertyAdapter, FrameworkElementCommonAdapter>();
        services.AddSingleton<IAnimatablePropertyAdapterRegistry, FrontedAnimatablePropertyAdapterRegistry>();
        services.AddSingleton<IFrontedAnimationRuntime, FrontedAnimationRuntime>();
        services.AddSingleton<FrontedDesignerPreviewAnimationScope>();
        services.AddSingleton<IFrontedDesignerLocalizationService, FrontedDesignerI18nLocalizationService>();
        services.AddSingleton<IFrontedControl, TextFrontedControl>();
        services.AddSingleton<IFrontedControl, LocalizedTextFrontedControl>();
        services.AddSingleton<IFrontedControl, ImageFrontedControl>();
        services.AddSingleton<IFrontedControl, BorderedImageFrontedControl>();
        services.AddSingleton<IFrontedControl, RectangleFrontedControl>();
        services.AddSingleton<IFrontedControl, PolygonFrontedControl>();
        services.AddSingleton<BackgroundImageTintProcessor>();
        services.AddSingleton<IFrontedControl, BackgroundTintRectangleFrontedControl>();
        services.AddSingleton<IFrontedControl, BackgroundTintPolygonFrontedControl>();
        services.AddSingleton<IFrontedControl, GlobalScoreRowFrontedControl>();
        services.AddSingleton<IFrontedControl, TalentTraitDisplayFrontedControl>();
        services.AddSingleton<IFrontedControl, GameProgressTextFrontedControl>();
        services.AddSingleton<IFrontedControl, MapNameTextFrontedControl>();
        services.AddSingleton<IFrontedControl, MapV2DisplayFrontedControl>();
        services.AddSingleton<IFrontedControlRegistry, FrontedControlRegistry>();
        services.AddSingleton<IFrontedLayoutService, FrontedLayoutService>();
        services.AddSingleton<IFrontedRenderer, FrontedRenderer>();
        services.AddSingleton<IFrontedBehaviorAnimationPartRenderer, FrontedBehaviorAnimationPartRenderer>();
        services.AddSingleton<FrontedLayoutReferenceScanner>();
        services.AddSingleton<FrontedLayoutDesignConverter>();
        services.AddSingleton<FrontedLayoutValidator>();
        services.AddSingleton<FrontedFontFamilyOptionProvider>();
        services.AddSingleton<FrontedPackageFontManager>();
        services.AddSingleton<FrontedPropertyGridBuilder>();
        services.AddSingleton<IFrontedBindingRootProvider, DefaultFrontedBindingRootProvider>();
        services.AddSingleton<IFrontedBindingCatalogProvider, FrontedBindingReflectionCatalogProvider>();
        services.AddSingleton<FrontedBindingBrowserProvider>();
        services.AddSingleton<FrontedResourceBrowserProvider>();
        services.AddSingleton<FrontedControlDefaultConfigFactory>();
        services.AddSingleton<FrontedControlNameGenerator>();
        services.AddSingleton<FrontedDesignerLayoutCatalog>(sp =>
            new FrontedDesignerLayoutCatalog(sp.GetRequiredService<IFrontedWindowRegistry>()));
        services.AddTransient<DesignerPreviewSharedDataService>();
        services.AddSingleton<ITextSettingsNavigationService, TextSettingsNavigationService>();
        services.AddSingleton<IPluginService, PluginService>();
        services.AddSingleton<IPluginMarketService, PluginMarketService>();
        services.AddSingleton<IPluginInstallService, PluginInstallService>();
        services.AddSingleton<IBpuiFileAssociationService, BpuiFileAssociationService>();
        services.AddSingleton<IBpuiFileActivationService, BpuiFileActivationService>();

        services.AddSingleton(sp =>
        {
            NeoBpsysTutorialRegistration.Register(
                sp.GetRequiredService<ITutorialPackageRegistry>(),
                sp.GetRequiredService<ITutorialSequenceRegistry>(),
                sp.GetRequiredService<ITutorialFlowRegistry>());
            return new ProductTourRegistrationMarker();
        });

        //Views and ViewModels
        //Windows
        services.AddTransient<FrontedDesignerWindowViewModel>();
        services.AddTransient<FrontedDesignerWindow>();
        services.AddTransient<FrontedBindingBrowserWindowViewModel>();
        services.AddTransient<FrontedBindingBrowserWindow>();
        services.AddTransient<FrontedResourceBrowserWindowViewModel>();
        services.AddTransient<FrontedResourceBrowserWindow>();
        services.AddTransient<FrontedPackageFontManagerWindowViewModel>();
        services.AddTransient<FrontedPackageFontManagerWindow>();
        services.AddTransient<FrontedLayoutPackageExportWindowViewModel>();
        services.AddTransient<FrontedLayoutPackageExportWindow>();
        services.AddTransient<FrontedBehaviorEventDebuggerViewModel>();
        services.AddTransient<FrontedBehaviorEventDebuggerWindow>();

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

        // 注册内置 v3 Layout 前台窗口（Canonical ID = LocalId，无 PackageId）
        services.AddFrontedV3LayoutWindow("BpWindow", isBuiltIn: true);
        services.AddFrontedV3LayoutWindow("CutSceneWindow", isBuiltIn: true);
        services.AddFrontedV3LayoutWindow("ScoreSurWindow", isBuiltIn: true);
        services.AddFrontedV3LayoutWindow("ScoreHunWindow", isBuiltIn: true);
        services.AddFrontedV3LayoutWindow("ScoreGlobalWindow", isBuiltIn: true);
        services.AddFrontedV3LayoutWindow("GameDataWindow", isBuiltIn: true);
        services.AddFrontedV3LayoutWindow("BpOverviewWindow", isBuiltIn: true);
        services.AddFrontedV3LayoutWindow("MapV2Window", isBuiltIn: true);

        PluginService.InitializePlugins(context, services);
    }
}

internal sealed class ProductTourRegistrationMarker;
