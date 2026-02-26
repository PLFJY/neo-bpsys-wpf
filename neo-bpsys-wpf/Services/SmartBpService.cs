using F23.StringSimilarity;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Helpers;
using OpenCvSharp;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 智慧 BP 服务实现。
/// 负责从窗口捕获帧中提取赛后统计数据，执行 OCR 识别并回填到当前对局数据。
/// </summary>
public class SmartBpService : ISmartBpService
{
    private readonly ISharedDataService _sharedDataService;
    private readonly IWindowCaptureService _windowCaptureService;
    private readonly IOcrService _ocrService;
    private readonly ISmartBpRegionConfigService _regionConfigService;
    private readonly ILogger<SmartBpService> _logger;
    private int _ocrWarmupStarted;

    public bool IsSmartBpRunning { get; private set; }

    public SmartBpService(
        ISharedDataService sharedDataService,
        IWindowCaptureService windowCaptureService,
        IOcrService ocrService,
        ISmartBpRegionConfigService regionConfigService,
        ILogger<SmartBpService> logger)
    {
        _sharedDataService = sharedDataService;
        _windowCaptureService = windowCaptureService;
        _ocrService = ocrService;
        _regionConfigService = regionConfigService;
        _logger = logger;
    }

    public void StartSmartBp()
    {
        // SmartBp 依赖 OCR 模型，未就绪时直接阻止启动。
        if (!IsOcrReady())
        {
            IsSmartBpRunning = false;
            _ = MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString("SmartBpOcrNotReadyFirstDownloadAndSwitchModel"));
            return;
        }

