using Microsoft.Extensions.Logging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.SmartBp.Module.Abstractions;
using OpenCvSharp;
using System.Diagnostics;
using System.Globalization;
using System.Threading;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 赛后数据 OCR 流水线。封装表格 ROI 裁剪、主 OCR、数字网格整块二次 OCR、
/// 渐进式单格补救以及 OCR 预热，并产出结构化性能快照。
/// </summary>
internal sealed class PostGameOcrEngine
{
    private readonly IOcrService _ocrService;
    private readonly ISmartBpDebugLog _debugLog;
    private readonly ILogger _logger;

    private readonly Lock _warmupGate = new();
    private (SmartBpOcrProviderKind Provider, string? Model)? _warmupSignature;
    private Task _warmupTask = Task.CompletedTask;

    private const int CellCropWidth = 76;
    private const int CellCropHeight = 34;

    /// <summary>
    /// 阶段进度回调。在主 OCR、数字网格 OCR、单格补救开始前被调用。
    /// 回调参数为（非线性百分比, 逻辑阶段）。回调在后台线程触发，订阅方需自行切换到 UI 线程。
    /// </summary>
    public Action<int, PostGameRecognitionStage>? StageProgress { get; set; }

    /// <summary>
    /// 初始化 <see cref="PostGameOcrEngine"/> 的新实例。
    /// </summary>
    /// <param name="ocrService">OCR 服务。</param>
    /// <param name="debugLog">SmartBP 统一识别调试日志。</param>
    /// <param name="logger">日志记录器。</param>
    public PostGameOcrEngine(IOcrService ocrService, ISmartBpDebugLog debugLog, ILogger logger)
    {
        _ocrService = ocrService;
        _debugLog = debugLog;
        _logger = logger;
    }

    /// <summary>
    /// 赛后数据表格 ROI 配置。本轮使用内置默认 ROI，可在测试中覆盖。
    /// </summary>
    public PostGameTableRegionProfile RegionProfile { get; set; } = PostGameTableRegionProfile.BuiltIn;

    /// <summary>
    /// 启动或复用当前 Provider/模型对应的 OCR 预热任务。Provider 或模型发生切换后会重新预热。
    /// 预热失败不会抛出异常，仅记录日志。
    /// </summary>
    /// <returns>代表预热执行的 <see cref="Task"/>。已完成时立即返回。</returns>
    public Task EnsureWarmupAsync()
    {
        var signature = (_ocrService.SelectedProvider, _ocrService.CurrentOcrModelKey);
        lock (_warmupGate)
        {
            if (_warmupSignature != signature || _warmupTask.IsFaulted || _warmupTask.IsCanceled)
            {
                _warmupSignature = signature;
                _warmupTask = Task.Run(RunWarmupCoreAsync);
            }

            return _warmupTask;
        }
    }

    /// <summary>
    /// 返回当前预热任务但不主动启动。供正式识别在开始前等待正在进行的预热。
    /// </summary>
    /// <returns>当前预热任务；未启动过时为已完成的任务。</returns>
    public Task GetActiveWarmupTask()
    {
        lock (_warmupGate)
            return _warmupTask;
    }

    /// <summary>
    /// 运行一次完整赛后数据 OCR 流水线。开始前会等待正在进行的预热任务结束。
    /// </summary>
    /// <param name="fullFrame">完整捕获画面（BGR）。</param>
    /// <param name="captureMs">捕获画面耗时（毫秒）。</param>
    /// <param name="bitmapToMatMs">BitmapSource 转 Mat 耗时（毫秒）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>流水线运行结果，包含解析结果、合并后的文本行与性能快照。</returns>
    public async Task<PostGameOcrRunResult> RunAsync(
        Mat fullFrame,
        long captureMs,
        long bitmapToMatMs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fullFrame);
        var engineSw = Stopwatch.StartNew();

