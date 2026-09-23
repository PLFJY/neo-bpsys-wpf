using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Media.Imaging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.SmartBp.Module.Abstractions;
using neo_bpsys_wpf.SmartBp.Module.Models.Recognition;

namespace neo_bpsys_wpf.SmartBp.Module.Services.Recognition;

internal static class SmartBpAutomaticMapping
{
    /// <summary>
    /// AI/OCR 识别结果允许返回的阶段名称集合。
    /// </summary>
    public static readonly IReadOnlyList<string> ValidPhases =
    [
        "屏蔽求生者", "屏蔽监管者", "选择求生者", "求生者选择角色中", "选择监管者",
        "求生者选择天赋中", "监管者选择天赋中", "天赋已锁定",
        "即将进入区域选择", "区域选择", "求生者选择区域中", "监管者选择区域中",
        "等待游戏开始", "加载中", "对局中", "等待中", "未知"
    ];

    /// <summary>
    /// 将 GameGuidance 动作映射为 prompt 中使用的区域、阵营和含义说明。
    /// </summary>
    /// <param name="action">GameGuidance 动作。</param>
    /// <returns>区域 ID、阵营和含义说明。</returns>
    /// <exception cref="NotSupportedException">动作不属于当前 BP 识别支持范围时抛出。</exception>
    public static (string Region, string Camp, string Meaning) Get(GameAction action) => action switch
    {
        GameAction.BanSur => ("right_top", "survivor", "the hunter-side operation area for banning survivors"),
        GameAction.BanHun => ("left_top", "hunter", "the survivor-side operation area for banning hunters"),
        GameAction.PickSur => ("left_bottom", "survivor", "the survivor picking area"),
            GameAction.DistributeChara => ("left_bottom", "survivor", "fixed survivor player slots with assigned characters"),
            GameAction.PickHun => ("right_bottom", "hunter", "the hunter picking area"),
            GameAction.PickSurTalent => ("left_bottom", "survivor", "the survivor talent selection area"),
            GameAction.PickHunTalent => ("right_bottom", "hunter", "the hunter talent selection area"),
            _ => throw new NotSupportedException($"GameGuidance action {action} is not supported by BP recognition.")
        };

    /// <summary>
    /// 将 GameGuidance 动作转换为自动识别任务类型。
    /// </summary>
    /// <param name="action">GameGuidance 动作。</param>
    /// <returns>识别任务类型。</returns>
    /// <exception cref="NotSupportedException">动作不属于当前 BP 识别支持范围时抛出。</exception>
    public static SmartBpRecognitionTask ToRecognitionTask(GameAction action) => action switch
    {
        GameAction.BanSur => SmartBpRecognitionTask.BanSur,
        GameAction.BanHun => SmartBpRecognitionTask.BanHun,
        GameAction.PickSur => SmartBpRecognitionTask.PickSur,
            GameAction.DistributeChara => SmartBpRecognitionTask.CharacterDistribution,
            GameAction.PickHun => SmartBpRecognitionTask.PickHun,
        _ => throw new NotSupportedException($"GameGuidance action {action} is not supported by BP recognition.")
    };

    /// <summary>
    /// 将识别输出中的动作文本解析为 GameGuidance 动作。
    /// </summary>
    /// <param name="value">识别输出动作名。</param>
    /// <param name="action">解析得到的动作。</param>
    /// <returns>解析成功返回 <see langword="true"/>。</returns>
    public static bool TryParseDetectedAction(string value, out GameAction action)
    {
        action = value switch
        {
            "BanSur" => GameAction.BanSur,
            "BanHun" => GameAction.BanHun,
            "PickSur" => GameAction.PickSur,
            "DistributeChara" => GameAction.DistributeChara,
            "PickHun" => GameAction.PickHun,
            _ => GameAction.None
        };
        return action != GameAction.None;
    }

    /// <summary>
    /// 将阶段名称映射到最接近的 GameGuidance 动作。
    /// </summary>
    /// <param name="phase">识别阶段名。</param>
    /// <param name="action">映射得到的动作。</param>
    /// <returns>存在动作映射返回 <see langword="true"/>。</returns>
    public static bool TryMapPhase(string phase, out GameAction action)
    {
        action = phase switch
        {
            "屏蔽求生者" => GameAction.BanSur,
            "屏蔽监管者" => GameAction.BanHun,
            "选择求生者" => GameAction.PickSur,
            "求生者选择角色中" => GameAction.DistributeChara,
            "选择监管者" => GameAction.PickHun,
            "求生者选择天赋中" => GameAction.PickSurTalent,
            "监管者选择天赋中" => GameAction.PickHunTalent,
            _ => GameAction.None
        };
        return action != GameAction.None;
    }

