using System.Text.Json.Serialization;
using neo_bpsys_wpf.Core.Enums;

namespace neo_bpsys_wpf.Core.Models;

/// <summary>
/// 表示一个 BP 槽位在主程序中的提交状态。
/// </summary>
public enum BpSlotCommitState
{
    /// <summary>槽位尚未提交，现有空值不能被解释为空操作。</summary>
    Pending,

    /// <summary>槽位已明确提交为空操作。</summary>
    CommittedEmpty,

    /// <summary>槽位已提交角色。</summary>
    CommittedCharacter
}

/// <summary>
/// 保存当前 <see cref="Game.Guid"/> 与 <see cref="Game.GameProgress"/> 上下文中的权威 BP 槽位提交状态。
/// </summary>
public sealed class BpSlotCommitStateSet
{
    /// <summary>
    /// 初始化权威 BP 槽位提交状态。
    /// </summary>
    /// <param name="gameGuid">所属对局标识。</param>
    /// <param name="gameProgress">所属对局进度。</param>
    /// <param name="survivorBans">求生者 Ban 槽位状态；旧存档缺失时全部为 Pending。</param>
    /// <param name="hunterBans">监管者 Ban 槽位状态；旧存档缺失时全部为 Pending。</param>
    /// <param name="survivorPicks">求生者 Pick 槽位状态；旧存档缺失时全部为 Pending。</param>
    /// <param name="hunterPick">监管者 Pick 槽位状态。</param>
    [JsonConstructor]
    public BpSlotCommitStateSet(
        Guid gameGuid,
        GameProgress gameProgress,
        BpSlotCommitState[]? survivorBans = null,
        BpSlotCommitState[]? hunterBans = null,
        BpSlotCommitState[]? survivorPicks = null,
        BpSlotCommitState hunterPick = BpSlotCommitState.Pending)
    {
        GameGuid = gameGuid;
        GameProgress = gameProgress;
        SurvivorBans = Normalize(survivorBans, AppConstants.CurrentBanSurCount);
        HunterBans = Normalize(hunterBans, AppConstants.CurrentBanHunCount);
        SurvivorPicks = Normalize(survivorPicks, 4);
        HunterPick = hunterPick;
    }

    /// <summary>获取所属对局标识。</summary>
    public Guid GameGuid { get; }

    /// <summary>获取所属对局进度。</summary>
    public GameProgress GameProgress { get; }

    /// <summary>获取求生者 Ban 槽位提交状态。</summary>
    public BpSlotCommitState[] SurvivorBans { get; }

    /// <summary>获取监管者 Ban 槽位提交状态。</summary>
    public BpSlotCommitState[] HunterBans { get; }

    /// <summary>获取求生者 Pick 槽位提交状态。</summary>
    public BpSlotCommitState[] SurvivorPicks { get; }

    /// <summary>获取或设置监管者 Pick 槽位提交状态。</summary>
    public BpSlotCommitState HunterPick { get; set; }

    /// <summary>
    /// 创建全部为 Pending 的新上下文。
    /// </summary>
    /// <param name="gameGuid">所属对局标识。</param>
    /// <param name="gameProgress">所属对局进度。</param>
    /// <returns>新的权威提交状态集合。</returns>
    public static BpSlotCommitStateSet CreatePending(Guid gameGuid, GameProgress gameProgress) =>
        new(gameGuid, gameProgress);

    /// <summary>
    /// 创建不可变读取快照。
    /// </summary>
    /// <returns>当前上下文与各槽位状态的快照。</returns>
    public BpSlotCommitStateSnapshot CreateSnapshot() =>
        new(GameGuid, GameProgress, SurvivorBans.ToArray(), HunterBans.ToArray(), SurvivorPicks.ToArray(), HunterPick);

    private static BpSlotCommitState[] Normalize(IReadOnlyList<BpSlotCommitState>? source, int count)
    {
        var result = Enumerable.Repeat(BpSlotCommitState.Pending, count).ToArray();
        if (source is null)
            return result;

        for (var index = 0; index < Math.Min(source.Count, count); index++)
            result[index] = source[index];
        return result;
    }
}

/// <summary>
/// 主程序权威 BP 槽位提交状态的不可变快照。
/// </summary>
/// <param name="GameGuid">所属对局标识。</param>
/// <param name="GameProgress">所属对局进度。</param>
/// <param name="SurvivorBans">求生者 Ban 槽位状态。</param>
/// <param name="HunterBans">监管者 Ban 槽位状态。</param>
/// <param name="SurvivorPicks">求生者 Pick 槽位状态。</param>
/// <param name="HunterPick">监管者 Pick 槽位状态。</param>
public sealed record BpSlotCommitStateSnapshot(
    Guid GameGuid,
    GameProgress GameProgress,
    IReadOnlyList<BpSlotCommitState> SurvivorBans,
    IReadOnlyList<BpSlotCommitState> HunterBans,
    IReadOnlyList<BpSlotCommitState> SurvivorPicks,
    BpSlotCommitState HunterPick);