        // 正式识别前等待正在进行的预热（不主动启动新的预热），预热失败不阻断识别。
        try
        {
            await GetActiveWarmupTask().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Post-game OCR warmup task faulted; recognition proceeds.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var provider = _ocrService.SelectedProvider.ToString();
        var model = _ocrService.CurrentOcrModelKey ?? string.Empty;
        var backend = ResolveBackend();

        StageProgress?.Invoke(15, PostGameRecognitionStage.PrimaryOcr);
        var (primaryLines, parsed, primaryOcrMs, primaryParseMs, primaryOcrCallCount, tableRoiWidth, tableRoiHeight, source) =
            RunPrimaryRecognition(fullFrame, cancellationToken);

        var initialMissing = parsed.MissingCells.Count;
        _debugLog.Write("post-game", $"primary finished: source={source}; lines={primaryLines.Count}; rows={parsed.Rows.Count}; initial_missing={initialMissing}.");

        if (initialMissing > 0)
            StageProgress?.Invoke(45, PostGameRecognitionStage.GridOcr);
        var (gridMs, gridCalls) = RunNumericGridRecovery(fullFrame, primaryLines, parsed, cancellationToken, out parsed, out primaryLines, out var remainingAfterGrid);

        if (remainingAfterGrid > 0)
            StageProgress?.Invoke(75, PostGameRecognitionStage.SingleCell);
        var (singleMs, singleCalls, singleVariants, visualMs) = RunProgressiveSingleCellFallback(fullFrame, primaryLines, parsed, cancellationToken, out parsed, out primaryLines);

        engineSw.Stop();
        var totalMs = captureMs + bitmapToMatMs + engineSw.ElapsedMilliseconds;

        var snapshot = new PostGameOcrPerformanceSnapshot(
            provider,
            model,
            backend,
            fullFrame.Width,
            fullFrame.Height,
            tableRoiWidth,
            tableRoiHeight,
            captureMs,
            bitmapToMatMs,
            primaryOcrMs,
            primaryParseMs,
            primaryLines.Count,
            parsed.Rows.Count,
            initialMissing,
            gridMs,
            gridCalls,
            remainingAfterGrid,
            singleMs,
            singleCalls,
            singleVariants,
            visualMs,
            totalMs,
            source);

        _logger.LogInformation("{Summary}", snapshot.ToSummaryLine());
        _debugLog.Write("post-game", snapshot.ToSummaryLine());
        return new PostGameOcrRunResult(parsed, primaryLines, snapshot);
    }

    private (List<OcrTextLine> Lines, GameDataTableParseResult Parsed, long OcrMs, long ParseMs, int OcrCalls, int RoiWidth, int RoiHeight, string Source) RunPrimaryRecognition(
        Mat fullFrame, CancellationToken cancellationToken)
    {
        var profile = RegionProfile;
        var roiPixel = profile.Roi.ToPixelRect(fullFrame.Width, fullFrame.Height);
        var roiRect = ClampRect(new Rect(roiPixel.X, roiPixel.Y, roiPixel.Width, roiPixel.Height), fullFrame.Width, fullFrame.Height);
        var ocrSw = Stopwatch.StartNew();
        List<OcrTextLine> lines;
        int primaryOcrCalls;
        string source;

        using (var roiMat = new Mat(fullFrame, roiRect))
        {
            _debugLog.Write("post-game", $"primary OCR: source=table-roi; roi={roiRect}; frame={fullFrame.Width}x{fullFrame.Height}; provider={_ocrService.SelectedProvider}.");
            var roiResult = _ocrService.RecognizeTextLines(roiMat);
            lines = MapLinesToFullFrame(roiResult.Lines, roiRect.X, roiRect.Y);
            primaryOcrCalls = 1;
            source = profile.Name;
        }

        ocrSw.Stop();
        var parseSw = Stopwatch.StartNew();
        var parsed = GameDataTableOcrParser.Parse(lines);
        parseSw.Stop();

        // ROI 识别不到足够的玩家名称行时，执行一次完整画面 OCR 作为安全回退。
        // 完整画面回退只调用现有 Parser，不复制另一套解析逻辑。
        if (parsed.Rows.Count < PostGameTableRegionProfile.MinUsefulRowCount)
        {
            _debugLog.Write("post-game", $"primary ROI rows={parsed.Rows.Count} < {PostGameTableRegionProfile.MinUsefulRowCount}; falling back to full-frame OCR once.");
            ocrSw.Restart();
            var fullResult = _ocrService.RecognizeTextLines(fullFrame);
            ocrSw.Stop();
            primaryOcrCalls++;
            lines = fullResult.Lines.ToList();
            source = PostGameTableRegionProfile.FullFrameFallbackName;
            LogRawLines("primary full-frame", lines);
            parseSw.Restart();
            parsed = GameDataTableOcrParser.Parse(lines);
            parseSw.Stop();
        }
        else
        {
            LogRawLines("primary table-roi", lines);
        }

        foreach (var diagnostic in parsed.Diagnostics)
            _debugLog.Write("post-game", $"parser: {diagnostic}");

        return (lines, parsed, ocrSw.ElapsedMilliseconds, parseSw.ElapsedMilliseconds, primaryOcrCalls, roiRect.Width, roiRect.Height, source);
    }

