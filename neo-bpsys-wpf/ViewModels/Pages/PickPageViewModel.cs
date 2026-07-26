using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Controls;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Tutorial;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Team = neo_bpsys_wpf.Core.Models.Team;

namespace neo_bpsys_wpf.ViewModels.Pages;

/// <summary>
/// 角色选择页面视图模型，管理求生者/监管者选择、全局禁用记录等角色选取流程。
/// </summary>
public partial class PickPageViewModel : ViewModelBase
{
    /// <summary>
    /// 用于设计时预览的无参构造函数。
    /// </summary>
#pragma warning disable CS8618
    public PickPageViewModel()
#pragma warning restore CS8618
    {
        // Decorative constructor for design-time only.
    }

    private readonly ISharedDataService _sharedDataService;
    private readonly ISettingsHostService _settingsHostService;

    /// <summary>
    /// 初始化角色选择页面视图模型。
    /// </summary>
    /// <param name="sharedDataService">共享数据服务</param>
    /// <param name="characterSelectionService">角色选择服务</param>
    /// <param name="settingsHostService">设置宿主服务</param>
    public PickPageViewModel(ISharedDataService sharedDataService,
        ICharacterSelectionService characterSelectionService,
        ISettingsHostService settingsHostService)
    {
        _sharedDataService = sharedDataService;
        _settingsHostService = settingsHostService;
        SurPickViewModelList =
        [
            .. Enumerable.Range(0, 4).Select(i =>
                new SurPickViewModel(sharedDataService, characterSelectionService, settingsHostService, i))
        ];
        HunPickVm = new HunPickViewModel(sharedDataService, characterSelectionService, settingsHostService);

        HomeSurGlobalBanRecordViewModelList =
        [
            .. Enumerable.Range(0, AppConstants.GlobalBanSurCount)
                .Select(i => new HomeSurGlobalBanRecordViewModel(sharedDataService, i))
        ];
        HomeHunGlobalBanRecordViewModelList =
        [
            .. Enumerable.Range(0, AppConstants.GlobalBanHunCount)
                .Select(i => new HomeHunGlobalBanRecordViewModel(sharedDataService, i))
        ];
        AwaySurGlobalBanRecordViewModelList =
        [
            .. Enumerable.Range(0, AppConstants.GlobalBanSurCount)
                .Select(i => new AwaySurGlobalBanRecordViewModel(sharedDataService, i))
        ];
        AwayHunGlobalBanRecordViewModelList =
        [
            .. Enumerable.Range(0, AppConstants.GlobalBanHunCount)
                .Select(i => new AwayHunGlobalBanRecordViewModel(sharedDataService, i))
        ];
        sharedDataService.TeamSwapped += (_, _) => RefreshCurrentSurvivorGlobalBanRecordTarget();
        sharedDataService.CurrentGameChanged += (_, _) => RefreshCurrentSurvivorGlobalBanRecordTarget();
    }

    /// <summary>
    /// 获取或设置是否自动记录全局禁用。
    /// </summary>
    public bool IsGlobalBanAutoRecord
    {
        get => _settingsHostService.Settings.IsRecordGlobalBan;

        set
        {
            if (_settingsHostService.Settings.IsRecordGlobalBan == value) return;
            _settingsHostService.Settings.IsRecordGlobalBan = value;
            _ = _settingsHostService.SaveConfigAsync();
        }
    }

    /// <summary>
    /// 获取或设置是否允许角色复选。设置时持久化并立即刷新所有 Pick 选择器的禁用集合。
    /// 已 Ban 角色的禁用规则不受此开关影响。
    /// </summary>
    public bool IsAllowCharacterReselect
    {
        get => _settingsHostService.Settings.IsAllowCharacterReselect;

        set
        {
            if (_settingsHostService.Settings.IsAllowCharacterReselect == value) return;
            _settingsHostService.Settings.IsAllowCharacterReselect = value;
            _ = _settingsHostService.SaveConfigAsync();
            // 开关切换不会触发 Ban/Pick 集合变更事件，需显式刷新所有 Pick VM 的禁用集合
            foreach (var vm in SurPickViewModelList)
                vm.RefreshDisabledKeys();
            HunPickVm.RefreshDisabledKeys();
        }
    }

    /// <summary>主队数据。</summary>
    public Team HomeTeam => _sharedDataService.HomeTeam;
    /// <summary>客队数据。</summary>
    public Team AwayTeam => _sharedDataService.AwayTeam;

    /// <summary>获取主队全局禁选记录区的教程目标标记。</summary>
    public string HomeSurGlobalBanRecordTargetTag =>
        HomeTeam.Camp == Camp.Sur ? "CurrentSurvivorGlobalBanRecordPanel" : string.Empty;

