#nullable enable

using Moq;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.ViewModels;
using neo_bpsys_wpf.ViewModels.Pages;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace neo_bpsys_wpf.Tests.ViewModels;

/// <summary>
/// 测试 CharaSelectViewModelBase 的 DisabledKeys 派生计算逻辑
/// </summary>
public class CharaSelectViewModelBaseDisabledKeysTest
{
    #region Helpers

    /// <summary>
    /// 用于测试的具体 VM 子类（最小实现）
    /// </summary>
    internal class TestCharaSelectViewModel : CharaSelectViewModelBase
    {
        public TestCharaSelectViewModel(
            ISharedDataService sharedDataService,
            Camp camp,
            int index = 0)
            : base(sharedDataService, camp, index)
        {
        }

        protected override Task SyncCharaToSourceAsync() => Task.CompletedTask;
        protected override void SyncCharaFromSourceAsync() { }
        protected override void SyncIsEnabled() { }
        protected override bool IsActionNameCorrect(GameAction? action) => false;
    }

    /// <summary>
    /// 创建测试用的 Character（带指定名称）
    /// </summary>
    private static Character CreateChara(string name, Camp camp = Camp.Sur)
    {
        return new Character(name, camp, $"{name}.png");
    }

    /// <summary>
    /// 创建完整的测试环境：真实的 Team + Game + 含 ObservableCollections 的 Mock ISharedDataService
    /// </summary>
    private static TestContext CreateContext()
    {
        var homeTeam = new Team(Camp.Sur, TeamType.HomeTeam);
        var awayTeam = new Team(Camp.Hun, TeamType.AwayTeam);
        var game = new Game(homeTeam, awayTeam, GameProgress.Free);

        // 填充测试用的角色字典，避免 SetCharaDict 后 ItemsSource 为空
        var surDict = new SortedDictionary<string, Character>
        {
            { "医生", CreateChara("医生") },
            { "律师", CreateChara("律师") },
            { "园丁", CreateChara("园丁") }
        };
        var hunDict = new SortedDictionary<string, Character>
        {
            { "杰克", CreateChara("杰克", Camp.Hun) },
            { "摄影师", CreateChara("摄影师", Camp.Hun) }
        };

        // 默认所有 Ban 位启用（模拟实际 SharedDataService 的初始化行为）
        var canCurrentSur = new ObservableCollection<bool>(Enumerable.Repeat(true, AppConstants.CurrentBanSurCount));
        var canCurrentHun = new ObservableCollection<bool>(Enumerable.Repeat(true, AppConstants.CurrentBanHunCount));
        var canGlobalSur = new ObservableCollection<bool>(Enumerable.Repeat(false, AppConstants.GlobalBanSurCount));
        var canGlobalHun = new ObservableCollection<bool>(Enumerable.Repeat(false, AppConstants.GlobalBanHunCount));

        var mock = new Mock<ISharedDataService>();
        mock.Setup(s => s.HomeTeam).Returns(homeTeam);
        mock.Setup(s => s.AwayTeam).Returns(awayTeam);
        mock.Setup(s => s.CurrentGame).Returns(game);
        mock.Setup(s => s.SurCharaDict).Returns(surDict);
        mock.Setup(s => s.HunCharaDict).Returns(hunDict);
        mock.Setup(s => s.CanCurrentSurBannedList).Returns(canCurrentSur);
        mock.Setup(s => s.CanCurrentHunBannedList).Returns(canCurrentHun);
        mock.Setup(s => s.CanGlobalSurBannedList).Returns(canGlobalSur);
        mock.Setup(s => s.CanGlobalHunBannedList).Returns(canGlobalHun);

        // Setup events: wire Game.TeamSwapped → SharedDataService.TeamSwapped relay (模拟生产代码中 SharedDataService 的 relay)
        EventHandler? sharedTeamSwappedHandler = null;
        mock.SetupAdd(s => s.TeamSwapped += It.IsAny<EventHandler>())
            .Callback<EventHandler>(h => sharedTeamSwappedHandler += h);
        mock.SetupRemove(s => s.TeamSwapped -= It.IsAny<EventHandler>())
            .Callback<EventHandler>(h => sharedTeamSwappedHandler -= h);
        game.TeamSwapped += (_, _) => sharedTeamSwappedHandler?.Invoke(mock.Object, EventArgs.Empty);

        mock.SetupAdd(s => s.CurrentGameChanged += It.IsAny<EventHandler>());
        mock.SetupRemove(s => s.CurrentGameChanged -= It.IsAny<EventHandler>());
        mock.SetupAdd(s => s.BanCountChanged += It.IsAny<EventHandler<Core.Events.BanCountChangedEventArgs>>());
        mock.SetupRemove(s => s.BanCountChanged -= It.IsAny<EventHandler<Core.Events.BanCountChangedEventArgs>>());

        return new TestContext(mock.Object, homeTeam, awayTeam, game, canCurrentSur, canCurrentHun, canGlobalSur, canGlobalHun);
    }