    private (long Ms, int Calls) RunNumericGridRecovery(
        Mat fullFrame,
        List<OcrTextLine> primaryLines,
        GameDataTableParseResult parsed,
        CancellationToken cancellationToken,
        out GameDataTableParseResult finalParsed,
        out List<OcrTextLine> finalLines,
        out int remainingMissing)
    {
        finalParsed = parsed;
        finalLines = primaryLines;
        remainingMissing = parsed.MissingCells.Count;

        if (parsed.MissingCells.Count == 0 || parsed.Layout is null)
            return (0, 0);

        var gridBounds = parsed.Layout.NumericGridBounds;
        var cropRect = ClampRect(gridBounds, fullFrame.Width, fullFrame.Height);
        if (cropRect.Width < 8 || cropRect.Height < 8)
            return (0, 0);

        using var gridMat = new Mat(fullFrame, cropRect);
        var variants = CreateNumericGridVariants(gridMat);
        var sw = Stopwatch.StartNew();
        var calls = 0;
        try
        {
            foreach (var (name, image) in variants)
            {
                cancellationToken.ThrowIfCancellationRequested();
                calls++;
                var result = _ocrService.RecognizeTextLines(image);
                var mapped = MapGridLinesToFullFrame(result.Lines, PostGameOcrThresholds.NumericGridUpscaleFactor, cropRect.X, cropRect.Y);
                _debugLog.Write("post-game", $"numeric grid OCR: variant={name}; grid_crop={cropRect}; raw_lines={result.Lines.Count}; mapped_lines={mapped.Count}.");
                finalLines = MergeOcrLines(finalLines, mapped);
                finalParsed = GameDataTableOcrParser.Parse(finalLines);
                foreach (var diagnostic in finalParsed.Diagnostics)
                    _debugLog.Write("post-game", $"parser: {diagnostic}");
                remainingMissing = finalParsed.MissingCells.Count;
                if (remainingMissing == 0)
                    break;
            }
        }
        finally
        {
            foreach (var (_, image) in variants)
                image.Dispose();
        }

        sw.Stop();
        _debugLog.Write("post-game", $"numeric grid OCR finished: calls={calls}; remaining_missing={remainingMissing}.");
        return (sw.ElapsedMilliseconds, calls);
    }

