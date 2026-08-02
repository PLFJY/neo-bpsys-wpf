using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.SmartBpModule;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.ViewModels.Pages;
using neo_bpsys_wpf.Views.Pages;
using neo_bpsys_wpf.Views.Windows;
using neo_bpsys_wpf.SmartBp.Module.Abstractions;
using neo_bpsys_wpf.SmartBp.Module.Services.Recognition;

namespace neo_bpsys_wpf.SmartBp.Module;

/// <summary>
/// SmartBP 运行时模块入口。
/// </summary>
public sealed class SmartBpModuleEntryPoint : ISmartBpModuleEntryPoint, ITutorialRegistrationContributor
{
    private ServiceProvider? _serviceProvider;

    /// <inheritdoc />
    public string RegistrationId => "neo-bpsys-wpf.SmartBp.Module";

    /// <inheritdoc />
    public void RegisterTutorials(ITutorialBuilder builder)
    {
        builder.RegisterOwner<SmartBpModuleContentView>();
        builder.RegisterOwner<RegionEditorWindow>();
    }

    /// <inheritdoc />
    public object CreateSmartBpContent(IServiceProvider hostServices)
    {
        var logger = hostServices.GetService<ILogger<SmartBpModuleEntryPoint>>();
        logger?.LogInformation("Creating SmartBP module content.");
        _serviceProvider ??= BuildServices(hostServices);
        // 在创建任何 OCR 服务前完成 Paddle native runtime 的选择与加载。
        // 参照原 App.OnStartup 的 bootstrap，但所有权归 Module。
        // forceCpuOcr 恒为 false：移除 --force-cpu-ocr 强制机制，靠 bootstrap 探测回退。
        _serviceProvider.GetRequiredService<IPaddleRuntimeBootstrapper>().Bootstrap(forceCpu: false);
        var view = ActivatorUtilities.CreateInstance<SmartBpModuleContentView>(_serviceProvider);
        view.DataContext = _serviceProvider.GetRequiredService<SmartBpModuleContentViewModel>();
        logger?.LogInformation("SmartBP module content created.");
        return view;
    }

    /// <inheritdoc />
    public IReadOnlyList<SmartBpFeatureCommand> GetFeatureCommands()
    {
        if (_serviceProvider == null)
            return [];

        var service = _serviceProvider.GetRequiredService<ISmartBpService>();
        return
        [
            new SmartBpFeatureCommand(
                SmartBpModuleConstants.AutoFillGameDataCommandId,
                "AutoDetectAndFillPostGameData",
                service.AutoFillGameDataAsync)
        ];
    }

    /// <inheritdoc />
    public ISmartBpPostGameRecognitionProgressSource? GetPostGameRecognitionProgressSource()
    {
        if (_serviceProvider == null)
            return null;

        var inner = _serviceProvider.GetRequiredService<IPostGameRecognitionProgressSource>();
        return PostGameRecognitionProgressSourceAdapter.Create(inner);
    }

