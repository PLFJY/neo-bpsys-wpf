using System.Text.Json.Serialization;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;

namespace neo_bpsys_wpf.CoopBpPlugin.Models;

/// <summary>
/// 可序列化角色数据
/// 用于网络传输的角色信息
/// </summary>
public class SerializableCharacter
{
    /// <summary>角色名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>阵营</summary>
    public Camp Camp { get; set; }

    /// <summary>
    /// 从角色对象创建可序列化角色
    /// </summary>
    public static SerializableCharacter? FromCharacter(Character? character)
    {
        if (character == null) return null;
        return new SerializableCharacter
        {
            Name = character.Name,
            Camp = character.Camp
        };
    }

    /// <summary>
    /// 转换为角色对象
    /// </summary>
    public Character? ToCharacter(SortedDictionary<string, Character> surDict, SortedDictionary<string, Character> hunDict)
    {
        if (string.IsNullOrEmpty(Name)) return null;
        var dict = Camp == Camp.Sur ? surDict : hunDict;
        return dict.TryGetValue(Name, out var character) ? character : null;
    }
}

/// <summary>
/// 可序列化队员数据
/// 用于网络传输的队员信息
/// </summary>
public class SerializableMember
{
    /// <summary>队员名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>阵营</summary>
    public Camp Camp { get; set; }

    /// <summary>头像URI</summary>
    public string? ImageUri { get; set; }

    /// <summary>是否上场</summary>
    public bool IsOnField { get; set; }

    /// <summary>
    /// 从队员对象创建可序列化队员
    /// </summary>
    public static SerializableMember FromMember(Member member)
    {
        return new SerializableMember
        {
            Name = member.Name,
            Camp = member.Camp,
            ImageUri = member.ImageUri,
            IsOnField = member.IsOnField
        };
    }

    /// <summary>
    /// 转换为队员对象
    /// </summary>
    public Member ToMember()
    {
        return new Member(Camp)
        {
            Name = Name,
            ImageUri = ImageUri,
            IsOnField = IsOnField
        };
    }
}

/// <summary>
/// 可序列化玩家数据
/// 用于网络传输的玩家信息
/// </summary>
public class SerializablePlayer
{
    /// <summary>队员信息</summary>
    public SerializableMember Member { get; set; } = new();

    /// <summary>选择的角色</summary>
    public SerializableCharacter? Character { get; set; }

    /// <summary>天赋</summary>
    public SerializableTalent Talent { get; set; } = new();

    /// <summary>特质</summary>
    public SerializableTrait Trait { get; set; } = new();

    /// <summary>
    /// 从玩家对象创建可序列化玩家
    /// </summary>
    public static SerializablePlayer FromPlayer(Player player)
    {
        return new SerializablePlayer
        {
            Member = SerializableMember.FromMember(player.Member),
            Character = SerializableCharacter.FromCharacter(player.Character),
            Talent = SerializableTalent.FromTalent(player.Talent),
            Trait = SerializableTrait.FromTrait(player.Trait)
        };
    }
}

/// <summary>
/// 可序列化天赋数据
/// 用于网络传输的天赋信息
/// </summary>
public class SerializableTalent
{
    /// <summary>回光返照</summary>
    public bool BorrowedTime { get; set; }

    /// <summary>飞轮效应</summary>
    public bool FlywheelEffect { get; set; }

    /// <summary>膝跳反射</summary>
    public bool KneeJerkReflex { get; set; }

    /// <summary>化险为夷</summary>
    public bool TideTurner { get; set; }

    /// <summary>困兽之斗</summary>
    public bool ConfinedSpace { get; set; }

    /// <summary>拘禁狂</summary>
    public bool Detention { get; set; }

    /// <summary>傲慢</summary>
    public bool Insolence { get; set; }

    /// <summary>底牌</summary>
    public bool TrumpCard { get; set; }

    /// <summary>
    /// 从天赋对象创建可序列化天赋
    /// </summary>
    public static SerializableTalent FromTalent(Talent talent)
    {
        return new SerializableTalent
        {
            BorrowedTime = talent.BorrowedTime,
            FlywheelEffect = talent.FlywheelEffect,
            KneeJerkReflex = talent.KneeJerkReflex,
            TideTurner = talent.TideTurner,
            ConfinedSpace = talent.ConfinedSpace,
            Detention = talent.Detention,
            Insolence = talent.Insolence,
            TrumpCard = talent.TrumpCard
        };
    }

    /// <summary>
    /// 转换为天赋对象
    /// </summary>
    public Talent ToTalent()
    {
        return new Talent
        {
            BorrowedTime = BorrowedTime,
            FlywheelEffect = FlywheelEffect,
            KneeJerkReflex = KneeJerkReflex,
            TideTurner = TideTurner,
            ConfinedSpace = ConfinedSpace,
            Detention = Detention,
            Insolence = Insolence,
            TrumpCard = TrumpCard
        };
    }
}

/// <summary>
/// 可序列化特质数据
/// 用于网络传输的特质信息
/// </summary>
public class SerializableTrait
{
    /// <summary>特质类型</summary>
    public TraitType? TraitName { get; set; }

    /// <summary>
    /// 从特质对象创建可序列化特质
    /// </summary>
    public static SerializableTrait FromTrait(Trait trait)
    {
        return new SerializableTrait
        {
            TraitName = trait.TraitName
        };
    }

