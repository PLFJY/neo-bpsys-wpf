using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.SmartBp.Module.Abstractions;
using neo_bpsys_wpf.SmartBp.Module.Models.Recognition;
using OpenCvSharp;
using System.Threading;
using System.Windows.Threading;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 智慧 BP 服务实现。
/// 负责对完整窗口捕获帧进行赛后数据 OCR，并按文本坐标回填当前对局数据。
/// </summary>
public class SmartBpService : ISmartBpService, IGameDataRecognitionDebugState
{
    private readonly ISharedDataService _sharedDataService;
    private readonly IWindowCaptureService _windowCaptureService;
    private readonly IOcrService _ocrService;
    private readonly ICharacterSelectionService _characterSelectionService;
    private readonly ISmartBpRecognitionSettingsService _recognitionSettingsService;
    private readonly ISmartBpDebugLog _debugLog;
    private readonly ILogger<SmartBpService> _logger;
    private readonly DispatcherTimer _timer;
    private int _ocrWarmupStarted;

    /// <inheritdoc />
    public event EventHandler? SnapshotChanged;

    /// <inheritdoc />
    public GameDataRecognitionDebugSnapshot Current { get; private set; } = GameDataRecognitionDebugSnapshot.Empty;

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
    /// <param name="characterSelectionService">角色匹配与选择服务。</param>
    /// <param name="recognitionSettingsService">SmartBP 识别设置服务。</param>
    /// <param name="debugLog">SmartBP 统一识别调试日志。</param>
    /// <param name="logger">日志记录器。</param>
    public SmartBpService(
        ISharedDataService sharedDataService,
        IWindowCaptureService windowCaptureService,
        IOcrService ocrService,
        ICharacterSelectionService characterSelectionService,
        ISmartBpRecognitionSettingsService recognitionSettingsService,
        ISmartBpDebugLog debugLog,
        ILogger<SmartBpService> logger)
    {
        _sharedDataService = sharedDataService;
        _windowCaptureService = windowCaptureService;
        _ocrService = ocrService;
        _characterSelectionService = characterSelectionService;
        _recognitionSettingsService = recognitionSettingsService;
        _debugLog = debugLog;
        _logger = logger;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) };
        _timer.Tick += Timer_Tick;
    }

    /// <inheritdoc />
    public void StartSmartBp()
    {
        if (!IsOcrReady())
        {
            IsSmartBpRunning = false;
            _ = MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString("SmartBpOcrNotReadyFirstDownloadAndSwitchModel"));
            return;
        }

        if (IsSmartBpRunning)
        {
            _ = MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString("SmartBpAlreadyRunning"));
            return;
        }

        _timer.Start();
        IsSmartBpRunning = true;
        StartOcrWarmupIfNeeded();
    }

    /// <inheritdoc />
    public void StopSmartBp()
    {
        if (!IsSmartBpRunning)
            return;

        _timer.Stop();
        IsSmartBpRunning = false;
    }

    /// <inheritdoc />
    public async Task AutoFillGameDataAsync(CancellationToken cancellationToken = default)
    {
        _debugLog.Write("post-game", "赛后数据识别 requested.");
        try
        {
            if (!IsOcrReady())
            {
                _logger.LogDebug("SmartBp AutoFill skipped: OCR model is not ready.");
                _debugLog.Write("post-game", "skipped: OCR provider is not ready.");
                await MessageBoxHelper.ShowErrorAsync(I18nHelper.GetLocalizedString("SmartBpOcrNotReadyFirstDownloadAndSwitchModel"));
                return;
            }

            if (!_windowCaptureService.IsCapturing || _windowCaptureService.GetCurrentFrame() == null)
            {
                _logger.LogDebug("SmartBp AutoFill skipped: capture or current frame is unavailable.");
                _debugLog.Write("post-game", $"skipped: capture_available={_windowCaptureService.IsCapturing}; frame_available={_windowCaptureService.GetCurrentFrame() != null}.");
                await MessageBoxHelper.ShowInfoAsync(I18nHelper.GetLocalizedString(
                    _windowCaptureService.IsCapturing
                        ? "SmartBpValidationCaptureFrameUnavailable"
                        : "SmartBpValidationCaptureNotRunning"));
                return;
            }

            var recognizedData = await Task.Run(
                () => CaptureAndRecognizeGameData(cancellationToken),
                cancellationToken);
            if (recognizedData == null)
            {
                _debugLog.Write("post-game", "finished: no usable post-game rows were parsed.");
                await MessageBoxHelper.ShowInfoAsync(I18nHelper.GetLocalizedString("SmartBpValidationGameDataRecognitionNoResult"));
                return;
            }

            ApplyRecognizedData(recognizedData);
            _logger.LogDebug("SmartBp AutoFill succeeded: {SurvivorCount} survivor rows applied.", recognizedData.SurvivorInfos.Count);
            _debugLog.Write("post-game", $"finished: hunter_present={recognizedData.HunterData != null}; survivor_rows={recognizedData.SurvivorInfos.Count}.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("SmartBp AutoFill canceled.");
            _debugLog.Write("post-game", "canceled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SmartBp AutoFill failed with exception. {Message}", ex.Message);
            _debugLog.Write("post-game", $"failed: {ToLogText(ex.ToString())}");
            await MessageBoxHelper.ShowErrorAsync(string.Format(
                I18nHelper.GetLocalizedString("SmartBpOperationFailedFormat"), ex.Message));
        }
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (!_windowCaptureService.IsCapturing)
            _logger.LogDebug("SmartBp auto BP skipped: capture is not running.");
    }

    private void StartOcrWarmupIfNeeded()
    {
        if (Interlocked.Exchange(ref _ocrWarmupStarted, 1) == 1)
            return;

        _ = Task.Run(() =>
        {
            try
            {
                using var warmup = new Mat(new Size(512, 96), MatType.CV_8UC1, Scalar.All(255));
                _ = _ocrService.RecognizeTextLines(warmup);
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
        var frame = _windowCaptureService.GetCurrentFrame();
        if (frame == null)
            return null;

        using var full = frame.ToBgrMat();
        _debugLog.Write(
            "post-game",
            $"capture: pixel_size={frame.PixelWidth}x{frame.PixelHeight}; provider={_ocrService.SelectedProvider}; configured_provider_details=[{ToLogText(_ocrService.GetProviderStatus(_ocrService.SelectedProvider).Details)}].");
        var ocrResult = _ocrService.RecognizeTextLines(full);
        _debugLog.Write("post-game", $"OCR result: provider={ocrResult.Provider ?? _ocrService.SelectedProvider.ToString()}; line_count={ocrResult.Lines.Count}.");
        foreach (var (line, index) in ocrResult.Lines.Select((line, index) => (line, index)))
        {
            _debugLog.Write(
                "post-game",
                $"raw_line[{index}]: provider={line.Provider ?? "unknown"}; text=[{ToLogText(line.Text)}]; bbox={line.BoundingBox}; center={line.CenterX:0.0},{line.CenterY:0.0}; confidence={line.Confidence:0.00}.");
        }

        var parsed = GameDataTableOcrParser.Parse(ocrResult.Lines);
        parsed = RecognizeMissingGameDataCells(full, ocrResult.Lines, parsed, cancellationToken);
        foreach (var diagnostic in parsed.Diagnostics)
        {
            _logger.LogDebug("SmartBp GameData table OCR: {Diagnostic}", diagnostic);
            _debugLog.Write("post-game", $"parser: {diagnostic}");
        }

        foreach (var row in parsed.Rows)
        {
            _debugLog.Write(
                "post-game",
                $"row[{row.RowIndex}]: raw_name=[{ToLogText(row.RawNameText)}]; player=[{ToLogText(row.PlayerName)}]; character=[{ToLogText(row.CharacterName)}]; values=[{string.Join(",", row.Values)}]; complete={row.HasAllDataColumns}.");
        }

        PublishGameDataDebugSnapshot(ocrResult.Lines.Count, parsed);
        if (parsed.Rows.Count == 0)
            return null;

        // OCR 常会漏掉界面中的“-”空值标记；名称和角色已可靠定位时，仍应回填该行，
        // 未识别的数据列沿用 PlayerData 的空字符串语义。
        var hunterRow = parsed.Rows.SingleOrDefault(row => row.RowIndex == 0);
        var hunterData = hunterRow == null ? null : ToHunterData(hunterRow.Values);
        var survivorInfos = parsed.Rows
            .Where(row => row.RowIndex is >= 1 and <= 4)
            .Select(row => new PlayerInfo(row.PlayerName, row.CharacterName, ToSurvivorData(row.Values)))
            .ToList();
        _logger.LogInformation(
            "SmartBp GameData table OCR parsed. OcrLineCount={OcrLineCount}, ParsedRowCount={ParsedRowCount}, CompleteRowCount={CompleteRowCount}, HunterFound={HunterFound}, SurvivorRowCount={SurvivorRowCount}",
            ocrResult.Lines.Count, parsed.Rows.Count, parsed.Rows.Count(row => row.HasAllDataColumns), hunterData != null, survivorInfos.Count);
        return hunterData == null && survivorInfos.Count == 0 ? null : new RecognizedGameData(hunterData, survivorInfos);
    }

    /// <summary>
    /// 对整表 OCR 漏掉的数据格执行局部直接识别，主要补救文本检测阶段漏掉的细窄数字。
    /// </summary>
    /// <param name="full">完整捕获画面。</param>
    /// <param name="initialLines">第一次整表 OCR 的文本行。</param>
    /// <param name="initialResult">第一次表格解析结果。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>合并局部复识别结果后的表格解析结果。</returns>
    private GameDataTableParseResult RecognizeMissingGameDataCells(
        Mat full,
        IReadOnlyList<OcrTextLine> initialLines,
        GameDataTableParseResult initialResult,
        CancellationToken cancellationToken)
    {
        if (initialResult.MissingCells.Count == 0)
            return initialResult;

        const int cropWidth = 76;
        const int cropHeight = 34;
        var mergedLines = initialLines.ToList();
        _debugLog.Write(
            "post-game",
            $"numeric fallback: missing_cells={initialResult.MissingCells.Count}; crop={cropWidth}x{cropHeight}; mode=known-region-direct-recognition.");

        foreach (var cell in initialResult.MissingCells)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cropRect = CreateGameDataCellCrop(full, cell.ExpectedCenterX, cell.ExpectedCenterY, cropWidth, cropHeight);
            using var cellMat = new Mat(full, cropRect);
            var variants = CreateGameDataCellRecognitionVariants(cellMat);
            var candidates = new List<GameDataCellOcrCandidate>();
            _debugLog.Write(
                "post-game",
                $"numeric fallback cell: row={cell.RowIndex}; column={cell.ColumnIndex}; expected_center={cell.ExpectedCenterX:0.#},{cell.ExpectedCenterY:0.#}; crop={cropRect}; variants={variants.Count}.");

            AppendGameDataCellCandidates(cell, variants, candidates, cancellationToken);
            var selection = GameDataCellOcrCandidateSelector.Select(candidates);
            var hasDigitOneCandidate = HasDigitOneCandidate(candidates);
            if (selection == null && hasDigitOneCandidate)
            {
                var refinementVariants = CreateDigitOneRefinementVariants(cellMat);
                _debugLog.Write(
                    "post-game",
                    $"numeric fallback one refinement: row={cell.RowIndex}; column={cell.ColumnIndex}; variants={refinementVariants.Count}.");
                AppendGameDataCellCandidates(cell, refinementVariants, candidates, cancellationToken);
                selection = GameDataCellOcrCandidateSelector.Select(candidates);
            }

            if (selection == null && hasDigitOneCandidate)
            {
                var hasVisualEvidence = GameDataCellVisualAnalyzer.TryDetectDigitOne(cellMat, out var visualEvidence);
                _debugLog.Write(
                    "post-game",
                    hasVisualEvidence
                        ? $"numeric fallback one visual evidence: row={cell.RowIndex}; column={cell.ColumnIndex}; detected=True; bbox={visualEvidence!.BoundingBox}; aspect={visualEvidence.AspectRatio:0.00}; fill={visualEvidence.FillRatio:0.00}; threshold={visualEvidence.Threshold:0.0}; confidence={visualEvidence.Confidence:0.000}."
                        : $"numeric fallback one visual evidence: row={cell.RowIndex}; column={cell.ColumnIndex}; detected=False.");
                if (visualEvidence != null)
                {
                    candidates.Add(new GameDataCellOcrCandidate(
                        "visual-vertical-stroke",
                        "1",
                        visualEvidence.Confidence,
                        "OpenCV/shape"));
                    selection = GameDataCellOcrCandidateSelector.Select(candidates);
                }
            }

            if (selection == null)
            {
                _debugLog.Write(
                    "post-game",
                    $"numeric fallback rejected: row={cell.RowIndex}; column={cell.ColumnIndex}; valid_candidate_count={candidates.Count(candidate => GameDataCellOcrCandidateSelector.NormalizeNumericText(candidate.RawText) != null)}.");
                continue;
            }

            var syntheticBox = new Rect(
                Math.Clamp((int)Math.Round(cell.ExpectedCenterX) - 1, 0, Math.Max(0, full.Width - 2)),
                Math.Clamp((int)Math.Round(cell.ExpectedCenterY) - 1, 0, Math.Max(0, full.Height - 2)),
                2,
                2);
            mergedLines.Add(new OcrTextLine(
                selection.Value,
                selection.Confidence,
                syntheticBox,
                cell.ExpectedCenterX,
                cell.ExpectedCenterY,
                selection.Provider));
            _debugLog.Write(
                "post-game",
                $"numeric fallback accepted: row={cell.RowIndex}; column={cell.ColumnIndex}; value=[{selection.Value}]; confidence={selection.Confidence:0.000}; support={selection.SupportCount}; provider={selection.Provider}.");
        }

        var mergedResult = GameDataTableOcrParser.Parse(mergedLines);
        _debugLog.Write(
            "post-game",
            $"numeric fallback finished: input_lines={initialLines.Count}; merged_lines={mergedLines.Count}; remaining_missing_cells={mergedResult.MissingCells.Count}.");
        return mergedResult;
    }

    private static Rect CreateGameDataCellCrop(Mat full, double centerX, double centerY, int width, int height)
    {
        var x = Math.Clamp((int)Math.Round(centerX - width / 2d), 0, Math.Max(0, full.Width - width));
        var y = Math.Clamp((int)Math.Round(centerY - height / 2d), 0, Math.Max(0, full.Height - height));
        return new Rect(x, y, Math.Min(width, full.Width - x), Math.Min(height, full.Height - y));
    }

    /// <summary>
    /// 运行一组数字单元格图像变体并追加带规范化诊断的 OCR 候选。
    /// </summary>
    private void AppendGameDataCellCandidates(
        GameDataTableMissingCell cell,
        IReadOnlyList<(string Name, Mat Image)> variants,
        ICollection<GameDataCellOcrCandidate> candidates,
        CancellationToken cancellationToken)
    {
        try
        {
            foreach (var variant in variants)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = _ocrService.RecognizeSingleText(variant.Image, new OcrRecognitionOptions
                {
                    RegionHint = $"post-game-row-{cell.RowIndex}-column-{cell.ColumnIndex}",
                    FieldHint = "numeric-cell",
                    PreferChinese = false,
                    PreferEnglish = false,
                    Psm = 10,
                    UsePreprocessingVariants = false
                });
                var hasNormalizedValue = GameDataCellOcrCandidateSelector.TryNormalizeNumericText(
                    result?.Text,
                    out var normalizedValue,
                    out var isExactValue);
                _debugLog.Write(
                    "post-game",
                    $"numeric fallback candidate: row={cell.RowIndex}; column={cell.ColumnIndex}; variant={variant.Name}; text=[{ToLogText(result?.Text)}]; normalized=[{(hasNormalizedValue ? normalizedValue : string.Empty)}]; normalization={(hasNormalizedValue ? isExactValue ? "exact" : "supporting" : "rejected")}; confidence={result?.Confidence.ToString("0.000") ?? "-"}; provider={result?.Provider ?? _ocrService.SelectedProvider.ToString()}.");
                if (result != null)
                    candidates.Add(new GameDataCellOcrCandidate(
                        variant.Name,
                        result.Text,
                        result.Confidence,
                        result.Provider));
            }
        }
        finally
        {
            foreach (var variant in variants)
                variant.Image.Dispose();
        }
    }

    private static bool HasDigitOneCandidate(IEnumerable<GameDataCellOcrCandidate> candidates) =>
        candidates.Any(candidate =>
            GameDataCellOcrCandidateSelector.TryNormalizeNumericText(candidate.RawText, out var value, out _) &&
            string.Equals(value, "1", StringComparison.Ordinal));

    /// <summary>
    /// 为已知数字单元格生成直接字符识别所需的图像变体。
    /// </summary>
    /// <param name="cell">单元格原始裁剪图。</param>
    /// <returns>由调用方负责释放图像的命名变体。</returns>
    private static IReadOnlyList<(string Name, Mat Image)> CreateGameDataCellRecognitionVariants(Mat cell)
    {
        const double scale = 3d;
        var variants = new List<(string Name, Mat Image)>();

        var original = new Mat();
        Cv2.Resize(cell, original, new Size(), scale, scale, InterpolationFlags.Cubic);
        variants.Add(("original-3x", original));

        using var gray = new Mat();
        if (cell.Channels() == 1)
            cell.CopyTo(gray);
        else
            Cv2.CvtColor(cell, gray, cell.Channels() == 4 ? ColorConversionCodes.BGRA2GRAY : ColorConversionCodes.BGR2GRAY);
        using var clahe = Cv2.CreateCLAHE(2.5, new Size(4, 4));
        using var enhanced = new Mat();
        clahe.Apply(gray, enhanced);

        using var enhancedBgr = new Mat();
        Cv2.CvtColor(enhanced, enhancedBgr, ColorConversionCodes.GRAY2BGR);
        var contrast = new Mat();
        Cv2.Resize(enhancedBgr, contrast, new Size(), scale, scale, InterpolationFlags.Cubic);
        variants.Add(("clahe-3x", contrast));

        using var binary = new Mat();
        Cv2.Threshold(enhanced, binary, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(2, 2));
        using var thickened = new Mat();
        Cv2.Dilate(binary, thickened, kernel);
        using var inverted = new Mat();
        Cv2.BitwiseNot(thickened, inverted);
        using var invertedBgr = new Mat();
        Cv2.CvtColor(inverted, invertedBgr, ColorConversionCodes.GRAY2BGR);
        var threshold = new Mat();
        Cv2.Resize(invertedBgr, threshold, new Size(), scale, scale, InterpolationFlags.Nearest);
        variants.Add(("otsu-inverted-thick-3x", threshold));

        return variants;
    }

    /// <summary>
    /// 为疑似数字 1 生成更窄的中心裁剪，降低复杂背景被字符模型解释为尾随字母的概率。
    /// </summary>
    private static IReadOnlyList<(string Name, Mat Image)> CreateDigitOneRefinementVariants(Mat cell)
    {
        const double scale = 4d;
        var width = Math.Min(40, cell.Width);
        var height = Math.Min(31, cell.Height);
        var rect = new Rect((cell.Width - width) / 2, 0, width, height);
        using var center = new Mat(cell, rect);
        var variants = new List<(string Name, Mat Image)>();

        var original = new Mat();
        Cv2.Resize(center, original, new Size(), scale, scale, InterpolationFlags.Cubic);
        variants.Add(("one-center-original-4x", original));

        using var gray = new Mat();
        if (center.Channels() == 1)
            center.CopyTo(gray);
        else
            Cv2.CvtColor(center, gray, center.Channels() == 4 ? ColorConversionCodes.BGRA2GRAY : ColorConversionCodes.BGR2GRAY);
        using var clahe = Cv2.CreateCLAHE(3, new Size(3, 3));
        using var enhanced = new Mat();
        clahe.Apply(gray, enhanced);
        using var enhancedBgr = new Mat();
        Cv2.CvtColor(enhanced, enhancedBgr, ColorConversionCodes.GRAY2BGR);
        var contrast = new Mat();
        Cv2.Resize(enhancedBgr, contrast, new Size(), scale, scale, InterpolationFlags.Cubic);
        variants.Add(("one-center-clahe-4x", contrast));

        using var binary = new Mat();
        Cv2.Threshold(enhanced, binary, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
        using var inverted = new Mat();
        Cv2.BitwiseNot(binary, inverted);
        using var invertedBgr = new Mat();
        Cv2.CvtColor(inverted, invertedBgr, ColorConversionCodes.GRAY2BGR);
        var threshold = new Mat();
        Cv2.Resize(invertedBgr, threshold, new Size(), scale, scale, InterpolationFlags.Nearest);
        variants.Add(("one-center-otsu-inverted-4x", threshold));

        return variants;
    }

    private void PublishGameDataDebugSnapshot(int ocrLineCount, GameDataTableParseResult parsed)
    {
        var rows = parsed.Rows.Select(row =>
        {
            var camp = row.RowIndex == 0 ? Camp.Hun : Camp.Sur;
            return new GameDataRecognitionDebugRow(
                row.RowIndex, row.PlayerName, row.CharacterName,
                _characterSelectionService.ResolveCharacter(row.CharacterName, camp)?.Name,
                row.Values, row.HasAllDataColumns);
        }).ToArray();
        Current = new GameDataRecognitionDebugSnapshot(ocrLineCount, rows, parsed.Diagnostics);
        SnapshotChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyRecognizedData(RecognizedGameData recognizedData)
    {
        if (recognizedData.HunterData != null)
        {
            _debugLog.Write("post-game", $"apply hunter data: values=[{recognizedData.HunterData.RemainingCipher},{recognizedData.HunterData.PalletsDestroyed},{recognizedData.HunterData.SurvivorHits},{recognizedData.HunterData.TerrorShocks},{recognizedData.HunterData.Knockdowns}].");
            var target = _sharedDataService.CurrentGame.HunPlayer.Data;
            target.RemainingCipher = recognizedData.HunterData.RemainingCipher;
            target.PalletsDestroyed = recognizedData.HunterData.PalletsDestroyed;
            target.SurvivorHits = recognizedData.HunterData.SurvivorHits;
            target.TerrorShocks = recognizedData.HunterData.TerrorShocks;
            target.Knockdowns = recognizedData.HunterData.Knockdowns;
        }

        foreach (var survivorInfo in recognizedData.SurvivorInfos)
        {
            var character = _characterSelectionService.ResolveCharacter(survivorInfo.CharacterName, Camp.Sur);
            var target = character == null ? null : _sharedDataService.CurrentGame.SurPlayerList
                .FirstOrDefault(player => string.Equals(player.Character?.Name, character.Name, StringComparison.Ordinal));
            if (target == null)
            {
                _logger.LogDebug("SmartBp Match failed: recognizedCharacter={Character}", ToLogText(survivorInfo.CharacterName));
                _debugLog.Write("post-game", $"apply survivor skipped: player=[{ToLogText(survivorInfo.PlayerName)}]; character=[{ToLogText(survivorInfo.CharacterName)}]; resolved_character=[{character?.Name ?? "unresolved"}].");
                continue;
            }

            _debugLog.Write("post-game", $"apply survivor: player=[{ToLogText(survivorInfo.PlayerName)}]; character=[{character?.Name ?? "unresolved"}]; values=[{survivorInfo.PlayerData.DecodingProgress},{survivorInfo.PlayerData.PalletStrikes},{survivorInfo.PlayerData.Rescues},{survivorInfo.PlayerData.Heals},{survivorInfo.PlayerData.ContainmentTime}].");
            target.Data.DecodingProgress = survivorInfo.PlayerData.DecodingProgress;
            target.Data.PalletStrikes = survivorInfo.PlayerData.PalletStrikes;
            target.Data.Rescues = survivorInfo.PlayerData.Rescues;
            target.Data.Heals = survivorInfo.PlayerData.Heals;
            target.Data.ContainmentTime = survivorInfo.PlayerData.ContainmentTime;
        }
    }

    private bool IsOcrReady() => _ocrService.GetProviderStatus(_ocrService.SelectedProvider).IsReady;

    private static PlayerData ToHunterData(IReadOnlyList<string> values) => new()
    {
        RemainingCipher = values[0], PalletsDestroyed = values[1], SurvivorHits = values[2], TerrorShocks = values[3], Knockdowns = values[4]
    };

    private static PlayerData ToSurvivorData(IReadOnlyList<string> values) => new()
    {
        DecodingProgress = values[0], PalletStrikes = values[1], Rescues = values[2], Heals = values[3], ContainmentTime = values[4]
    };

    private static string ToLogText(string? text, int maxLength = 120)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var normalized = text.Replace("\r", " ").Replace("\n", " ").Trim();
        return normalized.Length <= maxLength ? normalized : $"{normalized[..maxLength]}...";
    }

    /// <summary>
    /// 表示识别后的单个玩家信息。
    /// </summary>
    /// <param name="PlayerName">玩家名称。</param>
    /// <param name="CharacterName">角色名称。</param>
    /// <param name="PlayerData">玩家数据。</param>
    public record PlayerInfo(string PlayerName, string CharacterName, PlayerData PlayerData);

    private sealed record RecognizedGameData(PlayerData? HunterData, List<PlayerInfo> SurvivorInfos);
}