    /// <summary>获取客队全局禁选记录区的教程目标标记。</summary>
    public string AwaySurGlobalBanRecordTargetTag =>
        AwayTeam.Camp == Camp.Sur ? "CurrentSurvivorGlobalBanRecordPanel" : string.Empty;

    /// <summary>求生者选择视图模型列表。</summary>
    public ObservableCollection<SurPickViewModel> SurPickViewModelList { get; set; }
    /// <summary>监管者选择视图模型。</summary>
    public HunPickViewModel HunPickVm { get; set; }
    /// <summary>主队求生者全局禁用记录视图模型列表。</summary>
    public ObservableCollection<HomeSurGlobalBanRecordViewModel> HomeSurGlobalBanRecordViewModelList { get; set; }
    /// <summary>主队监管者全局禁用记录视图模型列表。</summary>
    public ObservableCollection<HomeHunGlobalBanRecordViewModel> HomeHunGlobalBanRecordViewModelList { get; set; }
    /// <summary>客队求生者全局禁用记录视图模型列表。</summary>
    public ObservableCollection<AwaySurGlobalBanRecordViewModel> AwaySurGlobalBanRecordViewModelList { get; set; }
    /// <summary>客队监管者全局禁用记录视图模型列表。</summary>
    public ObservableCollection<AwayHunGlobalBanRecordViewModel> AwayHunGlobalBanRecordViewModelList { get; set; }

    private void RefreshCurrentSurvivorGlobalBanRecordTarget()
    {
        OnPropertyChanged(nameof(HomeSurGlobalBanRecordTargetTag));
        OnPropertyChanged(nameof(AwaySurGlobalBanRecordTargetTag));
    }

    //基于模板基类的VM实现
    /// <summary>
    /// 求生者选择视图模型，管理单个求生者位置的角色选择。
    /// </summary>
    public partial class SurPickViewModel : CharaSelectViewModelBase
    {
        private readonly ICharacterSelectionService _characterSelectionService;
        private readonly ISettingsHostService _settingsHostService;
        /// <summary>获取当前求生者玩家数据。</summary>
        public Player ThisPlayer => SharedDataService.CurrentGame.SurPlayerList[Index];

