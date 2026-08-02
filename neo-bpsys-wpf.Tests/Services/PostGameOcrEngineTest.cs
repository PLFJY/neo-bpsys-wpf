extern alias smartbp;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using neo_bpsys_wpf.Core.Models;
using OpenCvSharp;
using Xunit;
using IOcrService = smartbp::neo_bpsys_wpf.Core.Abstractions.Services.IOcrService;
using OcrModelDefinition = smartbp::neo_bpsys_wpf.Core.Abstractions.Services.OcrModelDefinition;
using OcrRecognitionOptions = smartbp::neo_bpsys_wpf.Core.Abstractions.Services.OcrRecognitionOptions;
using OcrSingleTextResult = smartbp::neo_bpsys_wpf.Core.Abstractions.Services.OcrSingleTextResult;
using OcrTextBlockResult = smartbp::neo_bpsys_wpf.Core.Abstractions.Services.OcrTextBlockResult;
using OcrTextLine = smartbp::neo_bpsys_wpf.Core.Abstractions.Services.OcrTextLine;
using SmartBpDebugMessageEventArgs = smartbp::neo_bpsys_wpf.SmartBp.Module.Models.Recognition.SmartBpDebugMessageEventArgs;
using ISmartBpDebugLog = smartbp::neo_bpsys_wpf.SmartBp.Module.Abstractions.ISmartBpDebugLog;
using SmartBpOcrProviderKind = smartbp::neo_bpsys_wpf.Core.Abstractions.Services.SmartBpOcrProviderKind;
using SmartBpOcrProviderStatus = smartbp::neo_bpsys_wpf.Core.Abstractions.Services.SmartBpOcrProviderStatus;
using GameDataTableLayout = smartbp::neo_bpsys_wpf.Services.GameDataTableLayout;
using GameDataTableMissingCell = smartbp::neo_bpsys_wpf.Services.GameDataTableMissingCell;
using GameDataTableOcrParser = smartbp::neo_bpsys_wpf.Services.GameDataTableOcrParser;
using GameDataTableParseResult = smartbp::neo_bpsys_wpf.Services.GameDataTableParseResult;
using GameDataTableRow = smartbp::neo_bpsys_wpf.Services.GameDataTableRow;
using PostGameOcrEngine = smartbp::neo_bpsys_wpf.Services.PostGameOcrEngine;
using PostGameOcrPerformanceSnapshot = smartbp::neo_bpsys_wpf.Services.PostGameOcrPerformanceSnapshot;
using PostGameOcrRunResult = smartbp::neo_bpsys_wpf.Services.PostGameOcrRunResult;
using PostGameTableRegionProfile = smartbp::neo_bpsys_wpf.Services.PostGameTableRegionProfile;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// 可计数、可配置结果的 OCR 服务测试替身。用于验证赛后数据 OCR 流水线调用次数和顺序。
/// </summary>
public sealed class CountingOcrService : IOcrService
{
    private readonly Queue<Exception?> _textLinesExceptions = new();
    private readonly Queue<OcrTextBlockResult> _textLinesResults = new();
    private readonly Queue<Exception?> _singleTextExceptions = new();
    private readonly Queue<OcrSingleTextResult?> _singleTextResults = new();

    public SmartBpOcrProviderKind SelectedProvider { get; set; } = SmartBpOcrProviderKind.Paddle;
    public string? CurrentOcrModelKey { get; set; } = "test-model";
    public int RecognizeTextLinesCallCount { get; private set; }
    public int RecognizeSingleTextCallCount { get; private set; }
    public ManualResetEventSlim? FirstTextLinesGate { get; set; }

    public void EnqueueTextLines(OcrTextBlockResult result) => _textLinesResults.Enqueue(result);
    public void EnqueueTextLines(Exception ex) => _textLinesExceptions.Enqueue(ex);
    public void EnqueueSingleText(OcrSingleTextResult? result) => _singleTextResults.Enqueue(result);
    public void EnqueueSingleText(Exception ex) => _singleTextExceptions.Enqueue(ex);
    public void EnqueueEmptyTextLines(int count = 1)
    {
        for (var i = 0; i < count; i++)
            _textLinesResults.Enqueue(OcrTextBlockResult.Empty);
    }