    private record TestContext(
        ISharedDataService SharedDataService,
        Team HomeTeam,
        Team AwayTeam,
        Game Game,
        ObservableCollection<bool> CanCurrentSur,
        ObservableCollection<bool> CanCurrentHun,
        ObservableCollection<bool> CanGlobalSur,
        ObservableCollection<bool> CanGlobalHun
    );

    /// <summary>
    /// 辅助：设置当前生效的全局禁选
    /// </summary>
    private static void SetEffectiveGlobalBan(Team team, int index, Character? character, Camp camp)
    {
        if (camp == Camp.Sur)
            team.GlobalBannedSurList[index] = character;
        else
            team.GlobalBannedHunList[index] = character;
    }

    /// <summary>
    /// 辅助：设置下一次同阵营对局才会生效的暂存记录
    /// </summary>
    private static void SetGlobalBanRecord(Team team, int index, Character? character, Camp camp)
    {
        if (camp == Camp.Sur)
            team.GlobalBannedSurRecordList[index] = character;
        else
            team.GlobalBannedHunRecordList[index] = character;
    }

    #endregion

    #region 基础规则测试

    [Fact]
    public void DisabledKeys_EmptyByDefault()
    {
        var ctx = CreateContext();
        var vm = new TestCharaSelectViewModel(ctx.SharedDataService, Camp.Sur);
        Assert.Empty(vm.DisabledKeys);
    }

    [Fact]
    public void DisabledKeys_IncludesCurrentBannedCharacter()
    {
        var ctx = CreateContext();
        var vm = new TestCharaSelectViewModel(ctx.SharedDataService, Camp.Sur);

        ctx.Game.CurrentSurBannedList[0] = CreateChara("医生");

        Assert.Contains("医生", vm.DisabledKeys);
        Assert.DoesNotContain("律师", vm.DisabledKeys);
    }

    [Fact]
    public void DisabledKeys_IncludesPickedCharacter()
    {
        var ctx = CreateContext();
        var vm = new TestCharaSelectViewModel(ctx.SharedDataService, Camp.Sur);

        ctx.Game.SurPlayerList[0].Character = CreateChara("园丁");

        Assert.Contains("园丁", vm.DisabledKeys);
    }

    [Fact]
    public void DisabledKeys_IncludesEffectiveGlobalBan()
    {
        var ctx = CreateContext();
        // 启用全局 Ban 位[0]
        ctx.CanGlobalSur[0] = true;
        var vm = new TestCharaSelectViewModel(ctx.SharedDataService, Camp.Sur);

        SetEffectiveGlobalBan(ctx.HomeTeam, 0, CreateChara("律师"), Camp.Sur);

        Assert.Contains("律师", vm.DisabledKeys);
    }

    [Fact]
    public void DisabledKeys_IgnoresRecordUntilItBecomesEffective()
    {
        var ctx = CreateContext();
        ctx.CanGlobalSur[0] = true;
        ctx.CanGlobalSur[1] = true;
        var vm = new TestCharaSelectViewModel(ctx.SharedDataService, Camp.Sur);

        SetGlobalBanRecord(ctx.HomeTeam, 0, CreateChara("律师"), Camp.Sur);
        SetEffectiveGlobalBan(ctx.HomeTeam, 1, CreateChara("园丁"), Camp.Sur);
        ctx.Game.CurrentSurBannedList[0] = CreateChara("医生");

        Assert.DoesNotContain("律师", vm.DisabledKeys);
        Assert.Contains("医生", vm.DisabledKeys);
        Assert.Contains("园丁", vm.DisabledKeys);
    }

