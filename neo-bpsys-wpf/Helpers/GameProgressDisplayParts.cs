using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Helpers;

/// <summary>
/// 对局进度显示的结构化部件，供多种显示模式使用。
/// </summary>
public sealed class GameProgressDisplayParts
{
    /// <summary>
    /// 原始进度枚举值。
    /// </summary>
    public GameProgress Progress { get; init; }

    /// <summary>
    /// 是否为 Free 状态。
    /// </summary>
    public bool IsFree { get; init; }

    /// <summary>
    /// 第几局（从 1 开始）。
    /// </summary>
    public int? GameNumber { get; init; }

    /// <summary>
    /// 是否为加赛。
    /// </summary>
    public bool IsOvertime { get; init; }

    /// <summary>
    /// 半场标识。
    /// </summary>
    public GameProgressHalf? Half { get; init; }

    /// <summary>
    /// 仅 Game 标签的文本（如 "GAME 1" 或 "第1局"）。
    /// </summary>
    public string GameText { get; init; } = string.Empty;

    /// <summary>
    /// 仅半场标签的文本（如 "FIRST HALF" 或 "上半局"）。
    /// </summary>
    public string HalfText { get; init; } = string.Empty;

    /// <summary>
    /// 完整文本（兼容旧 Format 的单行输出）。
    /// </summary>
    public string FullText { get; init; } = string.Empty;
}
