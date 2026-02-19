using OpenCvSharp;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

public sealed record OcrModelDefinition(string Key, string DisplayName, string Description);

/// <summary>
/// OCR识别服务
/// </summary>
public interface IOcrService
{
    string? CurrentOcrModelKey { get; }

    bool IsDownloading { get; }

    double? DownloadProgress { get; }

    string DownloadStatusText { get; }

    event EventHandler? DownloadStateChanged;

    IReadOnlyList<OcrModelDefinition> GetAvailableModels();

    bool IsModelInstalled(string modelKey);

    Task DownloadModelAsync(string modelKey, CancellationToken cancellationToken = default);

    void CancelDownload();

    bool TryDeleteModel(string modelKey, out string errorMessage);

    bool TrySwitchOcrModel(string modelKey, out string errorMessage);

    string? RecognizeText(Mat bin);

    string? GetCharacterName(Mat bin);

    string? GetCharacterName(string? playerAndCharacterName);
}