    public OcrTextBlockResult RecognizeTextLines(Mat img)
    {
        var callIndex = RecognizeTextLinesCallCount++;
        if (callIndex == 0 && FirstTextLinesGate != null)
            FirstTextLinesGate.Wait();
        if (_textLinesExceptions.TryDequeue(out var ex) && ex != null)
            throw ex;
        return _textLinesResults.TryDequeue(out var result) ? result : OcrTextBlockResult.Empty;
    }

    public OcrSingleTextResult? RecognizeSingleText(Mat img, OcrRecognitionOptions? options = null)
    {
        RecognizeSingleTextCallCount++;
        if (_singleTextExceptions.TryDequeue(out var ex) && ex != null)
            throw ex;
        return _singleTextResults.TryDequeue(out var result) ? result : null;
    }

    // 以下成员为接口实现，赛后 OCR 流水线不使用，返回默认值。
    public bool IsDownloading => false;
    public bool IsDownloadPaused => false;
    public double? DownloadProgress => null;
    public string DownloadStatusText => string.Empty;
    public bool IsModelLoading => false;
    public event EventHandler? DownloadStateChanged;
    public event EventHandler? ModelLoadStateChanged;
    public SmartBpOcrProviderStatus GetProviderStatus(SmartBpOcrProviderKind kind) =>
        new(kind, true, null, "test-backend");
    public IReadOnlyList<OcrModelDefinition> GetAvailableModels() => [];
    public bool IsModelInstalled(string modelKey) => true;
    public Task DownloadModelAsync(string modelKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void CancelDownload() { }
    public void PauseDownload() { }
    public void ResumeDownload() { }
    public bool TryDeleteModel(string modelKey, out string errorMessage) { errorMessage = ""; return true; }
    public bool TrySwitchOcrModel(string modelKey, out string errorMessage) { errorMessage = ""; return true; }
    public void StartLoadingPreferredModel() { }
    public string? RecognizeText(Mat bin) => null;
}

/// <summary>
/// 空操作 SmartBP 调试日志替身。
/// </summary>
public sealed class NoopDebugLog : ISmartBpDebugLog
{
    public event EventHandler<SmartBpDebugMessageEventArgs>? MessageWritten;
    public bool IsEnabled { get; set; } = true;
    public void Write(string source, string message) { }
}

public sealed class PostGameOcrEngineTest
{
    private const double GridUpscaleFactor = 2.0;

    // ==================== 性能调用次数测试 ====================

    [Fact]
    public async Task NoMissingCellsRunsOnlyPrimaryOcr()
    {
        var ocr = new CountingOcrService();
        ocr.EnqueueTextLines(new OcrTextBlockResult(BuildCompleteTable(), "", "test"));
        var engine = CreateEngine(ocr);

        var result = await engine.RunAsync(CreateTestFrame(), 0, 0, default);

        Assert.Equal(0, result.Parsed.MissingCells.Count);
        Assert.Equal(1, ocr.RecognizeTextLinesCallCount);
        Assert.Equal(0, ocr.RecognizeSingleTextCallCount);
        Assert.Equal(0, result.Performance.SecondaryGridOcrCallCount);
        Assert.Equal(0, result.Performance.SingleCellOcrCallCount);
    }

    [Fact]
    public async Task MissingCellsUseGridOcrBeforeSingleCellFallback()
    {
        var ocr = new CountingOcrService();
        var primaryLines = BuildTableMissingCells([(1, 0), (2, 2)]);
        ocr.EnqueueTextLines(new OcrTextBlockResult(primaryLines, "", "test"));
        ocr.EnqueueEmptyTextLines(3);
        ocr.EnqueueSingleText(new OcrSingleTextResult("11", 0.98, "test"));

        var engine = CreateEngine(ocr);
        var result = await engine.RunAsync(CreateTestFrame(), 0, 0, default);

        Assert.Equal(4, ocr.RecognizeTextLinesCallCount);
        Assert.True(ocr.RecognizeSingleTextCallCount > 0);
        Assert.True(result.Performance.SecondaryGridOcrCallCount > 0);
    }

