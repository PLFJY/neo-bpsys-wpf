using neo_bpsys_wpf.Core.Enums;
using System.Windows;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 前台窗口接口服务
/// </summary>
public interface IFrontedWindowService
{
    #region Properties

    /// <summary>
    /// 前台画布列表
    /// </summary>
    List<(string, string)> FrontedCanvas { get; }

    /// <summary>
    /// 前台窗口列表
    /// </summary>
    Dictionary<string, Window> FrontedWindows { get; }

    /// <summary>
    /// 前台窗口状态列表
    /// </summary>
    Dictionary<string, bool> FrontedWindowStates { get; }

    #endregion

    #region Window Management

    /// <summary>
    /// 隐藏全部窗口
    /// </summary>
    void AllWindowHide();

    /// <summary>
    /// 显示全部窗口
    /// </summary>
    void AllWindowShow();

    /// <summary>
    /// 隐藏窗口
    /// </summary>
    /// <param name="windowType">窗口类型</param>
    void HideWindow(FrontedWindowType windowType);

    /// <summary>
    /// 隐藏窗口
    /// </summary>
    /// <param name="windowId">窗口 GUID</param>
    void HideWindow(string windowId);

    /// <summary>
    /// 显示窗口
    /// </summary>
    /// <param name="windowType">窗口类型</param>
    void ShowWindow(FrontedWindowType windowType);

    /// <summary>
    /// 显示窗口s
    /// </summary>
    /// <param name="windowId">窗口 GUID</param>
    void ShowWindow(string windowId);

    #endregion

    #region Animation Effects

    /// <summary>
    /// 呼吸灯启动
    /// </summary>
    /// <param name="windowType">窗口类型</param>
    /// <param name="controlNameHeader">控件名称头</param>
    /// <param name="controlIndex">控件索引(-1表示没有)</param>
    /// <param name="controlNameFooter">控件名称尾</param>
    /// <returns></returns>
    [Obsolete("请使用 IAnimationService.StartPickingBorderBreathingAsync 替代。此方法将在未来版本中移除。")]
    Task BreathingStart(FrontedWindowType windowType, string controlNameHeader, int controlIndex,
        string controlNameFooter);

    /// <summary>
    /// 呼吸灯启动
    /// </summary>
    /// <param name="windowId">窗口 GUID</param>
    /// <param name="controlNameHeader">控件名称头</param>
    /// <param name="controlIndex">控件索引(-1表示没有)</param>
    /// <param name="controlNameFooter">控件名称尾</param>
    /// <returns></returns>
    [Obsolete("请使用 IAnimationService.StartPickingBorderBreathingAsync 替代。此方法将在未来版本中移除。")]
    Task BreathingStart(string windowId, string controlNameHeader, int controlIndex, string controlNameFooter);

    /// <summary>
    /// 呼吸灯停止
    /// </summary>
    /// <param name="windowType">窗口类型</param>
    /// <param name="controlNameHeader">控件名称头</param>
    /// <param name="controlIndex">控件索引(-1表示没有)</param>
    /// <param name="controlNameFooter">控件名称尾</param>
    /// <returns></returns>
    [Obsolete("请使用 IAnimationService.StopPickingBorderBreathingAsync 替代。此方法将在未来版本中移除。")]
    Task BreathingStop(FrontedWindowType windowType, string controlNameHeader, int controlIndex,
        string controlNameFooter);

    /// <summary>
    /// 呼吸灯停止
    /// </summary>
    /// <param name="windowId">窗口 GUID</param>
    /// <param name="controlNameHeader">控件名称头</param>
    /// <param name="controlIndex">控件索引(-1表示没有)</param>
    /// <param name="controlNameFooter">控件名称尾</param>
    /// <returns></returns>
    [Obsolete("请使用 IAnimationService.StopPickingBorderBreathingAsync 替代。此方法将在未来版本中移除。")]
    Task BreathingStop(string windowId, string controlNameHeader, int controlIndex, string controlNameFooter);

    /// <summary>
    /// 渐显动画
    /// </summary>
    /// <param name="windowType">窗口类型</param>
    /// <param name="controlNameHeader">控件名称头</param>
    /// <param name="controlIndex">控件索引(-1表示没有)</param>
    /// <param name="controlNameFooter">控件名称尾</param>
    /// <returns></returns>
    [Obsolete("请使用 IAnimationService.PlayPickFadeIn 替代。此方法将在未来版本中移除。")]
    void FadeInAnimation(FrontedWindowType windowType, string controlNameHeader, int controlIndex,
        string controlNameFooter);

