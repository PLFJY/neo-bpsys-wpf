using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.SmartBp.Module.Abstractions;
using neo_bpsys_wpf.SmartBp.Module.Models.Recognition;
using OpenCvSharp;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 保留旧版 OCR 服务契约，同时把识别请求路由到用户显式选择的提供程序。
/// </summary>
public sealed class OcrService : IOcrService
{
    private readonly PaddleOcrProvider _paddle;
    private readonly TesseractOcrProvider _tesseract;
    private readonly RapidOcrNetProvider _rapid;
    private readonly IRapidOcrModelAssetManager _rapidAssets;
    private readonly SmartBpOcrProviderSelector _selector;
    private readonly ILogger<OcrService> _logger;

    /// <summary>初始化基于已选提供程序的 OCR 外观服务。</summary>
    /// <param name="paddle">Paddle OCR 提供程序。</param>
    /// <param name="tesseract">Tesseract OCR 提供程序。</param>
    /// <param name="rapid">RapidOCR 提供程序。</param>
    /// <param name="rapidAssets">RapidOCR 托管模型资产管理器。</param>
    /// <param name="selector">已配置的提供程序选择器。</param>
    /// <param name="logger">日志记录器。</param>
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

/// <summary>选择唯一一个已配置的 OCR 提供程序，不做自动降级。</summary>
public sealed class SmartBpOcrProviderSelector
{
    private readonly IReadOnlyDictionary<SmartBpOcrProviderKind, IOcrProvider> _providers;
    private readonly ISmartBpRecognitionSettingsService _settings;

    /// <summary>初始化 OCR 提供程序选择器。</summary>
    /// <param name="providers">已注册的 OCR 提供程序集合。</param>
    /// <param name="settings">识别设置服务。</param>
    public SmartBpOcrProviderSelector(
        IEnumerable<IOcrProvider> providers,
        ISmartBpRecognitionSettingsService settings)
    {
        _providers = providers.ToDictionary(provider => provider.Kind);
        _settings = settings;
    }

    /// <summary>获取当前识别设置。</summary>
    public SmartBpRecognitionSettings Settings => _settings.Settings;

    /// <summary>获取当前选中的提供程序类型。</summary>
    public SmartBpOcrProviderKind SelectedProvider => Settings.OcrProviderMode switch
    {
        SmartBpOcrProviderMode.Tesseract => SmartBpOcrProviderKind.Tesseract,
        SmartBpOcrProviderMode.Rapid => SmartBpOcrProviderKind.Rapid,
        _ => SmartBpOcrProviderKind.Paddle
    };

    /// <summary>获取当前选中的提供程序，且绝不替换为其他提供程序。</summary>
    /// <returns>用户显式选择的 OCR 提供程序。</returns>
    public IOcrProvider GetSelectedProvider() => _providers.TryGetValue(SelectedProvider, out var provider)
        ? provider
        : throw new InvalidOperationException($"OCR provider {SelectedProvider} is not registered.");
}
