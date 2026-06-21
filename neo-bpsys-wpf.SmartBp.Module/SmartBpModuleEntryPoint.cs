using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.SmartBpModule;
using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.ViewModels.Pages;
using neo_bpsys_wpf.Views.Pages;
using neo_bpsys_wpf.SmartBp.Module.Abstractions;
using neo_bpsys_wpf.SmartBp.Module.Services.Recognition;

namespace neo_bpsys_wpf.SmartBp.Module;

/// <summary>
/// SmartBP runtime module entry point.
/// </summary>
public sealed class SmartBpModuleEntryPoint : ISmartBpModuleEntryPoint
{
    private ServiceProvider? _serviceProvider;

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

    private static ServiceProvider BuildServices(IServiceProvider hostServices)
    {
        var services = new ServiceCollection();
        var loggerFactory = hostServices.GetRequiredService<ILoggerFactory>();
        services.AddSingleton(hostServices.GetRequiredService<ISharedDataService>());
        services.AddSingleton(hostServices.GetRequiredService<IGameGuidanceService>());
        services.AddSingleton(hostServices.GetRequiredService<ICharacterSelectionService>());
        services.AddSingleton(hostServices.GetRequiredService<IWindowCaptureService>());
        services.AddSingleton(hostServices.GetRequiredService<IFilePickerService>());
        services.AddSingleton(hostServices.GetRequiredService<ISettingsHostService>());
        services.AddSingleton(hostServices.GetRequiredService<ISmartBpOcrModelPathProvider>());
        services.AddSingleton(hostServices.GetRequiredService<ISmartBpModuleStorageProvider>());
        services.AddSingleton(hostServices.GetRequiredService<IGitHubDownloadUrlResolver>());
        services.AddSingleton(hostServices.GetRequiredService<ISmartBpAutoRecognitionGlobalControl>());
        services.AddSingleton(hostServices.GetRequiredService<ISmartBpAutoRecognitionGlobalControlSink>());
        services.AddSingleton(loggerFactory);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
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
        services.AddSingleton<ISmartBpImageEncoder, SmartBpImageEncoder>();
        services.AddSingleton<ISmartBpRecognitionRegionProfileService, SmartBpRecognitionRegionProfileService>();
        services.AddSingleton<ISmartBpRecognitionFrameCropper, SmartBpRecognitionFrameCropper>();
        services.AddSingleton<ISmartBpFrameRingBuffer, SmartBpFrameRingBuffer>();
        services.AddSingleton<ISmartBpCropChangeDetector, SmartBpCropChangeDetector>();
        services.AddSingleton<ISmartBpCharacterResolver, SmartBpCharacterResolver>();
        services.AddSingleton<ISmartBpOcrContactSheetBuilder, SmartBpOcrContactSheetBuilder>();
        services.AddSingleton<ISmartBpOcrTextResolver, SmartBpOcrTextResolver>();
        services.AddSingleton<SmartBpOcrRegionParser>();
        services.AddSingleton<ISmartBpOcrBpRecognitionService, SmartBpOcrBpRecognitionService>();
        services.AddSingleton<ISmartBpOcrSnapshotDeltaRecognitionService, SmartBpOcrSnapshotDeltaRecognitionService>();
        services.AddSingleton<ISmartBpAiOcrTranscriptRecognitionService, SmartBpAiOcrTranscriptRecognitionService>();
        services.AddSingleton<ISmartBpAiOcrTranscriptInterpreter, SmartBpAiOcrTranscriptInterpreter>();
        services.AddSingleton<ISmartBpBusinessAiFusionValidator, SmartBpBusinessAiFusionValidator>();
        services.AddSingleton<ISmartBpBusinessAiFusionService, SmartBpBusinessAiFusionService>();
        services.AddSingleton<ILlamaCppOpenAiClient, LlamaCppOpenAiClient>();
        services.AddSingleton<ILlamaCppServerManager, LlamaCppServerManager>();
        services.AddSingleton<ILlamaCppServerManagerFactory, LlamaCppServerManagerFactory>();
        services.AddSingleton<ISmartBpAiRecognitionService, SmartBpAiRecognitionService>();
        services.AddSingleton<ISmartBpGuidanceSyncService, SmartBpGuidanceSyncService>();
        services.AddSingleton<SmartBpCandidateOperationBuilder>();
        services.AddSingleton<ISmartBpBusinessStateMerger, SmartBpBusinessStateMerger>();
        services.AddSingleton<ISmartBpRegionSnapshotRecognitionService, SmartBpRegionSnapshotRecognitionService>();
        services.AddSingleton<ISmartBpRecognitionStateStore, SmartBpRecognitionStateStore>();
        services.AddSingleton<ISmartBpSnapshotRecognitionPlanner, SmartBpSnapshotRecognitionPlanner>();
        services.AddSingleton<SmartBpAiSnapshotDeltaRecognitionService>();
        services.AddSingleton<ISmartBpSnapshotDeltaRecognitionService, SmartBpSnapshotDeltaRecognitionRouter>();
        services.AddSingleton<ISmartBpAiFieldSnapshotRecognitionService, SmartBpAiFieldSnapshotRecognitionService>();
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
