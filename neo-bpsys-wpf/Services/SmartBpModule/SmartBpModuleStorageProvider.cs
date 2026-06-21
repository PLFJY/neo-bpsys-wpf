using neo_bpsys_wpf.Core.Abstractions.Services;
using System.IO;

namespace neo_bpsys_wpf.Services.SmartBpModule;

/// <summary>Resolves SmartBP-owned storage from the currently loaded module root.</summary>
public sealed class SmartBpModuleStorageProvider(SmartBpModuleManager moduleManager) : ISmartBpModuleStorageProvider
{
    /// <inheritdoc />
    public string ModuleRoot => moduleManager.ModuleRoot;
    /// <inheritdoc />
    public string OcrModelsRoot => Path.Combine(ModuleRoot, "OCRModels");
    /// <inheritdoc />
    public string TesseractDataRoot => Path.Combine(OcrModelsRoot, "Tesseract", "tessdata");
    /// <inheritdoc />
    public string RapidOcrModelsRoot => Path.Combine(OcrModelsRoot, "RapidOCR", "Models");
    /// <inheritdoc />
    public string AiRoot => Path.Combine(ModuleRoot, "AI");
    /// <inheritdoc />
    public string QwenModelsRoot => Path.Combine(AiRoot, "QwenModels");
    /// <inheritdoc />
    public string LlamaCppRoot => Path.Combine(AiRoot, "LlamaCpp");
    /// <inheritdoc />
    public string RecognitionLogsRoot => Path.Combine(AiRoot, "RecognitionLogs");
}
