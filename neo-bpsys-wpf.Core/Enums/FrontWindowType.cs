namespace neo_bpsys_wpf.Core.Enums;

/// <summary>
/// 前台窗口类型枚举
/// </summary>
public enum FrontWindowType
{
    BpWindow,
    CutSceneWindow,
    /// <summary>
    /// 为统一比分窗口操作设定的一个统一的类型，等同于同时操作以下三个比分窗口
    /// </summary>
    ScoreWindow,
    ScoreSurWindow,
    ScoreHunWindow,
    ScoreGlobalWindow,
    GameDataWindow,
    WidgetsWindow
}