    private (long Ms, int Calls, int VariantCalls, long VisualMs) RunProgressiveSingleCellFallback(
        Mat fullFrame,
        List<OcrTextLine> mergedLines,
        GameDataTableParseResult parsed,
        CancellationToken cancellationToken,
        out GameDataTableParseResult finalParsed,
        out List<OcrTextLine> finalLines)
    {
        finalParsed = parsed;
        finalLines = mergedLines;

        if (parsed.MissingCells.Count == 0)
            return (0, 0, 0, 0);

        _debugLog.Write("post-game", $"single-cell fallback: remaining_missing={parsed.MissingCells.Count}; mode=progressive-variants.");
        var sw = Stopwatch.StartNew();
        var visualSw = Stopwatch.StartNew();
        var visualMs = 0L;
        var callCount = 0;
        var variantCalls = 0;

        foreach (var cell in parsed.MissingCells)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var selection = TryRecognizeSingleCellProgressive(fullFrame, cell, cancellationToken, ref variantCalls, ref visualMs, visualSw);
            callCount++;
            if (selection == null)
            {
                _debugLog.Write("post-game", $"numeric fallback rejected: row={cell.RowIndex}; column={cell.ColumnIndex}.");
                continue;
            }

            var syntheticBox = new Rect(
                Math.Clamp((int)Math.Round(cell.ExpectedCenterX) - 1, 0, Math.Max(0, fullFrame.Width - 2)),
                Math.Clamp((int)Math.Round(cell.ExpectedCenterY) - 1, 0, Math.Max(0, fullFrame.Height - 2)),
                2,
                2);
            finalLines.Add(new OcrTextLine(
                selection.Value,
                selection.Confidence,
                syntheticBox,
                cell.ExpectedCenterX,
                cell.ExpectedCenterY,
                selection.Provider));
            _debugLog.Write("post-game", $"numeric fallback accepted: row={cell.RowIndex}; column={cell.ColumnIndex}; value=[{selection.Value}]; confidence={selection.Confidence:0.000}; support={selection.SupportCount}; provider={selection.Provider}.");
        }

        visualSw.Stop();
        visualMs += visualSw.ElapsedMilliseconds;
        sw.Stop();

