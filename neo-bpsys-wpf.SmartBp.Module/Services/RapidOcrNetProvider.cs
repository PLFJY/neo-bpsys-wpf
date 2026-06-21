using Microsoft.Extensions.Logging;
using System.IO;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.SmartBp.Module.Abstractions;
using OpenCvSharp;
using RapidOcrNet;
using SkiaSharp;

namespace neo_bpsys_wpf.Services;

internal sealed record RapidOcrRawLine(string Text, double Confidence, IReadOnlyList<Point> Polygon);

internal interface IRapidOcrEngine : IDisposable
{
    void Initialize(string detPath, string clsPath, string recPath, string dictPath);
    IReadOnlyList<RapidOcrRawLine> Detect(SKBitmap bitmap, RapidOcrOptions options);
}

internal sealed class RapidOcrEngine : IRapidOcrEngine
{
    private const int RapidOcrSessionThreadCount = 1;
    private RapidOcr? _ocr;

    public void Initialize(string detPath, string clsPath, string recPath, string dictPath)
    {
        // 防御：模型文件缺失时直接抛出可控异常，避免传入无效路径给原生代码导致 ExecutionEngineException
        ValidateModelFile(detPath, "det");
        ValidateModelFile(clsPath, "cls");
        ValidateModelFile(recPath, "rec");
        ValidateModelFile(dictPath, "dict");

        var replacement = new RapidOcr();
        try
        {
            replacement.InitModels(detPath, clsPath, recPath, dictPath, RapidOcrSessionThreadCount);
            _ocr?.Dispose();
            _ocr = replacement;
        }
        catch
        {
            replacement.Dispose();
            _ocr = null;
            throw;
        }
    }

    private static void ValidateModelFile(string path, string modelKind)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException($"RapidOCR {modelKind} model path is null or empty.");
        if (!File.Exists(path))
            throw new FileNotFoundException($"RapidOCR {modelKind} model file not found: {path}");
        try
        {
            if (new FileInfo(path).Length == 0)
                throw new InvalidDataException($"RapidOCR {modelKind} model file is empty: {path}");
        }
        catch (Exception ex) when (ex is not InvalidDataException)
        {
            throw new IOException($"Unable to read RapidOCR {modelKind} model file: {path}", ex);
        }
    }

    public IReadOnlyList<RapidOcrRawLine> Detect(SKBitmap bitmap, RapidOcrOptions options)
    {
        if (_ocr == null) throw new InvalidOperationException("RapidOCR runtime is not initialized.");
        return _ocr.Detect(bitmap, options).TextBlocks
            .Where(block => !string.IsNullOrWhiteSpace(block.Text))
            .Select(block => new RapidOcrRawLine(
                block.Text.Trim(),
                block.CharScores is { Length: > 0 } ? block.CharScores.Average(value => (double)value) : block.BoxScore,
                block.BoxPoints.Select(point => new Point(point.X, point.Y)).ToArray()))
            .ToArray();
    }

    public void Dispose() => _ocr?.Dispose();
}

/// <summary>Recognizes positioned text with RapidOcrNet and managed Chinese PP-OCR models.</summary>
public sealed class RapidOcrNetProvider : IOcrProvider, IDisposable
{
    private const string ProviderName = "RapidOCR";
    private readonly IRapidOcrModelAssetManager _assets;
    private readonly ISmartBpRecognitionSettingsService _settings;
    private readonly ILogger<RapidOcrNetProvider> _logger;
    private readonly IRapidOcrEngine _engine;
    private readonly object _sync = new();
    private string? _initializedKey;
    private string? _initializationError;

    /// <summary>Initializes the RapidOCR provider.</summary>
    /// <param name="assets">Managed model asset manager.</param>
    /// <param name="settings">Recognition settings.</param>
    /// <param name="logger">Logger.</param>
    public RapidOcrNetProvider(
        IRapidOcrModelAssetManager assets,
        ISmartBpRecognitionSettingsService settings,
        ILogger<RapidOcrNetProvider> logger)
        : this(assets, settings, logger, new RapidOcrEngine())
    {
    }