    [Fact]
    public void DisabledKeys_ContainsOnlyCurrentEffectiveBansEnabledCurrentBansAndPicks()
    {
        var ctx = CreateContext();
        ctx.CanGlobalSur[0] = true;
        ctx.CanGlobalSur[1] = false;
        var vm = new TestCharaSelectViewModel(ctx.SharedDataService, Camp.Sur);

        SetEffectiveGlobalBan(ctx.HomeTeam, 0, CreateChara("医生"), Camp.Sur);
        SetEffectiveGlobalBan(ctx.HomeTeam, 1, CreateChara("律师"), Camp.Sur);
        SetGlobalBanRecord(ctx.HomeTeam, 2, CreateChara("记录角色"), Camp.Sur);
        SetEffectiveGlobalBan(ctx.AwayTeam, 0, CreateChara("另一队角色"), Camp.Sur);
        ctx.Game.CurrentSurBannedList[0] = CreateChara("祭司");
        ctx.Game.CurrentSurBannedList[1] = CreateChara("关闭位置角色");
        ctx.CanCurrentSur[1] = false;
        ctx.Game.SurPlayerList[0].Character = CreateChara("园丁");

        Assert.Contains("医生", vm.DisabledKeys);
        Assert.Contains("祭司", vm.DisabledKeys);
        Assert.Contains("园丁", vm.DisabledKeys);
        Assert.DoesNotContain("律师", vm.DisabledKeys);
        Assert.DoesNotContain("记录角色", vm.DisabledKeys);
        Assert.DoesNotContain("另一队角色", vm.DisabledKeys);
        Assert.DoesNotContain("关闭位置角色", vm.DisabledKeys);
    }

    [Fact]
    public void DisabledKeys_FiltersNullAndEmptyName()
    {
        var ctx = CreateContext();
        var vm = new TestCharaSelectViewModel(ctx.SharedDataService, Camp.Sur);

        // CurrentSurBannedList 默认填充 new Character(Camp.Sur)，Name 为 null
        // UpdateDisabledKeys 应该过滤掉这些空名角色

        Assert.Empty(vm.DisabledKeys);
    }

    [Fact]
    public void ClearGlobalBanRecords_DoesNotChangeEffectiveGlobalBans()
    {
        var team = new Team(Camp.Sur, TeamType.HomeTeam);
        team.GlobalBannedSurRecordList[^1] = CreateChara("园丁");
        team.GlobalBannedSurList[0] = CreateChara("医生");

        team.ClearGlobalBanRecords();

        Assert.Null(team.GlobalBannedSurRecordList[^1]);
        Assert.Equal("医生", team.GlobalBannedSurList[0]?.Name);
    }

    #endregion

    #region Ban 位启用/禁用联动

    [Fact]
    public void DisabledKeys_DisabledCurrentBanSlot_NotIncluded()
    {
        var ctx = CreateContext();
        var vm = new TestCharaSelectViewModel(ctx.SharedDataService, Camp.Sur);

        // 先 Ban 一个角色
        ctx.Game.CurrentSurBannedList[0] = CreateChara("医生");
        Assert.Contains("医生", vm.DisabledKeys);

        // 关闭该 Ban 位
        ctx.CanCurrentSur[0] = false;

        Assert.DoesNotContain("医生", vm.DisabledKeys);
    }

    [Fact]
    public void DisabledKeys_DisabledGlobalBanSlot_NotIncluded()
    {
        var ctx = CreateContext();
        ctx.CanGlobalSur[0] = true;
        var vm = new TestCharaSelectViewModel(ctx.SharedDataService, Camp.Sur);

        SetEffectiveGlobalBan(ctx.HomeTeam, 0, CreateChara("律师"), Camp.Sur);
        Assert.Contains("律师", vm.DisabledKeys);

        // 关闭全局 Ban 位
        ctx.CanGlobalSur[0] = false;

        Assert.DoesNotContain("律师", vm.DisabledKeys);
    }

    [Fact]
    public void DisabledKeys_TogglingBanSlot_TriggersUpdate()
    {
        var ctx = CreateContext();
        var vm = new TestCharaSelectViewModel(ctx.SharedDataService, Camp.Sur);

        // 先确保 Ban 位启用且有角色
        ctx.Game.CurrentSurBannedList[0] = CreateChara("医生");
        Assert.Contains("医生", vm.DisabledKeys);

        // 切换 toggle 关闭
        ctx.CanCurrentSur[0] = false;
        Assert.DoesNotContain("医生", vm.DisabledKeys);

        // 切换 toggle 重新打开
        ctx.CanCurrentSur[0] = true;
        Assert.Contains("医生", vm.DisabledKeys);
    }

    #endregion

