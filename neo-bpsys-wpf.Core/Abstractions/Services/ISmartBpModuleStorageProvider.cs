namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>提供 SmartBP 模块拥有的所有持久化大文件存储位置。</summary>
public interface ISmartBpModuleStorageProvider
{
    /// <summary>获取模块根目录。</summary>
    string ModuleRoot { get; }
    /// <summary>获取模块拥有的 Paddle runtime 根目录。</summary>
    string PaddleRuntimeRoot { get; }
    /// <summary>获取 PaddleOCR 模型根目录。</summary>
    string OcrModelsRoot { get; }
    /// <summary>获取受管理的 Tesseract tessdata 根目录。</summary>
    string TesseractDataRoot { get; }
    /// <summary>获取受管理的 RapidOCR 模型配置根目录。</summary>
    string RapidOcrModelsRoot { get; }
    /// <summary>获取识别日志根目录。</summary>
    string RecognitionLogsRoot { get; }
}
