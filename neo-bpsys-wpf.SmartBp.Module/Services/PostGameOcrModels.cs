using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models;
using OpenCvSharp;

namespace neo_bpsys_wpf.Services;

/// <summary>
/// 赛后数据表格几何布局信息，由 <see cref="GameDataTableOcrParser"/> 从本次 OCR 结果推断。
/// </summary>
/// <param name="RowCenters">数据行中心 Y 坐标（完整画面坐标系）。</param>
/// <param name="ColumnCenters">五个数据列中心 X 坐标（完整画面坐标系）。</param>
/// <param name="NumericGridBounds">包含所有数据行列中心的数字网格边界（完整画面坐标系，含少量边距）。</param>
/// <param name="EstimatedRowHeight">估算行高。</param>
/// <param name="EstimatedColumnWidth">估算列宽。</param>
internal sealed record GameDataTableLayout(
    IReadOnlyList<double> RowCenters,
    IReadOnlyList<double> ColumnCenters,
    Rect NumericGridBounds,
    double EstimatedRowHeight,
    double EstimatedColumnWidth);

/// <summary>
/// 赛后数据表格 ROI 配置。使用归一化（0~100 百分比）坐标，与全流程 BP 区域编辑器解耦。
/// </summary>
/// <param name="Name">配置名称，用于诊断日志区分 <c>table-roi</c> 与 <c>full-frame-fallback</c>。</param>
/// <param name="Roi">归一化表格区域。</param>
internal sealed record PostGameTableRegionProfile(string Name, RelativeRect Roi)
{
    /// <summary>
    /// 内置默认赛后数据表格 ROI。范围稍宽松，覆盖五行玩家/角色名称与五列统计数字，
    /// 排除底部通常的动作按钮区域。识别不到足够名称行时会回退到完整画面。
    /// </summary>
    public static PostGameTableRegionProfile BuiltIn { get; } = new(
        "table-roi",
        new RelativeRect(0, 0, 100, 85));

    /// <summary>完整画面回退使用的配置名称。</summary>
    public const string FullFrameFallbackName = "full-frame-fallback";

    /// <summary>表格 ROI 识别结果不足此行数时触发一次完整画面回退。</summary>
    public const int MinUsefulRowCount = 2;
}

/// <summary>
/// 一次赛后数据 OCR 流水线的结构化性能快照。仅用于诊断日志，不进入 SmartBp 长期业务状态。
/// </summary>
internal sealed record PostGameOcrPerformanceSnapshot(
    string Provider,
    string Model,
    string Backend,
    int FrameWidth,
    int FrameHeight,
    int TableRoiWidth,
    int TableRoiHeight,
    long CaptureMs,
    long BitmapToMatMs,
    long PrimaryOcrMs,
    long PrimaryParseMs,
    int PrimaryLineCount,
    int ParsedRowCount,
    int InitialMissingCellCount,
    long SecondaryGridOcrMs,
    int SecondaryGridOcrCallCount,
    int RemainingMissingCellCount,
    long SingleCellOcrMs,
    int SingleCellOcrCallCount,
    int SingleCellVariantCallCount,
    long VisualAnalysisMs,
    long TotalMs,
    string Source)
{
    /// <summary>
    /// 将快照格式化为单行汇总日志文本。
    /// </summary>
    /// <returns>汇总日志文本。</returns>
    public string ToSummaryLine()
    {
        return $"post-game OCR summary: source={Source}; provider={Provider}; model={Model}; backend={Backend}; " +
               $"frame={FrameWidth}x{FrameHeight}; table_roi={TableRoiWidth}x{TableRoiHeight}; " +
               $"capture_ms={CaptureMs}; bitmap_to_mat_ms={BitmapToMatMs}; primary_ocr_ms={PrimaryOcrMs}; primary_parse_ms={PrimaryParseMs}; " +
               $"primary_lines={PrimaryLineCount}; parsed_rows={ParsedRowCount}; initial_missing={InitialMissingCellCount}; " +
               $"grid_ocr_ms={SecondaryGridOcrMs}; grid_ocr_calls={SecondaryGridOcrCallCount}; remaining_missing={RemainingMissingCellCount}; " +
               $"single_cell_ms={SingleCellOcrMs}; single_cell_calls={SingleCellOcrCallCount}; single_cell_variants={SingleCellVariantCallCount}; " +
               $"visual_ms={VisualAnalysisMs}; total_ms={TotalMs}.";
    }
}

