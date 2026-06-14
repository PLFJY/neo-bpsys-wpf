using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Core.Abstractions;
using System.Text.Json.Serialization;

namespace neo_bpsys_wpf.Core.Models;

/// <summary>
/// 旧比分镜像，用于仍读取 Team.Score 的过渡显示路径。
/// </summary>
/// <remarks>
/// Score System v2 的权威比分状态是 <see cref="ScoreSystem.MatchScoreState"/>；此类型只保留 legacy
/// `Team.Score` 显示兼容，不能作为新比分写入入口。
/// </remarks>
public partial class Score : ObservableObjectBase
{
    /// <summary>
    /// 大比分--胜
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MajorPointsOnFront))]
    private int _win;

    /// <summary>
    /// 大比分--平
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MajorPointsOnFront))]
    private int _tie;

    /// <summary>
    /// 小比分
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MajorPointsOnFront))]
    private int _gameScores;

    /// <summary>
    /// 前台显示的格式化的大比分
    /// </summary>
    [JsonIgnore] public string MajorPointsOnFront => $"W{Win}  D{Tie}";

    /// <summary>
    /// 重置所有比分数据为零。
    /// </summary>
    public void Reset()
    {
        Win = 0;
        Tie = 0;
        GameScores = 0;
    }
}