    #region 换边场景测试

    [Fact]
    public void DisabledKeys_AfterSwap_GlobalBanFollowsCurrentSurTeam()
    {
        // 初始: HomeTeam=Sur, AwayTeam=Hun
        var ctx = CreateContext();
        ctx.CanGlobalSur[0] = true;
        ctx.CanGlobalSur[1] = true;

        // HomeTeam 当前有两名生效的求生者全局禁选
        SetEffectiveGlobalBan(ctx.HomeTeam, 0, CreateChara("医生"), Camp.Sur);
        SetEffectiveGlobalBan(ctx.HomeTeam, 1, CreateChara("律师"), Camp.Sur);

        var vm = new TestCharaSelectViewModel(ctx.SharedDataService, Camp.Sur);

        // 初始 HomeTeam 是 SurTeam，应该包含
        Assert.Contains("医生", vm.DisabledKeys);
        Assert.Contains("律师", vm.DisabledKeys);

        // 换边
        ctx.Game.Swap();

        // 现在 AwayTeam 是 SurTeam，HomeTeam 是 HunTeam
        // SurTeam (AwayTeam) 没有生效的求生者全局禁选 → DisabledKeys 应为空
        Assert.Empty(vm.DisabledKeys);
    }

    [Fact]
    public void DisabledKeys_AfterSwapBack_RecordBecomesEffective()
    {
        // 初始: HomeTeam=Sur, AwayTeam=Hun
        var ctx = CreateContext();
        ctx.CanGlobalSur[0] = true;
        ctx.CanGlobalSur[1] = true;

        // 暂存记录当前不生效
        SetGlobalBanRecord(ctx.HomeTeam, 0, CreateChara("医生"), Camp.Sur);
        SetGlobalBanRecord(ctx.HomeTeam, 1, CreateChara("律师"), Camp.Sur);
        ctx.Game.CurrentSurBannedList[0] = CreateChara("祭司");
        ctx.Game.SurPlayerList[0].Character = CreateChara("园丁");

        var vm = new TestCharaSelectViewModel(ctx.SharedDataService, Camp.Sur);
        Assert.DoesNotContain("医生", vm.DisabledKeys);
        Assert.DoesNotContain("律师", vm.DisabledKeys);
        Assert.Contains("祭司", vm.DisabledKeys);
        Assert.Contains("园丁", vm.DisabledKeys);

        // 第一次换边: HomeTeam 变 HunTeam
        ctx.Game.Swap();
        Assert.DoesNotContain("医生", vm.DisabledKeys);
        Assert.DoesNotContain("律师", vm.DisabledKeys);
        Assert.Contains("祭司", vm.DisabledKeys);
        Assert.Contains("园丁", vm.DisabledKeys);

        // 第二次换边: HomeTeam 重新变回 SurTeam
        ctx.Game.Swap();
        Assert.Contains("医生", vm.DisabledKeys);
        Assert.Contains("律师", vm.DisabledKeys);
        Assert.Contains("祭司", vm.DisabledKeys);
        Assert.Contains("园丁", vm.DisabledKeys);
    }

    #endregion

    #region 用户报告的复现场景