    /// <summary>
    /// 转换为特质对象
    /// </summary>
    public Trait ToTrait()
    {
        return new Trait(TraitName);
    }
}

/// <summary>
/// 可序列化队伍数据
/// 用于网络传输的队伍信息
/// </summary>
public class SerializableTeam
{
    /// <summary>队伍名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>队伍头像URI</summary>
    public string ImageUri { get; set; } = string.Empty;

    /// <summary>队伍类型</summary>
    public TeamType TeamType { get; set; }

    /// <summary>阵营</summary>
    public Camp? Camp { get; set; }

    /// <summary>求生者队员列表</summary>
    public List<SerializableMember> SurMemberList { get; set; } = [];

    /// <summary>监管者队员列表</summary>
    public List<SerializableMember> HunMemberList { get; set; } = [];

    /// <summary>全局禁用的求生者角色名称列表</summary>
    public List<string?> GlobalBannedSurNames { get; set; } = [];

    /// <summary>全局禁用的监管者角色名称列表</summary>
    public List<string?> GlobalBannedHunNames { get; set; } = [];

    /// <summary>
    /// 从队伍对象创建可序列化队伍
    /// </summary>
    public static SerializableTeam FromTeam(Team team)
    {
        return new SerializableTeam
        {
            Name = team.Name,
            ImageUri = team.ImageUri,
            TeamType = team.TeamType,
            Camp = team.Camp,
            SurMemberList = team.SurMemberList.Select(SerializableMember.FromMember).ToList(),
            HunMemberList = team.HunMemberList.Select(SerializableMember.FromMember).ToList(),
            GlobalBannedSurNames = team.GlobalBannedSurRecordList.Select(c => c?.Name).ToList(),
            GlobalBannedHunNames = team.GlobalBannedHunRecordList.Select(c => c?.Name).ToList()
        };
    }
}

/// <summary>
/// 可序列化游戏数据
/// 用于网络传输的游戏状态信息
/// </summary>
public class SerializableGame
{
    /// <summary>游戏进度</summary>
    public GameProgress GameProgress { get; set; }

    /// <summary>求生者队伍</summary>
    public SerializableTeam SurTeam { get; set; } = new();

    /// <summary>监管者队伍</summary>
    public SerializableTeam HunTeam { get; set; } = new();

    /// <summary>求生者玩家列表</summary>
    public List<SerializablePlayer> SurPlayers { get; set; } = [];

    /// <summary>监管者玩家</summary>
    public SerializablePlayer? HunPlayer { get; set; }

    /// <summary>当前局禁用的求生者角色名称列表</summary>
    public List<string?> CurrentSurBannedNames { get; set; } = [];

    /// <summary>当前局禁用的监管者角色名称列表</summary>
    public List<string?> CurrentHunBannedNames { get; set; } = [];

    /// <summary>选择的地图</summary>
    public Map? PickedMap { get; set; }

    /// <summary>禁用的地图</summary>
    public Map? BannedMap { get; set; }

    /// <summary>MapV2数据字典</summary>
    public Dictionary<string, SerializableMapV2>? MapV2Data { get; set; }

    /// <summary>
    /// 从游戏对象创建可序列化游戏
    /// </summary>
    public static SerializableGame FromGame(Game game)
    {
        return new SerializableGame
        {
            GameProgress = game.GameProgress,
            SurTeam = SerializableTeam.FromTeam(game.SurTeam),
            HunTeam = SerializableTeam.FromTeam(game.HunTeam),
            SurPlayers = game.SurPlayerList.Select(SerializablePlayer.FromPlayer).ToList(),
            HunPlayer = game.HunPlayer != null ? SerializablePlayer.FromPlayer(game.HunPlayer) : null,
            CurrentSurBannedNames = game.CurrentSurBannedList.Select(c => c?.Name).ToList(),
            CurrentHunBannedNames = game.CurrentHunBannedList.Select(c => c?.Name).ToList(),
            PickedMap = game.PickedMap,
            BannedMap = game.BannedMap,
            MapV2Data = game.MapV2Dictionary.ToDictionary(
                kvp => kvp.Key,
                kvp => SerializableMapV2.FromMapV2(kvp.Value))
        };
    }
}

/// <summary>
/// 可序列化MapV2数据
/// 用于网络传输的地图V2信息
/// </summary>
public class SerializableMapV2
{
    /// <summary>地图名称</summary>
    public Map? MapName { get; set; }

    /// <summary>是否被选择</summary>
    public bool IsPicked { get; set; }

    /// <summary>是否被禁用</summary>
    public bool IsBanned { get; set; }

    /// <summary>
    /// 从MapV2对象创建可序列化MapV2
    /// </summary>
    public static SerializableMapV2 FromMapV2(MapV2 mapV2)
    {
        return new SerializableMapV2
        {
            MapName = mapV2.MapName,
            IsPicked = mapV2.IsPicked,
            IsBanned = mapV2.IsBanned
        };
    }
}

/// <summary>
/// 游戏同步数据
/// 包含完整的游戏状态用于同步
/// </summary>
public class GameSyncData
{
    /// <summary>游戏数据</summary>
    public SerializableGame Game { get; set; } = new();

    /// <summary>是否为BO3模式</summary>
    public bool IsBo3Mode { get; set; }

    /// <summary>是否显示特质</summary>
    public bool IsTraitVisible { get; set; }

    /// <summary>剩余秒数</summary>
    public string RemainingSeconds { get; set; } = string.Empty;
}
