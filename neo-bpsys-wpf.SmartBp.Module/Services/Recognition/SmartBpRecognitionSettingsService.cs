using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.SmartBp.Module.Abstractions;
using neo_bpsys_wpf.SmartBp.Module.Models.Recognition;

namespace neo_bpsys_wpf.SmartBp.Module.Services.Recognition;

/// <summary>
/// 读取、规范化并持久化 SmartBP 的 OCR 识别设置。
/// </summary>
internal sealed class SmartBpRecognitionSettingsService : ISmartBpRecognitionSettingsService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _path = Path.Combine(AppConstants.AppDataPath, "SmartBp", "RecognitionSettings.json");
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    /// <inheritdoc />
    public SmartBpRecognitionSettings Settings { get; private set; }

    /// <summary>
    /// 初始化 OCR 设置服务，并将历史 AI 策略值规范化为 OCR。
    /// </summary>
    public SmartBpRecognitionSettingsService()
    {
        try
        {
            var json = File.Exists(_path) ? File.ReadAllText(_path) : null;
            Settings = string.IsNullOrWhiteSpace(json)
                ? new SmartBpRecognitionSettings()
                : JsonSerializer.Deserialize<SmartBpRecognitionSettings>(NormalizeLegacyRecognitionJson(json), Options)
                    ?? new SmartBpRecognitionSettings();
        }
        catch
        {
            Settings = new SmartBpRecognitionSettings();
        }

        Settings.RecognitionStrategy = SmartBpRecognitionStrategy.PureOcr;
        Settings.RecognitionEngine = SmartBpRecognitionEngine.Ocr;
        Settings.OcrRecognitionIntervalMs = Math.Clamp(Settings.OcrRecognitionIntervalMs, 100, 5000);
        Settings.OcrFieldStaleMilliseconds = Math.Clamp(Settings.OcrFieldStaleMilliseconds, 250, 30000);
        Settings.OcrBackfillLookBehindSteps = Math.Clamp(Settings.OcrBackfillLookBehindSteps, 0, 20);
        Settings.RecognitionBackfillLookBehindSteps = Math.Clamp(Settings.RecognitionBackfillLookBehindSteps, 0, 20);
        Settings.RecognitionFieldStaleMilliseconds = Math.Clamp(Settings.RecognitionFieldStaleMilliseconds, 250, 30000);
        Settings.RecognitionFrameBufferMilliseconds = Math.Clamp(Settings.RecognitionFrameBufferMilliseconds, 250, 5000);
        Settings.RecognitionTransitionLookBehindMilliseconds = Math.Clamp(Settings.RecognitionTransitionLookBehindMilliseconds, 100, 5000);
        Settings.RecognitionTransitionReplayMinimumConfidence = Math.Clamp(Settings.RecognitionTransitionReplayMinimumConfidence, 0, 1);
        Settings.RecognitionCropChangeThreshold = Math.Clamp(Settings.RecognitionCropChangeThreshold, .001, 1);
        Settings.RecognitionCropStableFrames = Math.Clamp(Settings.RecognitionCropStableFrames, 1, 10);
        Settings.RequiredStableSnapshots = Math.Clamp(Settings.RequiredStableSnapshots, 1, 5);
        Settings.GuidanceSyncLookAheadSteps = Math.Clamp(Settings.GuidanceSyncLookAheadSteps, 1, 20);
        Settings.SmartBpProgressMismatchConfirmationCount = Math.Clamp(Settings.SmartBpProgressMismatchConfirmationCount, 1, 10);
        Settings.SmartBpProgressAutoCorrectionCooldownMs = Math.Clamp(Settings.SmartBpProgressAutoCorrectionCooldownMs, 1000, 60000);
        Settings.SmartBpProgressInferenceMinimumScore = Math.Clamp(Settings.SmartBpProgressInferenceMinimumScore, 0, 1);
        Settings.SmartBpProgressInferenceMinimumScoreMargin = Math.Clamp(Settings.SmartBpProgressInferenceMinimumScoreMargin, 0, 1);
        Settings.TesseractDefaultPsm = Math.Clamp(Settings.TesseractDefaultPsm, 0, 13);
        Settings.TesseractMaxPreprocessVariants = Math.Clamp(Settings.TesseractMaxPreprocessVariants, 1, 3);
        Settings.TesseractLanguages = string.IsNullOrWhiteSpace(Settings.TesseractLanguages) ? "chi_sim+eng" : Settings.TesseractLanguages.Trim();
        Settings.SelectedRapidOcrModelId = string.IsNullOrWhiteSpace(Settings.SelectedRapidOcrModelId) ? "ppocr-v5-zh-mobile" : Settings.SelectedRapidOcrModelId.Trim();
        Settings.RapidOcrPadding = Math.Clamp(Settings.RapidOcrPadding, 0, 256);
        Settings.RapidOcrMaxSideLen = Math.Clamp(Settings.RapidOcrMaxSideLen, 320, 4096);
        Settings.RapidOcrBoxScoreThreshold = Math.Clamp(Settings.RapidOcrBoxScoreThreshold, 0, 1);
        Settings.RapidOcrBoxThreshold = Math.Clamp(Settings.RapidOcrBoxThreshold, 0, 1);
        Settings.RapidOcrUnclipRatio = Math.Clamp(Settings.RapidOcrUnclipRatio, .1, 5);
    }

    /// <inheritdoc />
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await _saveLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temporary = _path + ".tmp";
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(Settings, Options), cancellationToken).ConfigureAwait(false);
            File.Move(temporary, _path, true);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    private static string NormalizeLegacyRecognitionJson(string json)
    {
        try
        {
            if (JsonNode.Parse(json) is not JsonObject node)
                return json;
            SetCaseInsensitive(node, "recognitionStrategy", (int)SmartBpRecognitionStrategy.PureOcr);
            SetCaseInsensitive(node, "recognitionEngine", (int)SmartBpRecognitionEngine.Ocr);
            return node.ToJsonString();
        }
        catch
        {
            return json;
        }
    }

    private static void SetCaseInsensitive(JsonObject node, string propertyName, JsonNode? value)
    {
        var existing = node.Select(item => item.Key)
            .FirstOrDefault(key => string.Equals(key, propertyName, StringComparison.OrdinalIgnoreCase));
        node[existing ?? propertyName] = value;
    }
}