        finalParsed = GameDataTableOcrParser.Parse(finalLines);
        foreach (var diagnostic in finalParsed.Diagnostics)
            _debugLog.Write("post-game", $"parser: {diagnostic}");
        _debugLog.Write("post-game", $"single-cell fallback finished: calls={callCount}; variant_calls={variantCalls}; remaining_missing={finalParsed.MissingCells.Count}.");
        return (sw.ElapsedMilliseconds, callCount, variantCalls, visualMs);
    }

    /// <summary>
    /// 对单个缺失格执行渐进式变体识别：original → CLAHE → Otsu → 数字 1 精修 → 形态证据。
    /// 极高置信首票或两个一致变体可提前结束，避免无条件运行全部变体。
    /// </summary>
    /// <param name="fullFrame">完整捕获画面。</param>
    /// <param name="cell">缺失格。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <param name="variantCalls">变体 OCR 调用累计数。</param>
    /// <param name="visualMs">形态分析累计耗时（毫秒）。</param>
    /// <param name="visualSw">形态分析计时器。</param>
    /// <returns>接受的数字候选；未通过安全规则时返回 <see langword="null"/>。</returns>
    private GameDataCellOcrSelection? TryRecognizeSingleCellProgressive(
        Mat fullFrame,
        GameDataTableMissingCell cell,
        CancellationToken cancellationToken,
        ref int variantCalls,
        ref long visualMs,
        Stopwatch visualSw)
    {
        var cropRect = CreateGameDataCellCrop(fullFrame, cell.ExpectedCenterX, cell.ExpectedCenterY, CellCropWidth, CellCropHeight);
        using var cellMat = new Mat(fullFrame, cropRect);
        var candidates = new List<GameDataCellOcrCandidate>();
        var disposables = new List<Mat>();

        try
        {
            var mainVariants = CreateGameDataCellRecognitionVariants(cellMat);
            disposables.AddRange(mainVariants.Select(variant => variant.Image));

            // 变体 1：original。极高置信、干净纯数字且符合列范围时提前结束。
            variantCalls++;
            var c1 = RecognizeSingleCellVariant(mainVariants[0].Image, mainVariants[0].Name, cell);
            if (c1 != null)
            {
                candidates.Add(c1);
                if (TryEarlyAcceptFirstVariant(c1, cell) is { } early)
                    return early;
            }

            // 变体 2：CLAHE。两次干净结果一致即接受。
            variantCalls++;
            var c2 = RecognizeSingleCellVariant(mainVariants[1].Image, mainVariants[1].Name, cell);
            if (c2 != null)
                candidates.Add(c2);
            if (GameDataCellOcrCandidateSelector.Select(candidates) is { } sel2)
                return sel2;

            // 变体 3：Otsu。
            variantCalls++;
            var c3 = RecognizeSingleCellVariant(mainVariants[2].Image, mainVariants[2].Name, cell);
            if (c3 != null)
                candidates.Add(c3);
            if (GameDataCellOcrCandidateSelector.Select(candidates) is { } sel3)
                return sel3;

            // 仅在疑似数字 1 且仍未通过时执行数字 1 精修变体。
            if (HasDigitOneCandidate(candidates))
            {
                var refinementVariants = CreateDigitOneRefinementVariants(cellMat);
                disposables.AddRange(refinementVariants.Select(variant => variant.Image));
                foreach (var (name, image) in refinementVariants)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    variantCalls++;
                    var cr = RecognizeSingleCellVariant(image, name, cell);
                    if (cr != null)
                        candidates.Add(cr);
                    if (GameDataCellOcrCandidateSelector.Select(candidates) is { } selRef)
                        return selRef;
                }

                // 形态证据不能独立产生结果，只能与 OCR 疑似候选联合判断。
                visualSw.Restart();
                var hasVisual = GameDataCellVisualAnalyzer.TryDetectDigitOne(cellMat, out var evidence);
                visualSw.Stop();
                visualMs += visualSw.ElapsedMilliseconds;
                _debugLog.Write("post-game", hasVisual
                    ? $"numeric fallback one visual evidence: row={cell.RowIndex}; column={cell.ColumnIndex}; detected=True; aspect={evidence!.AspectRatio:0.00}; fill={evidence.FillRatio:0.00}; confidence={evidence.Confidence:0.000}."
                    : $"numeric fallback one visual evidence: row={cell.RowIndex}; column={cell.ColumnIndex}; detected=False.");
                if (evidence != null)
                {
                    candidates.Add(new GameDataCellOcrCandidate(
                        "visual-vertical-stroke",
                        "1",
                        evidence.Confidence,
                        "OpenCV/shape"));
                    if (GameDataCellOcrCandidateSelector.Select(candidates) is { } selVisual)
                        return selVisual;
                }
            }

            return null;
        }
        finally
        {
            foreach (var disposable in disposables)
                disposable.Dispose();
        }
    }

    /// <summary>
    /// 第一变体提前结束判断：必须是干净纯数字（无尾随噪声）、置信度达到极高阈值、
    /// 且数值符合该统计列允许的范围。
    /// </summary>
    /// <param name="candidate">第一变体 OCR 候选。</param>
    /// <param name="cell">缺失格。</param>
    /// <returns>可提前结束的候选；不满足时返回 <see langword="null"/>。</returns>
    private static GameDataCellOcrSelection? TryEarlyAcceptFirstVariant(GameDataCellOcrCandidate candidate, GameDataTableMissingCell cell)
    {
        if (!GameDataCellOcrCandidateSelector.TryNormalizeNumericText(candidate.RawText, out var value, out var isExact) || !isExact)
            return null;
        if (candidate.Confidence < PostGameOcrThresholds.FirstVariantEarlyAcceptConfidence)
            return null;
        if (!IsPlausibleValue(value, cell.RowIndex, cell.ColumnIndex))
            return null;
        return new GameDataCellOcrSelection(value, Math.Clamp(candidate.Confidence, 0, 1), 1, candidate.Provider);
    }

    /// <summary>
    /// 按列进行数值范围校验。仅拒绝明显不可能的高置信误识别，不过滤合法极端数据。
    /// 不确定字段（如牵制时间）不增加上限限制。
    /// </summary>
    /// <param name="numericValue">规范化纯数字值。</param>
    /// <param name="rowIndex">数据行索引，0 为监管者行。</param>
    /// <param name="columnIndex">数据列索引。</param>
    /// <returns>数值在合理范围内返回 <see langword="true"/>。</returns>
    private static bool IsPlausibleValue(string numericValue, int rowIndex, int columnIndex)
    {
        if (!int.TryParse(numericValue, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
            return false;
        if (parsed < 0)
            return false;

        // 监管者行：第 0 列为剩余密码机数量（小整数），其余为次数类非负整数。
        if (rowIndex == 0)
            return columnIndex == 0 ? parsed <= 99 : parsed <= 99999;

        // 求生者行：第 0 列为破译进度（百分比，可能超过 100，不封顶），第 4 列为牵制时间（不限制上限）。
        return columnIndex switch
        {
            0 => parsed <= 99999,
            4 => parsed <= 99999,
            _ => parsed <= 99999
        };
    }

    private GameDataCellOcrCandidate? RecognizeSingleCellVariant(Mat img, string variantName, GameDataTableMissingCell cell)
    {
        var result = _ocrService.RecognizeSingleText(img, new OcrRecognitionOptions
        {
            RegionHint = $"post-game-row-{cell.RowIndex}-column-{cell.ColumnIndex}",
            FieldHint = "numeric-cell",
            PreferChinese = false,
            PreferEnglish = false,
            Psm = 10,
            UsePreprocessingVariants = false
        });
        var hasNormalizedValue = GameDataCellOcrCandidateSelector.TryNormalizeNumericText(
            result?.Text, out var normalizedValue, out var isExactValue);
        _debugLog.Write(
            "post-game",
            $"numeric fallback candidate: row={cell.RowIndex}; column={cell.ColumnIndex}; variant={variantName}; text=[{ToLogText(result?.Text)}]; normalized=[{(hasNormalizedValue ? normalizedValue : string.Empty)}]; normalization={(hasNormalizedValue ? isExactValue ? "exact" : "supporting" : "rejected")}; confidence={result?.Confidence.ToString("0.000", CultureInfo.InvariantCulture) ?? "-"}; provider={result?.Provider ?? _ocrService.SelectedProvider.ToString()}.");
        return result == null ? null : new GameDataCellOcrCandidate(variantName, result.Text, result.Confidence, result.Provider);
    }

    private static bool HasDigitOneCandidate(IEnumerable<GameDataCellOcrCandidate> candidates) =>
        candidates.Any(candidate =>
            GameDataCellOcrCandidateSelector.TryNormalizeNumericText(candidate.RawText, out var value, out _) &&
            string.Equals(value, "1", StringComparison.Ordinal));

    private async Task RunWarmupCoreAsync()
    {
        try
        {
            // A. 完整文字检测 + 识别：使用 OpenCV 绘制少量 ASCII 文本，确保 Detection 和 Recognition 真正执行。
            using var textImage = CreateWarmupTextImage();
            _ = _ocrService.RecognizeTextLines(textImage);

            // B. 单文本直接识别：紧密裁剪的数字，确保 Recognizer.Run 路径被预热。
            using var singleImage = CreateWarmupSingleTextImage();
            _ = _ocrService.RecognizeSingleText(singleImage, new OcrRecognitionOptions
            {
                Psm = 10,
                PreferChinese = false,
                PreferEnglish = false,
                UsePreprocessingVariants = false
            });

            _logger.LogDebug("Post-game OCR warmup completed for provider={Provider}; model={Model}.", _ocrService.SelectedProvider, _ocrService.CurrentOcrModelKey);
        }
        catch (Exception ex)
        {
            // 预热失败仅记录，不抛出，不弹窗。
            _logger.LogWarning(ex, "Post-game OCR warmup failed; formal recognition will still proceed.");
            _debugLog.Write("post-game", $"warmup failed: {ToLogText(ex.Message)}");
        }
    }

    private static Mat CreateWarmupTextImage()
    {
        var mat = new Mat(new Size(320, 96), MatType.CV_8UC3, Scalar.White);
        Cv2.PutText(mat, "Player(Test)", new Point(10, 30), HersheyFonts.HersheySimplex, 0.6, Scalar.Black, 1, LineTypes.AntiAlias);
        Cv2.PutText(mat, "123 45", new Point(10, 70), HersheyFonts.HersheySimplex, 0.9, Scalar.Black, 1, LineTypes.AntiAlias);
        return mat;
    }

    private static Mat CreateWarmupSingleTextImage()
    {
        var mat = new Mat(new Size(96, 36), MatType.CV_8UC3, Scalar.White);
        Cv2.PutText(mat, "123", new Point(8, 28), HersheyFonts.HersheySimplex, 1.0, Scalar.Black, 2, LineTypes.AntiAlias);
        return mat;
    }

    private static Rect ClampRect(Rect rect, int width, int height)
    {
        var x = Math.Clamp(rect.X, 0, Math.Max(0, width - 1));
        var y = Math.Clamp(rect.Y, 0, Math.Max(0, height - 1));
        var w = Math.Clamp(rect.Width, 1, Math.Max(1, width - x));
        var h = Math.Clamp(rect.Height, 1, Math.Max(1, height - y));
        return new Rect(x, y, w, h);
    }

    private static Rect CreateGameDataCellCrop(Mat full, double centerX, double centerY, int width, int height)
    {
        var x = Math.Clamp((int)Math.Round(centerX - width / 2d), 0, Math.Max(0, full.Width - width));
        var y = Math.Clamp((int)Math.Round(centerY - height / 2d), 0, Math.Max(0, full.Height - height));
        return new Rect(x, y, Math.Min(width, full.Width - x), Math.Min(height, full.Height - y));
    }

    private static List<OcrTextLine> MapLinesToFullFrame(IReadOnlyList<OcrTextLine> lines, int offsetX, int offsetY)
    {
        if (offsetX == 0 && offsetY == 0)
            return lines.ToList();

        var mapped = new List<OcrTextLine>(lines.Count);
        foreach (var line in lines)
            mapped.Add(MapLineToFullFrame(line, offsetX, offsetY));
        return mapped;
    }

    private static OcrTextLine MapLineToFullFrame(OcrTextLine line, int offsetX, int offsetY) =>
        line with
        {
            BoundingBox = new Rect(line.BoundingBox.X + offsetX, line.BoundingBox.Y + offsetY, line.BoundingBox.Width, line.BoundingBox.Height),
            CenterX = line.CenterX + offsetX,
            CenterY = line.CenterY + offsetY
        };

    private static List<OcrTextLine> MapGridLinesToFullFrame(IReadOnlyList<OcrTextLine> lines, double scale, int offsetX, int offsetY)
    {
        var mapped = new List<OcrTextLine>(lines.Count);
        foreach (var line in lines)
        {
            var box = line.BoundingBox;
            mapped.Add(line with
            {
                BoundingBox = new Rect(
                    (int)Math.Round(box.X / scale) + offsetX,
                    (int)Math.Round(box.Y / scale) + offsetY,
                    Math.Max(1, (int)Math.Round(box.Width / scale)),
                    Math.Max(1, (int)Math.Round(box.Height / scale))),
                CenterX = line.CenterX / scale + offsetX,
                CenterY = line.CenterY / scale + offsetY
            });
        }
        return mapped;
    }

    /// <summary>
    /// 合并 OCR 文本行：去除与已存在同位置同值重复项；新行按置信度降序插入，
    /// 让更高置信的候选优先被 Parser 分配到缺失格，低置信重复项由 Parser 的去重逻辑忽略。
    /// </summary>
    /// <param name="existing">已存在文本行（权威，保留）。</param>
    /// <param name="additional">新增文本行。</param>
    /// <returns>合并后的文本行列表。</returns>
    private static List<OcrTextLine> MergeOcrLines(List<OcrTextLine> existing, IReadOnlyList<OcrTextLine> additional)
    {
        var merged = new List<OcrTextLine>(existing);
        var ordered = additional
            .OrderByDescending(line => line.Confidence)
            .ThenBy(line => line.CenterY)
            .ThenBy(line => line.CenterX);
        var tolerance = PostGameOcrThresholds.MergePositionTolerancePx;
        foreach (var line in ordered)
        {
            var duplicate = merged.Any(existingLine =>
                string.Equals(existingLine.Text, line.Text, StringComparison.Ordinal) &&
                Math.Abs(existingLine.CenterX - line.CenterX) <= tolerance &&
                Math.Abs(existingLine.CenterY - line.CenterY) <= tolerance);
            if (!duplicate)
                merged.Add(line);
        }

        return merged;
    }

    private void LogRawLines(string label, IReadOnlyList<OcrTextLine> lines)
    {
        _debugLog.Write("post-game", $"{label}: line_count={lines.Count}.");
        foreach (var (line, index) in lines.Select((line, index) => (line, index)))
        {
            _debugLog.Write(
                "post-game",
                $"raw_line[{index}]: provider={line.Provider ?? "unknown"}; text=[{ToLogText(line.Text)}]; bbox={line.BoundingBox}; center={line.CenterX:0.0},{line.CenterY:0.0}; confidence={line.Confidence:0.00}.");
        }
    }

    private string ResolveBackend()
    {
        try
        {
            return _ocrService.GetProviderStatus(_ocrService.SelectedProvider).Details ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ToLogText(string? text, int maxLength = 120)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var normalized = text.Replace("\r", " ").Replace("\n", " ").Trim();
        return normalized.Length <= maxLength ? normalized : $"{normalized[..maxLength]}...";
    }

    /// <summary>
    /// 为数字网格生成整块二次 OCR 变体。最多三种：original/clahe/binary，均上采样。
    /// 不复制单格变体，每个变体只由调用方执行一次 <see cref="IOcrService.RecognizeTextLines"/>。
    /// </summary>
    /// <param name="grid">数字网格裁剪图。</param>
    /// <returns>由调用方负责释放的命名变体。</returns>
    private static List<(string Name, Mat Image)> CreateNumericGridVariants(Mat grid)
    {
        const double scale = PostGameOcrThresholds.NumericGridUpscaleFactor;
        var variants = new List<(string Name, Mat Image)>();

        var original = new Mat();
        Cv2.Resize(grid, original, new Size(), scale, scale, InterpolationFlags.Cubic);
        variants.Add(("numeric-grid-original-upscaled", original));

        using var gray = new Mat();
        if (grid.Channels() == 1)
            grid.CopyTo(gray);
        else
            Cv2.CvtColor(grid, gray, grid.Channels() == 4 ? ColorConversionCodes.BGRA2GRAY : ColorConversionCodes.BGR2GRAY);
        using var clahe = Cv2.CreateCLAHE(2.5, new Size(4, 4));
        using var enhanced = new Mat();
        clahe.Apply(gray, enhanced);
        using var enhancedBgr = new Mat();
        Cv2.CvtColor(enhanced, enhancedBgr, ColorConversionCodes.GRAY2BGR);
        var contrast = new Mat();
        Cv2.Resize(enhancedBgr, contrast, new Size(), scale, scale, InterpolationFlags.Cubic);
        variants.Add(("numeric-grid-clahe-upscaled", contrast));

        using var binary = new Mat();
        Cv2.Threshold(enhanced, binary, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
        using var inverted = new Mat();
        Cv2.BitwiseNot(binary, inverted);
        using var invertedBgr = new Mat();
        Cv2.CvtColor(inverted, invertedBgr, ColorConversionCodes.GRAY2BGR);
        var threshold = new Mat();
        Cv2.Resize(invertedBgr, threshold, new Size(), scale, scale, InterpolationFlags.Nearest);
        variants.Add(("numeric-grid-binary-upscaled", threshold));

        return variants;
    }

    /// <summary>
    /// 为已知数字单元格生成直接字符识别所需的图像变体。
    /// </summary>
    /// <param name="cell">单元格原始裁剪图。</param>
    /// <returns>由调用方负责释放图像的命名变体。</returns>
    private static List<(string Name, Mat Image)> CreateGameDataCellRecognitionVariants(Mat cell)
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
    /// <param name="cell">单元格原始裁剪图。</param>
    /// <returns>由调用方负责释放图像的命名变体。</returns>
    private static List<(string Name, Mat Image)> CreateDigitOneRefinementVariants(Mat cell)
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
}
