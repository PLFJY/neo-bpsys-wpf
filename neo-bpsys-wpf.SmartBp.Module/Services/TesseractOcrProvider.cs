using Microsoft.Extensions.Logging;
using System.IO;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.SmartBp.Module.Abstractions;
using OpenCvSharp;
using Tesseract;
using TesseractRect = Tesseract.Rect;
using OpenCvRect = OpenCvSharp.Rect;

namespace neo_bpsys_wpf.Services;

/// <summary>Recognizes positioned text using a locally installed Tesseract runtime.</summary>
public sealed class TesseractOcrProvider : IOcrProvider, IDisposable
{
    private readonly ISmartBpRecognitionSettingsService _settingsService;
    private readonly ILogger<TesseractOcrProvider> _logger;
    private readonly Lock _engineLock = new();
    private TesseractEngine? _engine;
    private string? _engineKey;

    /// <summary>Initializes the Tesseract OCR provider.</summary>
    /// <param name="settingsService">Recognition settings service.</param>
    /// <param name="logger">Logger.</param>
    public TesseractOcrProvider(
        ISmartBpRecognitionSettingsService settingsService,
        ILogger<TesseractOcrProvider> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    /// <inheritdoc />
    public SmartBpOcrProviderKind Kind => SmartBpOcrProviderKind.Tesseract;

    /// <summary>Gets the effective tessdata directory.</summary>
    public string EffectiveDataPath
    {
        get
        {
            var fallback = Path.Combine(AppConstants.AppDataPath, "SmartBp", "Tesseract", "tessdata");
            if (string.IsNullOrWhiteSpace(_settingsService.Settings.TesseractDataPath))
                return fallback;
            try { return Path.GetFullPath(_settingsService.Settings.TesseractDataPath); }
            catch { return fallback; }
        }
    }

    /// <inheritdoc />
    public bool IsReady => _settingsService.Settings.EnableTesseractOcr && GetMissingLanguages().Count == 0;

    /// <summary>Gets whether Tesseract is enabled in recognition settings.</summary>
    public bool IsEnabled => _settingsService.Settings.EnableTesseractOcr;

    /// <summary>Gets configured language data files that are not installed.</summary>
    /// <returns>Missing language identifiers.</returns>
    public IReadOnlyList<string> GetMissingLanguages() => ParseLanguages(_settingsService.Settings.TesseractLanguages)
        .Where(language => !File.Exists(Path.Combine(EffectiveDataPath, $"{language}.traineddata")))
        .ToArray();

    /// <inheritdoc />
    public OcrTextBlockResult RecognizeTextLines(Mat img, OcrRecognitionOptions? options = null)
    {
        if (img.Empty())
            return new([], string.Empty, "Tesseract");
        if (!IsReady)
        {
            _logger.LogWarning(
                "Tesseract is not ready. dataPath={DataPath}; missing=[{Missing}]",
                EffectiveDataPath, string.Join(",", GetMissingLanguages()));
            return new([], string.Empty, "Tesseract");
        }

        options ??= new OcrRecognitionOptions { Psm = _settingsService.Settings.TesseractDefaultPsm };
        var maxVariants = options.UsePreprocessingVariants
            ? Math.Clamp(_settingsService.Settings.TesseractMaxPreprocessVariants, 1, 3)
            : 1;
        var allLines = new List<OcrTextLine>();
        try
        {
            lock (_engineLock)
            {
                var engine = GetOrCreateEngineUnsafe();
                var variants = CreateVariants(img);
                try
                {
                    foreach (var variant in variants.Take(maxVariants))
                    {
                        try
                        {
                            allLines.AddRange(RecognizeVariant(engine, variant, img.Width, img.Height, options.Psm));
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Tesseract variant {Variant} failed.", variant.Name);
                            RebuildEngineUnsafe();
                            engine = GetOrCreateEngineUnsafe();
                        }
                    }
                }
                finally
                {
                    foreach (var variant in variants)
                        variant.Image.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tesseract recognition failed.");
            return new([], string.Empty, "Tesseract");
        }

        var lines = MergeVariantLines(allLines);
        return new(lines, string.Join(Environment.NewLine, lines.Select(line => line.Text)), "Tesseract");
    }

    /// <summary>Releases the cached native OCR engine.</summary>
    public void Dispose()
    {
        lock (_engineLock)
        {
            _engine?.Dispose();
            _engine = null;
            _engineKey = null;
        }
    }

    private TesseractEngine GetOrCreateEngineUnsafe()
    {
        var languages = NormalizeLanguageExpression(_settingsService.Settings.TesseractLanguages);
        var key = $"{EffectiveDataPath}|{languages}";
        if (_engine != null && string.Equals(_engineKey, key, StringComparison.Ordinal))
            return _engine;
        _engine?.Dispose();
        _engine = new TesseractEngine(EffectiveDataPath, languages, EngineMode.Default);
        _engineKey = key;
        return _engine;
    }

    private void RebuildEngineUnsafe()
    {
        _engine?.Dispose();
        _engine = null;
        _engineKey = null;
    }

    private IReadOnlyList<OcrTextLine> RecognizeVariant(
        TesseractEngine engine,
        TesseractVariant variant,
        int inputWidth,
        int inputHeight,
        int psm)
    {
        Cv2.ImEncode(".png", variant.Image, out var bytes);
        using var pix = Pix.LoadFromMemory(bytes);
        using var page = engine.Process(pix, ToPageSegMode(psm));
        using var iterator = page.GetIterator();
        var lines = new List<OcrTextLine>();
        iterator.Begin();
        do
        {
            var text = iterator.GetText(PageIteratorLevel.TextLine)?.Trim();
            if (string.IsNullOrWhiteSpace(text) ||
                !iterator.TryGetBoundingBox(PageIteratorLevel.TextLine, out TesseractRect tesseractBox))
                continue;
            var mapped = TesseractCoordinateMapper.MapToOriginal(
                new OpenCvRect(tesseractBox.X1, tesseractBox.Y1, tesseractBox.Width, tesseractBox.Height),
                variant.ScaleX, variant.ScaleY, inputWidth, inputHeight);
            var centerX = mapped.X + mapped.Width / 2d;
            var centerY = mapped.Y + mapped.Height / 2d;
            var confidence = Math.Clamp(iterator.GetConfidence(PageIteratorLevel.TextLine) / 100d, 0, 1);
            _logger.LogDebug(
                "provider=Tesseract; variant={Variant}; input={Width}x{Height}; line bbox={Box}; center={CenterX:0.0},{CenterY:0.0}; confidence={Confidence:0.00}",
                variant.Name, inputWidth, inputHeight, mapped, centerX, centerY, confidence);
            lines.Add(new(text, confidence, mapped, centerX, centerY, "Tesseract"));
        } while (iterator.Next(PageIteratorLevel.TextLine));
        return lines;
    }

    private static IReadOnlyList<TesseractVariant> CreateVariants(Mat input)
    {
        var original = new Mat();
        if (input.Channels() == 4)
            Cv2.CvtColor(input, original, ColorConversionCodes.BGRA2BGR);
        else if (input.Channels() == 1)
            Cv2.CvtColor(input, original, ColorConversionCodes.GRAY2BGR);
        else
            input.CopyTo(original);

        using var gray = new Mat();
        Cv2.CvtColor(original, gray, ColorConversionCodes.BGR2GRAY);
        using var enlargedGray = new Mat();
        Cv2.Resize(gray, enlargedGray, new Size(), 3, 3, InterpolationFlags.Cubic);
        var claheImage = new Mat();
        using (var clahe = Cv2.CreateCLAHE(2, new Size(8, 8)))
            clahe.Apply(enlargedGray, claheImage);

        var threshold = new Mat();
        var thresholdType = Cv2.Mean(enlargedGray).Val0 < 110
            ? ThresholdTypes.BinaryInv
            : ThresholdTypes.Binary;
        Cv2.AdaptiveThreshold(
            enlargedGray, threshold, 255, AdaptiveThresholdTypes.GaussianC,
            thresholdType, 31, 7);
        return
        [
            new("original", original, 1, 1),
            new("upscale-clahe", claheImage, 3, 3),
            new("upscale-adaptive", threshold, 3, 3)
        ];
    }

    private static IReadOnlyList<OcrTextLine> MergeVariantLines(IEnumerable<OcrTextLine> source)
    {
        var merged = new List<OcrTextLine>();
        foreach (var line in source.OrderByDescending(item => item.Confidence))
        {
            var duplicate = merged.Any(existing =>
                string.Equals(Normalize(existing.Text), Normalize(line.Text), StringComparison.Ordinal) &&
                IntersectionOverUnion(existing.BoundingBox, line.BoundingBox) >= .45);
            if (!duplicate)
                merged.Add(line);
        }
        return merged.OrderBy(line => line.CenterY).ThenBy(line => line.CenterX).ToArray();
    }

    private static double IntersectionOverUnion(OpenCvRect left, OpenCvRect right)
    {
        var intersection = left & right;
        if (intersection.Width <= 0 || intersection.Height <= 0)
            return 0;
        var intersectionArea = intersection.Width * intersection.Height;
        var unionArea = left.Width * left.Height + right.Width * right.Height - intersectionArea;
        return unionArea <= 0 ? 0 : intersectionArea / (double)unionArea;
    }

    private static string Normalize(string value) =>
        string.Concat(value.Normalize().Where(character => !char.IsWhiteSpace(character) && !char.IsPunctuation(character)));

    private static PageSegMode ToPageSegMode(int psm) =>
        Enum.IsDefined(typeof(PageSegMode), psm) && psm >= 0 && psm < (int)PageSegMode.Count
            ? (PageSegMode)psm
            : PageSegMode.SingleBlock;

    private static IReadOnlyList<string> ParseLanguages(string? languages) =>
        (string.IsNullOrWhiteSpace(languages) ? "chi_sim+eng" : languages)
        .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private static string NormalizeLanguageExpression(string? languages) => string.Join('+', ParseLanguages(languages));

    private sealed record TesseractVariant(string Name, Mat Image, double ScaleX, double ScaleY);
}

/// <summary>Maps Tesseract variant coordinates into the original input image.</summary>
internal static class TesseractCoordinateMapper
{
    /// <summary>Maps and clamps a variant-local rectangle.</summary>
    internal static OpenCvRect MapToOriginal(OpenCvRect box, double scaleX, double scaleY, int width, int height)
    {
        var left = Math.Clamp((int)Math.Floor(box.Left / scaleX), 0, width);
        var top = Math.Clamp((int)Math.Floor(box.Top / scaleY), 0, height);
        var right = Math.Clamp((int)Math.Ceiling(box.Right / scaleX), left, width);
        var bottom = Math.Clamp((int)Math.Ceiling(box.Bottom / scaleY), top, height);
        return new OpenCvRect(left, top, right - left, bottom - top);
    }
}