        public SurPickViewModel(ISharedDataService sharedDataService,
            ICharacterSelectionService characterSelectionService,
            ISettingsHostService settingsHostService,
            int index = 0) :
            base(sharedDataService, Camp.Sur, index)
        {
            _characterSelectionService = characterSelectionService;
            _settingsHostService = settingsHostService;
            sharedDataService.TeamSwapped += (_, _) => OnPropertyChanged(nameof(ThisPlayer));
            characterSelectionService.CharacterSelected += (sender, e) =>
            {
                if (e.Camp == Camp.Sur && e.PlayerIndex == index)
                {
                    SyncCharaFromSourceAsync();
                }
            };
            sharedDataService.CurrentGameChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(ThisPlayer));
                SyncCharaFromSourceAsync();
            };
        }

        /// <summary>
        /// 标记当前选择器为 Pick 类型，使其在 <see cref="IsAllowCharacterReselect"/> 开启时跳过已 Pick 角色的禁用。
        /// </summary>
        protected override bool IsPickSelector => true;

        /// <summary>
        /// 从设置服务读取是否允许角色复选。
        /// 注意：基类构造函数会在派生类字段赋值前调用 <c>UpdateDisabledKeys</c>，
        /// 因此此处必须对 <c>_settingsHostService</c> 做空安全处理，构造期间返回 <c>false</c>
        /// （与开关关闭等价，保持改动前行为）。构造完成后由事件触发的重新计算会读取真实值。
        /// </summary>
        protected override bool IsAllowCharacterReselect =>
            _settingsHostService?.Settings?.IsAllowCharacterReselect ?? false;

        protected override async Task SyncCharaToSourceAsync()
        {
            await _characterSelectionService.SelectSurvivorAsync(Index, SelectedChara, true, _settingsHostService.Settings.IsRecordGlobalBan);
            PreviewImage = ThisPlayer.Character?.HeaderImage;
            PublishPickSurvivorSelected(Index);
            if (SharedDataService.CurrentGame.SurPlayerList.All(player => player.Character != null))
            {
                TutorialSignalPublisher.Publish(TutorialSignalIds.PickSurvivorSlotsCompleted);
            }
        }

        protected override void SyncCharaFromSourceAsync()
        {
            SelectedChara = SharedDataService.CurrentGame.SurPlayerList[Index].Character;
            PreviewImage = SelectedChara?.HeaderImage;
        }

        [RelayCommand]
        private async Task SwapCharacterInPlayersAsync(CharacterChangerCommandParameter parameter)
        {
            await _characterSelectionService.SwapSurvivorsAsync(parameter.Source, parameter.Target);
            TutorialSignalPublisher.Publish(
                TutorialSignalIds.CharacterChangerApplied,
                new { parameter.Source, parameter.Target });
        }

        protected override void SyncIsEnabled() => throw new NotImplementedException();

        protected override bool IsActionNameCorrect(GameAction? action) => action == GameAction.PickSur;

        private static void PublishPickSurvivorSelected(int index)
        {
            var signalId = index switch
            {
                0 => TutorialSignalIds.PickCharacterSelectedSurvivor1,
                1 => TutorialSignalIds.PickCharacterSelectedSurvivor2,
                2 => TutorialSignalIds.PickCharacterSelectedSurvivor3,
                3 => TutorialSignalIds.PickCharacterSelectedSurvivor4,
                _ => null
            };

            if (signalId != null)
            {
                TutorialSignalPublisher.Publish(signalId, new { Index = index });
            }
        }
    }

    /// <summary>
    /// 监管者选择视图模型。
    /// </summary>
    public class HunPickViewModel : CharaSelectViewModelBase
    {
        private readonly ICharacterSelectionService _characterSelectionService1;
        private readonly ISettingsHostService _settingsHostService;

        public HunPickViewModel(
            ISharedDataService sharedDataService,
            ICharacterSelectionService characterSelectionService,
            ISettingsHostService settingsHostService) : base(sharedDataService, Camp.Hun)
        {
            _characterSelectionService1 = characterSelectionService;
            _settingsHostService = settingsHostService;
            characterSelectionService.CharacterSelected += (sender, e) =>
            {
                if (e.Camp == Camp.Hun)
                {
                    SyncCharaFromSourceAsync();
                }
            };
        }

        /// <summary>
        /// 标记当前选择器为 Pick 类型，使其在 <see cref="IsAllowCharacterReselect"/> 开启时跳过已 Pick 角色的禁用。
        /// </summary>
        protected override bool IsPickSelector => true;

        /// <summary>
        /// 从设置服务读取是否允许角色复选。
        /// 注意：基类构造函数会在派生类字段赋值前调用 <c>UpdateDisabledKeys</c>，
        /// 因此此处必须对 <c>_settingsHostService</c> 做空安全处理，构造期间返回 <c>false</c>
        /// （与开关关闭等价，保持改动前行为）。构造完成后由事件触发的重新计算会读取真实值。
        /// </summary>
        protected override bool IsAllowCharacterReselect =>
            _settingsHostService?.Settings?.IsAllowCharacterReselect ?? false;

        protected override async Task SyncCharaToSourceAsync()
        {
            await _characterSelectionService1.SelectHunterAsync(SelectedChara, true, _settingsHostService.Settings.IsRecordGlobalBan);
            PreviewImage = SharedDataService.CurrentGame.HunPlayer.Character?.HeaderImage;
        }

        protected override void SyncCharaFromSourceAsync()
        {
            SelectedChara = SharedDataService.CurrentGame.HunPlayer.Character;
            PreviewImage = SelectedChara?.HeaderImage;
        }

        protected override void SyncIsEnabled()
        {
            throw new NotImplementedException();
        }

        protected override bool IsActionNameCorrect(GameAction? action) => action == GameAction.PickHun;
    }

    /// <summary>
    /// 主队求生者全局禁用记录视图模型。
    /// </summary>
    public class HomeSurGlobalBanRecordViewModel : CharaSelectViewModelBase
    {
        private Character? _recordedChara;

        /// <summary>
        /// 初始化主队求生者全局禁用记录视图模型。
        /// </summary>
        /// <param name="sharedDataService">共享数据服务</param>
        /// <param name="index">序号</param>
        public HomeSurGlobalBanRecordViewModel(ISharedDataService sharedDataService, int index = 0) : base(
            sharedDataService, Camp.Sur, index)
        {
            SharedDataService.HomeTeam.GlobalBannedSurRecordList.CollectionChanged +=
                (_, _) => SyncCharaFromSourceAsync();
        }

        public Character? RecordedChara
        {
            get => _recordedChara;
            set => SetPropertyWithAction(ref _recordedChara, value,
                _ =>
                {
                    if (SharedDataService.HomeTeam.GlobalBannedSurRecordList[Index] != value)
                    {
                        SharedDataService.HomeTeam.GlobalBannedSurRecordList[Index] = value;
                        PublishGlobalBanRecordUpdated(TeamType.HomeTeam, Camp.Sur, Index, value);
                    }
                });
        }

        protected override Task SyncCharaToSourceAsync() => throw new NotImplementedException();

        protected override void SyncCharaFromSourceAsync() =>
            RecordedChara = SharedDataService.HomeTeam.GlobalBannedSurRecordList[Index];

        protected override void SyncIsEnabled()
        {
            throw new NotImplementedException();
        }

        protected override bool IsActionNameCorrect(GameAction? action) => false;
    }

    /// <summary>
    /// 主队监管者全局禁用记录视图模型。
    /// </summary>
    public class HomeHunGlobalBanRecordViewModel : CharaSelectViewModelBase
    {
        private Character? _recordedChara;

        public HomeHunGlobalBanRecordViewModel(ISharedDataService sharedDataService, int index = 0) : base(
            sharedDataService, Camp.Hun, index)
        {
            SharedDataService.HomeTeam.GlobalBannedHunRecordList.CollectionChanged +=
                (_, _) => SyncCharaFromSourceAsync();
        }

        public Character? RecordedChara
        {
            get => _recordedChara;
            set => SetPropertyWithAction(ref _recordedChara, value,
                _ =>
                {
                    if (SharedDataService.HomeTeam.GlobalBannedHunRecordList[Index] != value)
                    {
                        SharedDataService.HomeTeam.GlobalBannedHunRecordList[Index] = value;
                        PublishGlobalBanRecordUpdated(TeamType.HomeTeam, Camp.Hun, Index, value);
                    }
                });
        }

        protected override Task SyncCharaToSourceAsync() => throw new NotImplementedException();

        protected override void SyncCharaFromSourceAsync() =>
            RecordedChara = SharedDataService.HomeTeam.GlobalBannedHunRecordList[Index];

        protected override void SyncIsEnabled() => throw new NotImplementedException();

        protected override bool IsActionNameCorrect(GameAction? action) => false;
    }

    /// <summary>
    /// 客队求生者全局禁用记录视图模型。
    /// </summary>
    public class AwaySurGlobalBanRecordViewModel : CharaSelectViewModelBase
    {
        private Character? _recordedChara;

        public AwaySurGlobalBanRecordViewModel(ISharedDataService sharedDataService, int index = 0) : base(
            sharedDataService, Camp.Sur, index)
        {
            SharedDataService.AwayTeam.GlobalBannedSurRecordList.CollectionChanged +=
                (_, _) => SyncCharaFromSourceAsync();
        }

        public Character? RecordedChara
        {
            get => _recordedChara;
            set => SetPropertyWithAction(ref _recordedChara, value,
                _ =>
                {
                    if (SharedDataService.AwayTeam.GlobalBannedSurRecordList[Index] != value)
                    {
                        SharedDataService.AwayTeam.GlobalBannedSurRecordList[Index] = value;
                        PublishGlobalBanRecordUpdated(TeamType.AwayTeam, Camp.Sur, Index, value);
                    }
                });
        }

        protected override Task SyncCharaToSourceAsync() => throw new NotImplementedException();

        protected override void SyncCharaFromSourceAsync() =>
            RecordedChara = SharedDataService.AwayTeam.GlobalBannedSurRecordList[Index];

        protected override void SyncIsEnabled() => throw new NotImplementedException();

        protected override bool IsActionNameCorrect(GameAction? action) => false;
    }

    /// <summary>
    /// 客队监管者全局禁用记录视图模型。
    /// </summary>
    public class AwayHunGlobalBanRecordViewModel : CharaSelectViewModelBase
    {
        private Character? _recordedChara;

        public AwayHunGlobalBanRecordViewModel(ISharedDataService sharedDataService, int index = 0) : base(
            sharedDataService, Camp.Hun, index)
        {
            SharedDataService.AwayTeam.GlobalBannedHunRecordList.CollectionChanged +=
                (_, _) => SyncCharaFromSourceAsync();
        }

        public Character? RecordedChara
        {
            get => _recordedChara;
            set => SetPropertyWithAction(ref _recordedChara, value,
                _ =>
                {
                    if (SharedDataService.AwayTeam.GlobalBannedHunRecordList[Index] != value)
                    {
                        SharedDataService.AwayTeam.GlobalBannedHunRecordList[Index] = value;
                        PublishGlobalBanRecordUpdated(TeamType.AwayTeam, Camp.Hun, Index, value);
                    }
                });
        }

        protected override Task SyncCharaToSourceAsync() => throw new NotImplementedException();

        protected override void SyncCharaFromSourceAsync() =>
            RecordedChara = SharedDataService.AwayTeam.GlobalBannedHunRecordList[Index];

        protected override void SyncIsEnabled() => throw new NotImplementedException();

        protected override bool IsActionNameCorrect(GameAction? action) => false;
    }

    private static void PublishGlobalBanRecordUpdated(
        TeamType teamType,
        Camp camp,
        int index,
        Character? character)
    {
        TutorialSignalPublisher.Publish(
            TutorialSignalIds.GlobalBanRecordUpdated,
            new
            {
                TeamType = teamType,
                Camp = camp,
                Index = index,
                CharacterName = character?.Name
            });
    }
}
