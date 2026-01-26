using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Controls;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Messages;
using neo_bpsys_wpf.Core.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Team = neo_bpsys_wpf.Core.Models.Team;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class PickPageViewModel : ViewModelBase, IRecipient<HighlightMessage>
{
#pragma warning disable CS8618
    public PickPageViewModel()
#pragma warning restore CS8618
    {
        //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
    }

    private readonly ISharedDataService _sharedDataService;
    private readonly IAnimationService _animationService;

    public PickPageViewModel(ISharedDataService sharedDataService,
        ICharacterSelectionService characterSelectionService,
        IAnimationService animationService)
    {
        _sharedDataService = sharedDataService;
        _animationService = animationService;
        SurPickViewModelList =
        [
            .. Enumerable.Range(0, 4).Select(i =>
                new SurPickViewModel(sharedDataService, characterSelectionService, i))
        ];
        HunPickVm = new HunPickViewModel(sharedDataService, characterSelectionService);

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
    }

    [RelayCommand]
    private async Task PickingBorderSwitchAsync(string arg)
    {
        if (arg == "Hun")
        {
            if (HunPickingBorder)
                await _animationService.StartPickingBorderBreathingAsync(Camp.Hun, -1);
            else
                await _animationService.StopPickingBorderBreathingAsync(Camp.Hun, -1);
            return;
        }

        var argsMapSur = new Dictionary<string, int[]>
        {
            { "0", [0] },
            { "1", [1] },
            { "2", [2] },
            { "3", [3] },
            { "0and1", [0, 1] }
        };

        for (var i = 0; i < argsMapSur[arg].Length; i++)
        {
            var index = argsMapSur[arg][i];
            if (SurPickingBorderList[index])
            {
                if (i == argsMapSur[arg].Length - 1)
                {
                    await _animationService.StartPickingBorderBreathingAsync(Camp.Sur, index);
                }
                else
                {
                    _ = _animationService.StartPickingBorderBreathingAsync(Camp.Sur, index);
                }
            }
            else
            {
                if (i == argsMapSur[arg].Length - 1)
                {
                    await _animationService.StopPickingBorderBreathingAsync(Camp.Sur, index);
                }
                else
                {
                    _ = _animationService.StopPickingBorderBreathingAsync(Camp.Sur, index);
                }
            }
        }
    }

    public void Receive(HighlightMessage message)
    {
        if (message.GameAction == GameAction.PickSur)
        {
            if (message.Index == null) return;
            foreach (var i in message.Index)
            {
                SurPickingBorderList[i] = true;
                _ = PickingBorderSwitchAsync(i.ToString());
            }
        }
        else
        {
            for (var i = 0; i < SurPickingBorderList.Count; i++)
            {
                if (!SurPickingBorderList[i]) continue;
                SurPickingBorderList[i] = false;
                _ = PickingBorderSwitchAsync(i.ToString());
            }
        }

        if (message.GameAction == GameAction.PickHun)
        {
            HunPickingBorder = true;
            _ = PickingBorderSwitchAsync("Hun");
        }
        else
        {
            if (!HunPickingBorder) return;
            HunPickingBorder = false;
            _ = PickingBorderSwitchAsync("Hun");
        }
    }

    public Team MainTeam => _sharedDataService.HomeTeam;
    public Team AwayTeam => _sharedDataService.AwayTeam;

    public ObservableCollection<bool> SurPickingBorderList { get; set; } =
        [.. Enumerable.Range(0, 4).Select(_ => false)];

    [ObservableProperty] private bool _hunPickingBorder;

    public ObservableCollection<SurPickViewModel> SurPickViewModelList { get; set; }
    public HunPickViewModel HunPickVm { get; set; }
    public ObservableCollection<HomeSurGlobalBanRecordViewModel> HomeSurGlobalBanRecordViewModelList { get; set; }
    public ObservableCollection<HomeHunGlobalBanRecordViewModel> HomeHunGlobalBanRecordViewModelList { get; set; }
    public ObservableCollection<AwaySurGlobalBanRecordViewModel> AwaySurGlobalBanRecordViewModelList { get; set; }
    public ObservableCollection<AwayHunGlobalBanRecordViewModel> AwayHunGlobalBanRecordViewModelList { get; set; }

    //基于模板基类的VM实现
    public partial class SurPickViewModel : CharaSelectViewModelBase
    {
        private readonly ICharacterSelectionService _characterSelectionService;
        public Player ThisPlayer => SharedDataService.CurrentGame.SurPlayerList[Index];

        public SurPickViewModel(ISharedDataService sharedDataService,
            ICharacterSelectionService characterSelectionService, int index = 0) :
            base(sharedDataService, Camp.Sur, index)
        {
            _characterSelectionService = characterSelectionService;
            sharedDataService.TeamSwapped += (_, _) => OnPropertyChanged(nameof(ThisPlayer));
            ThisPlayer.PropertyChanged += OnThisPlayerPropertyChanged;
            sharedDataService.CurrentGameChanged += (_, _) =>
            {
                ThisPlayer.PropertyChanged -= OnThisPlayerPropertyChanged;
                OnPropertyChanged(nameof(ThisPlayer));
                ThisPlayer.PropertyChanged += OnThisPlayerPropertyChanged;
                SyncCharaFromSourceAsync();
            };
        }

        private void OnThisPlayerPropertyChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(ThisPlayer.Character))
                SyncCharaFromSourceAsync();
        }

        protected override async Task SyncCharaToSourceAsync()
        {
            await _characterSelectionService.SelectSurvivorAsync(Index, SelectedChara);
            PreviewImage = ThisPlayer.Character?.HeaderImage;
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
        }

        protected override void SyncIsEnabled() => throw new NotImplementedException();

        protected override bool IsActionNameCorrect(GameAction? action) => action == GameAction.PickSur;
    }

    public class HunPickViewModel(
        ISharedDataService sharedDataService,
        ICharacterSelectionService characterSelectionService)
        : CharaSelectViewModelBase(sharedDataService, Camp.Hun)
    {
        private readonly ICharacterSelectionService _characterSelectionService1 = characterSelectionService;

        protected override async Task SyncCharaToSourceAsync()
        {
            await _characterSelectionService1.SelectHunterAsync(SelectedChara);
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

    public class HomeSurGlobalBanRecordViewModel : CharaSelectViewModelBase
    {
        private Character? _recordedChara;

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
                        SharedDataService.HomeTeam.GlobalBannedSurRecordList[Index] = value;
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
                        SharedDataService.HomeTeam.GlobalBannedHunRecordList[Index] = value;
                });
        }

        protected override Task SyncCharaToSourceAsync() => throw new NotImplementedException();

        protected override void SyncCharaFromSourceAsync() =>
            RecordedChara = SharedDataService.HomeTeam.GlobalBannedHunRecordList[Index];

        protected override void SyncIsEnabled() => throw new NotImplementedException();

        protected override bool IsActionNameCorrect(GameAction? action) => false;
    }

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
                        SharedDataService.AwayTeam.GlobalBannedSurRecordList[Index] = value;
                });
        }

        protected override Task SyncCharaToSourceAsync() => throw new NotImplementedException();

        protected override void SyncCharaFromSourceAsync() =>
            RecordedChara = SharedDataService.AwayTeam.GlobalBannedSurRecordList[Index];

        protected override void SyncIsEnabled() => throw new NotImplementedException();

        protected override bool IsActionNameCorrect(GameAction? action) => false;
    }

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
                        SharedDataService.AwayTeam.GlobalBannedHunRecordList[Index] = value;
                });
        }

        protected override Task SyncCharaToSourceAsync() => throw new NotImplementedException();

        protected override void SyncCharaFromSourceAsync() =>
            RecordedChara = SharedDataService.AwayTeam.GlobalBannedHunRecordList[Index];

        protected override void SyncIsEnabled() => throw new NotImplementedException();

        protected override bool IsActionNameCorrect(GameAction? action) => false;
    }
}