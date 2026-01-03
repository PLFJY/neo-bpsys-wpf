using neo_bpsys_wpf.Core.Enums;
using System.Windows;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 前台窗口接口服务
/// </summary>
public interface IFrontedWindowService
{
    /// <summary>
    /// 前台画布列表
    /// </summary>
    List<(FrontedWindowType, string)> FrontedCanvas { get; }
    /// <summary>
    /// 前台窗口列表
    /// </summary>
    Dictionary<FrontedWindowType, Window> FrontedWindows { get; }
    /// <summary>
    /// 前台窗口状态列表
    /// </summary>
    Dictionary<FrontedWindowType, bool> FrontedWindowStates { get; }
    /// <summary>
    /// 隐藏全部窗口
    /// </summary>
    void AllWindowHide();
    /// <summary>
    /// 显示全部窗口
    /// </summary>
    void AllWindowShow();
    /// <summary>
    /// 呼吸灯启动
    /// </summary>
    /// <param name="windowType">窗口类型</param>
    /// <param name="controlNameHeader">控件名称头</param>
    /// <param name="controlIndex">控件索引(-1表示没有)</param>
    /// <param name="controlNameFooter">控件名称尾</param>
    /// <returns></returns>
    Task BreathingStart(FrontedWindowType windowType, string controlNameHeader, int controlIndex, string controlNameFooter);
    /// <summary>
    /// 呼吸灯停止
    /// </summary>
    /// <param name="windowType">窗口类型</param>
    /// <param name="controlNameHeader">控件名称头</param>
    /// <param name="controlIndex">控件索引(-1表示没有)</param>
    /// <param name="controlNameFooter">控件名称尾</param>
    /// <returns></returns>
    Task BreathingStop(FrontedWindowType windowType, string controlNameHeader, int controlIndex, string controlNameFooter);
    /// <summary>
    /// 渐显动画
    /// </summary>
    /// <param name="windowType">窗口类型</param>
    /// <param name="controlNameHeader">控件名称头</param>
    /// <param name="controlIndex">控件索引(-1表示没有)</param>
    /// <param name="controlNameFooter">控件名称尾</param>
    /// <returns></returns>
    void FadeInAnimation(FrontedWindowType windowType, string controlNameHeader, int controlIndex, string controlNameFooter);
    /// <summary>
    /// 渐隐动画
    /// </summary>
    /// <param name="windowType">窗口类型</param>
    /// <param name="controlNameHeader">控件名称头</param>
    /// <param name="controlIndex">控件索引(-1表示没有)</param>
    /// <param name="controlNameFooter">控件名称尾</param>
    /// <returns></returns>
    void FadeOutAnimation(FrontedWindowType windowType, string controlNameHeader, int controlIndex, string controlNameFooter);

    /// <summary>
    /// 获取窗口名称
    /// </summary>
    /// <param name="windowType"></param>
    /// <returns></returns>
    string? GetWindowName(FrontedWindowType windowType);

    /// <summary>
    /// 隐藏窗口
    /// </summary>
    /// <param name="windowType">窗口类型</param>
    void HideWindow(FrontedWindowType windowType);
    void RegisterFrontedWindowAndCanvas(FrontedWindowType windowType, Window window, string canvasName = "BaseCanvas");

    /// <summary>
    /// 重置全局分数
    /// </summary>
    void ResetGlobalScore();
    /// <summary>
    /// 恢复初始位置
    /// </summary>
    /// <param name="windowType">窗口类型</param>
    /// <param name="canvasName"></param>
    /// <returns></returns>
    Task RestoreInitialPositions(FrontedWindowType windowType, string canvasName = "BaseCanvas");
    /// <summary>
    /// 保存所有窗口元素位置
    /// </summary>
    void SaveAllWindowElementsPosition();
    /// <summary>
    /// 保存指定窗口的指定Canvas中元素的位置信息
    /// </summary>
    /// <param name="windowType">窗口类型</param>
    /// <param name="canvasName">画布名称</param>
    void SaveWindowCanvasElementsPosition(FrontedWindowType windowType, string canvasName = "BaseCanvas");

    /// <summary>
    /// 保存指定窗口的元素位置信息
    /// </summary>
    /// <param name="windowType">窗口类型</param>
    public void SaveWindowElementsPosition(FrontedWindowType windowType);
    /// <summary>
    /// 设置全局分数
    /// </summary>
    /// <param name="team">队伍</param>
    /// <param name="gameProgress">游戏进度</param>
    /// <param name="camp">阵营</param>
    /// <param name="score">分数</param>
    void SetGlobalScore(TeamType team, GameProgress gameProgress, Camp camp, int score);
    /// <summary>
    /// 设置全局分数
    /// </summary>
    /// <param name="team">队伍</param>
    /// <param name="gameProgress">对局进度</param>
    void SetGlobalScoreToBar(TeamType team, GameProgress gameProgress);
    /// <summary>
    /// 显示窗口
    /// </summary>
    /// <param name="windowType">窗口类型</param>
    void ShowWindow(FrontedWindowType windowType);
}