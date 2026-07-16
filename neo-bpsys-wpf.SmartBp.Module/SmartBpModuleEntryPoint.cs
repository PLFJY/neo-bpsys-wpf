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
        services.AddSingleton(hostServices.GetRequiredService<ISmartBpOcrModelPathProvider>());
        services.AddSingleton(hostServices.GetRequiredService<ISmartBpModuleStorageProvider>());
        services.AddSingleton(hostServices.GetRequiredService<IGitHubDownloadUrlResolver>());
        services.AddSingleton(hostServices.GetRequiredService<ISmartBpAutoRecognitionGlobalControl>());
        services.AddSingleton(hostServices.GetRequiredService<ISmartBpAutoRecognitionGlobalControlSink>());
        services.AddSingleton(hostServices.GetRequiredService<ITutorialRunner>());
        services.AddSingleton(loggerFactory);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

        // GameData 赛后数据 OCR 与 BP 自动识别共用模块服务，但配置文件和区域 profile 分开管理。
        services.AddSingleton<ISmartBpSceneDefinition, SmartBpGameDataSceneDefinition>();
        services.AddSingleton<ISmartBpRegionConfigService, SmartBpRegionConfigService>();
        services.AddSingleton<PaddleOcrProvider>();
        services.AddSingleton<TesseractOcrProvider>();
        services.AddSingleton<RapidOcrNetProvider>();
        services.AddSingleton<ITesseractDataAssetManager, TesseractDataAssetManager>();
        services.AddSingleton<IRapidOcrModelManifestProvider, RapidOcrModelManifestProvider>();
        services.AddSingleton<IRapidOcrModelAssetManager, RapidOcrModelAssetManager>();
        services.AddSingleton<ISmartBpAiPerformanceMonitor, NvmlAiPerformanceMonitor>();
        services.AddSingleton<IOcrProvider>(provider => provider.GetRequiredService<PaddleOcrProvider>());
        services.AddSingleton<IOcrProvider>(provider => provider.GetRequiredService<TesseractOcrProvider>());
        services.AddSingleton<IOcrProvider>(provider => provider.GetRequiredService<RapidOcrNetProvider>());
        services.AddSingleton<SmartBpOcrProviderSelector>();
        services.AddSingleton<IOcrService, OcrService>();
        services.AddSingleton<ISmartBpService, SmartBpService>();
        services.AddSingleton<IQwenModelManifestProvider, QwenModelManifestProvider>();
        services.AddSingleton<ILocalVisionModelManifestProvider>(provider =>
            (ILocalVisionModelManifestProvider)provider.GetRequiredService<IQwenModelManifestProvider>());
        services.AddSingleton<ISmartBpRecognitionSettingsService, SmartBpRecognitionSettingsService>();
        services.AddSingleton<IQwenModelAssetManager, QwenModelAssetManager>();
        services.AddSingleton<ILocalVisionModelAssetManager>(provider =>
            (ILocalVisionModelAssetManager)provider.GetRequiredService<IQwenModelAssetManager>());
        services.AddSingleton<ISmartBpPromptProfileProvider, SmartBpPromptProfileProvider>();
        services.AddSingleton<ILlamaCppRuntimeAssetManager, LlamaCppRuntimeAssetManager>();
        services.AddSingleton<ILlamaCppRuntimeUpdateService, LlamaCppRuntimeUpdateService>();
        services.AddSingleton<ISmartBpRecognitionRegionProfileService, SmartBpRecognitionRegionProfileService>();

        // 自动识别流水线：裁剪、OCR/AI 识别、状态合并、候选操作、GameGuidance 同步与实际应用。
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
        services.AddSingleton<ILlamaCppServerManager, LlamaCppServerManager>();
        services.AddSingleton<ILlamaCppServerManagerFactory, LlamaCppServerManagerFactory>();
        services.AddSingleton<ISmartBpGuidanceSyncService, SmartBpGuidanceSyncService>();
        services.AddSingleton<ISmartBpProgressInferenceService, SmartBpProgressInferenceService>();
        services.AddSingleton<ISmartBpProgressSyncService, SmartBpProgressSyncService>();
        services.AddSingleton<ISmartBpGameStateSyncService, SmartBpGameStateSyncService>();
        services.AddSingleton<SmartBpCandidateOperationBuilder>();
        services.AddSingleton<ISmartBpBusinessStateMerger, SmartBpBusinessStateMerger>();
        services.AddSingleton<ISmartBpRecognitionStateStore, SmartBpRecognitionStateStore>();
        services.AddSingleton<ISmartBpSnapshotRecognitionPlanner, SmartBpSnapshotRecognitionPlanner>();
        services.AddSingleton<ISmartBpRecognitionLedger, SmartBpRecognitionLedger>();
        services.AddSingleton<ISmartBpWorkflowBackfillService, SmartBpWorkflowBackfillService>();
        services.AddSingleton<ISmartBpDetectedOperationApplier, SmartBpDetectedOperationApplier>();
        services.AddSingleton<ISmartBpSceneGateService, SmartBpSceneGateService>();
        services.AddSingleton<SmartBpAutoRecognitionCoordinator>();
        services.AddSingleton<ISmartBpAutoRecognitionCoordinator>(provider => provider.GetRequiredService<SmartBpAutoRecognitionCoordinator>());
        services.AddSingleton<ISmartBpStepCommitScheduler>(provider => provider.GetRequiredService<SmartBpAutoRecognitionCoordinator>());
        services.AddSingleton<ISmartBpDebugLog, SmartBpDebugLog>();
        services.AddSingleton<SmartBpModuleContentViewModel>();
        services.AddTransient<SmartBpModuleContentView>();
        return services.BuildServiceProvider();
    }
}