/// <summary>
/// 一次赛后数据 OCR 流水线的运行结果。
/// </summary>
/// <param name="Parsed">最终表格解析结果。</param>
/// <param name="Lines">最终合并后的 OCR 文本行（完整画面坐标系）。</param>
/// <param name="Performance">性能快照。</param>
internal sealed record PostGameOcrRunResult(
    GameDataTableParseResult Parsed,
    IReadOnlyList<OcrTextLine> Lines,
    PostGameOcrPerformanceSnapshot Performance);

/// <summary>
/// 赛后数据单元格 OCR 渐进式变体执行的集中阈值。避免安全阈值散落硬编码在多个文件中。
/// </summary>
internal static class PostGameOcrThresholds
{
    /// <summary>
    /// 第一变体提前结束接受的极高置信度阈值。仅在文本为干净纯数字、无尾随噪声、
    /// 且数值符合该统计列允许范围时才允许第一变体单独结束。
    /// </summary>
    public const double FirstVariantEarlyAcceptConfidence = 0.97;

    /// <summary>
    /// 单一干净数字候选被接受所需的最低置信度（无第二票支持时）。
    /// </summary>
    public const double SingleVariantMinimumConfidence = 0.88;

    /// <summary>数字网格整块二次 OCR 最多生成的变体数（original/clahe，必要时 +binary）。</summary>
    public const int MaxNumericGridVariants = 3;

    /// <summary>数字网格上采样的默认缩放倍数。</summary>
    public const double NumericGridUpscaleFactor = 2d;

    /// <summary>合并 OCR 文本行时判断同位置重复的中心点像素容差。</summary>
    public const double MergePositionTolerancePx = 12d;
}

/// <summary>
/// 赛后数据 OCR 流水线的逻辑阶段。用于进度报告与本地化展示。
/// </summary>
public enum PostGameRecognitionStage
{
    /// <summary>空闲或未开始。</summary>
    Idle,
    /// <summary>准备阶段：等待预热、捕获画面。</summary>
    Preparing,
    /// <summary>主表格 OCR：对赛后数据表格 ROI 执行首次识别。</summary>
    PrimaryOcr,
    /// <summary>数字网格整块二次 OCR。</summary>
    GridOcr,
    /// <summary>渐进式单格补救。</summary>
    SingleCell,
    /// <summary>正在将识别结果写回对局数据。</summary>
    Applying,
    /// <summary>识别已完成。</summary>
    Completed
}

/// <summary>
/// 一次赛后数据识别进度的不可变快照。
/// </summary>
/// <param name="Percent">非线性进度百分比（0~100）。</param>
/// <param name="Stage">当前逻辑阶段。</param>
/// <param name="StageText">已本地化的阶段提示文本，供 UI 直接显示。</param>
public sealed record PostGameRecognitionProgress(int Percent, PostGameRecognitionStage Stage, string StageText)
{
    /// <summary>空闲状态快照。</summary>
    public static PostGameRecognitionProgress Idle { get; } = new(0, PostGameRecognitionStage.Idle, string.Empty);
}

/// <summary>
/// 赛后数据识别进度变化事件参数。
/// </summary>
public sealed class PostGameRecognitionProgressEventArgs : EventArgs
{
    /// <summary>获取当前进度快照。</summary>
    public PostGameRecognitionProgress Progress { get; }

    /// <summary>初始化 <see cref="PostGameRecognitionProgressEventArgs"/> 的新实例。</summary>
    /// <param name="progress">进度快照。</param>
    public PostGameRecognitionProgressEventArgs(PostGameRecognitionProgress progress)
    {
        Progress = progress;
    }
}
