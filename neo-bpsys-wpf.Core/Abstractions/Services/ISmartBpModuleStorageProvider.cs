namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>Provides all persistent large-file locations owned by the SmartBP module.</summary>
public interface ISmartBpModuleStorageProvider
{
    /// <summary>Gets the module root.</summary>
    string ModuleRoot { get; }
    /// <summary>Gets the PaddleOCR model root.</summary>
    string OcrModelsRoot { get; }
    /// <summary>Gets the managed Tesseract tessdata root.</summary>
    string TesseractDataRoot { get; }
    /// <summary>Gets the AI data root.</summary>
    string AiRoot { get; }
    /// <summary>Gets the Qwen model root.</summary>
    string QwenModelsRoot { get; }
    /// <summary>Gets the llama.cpp runtime root.</summary>
    string LlamaCppRoot { get; }
    /// <summary>Gets the recognition log root.</summary>
    string RecognitionLogsRoot { get; }
}
