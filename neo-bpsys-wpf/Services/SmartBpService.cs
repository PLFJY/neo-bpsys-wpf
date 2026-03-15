using F23.StringSimilarity;
using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Views.Pages;
using OpenCvSharp;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Threading;

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
    private readonly DispatcherTimer _timer;

    /// <summary>
    /// 获取当前 SmartBp 是否处于运行状态。
    /// </summary>
    public bool IsSmartBpRunning { get; private set; }

    /// <summary>
    /// 初始化 <see cref="SmartBpService"/> 的新实例。
    /// </summary>
    /// <param name="sharedDataService">共享对局数据服务。</param>
    /// <param name="windowCaptureService">窗口捕获服务。</param>
    /// <param name="ocrService">OCR 服务。</param>
    /// <param name="regionConfigService">SmartBp 区域配置服务。</param>
    /// <param name="logger">日志记录器。</param>
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

        _timer = new DispatcherTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(1000);
        _timer.Tick += Timer_Tick;
    }

    /// <inheritdoc/>
    public void StartSmartBp()
    {
        // SmartBp 依赖 OCR 模型，未就绪时直接阻止启动。
        if (!IsOcrReady())
        {
            IsSmartBpRunning = false;
            _ = MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString("SmartBpOcrNotReadyFirstDownloadAndSwitchModel"));
            return;
        }

        if(IsSmartBpRunning)
        {
            _ = MessageBoxHelper.ShowErrorAsync("请勿重复启动");
        }
        _timer.Start();
        IsSmartBpRunning = true;
        StartOcrWarmupIfNeeded();
    }

    /// <inheritdoc/>
    public void StopSmartBp()
    {
        if (!IsSmartBpRunning) return;
        _timer.Stop();
        IsSmartBpRunning = false;
    }


    private void Timer_Tick(object? sender, EventArgs e)
    {
        try
        {
            if (!_windowCaptureService.IsCapturing)
            {
                _logger.LogInformation("SmartBp auto BP skipped: capture is not running.");
                return;
            }

            
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SmartBp auto BP failed with exception. {Message}", ex.Message);
            IsSmartBpRunning = false;
            _timer.Stop();
        }
    }

    /// <inheritdoc/>
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
            _logger.LogError(ex, "SmartBp AutoFill failed with exception. {Message}", ex.Message);
        }
    }

    /// <summary>
    /// 在后台触发一次 OCR 预热，减少首次识别延迟。
    /// </summary>
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

    /// <summary>
    /// 捕获当前窗口帧并识别监管者与求生者数据。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>识别结果；当无法捕获或区域无效时返回 <see langword="null"/>。</returns>
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

    /// <summary>
    /// 识别单个求生者行信息。
    /// </summary>
    /// <param name="surRow">求生者行图像。</param>
    /// <param name="rowProfile">行布局配置。</param>
    /// <returns>识别后的求生者信息。</returns>
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

    /// <summary>
    /// 将识别结果写回当前对局数据。
    /// </summary>
    /// <param name="recognizedData">识别到的监管者与求生者数据。</param>
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

    /// <summary>
    /// 检查当前 OCR 模型是否已就绪。
    /// </summary>
    /// <returns>模型可用时返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    private bool IsOcrReady()
    {
        var currentModelKey = _ocrService.CurrentOcrModelKey;
        return !string.IsNullOrWhiteSpace(currentModelKey) &&
               _ocrService.IsModelInstalled(currentModelKey);
    }

    /// <summary>
    /// 识别监管者行的玩家名称、角色名称与数据字段。
    /// </summary>
    /// <param name="hunter">监管者行图像。</param>
    /// <param name="rowProfile">行布局配置。</param>
    /// <returns>监管者识别结果。</returns>
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

    /// <summary>
    /// 识别求生者行的玩家名称与角色名称。
    /// </summary>
    /// <param name="surRow">求生者行图像。</param>
    /// <param name="rowProfile">行布局配置。</param>
    /// <returns>玩家名称与角色名称元组。</returns>
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

    /// <summary>
    /// 识别求生者行的数据字段。
    /// </summary>
    /// <param name="surRow">求生者行图像。</param>
    /// <param name="rowProfile">行布局配置。</param>
    /// <returns>求生者数据对象。</returns>
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

    /// <summary>
    /// 识别监管者行的玩家名称与角色名称。
    /// </summary>
    /// <param name="hunter">监管者行图像。</param>
    /// <param name="rowProfile">行布局配置。</param>
    /// <returns>玩家名称与角色名称元组。</returns>
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

    /// <summary>
    /// 从 OCR 原始文本中解析玩家名称与角色名称。
    /// </summary>
    /// <param name="playerText">原始文本。</param>
    /// <returns>玩家名称与角色名称元组。</returns>
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

    /// <summary>
    /// 规范化名称文本，便于匹配比较。
    /// </summary>
    /// <param name="name">原始名称。</param>
    /// <returns>仅包含字母数字的标准化小写字符串。</returns>
    private static string NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        name = name.Trim().ToLowerInvariant();
        name = name.Normalize(NormalizationForm.FormKC);
        return new string(name.Where(char.IsLetterOrDigit).ToArray());
    }

    /// <summary>
    /// 识别监管者行的数据字段。
    /// </summary>
    /// <param name="hunter">监管者行图像。</param>
    /// <param name="rowProfile">行布局配置。</param>
    /// <returns>监管者数据对象。</returns>
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

    /// <summary>
    /// 读取一行中的 5 个数据列值。
    /// </summary>
    /// <param name="row">行图像。</param>
    /// <param name="rowProfile">行布局配置。</param>
    /// <returns>长度为 5 的字符串数组。</returns>
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

    /// <summary>
    /// 按单元格 ID 获取行内单元格；未找到时使用索引回退。
    /// </summary>
    /// <param name="row">行布局节点。</param>
    /// <param name="id">目标单元格 ID。</param>
    /// <param name="fallbackIndex">回退索引。</param>
    /// <returns>命中的单元格节点；未命中返回 <see langword="null"/>。</returns>
    private static RegionLayoutNode? GetCellById(RegionLayoutNode row, string id, int fallbackIndex)
    {
        return row.Children.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase))
               ?? (fallbackIndex >= 0 && fallbackIndex < row.Children.Count ? row.Children[fallbackIndex] : null);
    }

    /// <summary>
    /// 获取一行中 5 个数据列的相对矩形区域。
    /// </summary>
    /// <param name="row">行布局节点。</param>
    /// <returns>数据列相对矩形数组；缺列时返回空数组。</returns>
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

    /// <summary>
    /// 通过一次 OCR 识别整行数据列并解析为 5 个数字值。
    /// </summary>
    /// <param name="row">行图像。</param>
    /// <param name="dataColumns">数据列区域集合。</param>
    /// <param name="rawText">OCR 原始文本输出。</param>
    /// <returns>成功时返回长度为 5 的数组；失败返回 <see langword="null"/>。</returns>
    private string[]? TryGetRowDataValuesBySingleOcr(Mat row, IReadOnlyList<RelativeRect> dataColumns, out string? rawText)
    {
        // 将 5 个数字列拼成连续条带，减少 OCR 引擎多次启动开销。
        using var strip = BuildDataStrip(row, dataColumns);
        using var bin = PreprocessForDigits(strip);
        rawText = _ocrService.RecognizeText(bin);
        return TryParseFiveNumbers(rawText);
    }

    /// <summary>
    /// 从文本中提取 5 个数字。
    /// </summary>
    /// <param name="text">待解析文本。</param>
    /// <returns>提取成功时返回长度为 5 的数组；否则返回 <see langword="null"/>。</returns>
    private static string[]? TryParseFiveNumbers(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var matches = Regex.Matches(text, @"\d+");
        if (matches.Count != 5)
            return null;

        return [matches[0].Value, matches[1].Value, matches[2].Value, matches[3].Value, matches[4].Value];
    }

    /// <summary>
    /// 将多个数据列裁切并拼接为单条带图像。
    /// </summary>
    /// <param name="row">原始行图像。</param>
    /// <param name="rects">数据列区域集合。</param>
    /// <returns>拼接后的图像。</returns>
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

    /// <summary>
    /// 裁掉数据列周边干扰边缘区域。
    /// </summary>
    /// <param name="rect">原始矩形。</param>
    /// <returns>裁边后的矩形。</returns>
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

    /// <summary>
    /// 识别单个数据列值，失败时自动回退到未裁边区域重试。
    /// </summary>
    /// <param name="row">行图像。</param>
    /// <param name="rect">数据列相对区域。</param>
    /// <param name="rawDebugFileName">原图调试文件名。</param>
    /// <param name="binDebugFileName">二值图调试文件名。</param>
    /// <returns>识别到的数字字符串。</returns>
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

    /// <summary>
    /// 对指定区域执行数字识别。
    /// </summary>
    /// <param name="row">行图像。</param>
    /// <param name="dataRect">像素级识别区域。</param>
    /// <param name="rawDebugFileName">原图调试文件名。</param>
    /// <param name="binDebugFileName">二值图调试文件名。</param>
    /// <returns>仅包含数字字符的识别结果。</returns>
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

    /// <summary>
    /// 从字符串中移除非数字字符。
    /// </summary>
    /// <param name="s">输入字符串。</param>
    /// <returns>仅由数字组成的字符串。</returns>
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

    /// <summary>
    /// 规范化日志文本并限制最大长度。
    /// </summary>
    /// <param name="text">原始文本。</param>
    /// <param name="maxLength">最大保留长度。</param>
    /// <returns>用于日志输出的安全文本。</returns>
    private static string ToLogText(string? text, int maxLength = 120)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var normalized = text.Replace("\r", " ").Replace("\n", " ").Trim();
        return normalized.Length <= maxLength
            ? normalized
            : $"{normalized[..maxLength]}...";
    }

    /// <summary>
    /// 将调试图像写入应用目录下的 <c>debug</c> 文件夹。
    /// </summary>
    /// <param name="mat">待保存图像。</param>
    /// <param name="fileName">输出文件名。</param>
    private static void SaveDebug(Mat mat, string fileName)
    {
        var outputDir = Path.Combine(AppContext.BaseDirectory, "debug");
        Directory.CreateDirectory(outputDir);
        var fullPath = Path.Combine(outputDir, fileName);
        mat.SaveImage(fullPath);
    }

    /// <summary>
    /// 对名称文本区域执行 OCR 预处理。
    /// </summary>
    /// <param name="bgr">输入 BGR 图像。</param>
    /// <returns>预处理后的二值图像。</returns>
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

    /// <summary>
    /// 对数字文本区域执行 OCR 预处理。
    /// </summary>
    /// <param name="bgr">输入 BGR 图像。</param>
    /// <returns>预处理后的二值图像。</returns>
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

    /// <summary>
    /// 表示识别后的单个玩家信息。
    /// </summary>
    /// <param name="PlayerName">玩家名称。</param>
    /// <param name="CharacterName">角色名称。</param>
    /// <param name="PlayerData">玩家数据。</param>
    public record PlayerInfo(string PlayerName, string CharacterName, PlayerData PlayerData);

    private sealed record RecognizedGameData(PlayerData HunterData, List<PlayerInfo> SurvivorInfos);

    private sealed record RecognizedBpStatus(List<Character> SurCharacters, Character HunCharacter, List<Character> SurBannedCharacters, List<Character> HunBannedCharacters);
}