    /// <summary>
    /// 判断动作是否属于可自动应用角色变更的 BP 角色操作。
    /// </summary>
    /// <param name="action">GameGuidance 动作。</param>
    /// <returns>角色操作返回 <see langword="true"/>。</returns>
    public static bool IsCharacterOperationAction(GameAction action) =>
        action is GameAction.BanSur or GameAction.BanHun or GameAction.PickSur or GameAction.DistributeChara or GameAction.PickHun;

    /// <summary>
    /// 求生者选择锁定后不再按视觉槽位索引合并的权威阶段集合。
    /// 仅包含「分配角色」之后明确不再进行求生者角色选择的阶段。
    /// 注意：<c>求生者选择角色中</c> 不在此集合内，因为它在 PickSur 阶段仍可能按视觉槽位索引合并。
    /// </summary>
    public static readonly IReadOnlyCollection<string> SurvivorPickLockedPhases = new HashSet<string>(StringComparer.Ordinal)
    {
        "求生者选择天赋中",
        "选择监管者", "监管者选择天赋中", "天赋已锁定",
        "即将进入区域选择", "区域选择", "求生者选择区域中", "监管者选择区域中",
        "等待游戏开始", "加载中", "对局中"
    };

    /// <summary>
    /// 判断当前是否处于求生者选择锁定状态。
    /// 锁定后 picked_sur 视觉槽位更新不得按索引直接合并到内部状态，只能按 player_id 生成分配交换。
    /// 优先级：GameGuidance 已启动时信任 guidance action；未启动时保守使用权威阶段名。
    /// </summary>
    /// <param name="snapshot">当前 GameGuidance 运行时快照。</param>
    /// <param name="authoritativePhase">权威识别阶段名。</param>
    /// <returns>锁定返回 <see langword="true"/>。</returns>
    public static bool IsSurvivorPickLocked(GameGuidanceRuntimeSnapshot snapshot, string authoritativePhase)
    {
        // 优先级 1：GameGuidance 已启动时，信任 guidance action / workflow 位置。
        if (snapshot.IsStarted)
        {
            // PickSur 明确不锁定：即使权威阶段是「求生者选择角色中」也允许按槽位索引合并。
            if (snapshot.CurrentAction == GameAction.PickSur)
                return false;
            // DistributeChara 及之后动作锁定。
            if (snapshot.CurrentAction is GameAction.DistributeChara or GameAction.PickSurTalent or GameAction.PickHun or GameAction.PickHunTalent or GameAction.EndGuidance)
                return true;
            // 若工作流中已执行过 DistributeChara，则视为锁定（防止 action 回退）。
            if (snapshot.Workflow.Any(step => step.StepIndex < snapshot.CurrentStepIndex && step.Action == GameAction.DistributeChara))
                return true;
        }
        // 优先级 2：GameGuidance 未启动或 action 未知时，保守使用权威阶段名。
        return SurvivorPickLockedPhases.Contains(authoritativePhase);
    }

    /// <summary>
    /// 将 GameGuidance 动作转换为 SmartBP 阶段名。
    /// </summary>
    /// <param name="action">GameGuidance 动作。</param>
    /// <returns>阶段名。</returns>
    public static string ToPhase(GameAction action) => action switch
    {
        GameAction.BanSur => "屏蔽求生者",
        GameAction.BanHun => "屏蔽监管者",
        GameAction.PickSur => "选择求生者",
        GameAction.DistributeChara => "求生者选择角色中",
        GameAction.PickHun => "选择监管者",
        GameAction.PickSurTalent => "求生者选择天赋中",
        GameAction.PickHunTalent => "监管者选择天赋中",
        _ => "未知"
    };

    /// <summary>
    /// 获取某个角色操作需要重点刷新和解析的画面区域及字段名。
    /// </summary>
    /// <param name="action">GameGuidance 动作。</param>
    /// <returns>识别区域和业务字段名。</returns>
    /// <exception cref="NotSupportedException">动作没有角色字段目标时抛出。</exception>
    public static (SmartBpRecognitionRegion Region, string TargetField) GetFocusedTarget(GameAction action) => action switch
    {
        GameAction.BanSur => (SmartBpRecognitionRegion.RightTop, "banned_sur"),
        GameAction.BanHun => (SmartBpRecognitionRegion.LeftTop, "banned_hun"),
        GameAction.PickSur => (SmartBpRecognitionRegion.LeftBottom, "picked_sur"),
        GameAction.DistributeChara => (SmartBpRecognitionRegion.LeftBottom, "picked_sur"),
        GameAction.PickHun => (SmartBpRecognitionRegion.RightBottom, "picked_hun"),
        _ => throw new NotSupportedException($"GameGuidance action {action} has no focused character extraction region.")
    };
}

/// <summary>
/// 解析并校验 OCR 返回的完整 BP 业务状态 JSON。
/// </summary>