    internal RapidOcrNetProvider(
        IRapidOcrModelAssetManager assets,
        ISmartBpRecognitionSettingsService settings,
        ILogger<RapidOcrNetProvider> logger,
        IRapidOcrEngine engine)
    {
        _assets = assets;
        _settings = settings;
        _logger = logger;
        _engine = engine;
    }

    /// <inheritdoc />
    public SmartBpOcrProviderKind Kind => SmartBpOcrProviderKind.Rapid;

    /// <inheritdoc />
    public bool IsReady
    {
        get
        {
            try
            {
                var status = _assets.GetStatusAsync().GetAwaiter().GetResult();
                if (!status.IsInstalled)
                {
                    _initializationError = status.MissingFiles.Count == 0
                        ? "RapidOCR managed model is not installed."
                        : $"RapidOCR managed model is missing files: {string.Join(", ", status.MissingFiles)}";
                    return false;
                }

                if (!TryValidateManagedRuntime(out var runtimeError))
                {
                    _initializationError = runtimeError;
                    return false;
                }

                _initializationError = null;
                return true;
            }
            catch (Exception ex)
            {
                _initializationError = ex.Message;
                _logger.LogDebug(ex, "RapidOCR is not ready.");
                return false;
            }
        }
    }

    internal string? InitializationError => _initializationError;

    private static bool TryValidateManagedRuntime(out string? error)
    {
        try
        {
            _ = typeof(RapidOcr).Assembly.FullName;
            _ = typeof(RapidOcrOptions).Assembly.FullName;
            var onnxRuntimeType = Type.GetType("Microsoft.ML.OnnxRuntime.InferenceSession, Microsoft.ML.OnnxRuntime", throwOnError: false);
            if (onnxRuntimeType == null)
            {
                error = "Microsoft.ML.OnnxRuntime is not available for RapidOCR.";
                return false;
            }

            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = $"RapidOCR managed runtime is not available: {ex.Message}";
            return false;
        }
    }

    /// <inheritdoc />
    public OcrTextBlockResult RecognizeTextLines(Mat img, OcrRecognitionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(img);
        if (img.Empty()) return new([], string.Empty, ProviderName);
        lock (_sync)
        {
            EnsureInitializedUnsafe();
            var rapidOptions = BuildOptions();
            var rawLines = Detect(img, rapidOptions).ToList();
            if (_settings.Settings.RapidOcrUsePreprocessingVariants && options?.UsePreprocessingVariants != false)
            {
                using var variant = CreateContrastVariant(img);
                rawLines.AddRange(Detect(variant, rapidOptions));
            }

            var lines = NormalizeAndMerge(rawLines, img.Width, img.Height);
            _logger.LogInformation("provider={Provider} line_count={LineCount}", ProviderName, lines.Count);
            foreach (var line in lines)
                _logger.LogDebug("provider={Provider} text={Text} confidence={Confidence:F3} bbox={BoundingBox}",
                    ProviderName, line.Text, line.Confidence, line.BoundingBox);
            return new(lines, string.Join(Environment.NewLine, lines.Select(line => line.Text)), ProviderName);
        }
    }

    private IReadOnlyList<RapidOcrRawLine> Detect(Mat image, RapidOcrOptions options)
    {
        Cv2.ImEncode(".png", image, out var bytes);
        using var bitmap = SKBitmap.Decode(bytes) ?? throw new InvalidDataException("RapidOCR could not decode the input image.");
        return _engine.Detect(bitmap, options);
    }

    private RapidOcrOptions BuildOptions()
    {
        var settings = _settings.Settings;
        return RapidOcrOptions.Default with
        {
            Padding = settings.RapidOcrPadding,
            ImgResize = settings.RapidOcrMaxSideLen,
            BoxScoreThresh = (float)settings.RapidOcrBoxScoreThreshold,
            BoxThresh = (float)settings.RapidOcrBoxThreshold,
            UnClipRatio = (float)settings.RapidOcrUnclipRatio,
            DoAngle = settings.RapidOcrUseAngleClassifier
        };
    }

