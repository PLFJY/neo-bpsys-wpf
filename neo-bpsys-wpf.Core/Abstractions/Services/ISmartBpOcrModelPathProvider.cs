namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 提供已加载 SmartBP 模块的 OCR 模型根目录。
/// </summary>
public interface ISmartBpOcrModelPathProvider
{
    /// <summary>
    /// OCR 模型根目录。
    /// </summary>
    string RootDirectory { get; }
}
