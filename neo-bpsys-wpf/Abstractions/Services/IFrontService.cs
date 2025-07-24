using neo_bpsys_wpf.Enums;

namespace neo_bpsys_wpf.Abstractions.Services;

/// <summary>
/// 前台窗口接口服务
/// </summary>
public interface IFrontService
{
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
    Task BreathingStart(FrontWindowType windowType, string controlNameHeader, int controlIndex, string controlNameFooter);
    /// <summary>
    /// 呼吸灯停止
    /// </summary>
    /// <param name="windowType">窗口类型</param>
    /// <param name="controlNameHeader">控件名称头</param>
    /// <param name="controlIndex">控件索引(-1表示没有)</param>
    /// <param name="controlNameFooter">控件名称尾</param>
    /// <returns></returns>
    Task BreathingStop(FrontWindowType windowType, string controlNameHeader, int controlIndex, string controlNameFooter);
    /// <summary>
    /// 渐显动画
    /// </summary>
    /// <param name="windowType">窗口类型</param>
    /// <param name="controlNameHeader">控件名称头</param>
    /// <param name="controlIndex">控件索引(-1表示没有)</param>
    /// <param name="controlNameFooter">控件名称尾</param>
    /// <returns></returns>
    void FadeInAnimation(FrontWindowType windowType, string controlNameHeader, int controlIndex, string controlNameFooter);
    /// <summary>
    /// 渐隐动画
    /// </summary>
    /// <param name="windowType">窗口类型</param>
    /// <param name="controlNameHeader">控件名称头</param>
    /// <param name="controlIndex">控件索引(-1表示没有)</param>
    /// <param name="controlNameFooter">控件名称尾</param>
    /// <returns></returns>
    void FadeOutAnimation(FrontWindowType windowType, string controlNameHeader, int controlIndex, string controlNameFooter);
    /// <summary>
    /// 隐藏窗口
    /// </summary>
    /// <param name="windowType">窗口类型</param>
    void HideWindow(FrontWindowType windowType);
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
    Task RestoreInitialPositions(FrontWindowType windowType, string canvasName = "BaseCanvas");
    /// <summary>
    /// 保存所有窗口元素位置
    /// </summary>
    void SaveAllWindowElementsPosition();
    /// <summary>
    /// 保存窗口元素位置
    /// </summary>
    /// <param name="windowType">窗口类型</param>
    /// <param name="canvasName">画布名称</param>
    void SaveWindowElementsPosition(FrontWindowType windowType, string canvasName = "BaseCanvas");
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
    void ShowWindow(FrontWindowType windowType);
}