    private void EnsureInitialized()
    {
        lock (_sync) EnsureInitializedUnsafe();
    }

    private void EnsureInitializedUnsafe()
    {
        var paths = _assets.GetInstalledPathsAsync().GetAwaiter().GetResult();
        var key = string.Join('|', paths.DetPath, paths.ClsPath, paths.RecPath, paths.DictPath);
        if (string.Equals(_initializedKey, key, StringComparison.Ordinal)) return;
        try
        {
            _engine.Initialize(paths.DetPath, paths.ClsPath, paths.RecPath, paths.DictPath);
            _initializedKey = key;
            _initializationError = null;
            _logger.LogInformation("RapidOCR runtime initialized. Profile={ProfileId}, Directory={Directory}", paths.ProfileId, paths.Directory);
        }
        catch (Exception ex)
        {
            _initializedKey = null;
            _initializationError = ex.Message;
            _logger.LogError(ex, "RapidOCR initialization failed. Profile={ProfileId}, Directory={Directory}", paths.ProfileId, paths.Directory);
            throw new InvalidOperationException($"RapidOCR runtime is not available: {ex.Message}", ex);
        }
    }

    private static Mat CreateContrastVariant(Mat source)
    {
        using var gray = new Mat();
        if (source.Channels() == 1) source.CopyTo(gray);
        else Cv2.CvtColor(source, gray, source.Channels() == 4 ? ColorConversionCodes.BGRA2GRAY : ColorConversionCodes.BGR2GRAY);
        using var clahe = Cv2.CreateCLAHE(2.0, new Size(8, 8));
        using var enhanced = new Mat();
        clahe.Apply(gray, enhanced);
        var result = new Mat();
        Cv2.CvtColor(enhanced, result, ColorConversionCodes.GRAY2BGR);
        return result;
    }

    internal static IReadOnlyList<OcrTextLine> NormalizeAndMerge(IEnumerable<RapidOcrRawLine> rawLines, int width, int height)
    {
        var candidates = rawLines.Select(raw => Normalize(raw, width, height)).Where(line => line != null).Cast<OcrTextLine>()
            .OrderByDescending(line => line.Confidence).ToList();
        var selected = new List<OcrTextLine>();
        foreach (var candidate in candidates)
        {
            var duplicate = selected.Any(existing =>
                string.Equals(existing.Text, candidate.Text, StringComparison.OrdinalIgnoreCase) && IntersectionOverUnion(existing.BoundingBox, candidate.BoundingBox) >= .5);
            if (!duplicate) selected.Add(candidate);
        }
        return selected.OrderBy(line => line.BoundingBox.Y).ThenBy(line => line.BoundingBox.X).ToArray();
    }

    private static OcrTextLine? Normalize(RapidOcrRawLine raw, int width, int height)
    {
        if (string.IsNullOrWhiteSpace(raw.Text) || raw.Polygon.Count == 0 || width <= 0 || height <= 0) return null;
        var left = Math.Clamp(raw.Polygon.Min(point => point.X), 0, width - 1);
        var top = Math.Clamp(raw.Polygon.Min(point => point.Y), 0, height - 1);
        var right = Math.Clamp(raw.Polygon.Max(point => point.X), left + 1, width);
        var bottom = Math.Clamp(raw.Polygon.Max(point => point.Y), top + 1, height);
        var box = new Rect(left, top, right - left, bottom - top);
        return new(raw.Text.Trim(), Math.Clamp(raw.Confidence, 0, 1), box,
            box.X + box.Width / 2D, box.Y + box.Height / 2D, ProviderName);
    }

    private static double IntersectionOverUnion(Rect left, Rect right)
    {
        var x1 = Math.Max(left.Left, right.Left);
        var y1 = Math.Max(left.Top, right.Top);
        var x2 = Math.Min(left.Right, right.Right);
        var y2 = Math.Min(left.Bottom, right.Bottom);
        var intersection = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
        var union = left.Width * left.Height + right.Width * right.Height - intersection;
        return union <= 0 ? 0 : (double)intersection / union;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_sync) _engine.Dispose();
    }
}