    [Fact]
    public async Task GridRecoveryAvoidsPerCellOcr()
    {
        var ocr = new CountingOcrService();
        var primaryLines = BuildTableMissingCells([(1, 0), (2, 2), (3, 4)]);
        var gridLines = BuildGridRecoveryLines(primaryLines);
        ocr.EnqueueTextLines(new OcrTextBlockResult(primaryLines, "", "test"));
        ocr.EnqueueTextLines(new OcrTextBlockResult(gridLines, "", "test"));

        var engine = CreateEngine(ocr);
        var result = await engine.RunAsync(CreateTestFrame(), 0, 0, default);

        Assert.Equal(0, result.Parsed.MissingCells.Count);
        Assert.Equal(2, ocr.RecognizeTextLinesCallCount);
        Assert.Equal(0, ocr.RecognizeSingleTextCallCount);
    }

    [Fact]
    public async Task ManyMissingCellsDoNotCauseLinearVariantExplosion()
    {
        var ocr = new CountingOcrService();
        var missing = Enumerable.Range(2, 3)
            .SelectMany(row => Enumerable.Range(0, 5).Select(col => (Row: row, Col: col)))
            .ToArray();
        var primaryLines = BuildTableMissingCells(missing);
        var gridLines = BuildGridRecoveryLines(primaryLines);
        ocr.EnqueueTextLines(new OcrTextBlockResult(primaryLines, "", "test"));
        ocr.EnqueueTextLines(new OcrTextBlockResult(gridLines, "", "test"));

        var engine = CreateEngine(ocr);
        var result = await engine.RunAsync(CreateTestFrame(), 0, 0, default);

        Assert.Equal(0, result.Parsed.MissingCells.Count);
        Assert.True(ocr.RecognizeTextLinesCallCount <= 4);
        Assert.Equal(0, ocr.RecognizeSingleTextCallCount);
    }

    // ==================== 渐进式单格识别测试 ====================

    [Fact]
    public async Task ExtremelyConfidentExactFirstVariantStopsEarly()
    {
        var ocr = new CountingOcrService();
        ocr.EnqueueTextLines(new OcrTextBlockResult(BuildTableMissingCells([(1, 0)]), "", "test"));
        ocr.EnqueueEmptyTextLines(3);
        ocr.EnqueueSingleText(new OcrSingleTextResult("14", 0.98, "test"));

        var engine = CreateEngine(ocr);
        var result = await engine.RunAsync(CreateTestFrame(), 0, 0, default);

        Assert.Equal(1, ocr.RecognizeSingleTextCallCount);
        Assert.Equal(0, result.Parsed.MissingCells.Count);
    }

    [Fact]
    public async Task TwoAgreeingVariantsStopBeforeThirdVariant()
    {
        var ocr = new CountingOcrService();
        ocr.EnqueueTextLines(new OcrTextBlockResult(BuildTableMissingCells([(1, 0)]), "", "test"));
        ocr.EnqueueEmptyTextLines(3);
        ocr.EnqueueSingleText(new OcrSingleTextResult("14", 0.85, "test"));
        ocr.EnqueueSingleText(new OcrSingleTextResult("14", 0.85, "test"));

        var engine = CreateEngine(ocr);
        var result = await engine.RunAsync(CreateTestFrame(), 0, 0, default);

        Assert.Equal(2, ocr.RecognizeSingleTextCallCount);
        Assert.Equal(0, result.Parsed.MissingCells.Count);
    }

    [Fact]
    public async Task ConflictingVariantsRunThirdVariant()
    {
        var ocr = new CountingOcrService();
        ocr.EnqueueTextLines(new OcrTextBlockResult(BuildTableMissingCells([(1, 0)]), "", "test"));
        ocr.EnqueueEmptyTextLines(3);
        ocr.EnqueueSingleText(new OcrSingleTextResult("14", 0.85, "test"));
        ocr.EnqueueSingleText(new OcrSingleTextResult("23", 0.85, "test"));
        ocr.EnqueueSingleText(new OcrSingleTextResult("14", 0.85, "test"));

        var engine = CreateEngine(ocr);
        var result = await engine.RunAsync(CreateTestFrame(), 0, 0, default);

        Assert.Equal(3, ocr.RecognizeSingleTextCallCount);
        Assert.Equal(0, result.Parsed.MissingCells.Count);
    }