    /// <summary>
    /// 完整复现用户报告的 Bug 流程：
    /// 1. 在全局禁选记录记录两名求生者（HomeTeam's GlobalBannedSurRecordList）
    /// 2. 执行一次换边操作
    /// 3. 再次执行换边操作（队伍再次变为求生者）→ 全局禁选显示并禁用
    /// 4. 再次执行换边操作（队伍变为监管者）→ 全局禁选清除，角色应恢复可选
    /// </summary>
    [Fact]
    public void Reproduction_GlobalBanClearedAfterSwap()
    {
        var ctx = CreateContext();

        // 启用相关 Ban 位
        ctx.CanGlobalSur[0] = true;
        ctx.CanGlobalSur[1] = true;

        // Step 1: 在全局禁选记录记录两名求生者（记录在 HomeTeam）
        SetGlobalBanRecord(ctx.HomeTeam, 0, CreateChara("医生"), Camp.Sur);
        SetGlobalBanRecord(ctx.HomeTeam, 1, CreateChara("律师"), Camp.Sur);
        Assert.True(string.IsNullOrEmpty(ctx.HomeTeam.GlobalBannedSurList[0]?.Name));
        Assert.True(string.IsNullOrEmpty(ctx.HomeTeam.GlobalBannedSurList[1]?.Name));

        // Step 2: 第一次换边（HomeTeam: Sur→Hun, AwayTeam: Hun→Sur）
        ctx.Game.Swap();
        Assert.Equal("医生", ctx.HomeTeam.GlobalBannedSurList[0]?.Name);
        Assert.Equal("律师", ctx.HomeTeam.GlobalBannedSurList[1]?.Name);

        // 创建一个新的 VM（模拟导航到 BanSur 页面）
        var vm = new TestCharaSelectViewModel(ctx.SharedDataService, Camp.Sur);

        // 现在 SurTeam 是 AwayTeam，没有生效的求生者全局禁选 → DisabledKeys 为空
        Assert.Empty(vm.DisabledKeys);

        // Step 3: 第二次换边（AwayTeam: Sur→Hun, HomeTeam: Hun→Sur）
        ctx.Game.Swap();

        // HomeTeam 重新成为 SurTeam，角色应出现在 DisabledKeys 中
        Assert.Contains("医生", vm.DisabledKeys);
        Assert.Contains("律师", vm.DisabledKeys);

        // Step 4: 第三次换边（HomeTeam: Sur→Hun, AwayTeam: Hun→Sur）
        ctx.Game.Swap();

        // SurTeam = AwayTeam，AwayTeam 没有生效的求生者全局禁选 → DisabledKeys 应为空
        // 这是用户报告的 Bug: 换边后角色没有恢复可选
        Assert.Empty(vm.DisabledKeys);
        Assert.Equal("医生", ctx.HomeTeam.GlobalBannedSurList[0]?.Name);
        Assert.Equal("律师", ctx.HomeTeam.GlobalBannedSurList[1]?.Name);
    }

    [Fact]
    public void AfterHomeTeamLeavesSurAgain_ItsEffectiveGlobalBansCanBeBannedAndPicked()
    {
        var ctx = CreateContext();
        ctx.CanGlobalSur[0] = true;
        ctx.CanGlobalSur[1] = true;

        SetGlobalBanRecord(ctx.HomeTeam, 0, CreateChara("医生"), Camp.Sur);
        SetGlobalBanRecord(ctx.HomeTeam, 1, CreateChara("律师"), Camp.Sur);

        ctx.Game.Swap(); // HomeTeam: Sur -> Hun，记录覆盖到生效表
        ctx.Game.Swap(); // HomeTeam: Hun -> Sur，医生和律师对当前求生者生效

        var characterSelectionService = new Mock<ICharacterSelectionService>().Object;
        var settingsHostService = new Mock<ISettingsHostService>().Object;
        var banVm = new BanSurPageViewModel.BanSurCurrentViewModel(
            ctx.SharedDataService,
            characterSelectionService);
        var pickVm = new PickPageViewModel.SurPickViewModel(
            ctx.SharedDataService,
            characterSelectionService,
            settingsHostService);

        Assert.Contains("医生", banVm.DisabledKeys);
        Assert.Contains("律师", banVm.DisabledKeys);
        Assert.Contains("医生", pickVm.DisabledKeys);
        Assert.Contains("律师", pickVm.DisabledKeys);

        ctx.Game.Swap(); // HomeTeam: Sur -> Hun，当前求生者队伍改为 AwayTeam

        Assert.DoesNotContain("医生", banVm.DisabledKeys);
        Assert.DoesNotContain("律师", banVm.DisabledKeys);
        Assert.DoesNotContain("医生", pickVm.DisabledKeys);
        Assert.DoesNotContain("律师", pickVm.DisabledKeys);
        Assert.Equal("医生", ctx.HomeTeam.GlobalBannedSurList[0]?.Name);
        Assert.Equal("律师", ctx.HomeTeam.GlobalBannedSurList[1]?.Name);
    }

    /// <summary>
    /// 换边后当前 Ban 位中的角色不应被换边影响
    /// </summary>
    [Fact]
    public void DisabledKeys_CurrentBansUnaffectedBySwap()
    {
        var ctx = CreateContext();
        var vm = new TestCharaSelectViewModel(ctx.SharedDataService, Camp.Sur);

        // 在当局 Ban 位中 Ban 一个角色
        ctx.Game.CurrentSurBannedList[0] = CreateChara("园丁");
        Assert.Contains("园丁", vm.DisabledKeys);

        // 换边
        ctx.Game.Swap();

        // CurrentSurBannedList 是 Game 级别的，不受换边影响 → 仍然禁用
        Assert.Contains("园丁", vm.DisabledKeys);
    }