        IsSmartBpRunning = true;
        StartOcrWarmupIfNeeded();
    }

    public void StopSmartBp()
    {
        IsSmartBpRunning = false;
    }

    public async Task AutoFillGameDataAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!IsOcrReady())
            {
                _logger.LogWarning("SmartBp AutoFill skipped: OCR model is not ready.");
                await MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString("SmartBpOcrNotReadyFirstDownloadAndSwitchModel"));
                return;
            }

            if (!_windowCaptureService.IsCapturing)
            {
                _logger.LogInformation("SmartBp AutoFill skipped: capture is not running.");
                return;
            }

            var recognizedData = await Task.Run(
                () => CaptureAndRecognizeGameData(cancellationToken),
                cancellationToken);

            if (recognizedData == null)
            {
                _logger.LogInformation("SmartBp AutoFill finished with no result.");
                return;
            }

            ApplyRecognizedData(recognizedData);
            _logger.LogInformation("SmartBp AutoFill succeeded: {SurvivorCount} survivor rows applied.", recognizedData.SurvivorInfos.Count);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("SmartBp AutoFill canceled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SmartBp AutoFill failed with exception.");
        }
    }

    private void StartOcrWarmupIfNeeded()
    {
        // 预热只执行一次，降低首次真实识别的延迟尖峰。
        if (Interlocked.Exchange(ref _ocrWarmupStarted, 1) == 1)
            return;

        _ = Task.Run(() =>
        {
            try
            {
                using var warmup1 = new Mat(new Size(192, 64), MatType.CV_8UC1, Scalar.All(255));
                using var warmup2 = new Mat(new Size(512, 96), MatType.CV_8UC1, Scalar.All(255));
                _ = _ocrService.RecognizeText(warmup1);
                _ = _ocrService.RecognizeText(warmup2);
            }
            catch
            {
                // 预热失败不影响主流程。
            }
        });
    }

    private RecognizedGameData? CaptureAndRecognizeGameData(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // 获取当前捕获帧（客户端区域截图）。
        var frame = _windowCaptureService.GetCurrentFrame();
        if (frame == null)
            return null;

        using var full = frame.ToBgrMat();
        // 识别区域来自可编辑配置，不再依赖硬编码像素。
        var profile = _regionConfigService.GetCurrentGameDataProfile();
        if (!TryResolveGameDataRows(profile, out var hunterRowProfile, out var survivorRowProfiles))
            return null;

        using var hunterRow = new Mat(full, hunterRowProfile.Rect.ToPixelRect(full.Width, full.Height));
        var hunterData = GetHunInfo(hunterRow, hunterRowProfile).PlayerData;

        var survivorInfos = new List<PlayerInfo>();
        foreach (var rowProfile in survivorRowProfiles)
        {
            using var survivorRow = new Mat(full, rowProfile.Rect.ToPixelRect(full.Width, full.Height));
            var rowInfo = GetSurInfo(survivorRow, rowProfile);
            survivorInfos.Add(rowInfo);
        }

        return new RecognizedGameData(hunterData, survivorInfos);
    }

    /// <summary>
    /// 解析 GameData 行映射。
    /// 优先按稳定 ID（row0_hunter / rowX_survivor）定位，避免因顺序变化导致识别错位。
    /// </summary>
    private static bool TryResolveGameDataRows(
        SmartBpRegionProfile profile,
        out RegionLayoutNode hunterRow,
        out List<RegionLayoutNode> survivorRows)
    {
        hunterRow = null!;
        survivorRows = [];

        if (profile.Layout.Roots.Count == 0)
            return false;

        hunterRow = profile.Layout.Roots.FirstOrDefault(r =>
                        string.Equals(r.Id, "row0_hunter", StringComparison.OrdinalIgnoreCase))
                    ?? profile.Layout.Roots.First();
        var resolvedHunter = hunterRow;

        survivorRows = profile.Layout.Roots
            .Where(r => !ReferenceEquals(r, resolvedHunter))
            .OrderBy(r =>
            {
                var m = Regex.Match(r.Id ?? string.Empty, @"row(\d+)_survivor", RegexOptions.IgnoreCase);
                return m.Success && int.TryParse(m.Groups[1].Value, out var n) ? n : int.MaxValue;
            })
            .Take(4)
            .ToList();

        return survivorRows.Count > 0;
    }

    private PlayerInfo GetSurInfo(Mat surRow, RegionLayoutNode rowProfile)
    {
        var (playerName, characterName) = GetSurPlayerNameAndCharacterName(surRow, rowProfile);
        var data = GetSurPlayerData(surRow, rowProfile);
        _logger.LogInformation(
            "SmartBp OCR Survivor row={RowId}: player={PlayerName}, character={CharacterName}, values=[{Decoding},{Pallet},{Rescue},{Heal},{Contain}]",
            rowProfile.Id,
            ToLogText(playerName),
            ToLogText(characterName),
            data.DecodingProgress,
            data.PalletStrikes,
            data.Rescues,
            data.Heals,
            data.ContainmentTime);
        return new PlayerInfo(playerName, characterName, data);
    }

    private void ApplyRecognizedData(RecognizedGameData recognizedData)
    {
        // 监管者数据直接按字段写回。
        _sharedDataService.CurrentGame.HunPlayer.Data.RemainingCipher = recognizedData.HunterData.RemainingCipher;
        _sharedDataService.CurrentGame.HunPlayer.Data.PalletsDestroyed = recognizedData.HunterData.PalletsDestroyed;
        _sharedDataService.CurrentGame.HunPlayer.Data.SurvivorHits = recognizedData.HunterData.SurvivorHits;
        _sharedDataService.CurrentGame.HunPlayer.Data.TerrorShocks = recognizedData.HunterData.TerrorShocks;
        _sharedDataService.CurrentGame.HunPlayer.Data.Knockdowns = recognizedData.HunterData.Knockdowns;

        var jw = new JaroWinkler();
        const double threshold = 0.50;

        // 求生者通过角色名匹配目标对象。先做规范化精确匹配，再做近似匹配兜底。
        foreach (var survivorInfo in recognizedData.SurvivorInfos)
        {
            var target = survivorInfo.CharacterName;
            var sur = _sharedDataService.CurrentGame.SurPlayerList
                .FirstOrDefault(p => NormalizeName(p.Character?.Name) == target);
            var matchMode = "exact";
            var fuzzyScore = 1.0;

            if (sur == null)
            {
                var fuzzy = _sharedDataService.CurrentGame.SurPlayerList
                    .Select(p => new
                    {
                        Player = p,
                        Name = NormalizeName(p.Character?.Name)
                    })
                    .Where(x => !string.IsNullOrEmpty(x.Name))
                    .Select(x => new { x.Player, Score = jw.Similarity(x.Name, target) })
                    .OrderByDescending(x => x.Score)
                    .FirstOrDefault(x => x.Score >= threshold);
                sur = fuzzy?.Player;
                fuzzyScore = fuzzy?.Score ?? 0.0;
                matchMode = "fuzzy";
            }

            if (sur == null)
            {
                _logger.LogWarning(
                    "SmartBp Match failed: recognizedCharacter={Character}, threshold={Threshold}",
                    ToLogText(target),
                    threshold);
                continue;
            }

            _logger.LogInformation(
                "SmartBp Match success: mode={Mode}, recognizedCharacter={Character}, mappedTo={MappedCharacter}, score={Score:F3}",
                matchMode,
                ToLogText(target),
                ToLogText(sur.Character?.Name),
                fuzzyScore);

            sur.Data.DecodingProgress = survivorInfo.PlayerData.DecodingProgress;
            sur.Data.PalletStrikes = survivorInfo.PlayerData.PalletStrikes;
            sur.Data.Rescues = survivorInfo.PlayerData.Rescues;
            sur.Data.Heals = survivorInfo.PlayerData.Heals;
            sur.Data.ContainmentTime = survivorInfo.PlayerData.ContainmentTime;
        }
    }

    private bool IsOcrReady()
    {
        var currentModelKey = _ocrService.CurrentOcrModelKey;
        return !string.IsNullOrWhiteSpace(currentModelKey) &&
               _ocrService.IsModelInstalled(currentModelKey);
    }

    public PlayerInfo GetHunInfo(Mat hunter, RegionLayoutNode rowProfile)
    {
        var hunCharacterName = GetHunPlayerNameAndCharacterName(hunter, rowProfile);
        var data = GetHunPlayerData(hunter, rowProfile);
        _logger.LogInformation(
            "SmartBp OCR Hunter row={RowId}: player={PlayerName}, character={CharacterName}, values=[{Cipher},{Pallets},{Hits},{Terror},{Knockdowns}]",
            rowProfile.Id,
            ToLogText(hunCharacterName.Item1),
            ToLogText(hunCharacterName.Item2),
            data.RemainingCipher,
            data.PalletsDestroyed,
            data.SurvivorHits,
            data.TerrorShocks,
            data.Knockdowns);
        return new PlayerInfo(hunCharacterName.Item1, hunCharacterName.Item2, data);
    }

    public (string, string) GetSurPlayerNameAndCharacterName(Mat surRow, RegionLayoutNode rowProfile)
    {
        var nameCell = GetCellById(rowProfile, "name", 0);
        if (nameCell == null)
            return (string.Empty, string.Empty);

        var nameRect = nameCell.Rect.ToPixelRect(surRow.Width, surRow.Height);
        using var name = new Mat(surRow, nameRect);
        using var bin = PreprocessForText(name);
        var text = _ocrService.RecognizeText(bin) ?? string.Empty;
        _logger.LogInformation("SmartBp OCR Survivor Name row={RowId}: raw={Raw}", rowProfile.Id, ToLogText(text));
        return GetPlayerNameAndCharacterName(text);
    }

    private PlayerData GetSurPlayerData(Mat surRow, RegionLayoutNode rowProfile)
    {
        // cells[1..5] 固定映射 5 个数据字段。
        var values = GetRowDataValues(surRow, rowProfile);
        return new PlayerData
        {
            DecodingProgress = values[0],
            PalletStrikes = values[1],
            Rescues = values[2],
            Heals = values[3],
            ContainmentTime = values[4]
        };
    }

    public (string, string) GetHunPlayerNameAndCharacterName(Mat hunter, RegionLayoutNode rowProfile)
    {
        // 监管者与求生者共用“名称+角色”解析规则。
        var nameCell = GetCellById(rowProfile, "name", 0);
        if (nameCell == null)
            return (string.Empty, string.Empty);

        var nameRect = nameCell.Rect.ToPixelRect(hunter.Width, hunter.Height);
        using var name = new Mat(hunter, nameRect);
        using var bin = PreprocessForText(name);
        var hunterText = _ocrService.RecognizeText(bin) ?? string.Empty;
        _logger.LogInformation("SmartBp OCR Hunter Name row={RowId}: raw={Raw}", rowProfile.Id, ToLogText(hunterText));
        return GetPlayerNameAndCharacterName(hunterText);
    }

    private static (string, string) GetPlayerNameAndCharacterName(string playerText)
    {
        playerText = playerText.Replace('（', '(').Replace('）', ')');

        var match = Regex.Match(playerText, @"^([^()]*?)\(([^)]*)\)");
        var playerName = match.Success ? match.Groups[1].Value.Trim() : string.Empty;
        var characterName = match.Success ? match.Groups[2].Value : string.Empty;
        playerName = NormalizeName(playerName);
        characterName = NormalizeName(characterName);
        return (playerName, characterName);
    }

    private static string NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        name = name.Trim().ToLowerInvariant();
        name = name.Normalize(NormalizationForm.FormKC);
        return new string(name.Where(char.IsLetterOrDigit).ToArray());
    }

    public PlayerData GetHunPlayerData(Mat hunter, RegionLayoutNode rowProfile)
    {
        var values = GetRowDataValues(hunter, rowProfile);
        return new PlayerData
        {
            RemainingCipher = values[0],
            PalletsDestroyed = values[1],
            SurvivorHits = values[2],
            TerrorShocks = values[3],
            Knockdowns = values[4]
        };
    }

    private string[] GetRowDataValues(Mat row, RegionLayoutNode rowProfile)
    {
        // 优先尝试“整行数字拼条一次 OCR”，性能更好且通常更稳定。
        var dataColumns = GetDataRects(rowProfile);
        if (dataColumns.Length != 5)
            return ["", "", "", "", ""];

        var batchValues = TryGetRowDataValuesBySingleOcr(row, dataColumns, out var batchRawText);
        if (batchValues != null)
        {
            _logger.LogInformation(
                "SmartBp OCR RowData batch row={RowId}: raw={Raw}, parsed=[{D1},{D2},{D3},{D4},{D5}]",
                rowProfile.Id,
                ToLogText(batchRawText),
                batchValues[0],
                batchValues[1],
                batchValues[2],
                batchValues[3],
                batchValues[4]);
            return batchValues;
        }

        // 批量失败后降级到逐列 OCR，提高复杂噪声场景容错性。
        string[] fallback =
        [
            GetData(row, dataColumns[0]),
            GetData(row, dataColumns[1]),
            GetData(row, dataColumns[2]),
            GetData(row, dataColumns[3]),
            GetData(row, dataColumns[4])
        ];
        _logger.LogInformation(
            "SmartBp OCR RowData fallback row={RowId}: values=[{D1},{D2},{D3},{D4},{D5}]",
            rowProfile.Id,
            fallback[0],
            fallback[1],
            fallback[2],
            fallback[3],
            fallback[4]);
        return fallback;
    }

    private static RegionLayoutNode? GetCellById(RegionLayoutNode row, string id, int fallbackIndex)
    {
        return row.Children.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase))
               ?? (fallbackIndex >= 0 && fallbackIndex < row.Children.Count ? row.Children[fallbackIndex] : null);
    }

    private static RelativeRect[] GetDataRects(RegionLayoutNode row)
    {
        var ids = new[] { "d1", "d2", "d3", "d4", "d5" };
        var cells = ids
            .Select((id, index) => GetCellById(row, id, index + 1))
            .ToArray();
        if (cells.Any(c => c == null))
            return [];
        return cells.Select(c => c!.Rect).ToArray();
    }

    private string[]? TryGetRowDataValuesBySingleOcr(Mat row, IReadOnlyList<RelativeRect> dataColumns, out string? rawText)
    {
        // 将 5 个数字列拼成连续条带，减少 OCR 引擎多次启动开销。
        using var strip = BuildDataStrip(row, dataColumns);
        using var bin = PreprocessForDigits(strip);
        rawText = _ocrService.RecognizeText(bin);
        return TryParseFiveNumbers(rawText);
    }

    private static string[]? TryParseFiveNumbers(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var matches = Regex.Matches(text, @"\d+");
        if (matches.Count != 5)
            return null;

        return [matches[0].Value, matches[1].Value, matches[2].Value, matches[3].Value, matches[4].Value];
    }

    private static Mat BuildDataStrip(Mat row, IReadOnlyList<RelativeRect> rects)
    {
        // 先裁掉列边缘无效区域，再按固定间隔拼接成一张新图。
        var columnRects = rects.Select(r => TrimDataRect(r.ToPixelRect(row.Width, row.Height))).ToArray();

        var gap = Math.Max(20, row.Width / 55);
        var totalWidth = columnRects.Sum(r => r.Width) + gap * Math.Max(0, columnRects.Length - 1);
        var strip = new Mat(new Size(totalWidth, row.Height), MatType.CV_8UC3, Scalar.All(255));

        var x = 0;
        foreach (var rect in columnRects)
        {
            using var src = new Mat(row, rect);
            using var dst = new Mat(strip, new Rect(x, 0, rect.Width, rect.Height));
            src.CopyTo(dst);
            x += rect.Width + gap;
        }

        return strip;
    }

    private static Rect TrimDataRect(Rect rect)
    {
        // 去除列左右/上下边缘，尽量减少装饰元素对数字识别的干扰。
        var trimX = Math.Clamp(rect.Width / 10, 2, 10);
        var trimY = Math.Clamp(rect.Height / 16, 0, 4);

        var x = rect.X + trimX;
        var y = rect.Y + trimY;
        var w = Math.Max(1, rect.Width - trimX * 2);
        var h = Math.Max(1, rect.Height - trimY * 2);
        return new Rect(x, y, w, h);
    }

    private string GetData(Mat row, RelativeRect rect, string? rawDebugFileName = null, string? binDebugFileName = null)
    {
        var rawRect = rect.ToPixelRect(row.Width, row.Height);
        var trimmedRect = TrimDataRect(rawRect);

        // 先识别裁边区域，不行再回退到原始区域。
        var value = RecognizeDigits(row, trimmedRect, rawDebugFileName, binDebugFileName);
        if (!string.IsNullOrEmpty(value))
            return value;

        return RecognizeDigits(row, rawRect);
    }

    private string RecognizeDigits(Mat row, Rect dataRect, string? rawDebugFileName = null, string? binDebugFileName = null)
    {
        using var column = new Mat(row, dataRect);

        if (!string.IsNullOrWhiteSpace(rawDebugFileName))
            SaveDebug(column, rawDebugFileName);

        using var bin = PreprocessForDigits(column);

        if (!string.IsNullOrWhiteSpace(binDebugFileName))
            SaveDebug(bin, binDebugFileName);

        // 只保留 0-9，避免 OCR 把噪声字符带入结果。
        return CleanDigitsOnly(_ocrService.RecognizeText(bin));
    }

    public static string CleanDigitsOnly(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;

        s = s.Trim();
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s.Where(ch => ch is >= '0' and <= '9'))
        {
            sb.Append(ch);
        }

        return sb.ToString();
    }

    private static string ToLogText(string? text, int maxLength = 120)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var normalized = text.Replace("\r", " ").Replace("\n", " ").Trim();
        return normalized.Length <= maxLength
            ? normalized
            : $"{normalized[..maxLength]}...";
    }

    private static void SaveDebug(Mat mat, string fileName)
    {
        var outputDir = Path.Combine(AppContext.BaseDirectory, "debug");
        Directory.CreateDirectory(outputDir);
        var fullPath = Path.Combine(outputDir, fileName);
        mat.SaveImage(fullPath);
    }

    public static Mat PreprocessForText(Mat bgr)
    {
        // 名称文本预处理：放大 -> 灰度 -> 背景抑制 -> 二值化 -> 形态学 -> 反色。
        using var scaled = new Mat();
        Cv2.Resize(bgr, scaled, new Size(), 3.0, 3.0, InterpolationFlags.Cubic);

        using var gray = new Mat();
        Cv2.CvtColor(scaled, gray, ColorConversionCodes.BGR2GRAY);

        using var bg = new Mat();
        Cv2.GaussianBlur(gray, bg, new Size(0, 0), 12);

        using var diff = new Mat();
        Cv2.Subtract(gray, bg, diff);

        using var norm = new Mat();
        Cv2.Normalize(diff, norm, 0, 255, NormTypes.MinMax);

        using var denoise = new Mat();
        Cv2.GaussianBlur(norm, denoise, new Size(3, 3), 0);

        var bin = new Mat();
        Cv2.Threshold(denoise, bin, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

        using var kClose = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
        Cv2.MorphologyEx(bin, bin, MorphTypes.Close, kClose, iterations: 1);

        using var kOpen = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(2, 2));
        Cv2.MorphologyEx(bin, bin, MorphTypes.Open, kOpen, iterations: 1);

        Cv2.BitwiseNot(bin, bin);
        return bin;
    }

    public static Mat PreprocessForDigits(Mat bgr)
    {
        // 数字预处理相对更轻，优先保持数字笔画边界。
        using var scaled = new Mat();
        Cv2.Resize(bgr, scaled, new Size(), 2.0, 2.0, InterpolationFlags.Linear);

        using var gray = new Mat();
        Cv2.CvtColor(scaled, gray, ColorConversionCodes.BGR2GRAY);

        using var norm = new Mat();
        Cv2.Normalize(gray, norm, 0, 255, NormTypes.MinMax);

        var bin = new Mat();
        Cv2.Threshold(norm, bin, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(2, 2));
        Cv2.MorphologyEx(bin, bin, MorphTypes.Close, kernel, iterations: 1);

        Cv2.BitwiseNot(bin, bin);
        return bin;
    }

    public record PlayerInfo(string PlayerName, string CharacterName, PlayerData PlayerData);

    private sealed record RecognizedGameData(PlayerData HunterData, List<PlayerInfo> SurvivorInfos);
}