    [Fact]
    public async Task DigitOneRefinementRunsOnlyForSuspectedOne()
    {
        var ocr = new CountingOcrService();
        ocr.EnqueueTextLines(new OcrTextBlockResult(BuildTableMissingCells([(1, 0)]), "", "test"));
        ocr.EnqueueEmptyTextLines(3);
        ocr.EnqueueSingleText(new OcrSingleTextResult("1", 0.85, "test"));
        ocr.EnqueueSingleText(new OcrSingleTextResult("noise", 0.5, "test"));
        ocr.EnqueueSingleText(new OcrSingleTextResult("-", 0.5, "test"));
        ocr.EnqueueSingleText(new OcrSingleTextResult("1", 0.85, "test"));

        var engine = CreateEngine(ocr);
        var result = await engine.RunAsync(CreateTestFrame(), 0, 0, default);

        Assert.True(ocr.RecognizeSingleTextCallCount > 3);
        Assert.Equal(0, result.Parsed.MissingCells.Count);
    }

    [Fact]
    public async Task VisualDigitOneEvidenceCannotWinWithoutOcrSupport()
    {
        var ocr = new CountingOcrService();
        ocr.EnqueueTextLines(new OcrTextBlockResult(BuildTableMissingCells([(1, 0)]), "", "test"));
        ocr.EnqueueEmptyTextLines(3);
        ocr.EnqueueSingleText(new OcrSingleTextResult("abc", 0.5, "test"));
        ocr.EnqueueSingleText(new OcrSingleTextResult("def", 0.5, "test"));
        ocr.EnqueueSingleText(new OcrSingleTextResult("ghi", 0.5, "test"));

        var engine = CreateEngine(ocr);
        var result = await engine.RunAsync(CreateTestFrame(), 0, 0, default);

        Assert.Equal(3, ocr.RecognizeSingleTextCallCount);
        Assert.Equal(1, result.Parsed.MissingCells.Count);
    }

    // ==================== ROI 测试 ====================

    [Fact]
    public async Task PostGameTableRoiMapsOcrCoordinatesToFullFrame()
    {
        var ocr = new CountingOcrService();
        var engine = CreateEngine(ocr);

        const double roiXPercent = 10;
        const double roiYPercent = 20;
        engine.RegionProfile = new PostGameTableRegionProfile(
            "test-roi",
            new RelativeRect(roiXPercent, roiYPercent, 80, 70));

        const int frameW = 1920;
        const int frameH = 1080;
        var roiOffsetX = (int)Math.Round(roiXPercent / 100.0 * frameW);
        var roiOffsetY = (int)Math.Round(roiYPercent / 100.0 * frameH);

        var roiLocalLines = BuildCompleteTable();
        ocr.EnqueueTextLines(new OcrTextBlockResult(roiLocalLines, "", "test"));

        var result = await engine.RunAsync(CreateTestFrame(frameW, frameH), 0, 0, default);

        Assert.True(result.Parsed.Rows.Count >= 2);
        var expectedFirstRowY = 140 + roiOffsetY;
        Assert.Equal(expectedFirstRowY, result.Parsed.Rows[0].CenterY, 1);
    }

    [Fact]
    public async Task InsufficientNameRowsFallsBackToFullFrameOnce()
    {
        var ocr = new CountingOcrService();
        ocr.EnqueueTextLines(new OcrTextBlockResult([new OcrTextLine("7", 0.9, new Rect(250, 170, 40, 20), 250, 175, "test")], "", "test"));
        ocr.EnqueueTextLines(new OcrTextBlockResult(BuildCompleteTable(), "", "test"));

        var engine = CreateEngine(ocr);
        var result = await engine.RunAsync(CreateTestFrame(), 0, 0, default);

        Assert.Equal(2, ocr.RecognizeTextLinesCallCount);
        Assert.Equal(5, result.Parsed.Rows.Count);
        Assert.Equal(PostGameTableRegionProfile.FullFrameFallbackName, result.Performance.Source);
    }

