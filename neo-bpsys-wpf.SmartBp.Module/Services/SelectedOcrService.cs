using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.SmartBp.Module.Abstractions;
using neo_bpsys_wpf.SmartBp.Module.Models.Recognition;
using OpenCvSharp;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// Preserves the legacy OCR service contract while routing recognition to the provider explicitly selected by the user.
/// </summary>
public sealed class OcrService : IOcrService
{
    private readonly PaddleOcrProvider _paddle;
    private readonly TesseractOcrProvider _tesseract;
    private readonly RapidOcrNetProvider _rapid;
    private readonly IRapidOcrModelAssetManager _rapidAssets;
    private readonly SmartBpOcrProviderSelector _selector;
    private readonly ILogger<OcrService> _logger;

    /// <summary>Initializes the selected-provider OCR facade.</summary>
    /// <param name="paddle">Paddle provider.</param>
    /// <param name="tesseract">Tesseract provider.</param>
    /// <param name="rapid">RapidOCR provider.</param>
    /// <param name="rapidAssets">Managed RapidOCR model assets.</param>
    /// <param name="selector">Configured provider selector.</param>
    /// <param name="logger">Logger.</param>
    public OcrService(
        PaddleOcrProvider paddle,
        TesseractOcrProvider tesseract,
        RapidOcrNetProvider rapid,
        IRapidOcrModelAssetManager rapidAssets,
        SmartBpOcrProviderSelector selector,
        ILogger<OcrService> logger)
    {
        _paddle = paddle;
        _tesseract = tesseract;
        _rapid = rapid;
        _rapidAssets = rapidAssets;
        _selector = selector;
        _logger = logger;
    }

    /// <inheritdoc />
    public SmartBpOcrProviderKind SelectedProvider => _selector.SelectedProvider;

    /// <inheritdoc />
    public string? CurrentOcrModelKey => _paddle.CurrentOcrModelKey;
    /// <inheritdoc />
    public bool IsDownloading => _paddle.IsDownloading;
    /// <inheritdoc />
    public double? DownloadProgress => _paddle.DownloadProgress;
    /// <inheritdoc />
    public string DownloadStatusText => _paddle.DownloadStatusText;

    /// <inheritdoc />
    public event EventHandler? DownloadStateChanged
    {
        add => _paddle.DownloadStateChanged += value;
        remove => _paddle.DownloadStateChanged -= value;
    }

    /// <inheritdoc />
    public SmartBpOcrProviderStatus GetProviderStatus(SmartBpOcrProviderKind kind)
    {
        if (kind == SmartBpOcrProviderKind.Paddle)
            return new(kind, _paddle.IsReady, null, _paddle.IsReady ? "installed" : "missing");
        if (kind == SmartBpOcrProviderKind.Rapid)
        {
            var status = _rapidAssets.GetStatusAsync().GetAwaiter().GetResult();
            var runtimeReady = status.IsInstalled && _rapid.IsReady;
            var details = $"profile={status.ProfileId}; directory={status.ModelDirectory}; missing=[{string.Join(", ", status.MissingFiles)}]; fallback={status.IsUsingFallback}; installedVersion={status.InstalledVersion ?? "unknown"}; latestVersion={status.LatestVersion ?? "unknown"}; update={status.HasUpdate}";
            if (!runtimeReady && !string.IsNullOrWhiteSpace(_rapid.InitializationError)) details += $"; runtime={_rapid.InitializationError}";
            return new(kind, runtimeReady, status.ModelDirectory, details);
        }
        var missing = _tesseract.GetMissingLanguages();
        return new(
            kind,
            _tesseract.IsReady,
            _tesseract.EffectiveDataPath,
            !_tesseract.IsEnabled ? "disabled" : missing.Count == 0 ? "installed" : $"missing: {string.Join(", ", missing)}");
    }

    /// <inheritdoc />
    public IReadOnlyList<OcrModelDefinition> GetAvailableModels() => _paddle.GetAvailableModels();
    /// <inheritdoc />
    public bool IsModelInstalled(string modelKey) => _paddle.IsModelInstalled(modelKey);
    /// <inheritdoc />
    public Task DownloadModelAsync(string modelKey, CancellationToken cancellationToken = default) =>
        _paddle.DownloadModelAsync(modelKey, cancellationToken);
    /// <inheritdoc />
    public void CancelDownload() => _paddle.CancelDownload();
    /// <inheritdoc />
    public bool TryDeleteModel(string modelKey, out string errorMessage) =>
        _paddle.TryDeleteModel(modelKey, out errorMessage);
    /// <inheritdoc />
    public bool TrySwitchOcrModel(string modelKey, out string errorMessage) =>
        _paddle.TrySwitchOcrModel(modelKey, out errorMessage);

    /// <inheritdoc />
    public string? RecognizeText(Mat img)
    {
        if (SelectedProvider == SmartBpOcrProviderKind.Paddle)
        {
            if (!_paddle.IsReady)
            {
                _logger.LogWarning("Selected OCR provider Paddle is not ready; recognition was not downgraded.");
                return null;
            }
            return _paddle.RecognizeText(img);
        }
        var result = RecognizeTextLines(img);
        return string.IsNullOrWhiteSpace(result.FullText) ? null : result.FullText;
    }

    /// <inheritdoc />
    public OcrTextBlockResult RecognizeTextLines(Mat img)
    {
        var provider = _selector.GetSelectedProvider();
        if (!provider.IsReady)
        {
            _logger.LogWarning("Selected OCR provider {Provider} is not ready; recognition was not downgraded.", provider.Kind);
            return new([], string.Empty, provider.Kind.ToString());
        }
        return provider.RecognizeTextLines(img, new OcrRecognitionOptions
        {
            Psm = _selector.Settings.TesseractDefaultPsm,
            UsePreprocessingVariants = true
        });
    }
}

/// <summary>Selects exactly one configured OCR provider without automatic fallback.</summary>
public sealed class SmartBpOcrProviderSelector
{
    private readonly IReadOnlyDictionary<SmartBpOcrProviderKind, IOcrProvider> _providers;
    private readonly ISmartBpRecognitionSettingsService _settings;

    /// <summary>Initializes the provider selector.</summary>
    /// <param name="providers">Registered OCR providers.</param>
    /// <param name="settings">Recognition settings.</param>
    public SmartBpOcrProviderSelector(
        IEnumerable<IOcrProvider> providers,
        ISmartBpRecognitionSettingsService settings)
    {
        _providers = providers.ToDictionary(provider => provider.Kind);
        _settings = settings;
    }

    /// <summary>Gets the current recognition settings.</summary>
    public SmartBpRecognitionSettings Settings => _settings.Settings;

    /// <summary>Gets the selected provider kind.</summary>
    public SmartBpOcrProviderKind SelectedProvider => Settings.OcrProviderMode switch
    {
        SmartBpOcrProviderMode.Tesseract => SmartBpOcrProviderKind.Tesseract,
        SmartBpOcrProviderMode.Rapid => SmartBpOcrProviderKind.Rapid,
        _ => SmartBpOcrProviderKind.Paddle
    };

    /// <summary>Gets the selected provider and never substitutes another provider.</summary>
    /// <returns>The explicitly selected provider.</returns>
    public IOcrProvider GetSelectedProvider() => _providers.TryGetValue(SelectedProvider, out var provider)
        ? provider
        : throw new InvalidOperationException($"OCR provider {SelectedProvider} is not registered.");
}
