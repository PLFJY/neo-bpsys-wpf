using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Core.Models.ScoreSystem;

/// <summary>
/// 供 Designer v3 BindingPath 按当前 BO 上下文读取半场比分的只读索引视图。
/// </summary>
/// <remarks>
/// 该视图刻意不把 <see cref="GameProgress"/> 的数值当作数组位置；重叠值 6/7 仍由
/// <see cref="MatchScoreState.GetHalf(GameProgress,bool)"/> 的权威映射结合当前 BO 模式解析。
/// </remarks>
public sealed class MatchScoreHalfByProgressView
{
    private readonly MatchScoreState _owner;

    internal MatchScoreHalfByProgressView(MatchScoreState owner)
    {
        _owner = owner;
    }

    /// <summary>
    /// 按对局进度和当前 BO 模式取得对应的单半场比分。
    /// </summary>
    /// <param name="progress">要解析的对局进度。</param>
    /// <returns>对应半场；进度不代表有效半场时为 <see langword="null"/>。</returns>
    public ScoreHalf? this[GameProgress progress] => _owner.GetHalf(progress, _owner.LastRecalculateIsBo3Mode);
}
