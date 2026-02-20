using F23.StringSimilarity;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Helpers;
using OpenCvSharp;
using System.IO;
using System.Text;
using System.Threading;

namespace neo_bpsys_wpf.Services;

public class SmartBpService : ISmartBpService
{
    private readonly ISharedDataService _sharedDataService;
    private readonly IWindowCaptureService _windowCaptureService;
    private readonly IOcrService _ocrService;

    public bool IsSmartBpRunning { get; private set; }

    public SmartBpService(ISharedDataService sharedDataService, IWindowCaptureService windowCaptureService,
        IOcrService ocrService)
    {
        _sharedDataService = sharedDataService;
        _windowCaptureService = windowCaptureService;
        _ocrService = ocrService;
    }

    public void StartSmartBp()
    {
        if (!IsOcrReady())
        {
            IsSmartBpRunning = false;
            _ = MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString("SmartBpOcrNotReadyFirstDownloadAndSwitchModel"));
            return;
        }

        IsSmartBpRunning = true;
    }

    public void StopSmartBp()
    {
        IsSmartBpRunning = false;
    }

    public async Task AutoFillGameDataAsync(CancellationToken cancellationToken = default)
    {
        var recognizedData = await Task.Run(
            () => CaptureAndRecognizeGameData(cancellationToken),
            cancellationToken);

        if (recognizedData == null)
            return;

        ApplyRecognizedData(recognizedData);
    }

    private RecognizedGameData? CaptureAndRecognizeGameData(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var table = GetTable();

        if (table == null)
            return null;

        cancellationToken.ThrowIfCancellationRequested();

        var hunterData = GetHunInfo(table).PlayerData;
        var survivorInfos = GetSurInfos(table);
        return new RecognizedGameData(hunterData, survivorInfos);
    }

    private void ApplyRecognizedData(RecognizedGameData recognizedData)
    {
        // Hunter
        _sharedDataService.CurrentGame.HunPlayer.Data.RemainingCipher = recognizedData.HunterData.RemainingCipher;
        _sharedDataService.CurrentGame.HunPlayer.Data.PalletsDestroyed = recognizedData.HunterData.PalletsDestroyed;
        _sharedDataService.CurrentGame.HunPlayer.Data.SurvivorHits = recognizedData.HunterData.SurvivorHits;
        _sharedDataService.CurrentGame.HunPlayer.Data.TerrorShocks = recognizedData.HunterData.TerrorShocks;
        _sharedDataService.CurrentGame.HunPlayer.Data.Knockdowns = recognizedData.HunterData.Knockdowns;

        // Survivors
        var jw = new JaroWinkler(); // 0~1，越大越像
        const double threshold = 0.88; // 你可以根据OCR质量调：0.85~0.92

        foreach (var survivorInfo in recognizedData.SurvivorInfos)
        {
            var target = survivorInfo.CharacterName; // ✅ 你说上游已Normalize

            // 先精确匹配（更快更稳）
            var sur = _sharedDataService.CurrentGame.SurPlayerList
                .FirstOrDefault(p => NormalizeName(p.Character?.Name) == target);

            // 再用相似度兜底
            if (sur == null)
            {
                sur = _sharedDataService.CurrentGame.SurPlayerList
                    .Select(p => new
                    {
                        Player = p,
                        Name = NormalizeName(p.Character?.Name),
                    })
                    .Where(x => !string.IsNullOrEmpty(x.Name))
                    .Select(x => new
                    {
                        x.Player,
                        Score = jw.Similarity(x.Name, target)
                    })
                    .OrderByDescending(x => x.Score)
                    .Where(x => x.Score >= threshold)
                    .Select(x => x.Player)
                    .FirstOrDefault();
            }

            if (sur == null)
            {
                // 找不到就跳过（建议加日志）
                // _logger.LogWarning("Cannot match character name: {Name}", target);
                continue;
            }

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

    public Mat? GetTable()
    {
        var frame = _windowCaptureService.GetCurrentFrame();
        if (frame == null)
            return null;

        using var mat = frame.ToBgrMat();

        // SaveDebug(mat, "01_full.png");

        // === Step 1: 裁剪 Table ===
        var tableRect = new RelativeRect(0.1, 0.165, 0.845, 0.62)
            .ToPixelRect(mat.Width, mat.Height);

        var table = new Mat(mat, tableRect);
        // SaveDebug(table, "02_table.png");

        return table;
    }

    public PlayerInfo GetHunInfo(Mat table)
    {
        // === Step 2: 裁剪 Hunter 行 ===
        var hunterRect = new RelativeRect(0, 0, 1, 0.16)
            .ToPixelRect(table.Width, table.Height);
        using var hunter = new Mat(table, hunterRect);
        // SaveDebug(hunter, "03_hunter_row.png");

        var hunCharacterName = GetHunPlayerNameAndCharacterName(hunter);
        var data = GetHunPlayerData(hunter);
        return new PlayerInfo(hunCharacterName.Item1, hunCharacterName.Item2, data);
    }

    public List<PlayerInfo> GetSurInfos(Mat table)
    {
        var surInfos = new List<PlayerInfo>();

        var surFirstDataRowRect = new RelativeRect(0, 0.279, 1, 0.14)
            .ToPixelRect(table.Width, table.Height);
        using var surFirstDataRow = new Mat(table, surFirstDataRowRect);
        // SaveDebug(surFirstDataRow, "03_sur_first_data_row.png");

        const double rowStep = 0.19;
        var baseRowRel = new RelativeRect(0, 0.279, 1, 0.14);

        for (var i = 0; i < 4; i++)
        {
            var rowRel = baseRowRel with { Y = baseRowRel.Y + i * rowStep };

            var rowPx = rowRel.ToPixelRect(table.Width, table.Height);
            using var row = new Mat(table, rowPx);

            // SaveDebug(row, $"sur_row_{i}.png"); // 你调试时可以打开

            var (playerName, characterName) = GetSurPlayerNameAndCharacterName(row);
            var data = GetSurPlayerData(row);

            surInfos.Add(new PlayerInfo(playerName, characterName, data));
        }

        return surInfos;
    }

    public (string, string) GetSurPlayerNameAndCharacterName(Mat surRow)
    {
        // Sur 行里名字在左侧；高度用整行
        var nameRect = new RelativeRect(0, 0, 0.28, 0.47)
            .ToPixelRect(surRow.Width, surRow.Height);

        using var name = new Mat(surRow, nameRect);

        // 这里先用你现成的 PreprocessForText 跑通（后面你想更稳再拆 PreprocessForName）
        using var bin = PreprocessForText(name);

        var text = _ocrService.RecognizeText(bin) ?? string.Empty;
        return GetPlayerNameAndCharacterName(text);
    }

    private PlayerData GetSurPlayerData(Mat surRow)
    {
        // 这些列 x 位置你 hunter 已经调好，sur 直接复用就很合理
        var data = new PlayerData
        {
            DecodingProgress = GetData(surRow, new RelativeRect(0.255, 0, 0.1, 1), "sur_decoding_progress.png"),
            PalletStrikes = GetData(surRow, new RelativeRect(0.4, 0, 0.1, 1), "sur_pallet_strikes.png"),
            Rescues = GetData(surRow, new RelativeRect(0.515, 0, 0.1, 1), "sur_rescues.png"),
            Heals = GetData(surRow, new RelativeRect(0.65, 0, 0.1, 1), "sur_heals.png"),
            ContainmentTime = GetData(surRow, new RelativeRect(0.79, 0, 0.1, 1), "sur_containment_time.png")
        };
        return data;
    }

    public (string, string) GetHunPlayerNameAndCharacterName(Mat hunter)
    {
        // === Step 3: 裁剪 Name 列 ===
        var nameRect = new RelativeRect(0, 0, 0.4, 0.5)
            .ToPixelRect(hunter.Width, hunter.Height);

        using var name = new Mat(hunter, nameRect);
        // SaveDebug(name, "04_hunter_name.png");

        // === Step 4: 预处理 ===
        using var bin = PreprocessForText(name);
        // SaveDebug(bin, "05_hunter_name_bin.png");

        var hunterText = _ocrService.RecognizeText(bin) ?? string.Empty;

        var (playerName, characterName) = GetPlayerNameAndCharacterName(hunterText);

        return (playerName, characterName);
    }

    private static (string, string) GetPlayerNameAndCharacterName(string playerText)
    {
        playerText = playerText
            .Replace('（', '(')
            .Replace('）', ')');

        // 使用正则表达式分别提取括号前和括号内的内容
        var match = System.Text.RegularExpressions.Regex.Match(playerText, @"^([^()]*?)\(([^)]*)\)");
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

        // 统一全角到半角（可选但很有用：全角空格、全角字母数字）
        name = name.Normalize(NormalizationForm.FormKC);

        // 只保留：字母(含汉字/日文/韩文等所有Unicode字母) + 数字
        return new string(name.Where(char.IsLetterOrDigit).ToArray());
    }

    public PlayerData GetHunPlayerData(Mat hunter)
    {
        var data = new PlayerData
        {
            RemainingCipher = GetData(
                hunter,
                new RelativeRect(0.255, 0, 0.1, 1)),
            PalletsDestroyed = GetData(
                hunter,
                new RelativeRect(0.4, 0, 0.1, 1)),
            SurvivorHits = GetData(
                hunter,
                new RelativeRect(0.515, 0, 0.1, 1)),
            TerrorShocks = GetData(
                hunter,
                new RelativeRect(0.65, 0, 0.1, 1)),
            Knockdowns = GetData(
                hunter,
                new RelativeRect(0.79, 0, 0.1, 1))
        };

        return data;
    }

    private string GetData(Mat row, RelativeRect rect, string? rawDebugFileName = null, string? binDebugFileName = null)
    {
        var dataRect = rect.ToPixelRect(row.Width, row.Height);
        using var column = new Mat(row, dataRect);

        if (!string.IsNullOrWhiteSpace(rawDebugFileName))
            SaveDebug(column, rawDebugFileName);

        using var bin = PreprocessForText(column);

        if (!string.IsNullOrWhiteSpace(binDebugFileName))
            SaveDebug(bin, binDebugFileName);

        return CleanDigitsOnly(_ocrService.RecognizeText(bin));
    }

    public static string CleanDigitsOnly(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;

        // 先 Trim 掉前后空白（包括 \r \n \t）
        s = s.Trim();

        var sb = new StringBuilder(s.Length);
        foreach (var ch in s.Where(ch => ch is >= '0' and <= '9'))
        {
            sb.Append(ch);
        }

        return sb.ToString();
    }

    private static void SaveDebug(Mat mat, string fileName)
    {
        var outputDir = Path.Combine(
            AppContext.BaseDirectory,
            "debug");

        Directory.CreateDirectory(outputDir);

        var fullPath = Path.Combine(outputDir, fileName);
        mat.SaveImage(fullPath);
    }

    public readonly record struct RelativeRect(double X, double Y, double W, double H)
    {
        public Rect ToPixelRect(int imgW, int imgH)
        {
            var x = Math.Clamp((int)(X * imgW), 0, Math.Max(imgW - 1, 0));
            var y = Math.Clamp((int)(Y * imgH), 0, Math.Max(imgH - 1, 0));

            var maxW = Math.Max(imgW - x, 1);
            var maxH = Math.Max(imgH - y, 1);

            var w = Math.Clamp((int)(W * imgW), 1, maxW);
            var h = Math.Clamp((int)(H * imgH), 1, maxH);
            return new Rect(x, y, w, h);
        }
    }

    public static Mat PreprocessForText(Mat bgr)
    {
        // 0) 放大：数字太小的话，det/rec 都会难受
        var scaled = new Mat();
        Cv2.Resize(bgr, scaled, new Size(), 3.0, 3.0, InterpolationFlags.Cubic);

        // 1) 灰度
        var gray = new Mat();
        Cv2.CvtColor(scaled, gray, ColorConversionCodes.BGR2GRAY);

        // 2) 背景抹平：用大尺度模糊当“背景”，再做差分
        //    直觉：把慢变化的背景(头像/渐变)去掉，只剩“亮的细东西”(数字)
        var bg = new Mat();
        Cv2.GaussianBlur(gray, bg, new Size(0, 0), 12); // sigma 可调：8~18

        var diff = new Mat();
        Cv2.Subtract(gray, bg, diff); // 亮的东西会更突出，背景会趋近 0

        // 3) 拉伸对比度，避免 diff 太灰
        var norm = new Mat();
        Cv2.Normalize(diff, norm, 0, 255, NormTypes.MinMax);

        // 4) 轻去噪（别太重，不然笔画断）
        var denoise = new Mat();
        Cv2.GaussianBlur(norm, denoise, new Size(3, 3), 0);

        // 5) 二值：用 Otsu 自动阈值更稳（比 adaptive 更不容易把背景线条提出来）
        var bin = new Mat();
        Cv2.Threshold(denoise, bin, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

        // 6) 形态学：把断笔画接上、去掉小噪点
        var kClose = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
        Cv2.MorphologyEx(bin, bin, MorphTypes.Close, kClose, iterations: 1);

        var kOpen = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(2, 2));
        Cv2.MorphologyEx(bin, bin, MorphTypes.Open, kOpen, iterations: 1);

        // 7) Paddle 识别通常更喜欢 “黑字白底”
        Cv2.BitwiseNot(bin, bin);

        return bin;
    }

    public record PlayerInfo(string PlayerName, string CharacterName, PlayerData PlayerData);

    private sealed record RecognizedGameData(PlayerData HunterData, List<PlayerInfo> SurvivorInfos);
}