    /// <summary>
    /// 渐显动画
    /// </summary>
    /// <param name="windowId">窗口 GUID</param>
    /// <param name="controlNameHeader">控件名称头</param>
    /// <param name="controlIndex">控件索引(-1表示没有)</param>
    /// <param name="controlNameFooter">控件名称尾</param>
    /// <returns></returns>
    [Obsolete("请使用 IAnimationService.PlayPickFadeIn 替代。此方法将在未来版本中移除。")]
    void FadeInAnimation(string windowId, string controlNameHeader, int controlIndex, string controlNameFooter);

    /// <summary>
    /// 渐隐动画
    /// </summary>
    /// <param name="windowType">窗口类型</param>
    /// <param name="controlNameHeader">控件名称头</param>
    /// <param name="controlIndex">控件索引(-1表示没有)</param>
    /// <param name="controlNameFooter">控件名称尾</param>
    /// <returns></returns>
    [Obsolete("请使用 IAnimationService.PlayPickFadeOut 替代。此方法将在未来版本中移除。")]
    void FadeOutAnimation(FrontedWindowType windowType, string controlNameHeader, int controlIndex,
        string controlNameFooter);

    /// <summary>
    /// 渐隐动画
    /// </summary>
    /// <param name="windowId">窗口 GUID</param>
    /// <param name="controlNameHeader">控件名称头</param>
    /// <param name="controlIndex">控件索引(-1表示没有)</param>
    /// <param name="controlNameFooter">控件名称尾</param>
    /// <returns></returns>
    [Obsolete("请使用 IAnimationService.PlayPickFadeOut 替代。此方法将在未来版本中移除。")]
    void FadeOutAnimation(string windowId, string controlNameHeader, int controlIndex,
        string controlNameFooter);

    #endregion

    #region Window Registration

    /// <summary>
    /// 注册窗口和画布
    /// </summary>
    /// <param name="windowId">窗口 GUID</param>
    /// <param name="window">窗口</param>
    /// <param name="canvasNames">画布名称集合</param>
    void RegisterFrontedWindowAndCanvas(string windowId, Window window, string[] canvasNames);

    #endregion

    #region Window Position Management

    /// <summary>
    /// 恢复初始位置
    /// </summary>
    /// <param name="windowType">窗口类型</param>
    /// <param name="canvasName"></param>
    /// <returns></returns>
    Task RestoreInitialPositions(FrontedWindowType windowType, string canvasName = "BaseCanvas");

    /// <summary>
    /// 恢复初始位置
    /// </summary>
    /// <param name="windowId">窗口 GUID</param>
    /// <param name="canvasName"></param>
    Task RestoreInitialPositions(string windowId, string canvasName = "BaseCanvas");

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
    /// 保存指定窗口的指定Canvas中元素位置信息
    /// </summary>
    /// <param name="windowId">窗口 GUID</param>
    /// <param name="canvasName">画布名称</param>
    void SaveWindowCanvasElementsPosition(string windowId, string canvasName = "BaseCanvas");

    /// <summary>
    /// 保存指定窗口的元素位置信息
    /// </summary>
    /// <param name="windowType">窗口类型</param>
    public void SaveWindowElementsPosition(FrontedWindowType windowType);

    /// <summary>
    /// 保存指定窗口的元素位置信息
    /// </summary>
    /// <param name="windowId">窗口 GUID</param>
    public void SaveWindowElementsPosition(string windowId);

    #endregion

    #region Window Information

    /// <summary>
    /// 获取窗口名称
    /// </summary>
    /// <param name="windowType"></param>
    /// <returns></returns>
    string? GetWindowName(FrontedWindowType windowType);

    /// <summary>
    /// 获取窗口名称
    /// </summary>
    /// <param name="windowId">窗口 GUID</param>
    /// <returns></returns>
    string? GetWindowName(string windowId);

    #endregion

    #region Control Injection

    /// <summary>
    /// 获取注入的控件
    /// </summary>
    /// <param name="guid">控件 GUID</param>
    /// <returns>控件实例</returns>
    public FrameworkElement GetInjectedControl(string guid);

    #endregion

    #region Score Management

    /// <summary>
    /// 重置全局分数
    /// </summary>
    void ResetGlobalScore();

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

    #endregion
}