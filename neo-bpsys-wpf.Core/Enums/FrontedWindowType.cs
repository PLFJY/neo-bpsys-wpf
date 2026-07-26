namespace neo_bpsys_wpf.Core.Enums;

/// <summary>
/// 前台窗口类型枚举
/// </summary>
public enum FrontedWindowType
{
    /// <summary>BP窗口</summary>
    BpWindow,
    /// <summary>转场窗口</summary>
    CutSceneWindow,
    /// <summary>
    /// 为统一比分窗口操作设定的一个统一的类型，等同于同时操作以下三个比分窗口
    /// </summary>
    ScoreWindow,
    /// <summary>求生者比分窗口</summary>
    ScoreSurWindow,
    /// <summary>监管者比分窗口</summary>
    ScoreHunWindow,
    /// <summary>全局比分窗口</summary>
    ScoreGlobalWindow,
    /// <summary>对局数据窗口</summary>
    GameDataWindow,
    /// <summary>BP概览窗口</summary>
    BpOverviewWindow,
    /// <summary>地图V2窗口</summary>
    MapV2Window
}