    /// <summary>
    /// 换边中间态不应产生错误的 DisabledKeys
    /// （验证 Camp 校验生效）
    /// </summary>
    [Fact]
    public void DisabledKeys_SwapIntermediateState_IsSafe()
    {
        var ctx = CreateContext();
        ctx.CanGlobalSur[0] = true;

        // HomeTeam (Sur) 记录一个角色
        SetGlobalBanRecord(ctx.HomeTeam, 0, CreateChara("医生"), Camp.Sur);
        ctx.Game.Swap(); // HomeTeam→Hun
        ctx.Game.Swap(); // HomeTeam→Sur again

        var vm = new TestCharaSelectViewModel(ctx.SharedDataService, Camp.Sur);
        Assert.Contains("医生", vm.DisabledKeys);

        // 触发多次 Swap，每次检查 DisabledKeys 的最终状态
        ctx.Game.Swap(); // HomeTeam→Hun
        Assert.Empty(vm.DisabledKeys);

        ctx.Game.Swap(); // HomeTeam→Sur
        Assert.Contains("医生", vm.DisabledKeys);
    }

    #endregion

    #region 监管者侧测试

    [Fact]
    public void DisabledKeys_Hunter_BanAndPick()
    {
        var ctx = CreateContext();
        ctx.CanGlobalHun[0] = true;
        var vm = new TestCharaSelectViewModel(ctx.SharedDataService, Camp.Hun);

        // 当局 Ban
        ctx.Game.CurrentHunBannedList[0] = CreateChara("杰克", Camp.Hun);
        Assert.Contains("杰克", vm.DisabledKeys);

        // Pick
        ctx.Game.HunPlayer.Character = CreateChara("摄影师", Camp.Hun);
        Assert.Contains("摄影师", vm.DisabledKeys);

        // 全局 Ban 记录
        SetEffectiveGlobalBan(ctx.AwayTeam, 0, CreateChara("杰克", Camp.Hun), Camp.Hun);
        Assert.Contains("杰克", vm.DisabledKeys);
    }

    [Fact]
    public void DisabledKeys_Hunter_DisabledSlotNotIncluded()
    {
        var ctx = CreateContext();
        var vm = new TestCharaSelectViewModel(ctx.SharedDataService, Camp.Hun);

        ctx.Game.CurrentHunBannedList[0] = CreateChara("杰克", Camp.Hun);
        Assert.Contains("杰克", vm.DisabledKeys);

        // 关闭 Ban 位
        ctx.CanCurrentHun[0] = false;
        Assert.DoesNotContain("杰克", vm.DisabledKeys);
    }

    [Fact]
    public void DisabledKeys_Hunter_GlobalBanFollowsSwap()
    {
        var ctx = CreateContext();
        ctx.CanGlobalHun[0] = true;

        // HomeTeam (Sur) 记录监管者 Ban
        SetGlobalBanRecord(ctx.HomeTeam, 0, CreateChara("杰克", Camp.Hun), Camp.Hun);

        var vm = new TestCharaSelectViewModel(ctx.SharedDataService, Camp.Hun);

        // HomeTeam 是 SurTeam → HunTeam 是 AwayTeam
        Assert.DoesNotContain("杰克", vm.DisabledKeys);

        // Swap: HomeTeam→Hun, AwayTeam→Sur
        ctx.Game.Swap();

        // 现在 HomeTeam 是 HunTeam，监管者暂存记录已覆盖到生效列表
        Assert.Contains("杰克", vm.DisabledKeys);

        // Swap back
        ctx.Game.Swap();

        // HomeTeam 重新是 SurTeam，当前 HunTeam 不再包含该生效禁选
        Assert.DoesNotContain("杰克", vm.DisabledKeys);
    }

    #endregion

    #region Pick 位对 Ban 位的影响

    [Fact]
    public void DisabledKeys_PickedCharacterDisabledInBanSlots()
    {
        var ctx = CreateContext();
        var vm = new TestCharaSelectViewModel(ctx.SharedDataService, Camp.Sur);

        // Pick 一个求生者
        ctx.Game.SurPlayerList[0].Character = CreateChara("园丁");
        Assert.Contains("园丁", vm.DisabledKeys);

        // Pick 换位
        ctx.Game.SwapCharactersInPlayers(0, 1);
        Assert.Contains("园丁", vm.DisabledKeys);
    }

    #endregion
}
