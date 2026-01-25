using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 角色选择动画服务接口
/// 负责管理角色选择过程中的动画效果
/// 插件可以实现此接口并覆盖注入进 IOC 容器来自定义动画效果
/// </summary>
public interface IAnimationService
{
    #region 角色选择动画 (Pick)

    /// <summary>
    /// 播放角色选择切换动画（渐隐+渐显）
    /// </summary>
    /// <param name="camp">阵营（求生者/监管者）</param>
    /// <param name="index">角色索引，监管者为-1</param>
    Task PlayPickTransitionAsync(Camp camp, int index);

    /// <summary>
    /// 播放角色选择渐显动画
    /// </summary>
    /// <param name="camp">阵营（求生者/监管者）</param>
    /// <param name="index">角色索引，监管者为-1</param>
    void PlayPickFadeIn(Camp camp, int index);

    /// <summary>
    /// 播放角色选择渐隐动画
    /// </summary>
    /// <param name="camp">阵营（求生者/监管者）</param>
    /// <param name="index">角色索引，监管者为-1</param>
    void PlayPickFadeOut(Camp camp, int index);

    #endregion

    #region 角色禁用动画 (Ban)

    /// <summary>
    /// 播放角色禁用动画
    /// </summary>
    /// <param name="camp">阵营</param>
    /// <param name="index">禁用位索引</param>
    Task PlayBanAnimationAsync(Camp camp, int index);

    #endregion

    #region 待选框呼吸灯动画 (Picking Border)

    /// <summary>
    /// 开始待选框呼吸灯效果
    /// </summary>
    /// <param name="camp">阵营</param>
    /// <param name="index">角色索引，监管者为-1</param>
    Task StartPickingBorderBreathingAsync(Camp camp, int index);

    /// <summary>
    /// 停止待选框呼吸灯效果
    /// </summary>
    /// <param name="camp">阵营</param>
    /// <param name="index">角色索引，监管者为-1</param>
    Task StopPickingBorderBreathingAsync(Camp camp, int index);

    #endregion

    #region 角色互换动画

    /// <summary>
    /// 播放角色互换动画
    /// </summary>
    /// <param name="sourceIndex">源角色索引</param>
    /// <param name="targetIndex">目标角色索引</param>
    Task PlaySwapCharacterAnimationAsync(int sourceIndex, int targetIndex);

    #endregion

    #region 动画预设

    /// <summary>
    /// 呼吸灯启动
    /// </summary>
    /// <param name="windowType">窗口类型</param>
    /// <param name="controlNameHeader">控件名称头</param>
    /// <param name="controlIndex">控件索引(-1表示没有)</param>
    /// <param name="controlNameFooter">控件名称尾</param>
    /// <returns></returns>
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
    Task BreathingStart(string windowId, string controlNameHeader, int controlIndex, string controlNameFooter);

    /// <summary>
    /// 呼吸灯停止
    /// </summary>
    /// <param name="windowType">窗口类型</param>
    /// <param name="controlNameHeader">控件名称头</param>
    /// <param name="controlIndex">控件索引(-1表示没有)</param>
    /// <param name="controlNameFooter">控件名称尾</param>
    /// <returns></returns>
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
    Task BreathingStop(string windowId, string controlNameHeader, int controlIndex, string controlNameFooter);

    /// <summary>
    /// 渐显动画
    /// </summary>
    /// <param name="windowType">窗口类型</param>
    /// <param name="controlNameHeader">控件名称头</param>
    /// <param name="controlIndex">控件索引(-1表示没有)</param>
    /// <param name="controlNameFooter">控件名称尾</param>
    /// <returns></returns>
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
    void FadeInAnimation(string windowId, string controlNameHeader, int controlIndex, string controlNameFooter);

    /// <summary>
    /// 渐隐动画
    /// </summary>
    /// <param name="windowType">窗口类型</param>
    /// <param name="controlNameHeader">控件名称头</param>
    /// <param name="controlIndex">控件索引(-1表示没有)</param>
    /// <param name="controlNameFooter">控件名称尾</param>
    /// <returns></returns>
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
    void FadeOutAnimation(string windowId, string controlNameHeader, int controlIndex,
        string controlNameFooter);

    #endregion
}