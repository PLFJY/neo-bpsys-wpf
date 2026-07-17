namespace neo_bpsys_wpf.SmartBp.Module.Abstractions;

/// <summary>
/// 向赛后数据页面提供最近一次整表 OCR 的诊断快照。
/// </summary>
public interface IGameDataRecognitionDebugState
{
    /// <summary>
    /// 最近一次赛后数据 OCR 快照发生变化时触发。
    /// </summary>
    event EventHandler? SnapshotChanged;

    /// <summary>
    /// 获取最近一次赛后数据 OCR 快照。
    /// </summary>
    GameDataRecognitionDebugSnapshot Current { get; }
}

/// <summary>
/// 最近一次赛后数据整表 OCR 的可显示诊断信息。
/// </summary>
/// <param name="OcrLineCount">OCR 返回的文本行数量。</param>
/// <param name="Rows">已重建的表格行。</param>
/// <param name="Diagnostics">坐标解析诊断。</param>
public sealed record GameDataRecognitionDebugSnapshot(
    int OcrLineCount,
    IReadOnlyList<GameDataRecognitionDebugRow> Rows,
    IReadOnlyList<string> Diagnostics)
{
    /// <summary>
    /// 空调试快照。
    /// </summary>
    public static GameDataRecognitionDebugSnapshot Empty { get; } = new(0, [], []);
}

/// <summary>
/// 一行赛后数据的 OCR 重建与角色解析结果。
/// </summary>
/// <param name="RowIndex">表格固定行索引，0 为监管者，1 至 4 为求生者。</param>
/// <param name="PlayerName">从名称文本分离出的玩家 ID。</param>
/// <param name="ExtractedCharacterName">从括号中分离出的角色文本。</param>
/// <param name="ResolvedCharacterName">通过角色字典解析出的角色名称；未匹配时为 <see langword="null"/>。</param>
/// <param name="Values">按五个数据列顺序重建出的值。</param>
/// <param name="HasAllDataColumns">五个数据列是否都检测到 OCR 文本。</param>
public sealed record GameDataRecognitionDebugRow(
    int RowIndex,
    string PlayerName,
    string ExtractedCharacterName,
    string? ResolvedCharacterName,
    IReadOnlyList<string> Values,
    bool HasAllDataColumns);
