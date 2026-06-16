namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// Provides the OCR model root directory for the loaded SmartBP module.
/// </summary>
public interface ISmartBpOcrModelPathProvider
{
    /// <summary>
    /// OCR model root directory.
    /// </summary>
    string RootDirectory { get; }
}