    /// <summary>
    /// 构建 SmartBP 模块内部 DI 容器，并桥接宿主提供的全局服务。
    /// </summary>
    /// <param name="hostServices">宿主应用的服务容器。</param>
    /// <returns>模块内部服务容器。</returns>
    private static ServiceProvider BuildServices(IServiceProvider hostServices)
    {
        var services = new ServiceCollection();
        var loggerFactory = hostServices.GetRequiredService<ILoggerFactory>();

        // 宿主状态与导播控制服务仍由主程序持有，模块只复用引用，不复制比赛状态。
        services.AddSingleton(hostServices.GetRequiredService<ISharedDataService>());
        services.AddSingleton(hostServices.GetRequiredService<IGameGuidanceService>());
        services.AddSingleton(hostServices.GetRequiredService<ICharacterSelectionService>());
        services.AddSingleton(hostServices.GetRequiredService<IWindowCaptureService>());
        services.AddSingleton(hostServices.GetRequiredService<IFilePickerService>());
        services.AddSingleton(hostServices.GetRequiredService<IInfoBarService>());
        services.AddSingleton(hostServices.GetRequiredService<ISettingsHostService>());
        services.AddSingleton(hostServices.GetRequiredService<IGlobalRestartService>());
        services.AddSingleton(hostServices.GetRequiredService<IFileDownloadService>());
        // Paddle / CUDA runtime 实现由 Module 自持（不再从宿主桥接）。
        // 实现类位于 neo_bpsys_wpf.SmartBp.Module.PaddleRuntime 命名空间，接口位于 Core。
        services.AddSingleton<ICudaDeviceDetector, PaddleRuntime.CudaDeviceDetector>();
        services.AddSingleton<IPaddleRuntimeManifestProvider, PaddleRuntime.PaddleRuntimeManifestProvider>();
        services.AddSingleton<IPaddleRuntimeComponentService, PaddleRuntime.PaddleRuntimeComponentService>();
        services.AddSingleton<IPaddleCudaPrerequisiteSetupService, PaddleRuntime.PaddleCudaPrerequisiteSetupService>();
        services.AddSingleton<IPaddleRuntimeState, PaddleRuntime.PaddleRuntimeState>();
        services.AddSingleton<IPaddleRuntimeBootstrapper, PaddleRuntime.PaddleRuntimeBootstrapper>();
        services.AddSingleton(hostServices.GetRequiredService<ISmartBpOcrModelPathProvider>());
        services.AddSingleton(hostServices.GetRequiredService<ISmartBpModuleStorageProvider>());
        services.AddSingleton(hostServices.GetRequiredService<IGitHubDownloadUrlResolver>());
        services.AddSingleton(hostServices.GetRequiredService<ISmartBpAutoRecognitionGlobalControl>());
        services.AddSingleton(hostServices.GetRequiredService<ISmartBpAutoRecognitionGlobalControlSink>());
        services.AddSingleton(hostServices.GetRequiredService<ITutorialRunner>());
        services.AddSingleton(loggerFactory);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

        // 赛后数据 OCR 使用完整捕获帧坐标聚类；只有全流程 BP 保留可编辑识别区域。
        services.AddSingleton<PaddleOcrProvider>();
        services.AddSingleton<TesseractOcrProvider>();
        services.AddSingleton<RapidOcrNetProvider>();
        services.AddSingleton<ITesseractDataAssetManager, TesseractDataAssetManager>();
        services.AddSingleton<IRapidOcrModelManifestProvider, RapidOcrModelManifestProvider>();
        services.AddSingleton<IRapidOcrModelAssetManager, RapidOcrModelAssetManager>();
        services.AddSingleton<IOcrProvider>(provider => provider.GetRequiredService<PaddleOcrProvider>());
        services.AddSingleton<IOcrProvider>(provider => provider.GetRequiredService<TesseractOcrProvider>());
        services.AddSingleton<IOcrProvider>(provider => provider.GetRequiredService<RapidOcrNetProvider>());
        services.AddSingleton<SmartBpOcrProviderSelector>();
        services.AddSingleton<IOcrService, OcrService>();
        services.AddSingleton<SmartBpService>();
        services.AddSingleton<ISmartBpService>(provider => provider.GetRequiredService<SmartBpService>());
        services.AddSingleton<IGameDataRecognitionDebugState>(provider => provider.GetRequiredService<SmartBpService>());
        services.AddSingleton<IPostGameRecognitionProgressSource>(provider => provider.GetRequiredService<SmartBpService>());
        services.AddSingleton<ISmartBpRecognitionSettingsService, SmartBpRecognitionSettingsService>();
        services.AddSingleton<ISmartBpRecognitionRegionProfileService, SmartBpRecognitionRegionProfileService>();

        // 自动识别流水线：裁剪、OCR、候选操作，以及基于宿主槽位状态的统一对账。
        services.AddSingleton<ISmartBpRecognitionFrameCropper, SmartBpRecognitionFrameCropper>();
        services.AddSingleton<ISmartBpFrameRingBuffer, SmartBpFrameRingBuffer>();
        services.AddSingleton<ISmartBpCropChangeDetector, SmartBpCropChangeDetector>();
        services.AddSingleton<ISmartBpCharacterResolver, SmartBpCharacterResolver>();
        services.AddSingleton<ISmartBpPlayerIdentityMatcher, SmartBpPlayerIdentityMatcher>();
        services.AddSingleton<ISmartBpOcrContactSheetBuilder, SmartBpOcrContactSheetBuilder>();
        services.AddSingleton<ISmartBpOcrTextResolver, SmartBpOcrTextResolver>();
        services.AddSingleton<ISmartBpLifecycleStatusDetector, SmartBpLifecycleStatusDetector>();
        services.AddSingleton<SmartBpOcrRegionParser>();
        services.AddSingleton<ISmartBpOcrBpRecognitionService, SmartBpOcrBpRecognitionService>();
        services.AddSingleton<ISmartBpOcrSnapshotDeltaRecognitionService, SmartBpOcrSnapshotDeltaRecognitionService>();
        services.AddSingleton<SmartBpHistoricalFrameReviewService>();
        services.AddSingleton<ISmartBpReconciliationService, SmartBpReconciliationService>();
        services.AddSingleton<ISmartBpGameStateSyncService, SmartBpGameStateSyncService>();
        services.AddSingleton<SmartBpCandidateOperationBuilder>();
        services.AddSingleton<ISmartBpBusinessStateMerger, SmartBpBusinessStateMerger>();
        services.AddSingleton<ISmartBpSnapshotRecognitionPlanner, SmartBpSnapshotRecognitionPlanner>();
        services.AddSingleton<ISmartBpDetectedOperationApplier, SmartBpDetectedOperationApplier>();
        services.AddSingleton<ISmartBpSceneGateService, SmartBpSceneGateService>();
        services.AddSingleton<SmartBpAutoRecognitionCoordinator>();
        services.AddSingleton<ISmartBpAutoRecognitionCoordinator>(provider => provider.GetRequiredService<SmartBpAutoRecognitionCoordinator>());
        services.AddSingleton<ISmartBpDebugLog, SmartBpDebugLog>();
        services.AddSingleton<SmartBpModuleContentViewModel>();
        services.AddTransient<SmartBpModuleContentView>();
        return services.BuildServiceProvider();
    }
}