    [Fact]
    public async Task FullFrameFallbackDoesNotDuplicateParserImplementation()
    {
        var ocr = new CountingOcrService();
        ocr.EnqueueTextLines(OcrTextBlockResult.Empty);
        var fullFrameLines = BuildCompleteTable();
        ocr.EnqueueTextLines(new OcrTextBlockResult(fullFrameLines, "", "test"));

        var engine = CreateEngine(ocr);
        var result = await engine.RunAsync(CreateTestFrame(), 0, 0, default);

        var directParse = GameDataTableOcrParser.Parse(fullFrameLines);
        Assert.Equal(directParse.Rows.Count, result.Parsed.Rows.Count);
        for (var i = 0; i < directParse.Rows.Count; i++)
        {
            Assert.Equal(directParse.Rows[i].PlayerName, result.Parsed.Rows[i].PlayerName);
            Assert.Equal(directParse.Rows[i].Values, result.Parsed.Rows[i].Values);
        }
    }

    // ==================== 预热测试 ====================

    [Fact]
    public async Task WarmupRunsOnlyOncePerProviderAndModel()
    {
        var ocr = new CountingOcrService();
        var engine = CreateEngine(ocr);

        await engine.EnsureWarmupAsync();
        var firstCallCount = ocr.RecognizeTextLinesCallCount;
        await engine.EnsureWarmupAsync();

        Assert.Equal(firstCallCount, ocr.RecognizeTextLinesCallCount);
        Assert.Equal(1, ocr.RecognizeTextLinesCallCount);
        Assert.Equal(1, ocr.RecognizeSingleTextCallCount);
    }

    [Fact]
    public async Task ModelSwitchInvalidatesWarmup()
    {
        var ocr = new CountingOcrService();
        var engine = CreateEngine(ocr);

        await engine.EnsureWarmupAsync();
        ocr.CurrentOcrModelKey = "different-model";
        await engine.EnsureWarmupAsync();

        Assert.Equal(2, ocr.RecognizeTextLinesCallCount);
        Assert.Equal(2, ocr.RecognizeSingleTextCallCount);
    }

    [Fact]
    public async Task WarmupFailureDoesNotBlockPostGameRecognition()
    {
        var ocr = new CountingOcrService();
        ocr.EnqueueTextLines(new InvalidOperationException("warmup boom"));
        ocr.EnqueueTextLines(new OcrTextBlockResult(BuildCompleteTable(), "", "test"));

        var engine = CreateEngine(ocr);
        await engine.EnsureWarmupAsync();
        var result = await engine.RunAsync(CreateTestFrame(), 0, 0, default);

        Assert.Equal(5, result.Parsed.Rows.Count);
        Assert.Equal(0, result.Parsed.MissingCells.Count);
    }

    [Fact]
    public async Task FormalRecognitionWaitsForActiveWarmup()
    {
        var ocr = new CountingOcrService();
        var gate = new ManualResetEventSlim(false);
        ocr.FirstTextLinesGate = gate;
        ocr.EnqueueTextLines(OcrTextBlockResult.Empty);
        ocr.EnqueueTextLines(new OcrTextBlockResult(BuildCompleteTable(), "", "test"));

        var engine = CreateEngine(ocr);
        var warmupTask = engine.EnsureWarmupAsync();

        var runTask = Task.Run(() => engine.RunAsync(CreateTestFrame(), 0, 0, default));
        Thread.Sleep(100);
        Assert.Equal(1, ocr.RecognizeTextLinesCallCount);

        gate.Set();
        await warmupTask;
        var result = await runTask;

        Assert.True(ocr.RecognizeTextLinesCallCount >= 2);
        Assert.Equal(5, result.Parsed.Rows.Count);
    }

    // ==================== 性能统计测试 ====================

    [Fact]
    public async Task PerformanceSnapshotReportsOcrCallCounts()
    {
        var ocr = new CountingOcrService();
        ocr.EnqueueTextLines(new OcrTextBlockResult(BuildCompleteTable(), "", "test"));
        var engine = CreateEngine(ocr);

        var result = await engine.RunAsync(CreateTestFrame(), 10, 5, default);

        Assert.Equal(0, result.Performance.SecondaryGridOcrCallCount);
        Assert.Equal(0, result.Performance.SingleCellOcrCallCount);
        Assert.Equal(0, result.Performance.SingleCellVariantCallCount);
        Assert.Equal(0, result.Performance.InitialMissingCellCount);
    }

