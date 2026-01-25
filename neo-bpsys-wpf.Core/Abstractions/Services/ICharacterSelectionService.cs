using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;

namespace neo_bpsys_wpf.Core.Abstractions.Services;

/// <summary>
/// 角色选择服务接口
/// 处理角色选择的核心业务逻辑，依赖动画服务执行视觉效果
/// </summary>
public interface ICharacterSelectionService
{
    /// <summary>
    /// 获取动画服务实例
    /// </summary>
    IAnimationService AnimationService { get; }

    /// <summary>
    /// 选择求生者角色
    /// </summary>
    /// <param name="playerIndex">玩家索引 (0-3)</param>
    /// <param name="character">选择的角色</param>
    /// <param name="playAnimation">是否播放动画</param>
    Task SelectSurvivorAsync(int playerIndex, Character? character, bool playAnimation = true);

    /// <summary>
    /// 选择监管者角色
    /// </summary>
    /// <param name="character">选择的角色</param>
    /// <param name="playAnimation">是否播放动画</param>
    Task SelectHunterAsync(Character? character, bool playAnimation = true);

    /// <summary>
    /// 禁用角色
    /// </summary>
    /// <param name="camp">阵营</param>
    /// <param name="index">禁用位索引</param>
    /// <param name="character">被禁用的角色</param>
    /// <param name="banType">禁用类型</param>
    /// <param name="playAnimation">是否播放动画</param>
    Task BanCharacterAsync(Camp camp, int index, Character? character, BanType banType, bool playAnimation = true);

    /// <summary>
    /// 互换求生者角色
    /// </summary>
    /// <param name="sourceIndex">源玩家索引</param>
    /// <param name="targetIndex">目标玩家索引</param>
    /// <param name="playAnimation">是否播放动画</param>
    Task SwapSurvivorsAsync(int sourceIndex, int targetIndex, bool playAnimation = true);

    /// <summary>
    /// 开始待选框呼吸灯效果
    /// </summary>
    /// <param name="camp">阵营</param>
    /// <param name="index">角色索引，监管者为-1</param>
    Task StartPickingIndicatorAsync(Camp camp, int index);

    /// <summary>
    /// 停止待选框呼吸灯效果
    /// </summary>
    /// <param name="camp">阵营</param>
    /// <param name="index">角色索引，监管者为-1</param>
    Task StopPickingIndicatorAsync(Camp camp, int index);
}