    [Fact]
    public async Task PerformanceSnapshotReportsAllPipelineDurations()
    {
        var ocr = new CountingOcrService();
        ocr.EnqueueTextLines(new OcrTextBlockResult(BuildCompleteTable(), "", "test"));
        var engine = CreateEngine(ocr);

        var result = await engine.RunAsync(CreateTestFrame(), 10, 5, default);
        var s = result.Performance;

        Assert.True(s.CaptureMs >= 0);
        Assert.True(s.BitmapToMatMs >= 0);
        Assert.True(s.PrimaryOcrMs >= 0);
        Assert.True(s.PrimaryParseMs >= 0);
        Assert.True(s.SecondaryGridOcrMs >= 0);
        Assert.True(s.SingleCellOcrMs >= 0);
        Assert.True(s.VisualAnalysisMs >= 0);
        Assert.True(s.TotalMs >= 0);
        Assert.True(s.FrameWidth > 0);
        Assert.True(s.FrameHeight > 0);
        Assert.True(s.TableRoiWidth > 0);
        Assert.True(s.TableRoiHeight > 0);
        Assert.False(string.IsNullOrEmpty(s.Provider));
        Assert.False(string.IsNullOrEmpty(s.Source));
    }

    // ==================== 辅助方法 ====================

    private static PostGameOcrEngine CreateEngine(CountingOcrService ocr) =>
        new(ocr, new NoopDebugLog(), new Mock<ILogger>().Object);

    private static Mat CreateTestFrame(int width = 1920, int height = 1080) =>
        new(new Size(width, height), MatType.CV_8UC3, Scalar.White);

    private static OcrTextLine Line(string text, double x, double y, double conf = 0.95) =>
        new(text, conf, new Rect((int)x - 20, (int)y - 10, 40, 20), x, y, "test");

    private static List<OcrTextLine> BuildCompleteTable(double offsetX = 0, double offsetY = 0)
    {
        var lines = new List<OcrTextLine>();
        for (var row = 0; row < 5; row++)
        {
            var nameY = 140 + row * 100 + offsetY;
            lines.Add(Line($"玩家{row}(角色{row})", 80 + offsetX, nameY));
            for (var col = 0; col < 5; col++)
                lines.Add(Line($"{row}{col + 1}", 250 + col * 110 + offsetX, nameY + 35));
        }
        return lines;
    }

    private static List<OcrTextLine> BuildTableMissingCells(params (int Row, int Col)[] missing) =>
        BuildTableMissingCells((IEnumerable<(int, int)>)missing);

    private static List<OcrTextLine> BuildTableMissingCells(IEnumerable<(int Row, int Col)> missing)
    {
        var lines = BuildCompleteTable();
        foreach (var (row, col) in missing)
        {
            var nameY = 140 + row * 100;
            var dataX = 250 + col * 110;
            lines.RemoveAll(l => Math.Abs(l.CenterX - dataX) < 1 && Math.Abs(l.CenterY - (nameY + 35)) < 1);
        }
        return lines;
    }

    private static List<OcrTextLine> BuildGridRecoveryLines(List<OcrTextLine> primaryLines)
    {
        var parsed = GameDataTableOcrParser.Parse(primaryLines);
        if (parsed.Layout is null)
            return new List<OcrTextLine>();

        var cropRect = parsed.Layout.NumericGridBounds;
        var gridLines = new List<OcrTextLine>();
        foreach (var cell in parsed.MissingCells)
        {
            var gridX = (cell.ExpectedCenterX - cropRect.X) * GridUpscaleFactor;
            var gridY = (cell.ExpectedCenterY - cropRect.Y) * GridUpscaleFactor;
            gridLines.Add(Line($"{cell.RowIndex}{cell.ColumnIndex + 1}", gridX, gridY));
        }
        return gridLines;
    }
}
