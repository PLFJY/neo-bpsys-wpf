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
    public PickPageViewModel()
    {
        //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
    }

    private readonly ISharedDataService _sharedDataService;
    private readonly IFrontedWindowService _frontedWindowService;

    public PickPageViewModel(ISharedDataService sharedDataService, IFrontedWindowService frontedWindowService)
    {
        _sharedDataService = sharedDataService;
        _frontedWindowService = frontedWindowService;
        SurPickViewModelList =
            [.. Enumerable.Range(0, 4).Select(i => new SurPickViewModel(sharedDataService, frontedWindowService, i))];
        HunPickVm = new HunPickViewModel(sharedDataService, frontedWindowService);
        MainSurGlobalBanRecordViewModelList =
        [
            .. Enumerable.Range(0, AppConstants.GlobalBanSurCount)
                .Select(i => new MainSurGlobalBanRecordViewModel(sharedDataService, i))
        ];
        MainHunGlobalBanRecordViewModelList =
        [
            .. Enumerable.Range(0, AppConstants.GlobalBanHunCount)
                .Select(i => new MainHunGlobalBanRecordViewModel(sharedDataService, i))
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
                await _frontedWindowService.BreathingStart(FrontedWindowType.BpWindow, "HunPickingBorder", -1, string.Empty);
            else
                await _frontedWindowService.BreathingStop(FrontedWindowType.BpWindow, "HunPickingBorder", -1, string.Empty);
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
                    await _frontedWindowService.BreathingStart(FrontedWindowType.BpWindow, "SurPickingBorder", index,
                        string.Empty);
                }
                else
                {
                    _ = _frontedWindowService.BreathingStart(FrontedWindowType.BpWindow, "SurPickingBorder", index,
                        string.Empty);
                }
            }
            else
            {
                if (i == argsMapSur[arg].Length - 1)
                {
                    await _frontedWindowService.BreathingStop(FrontedWindowType.BpWindow, "SurPickingBorder", index,
                        string.Empty);
                }
                else
                {
                    _ = _frontedWindowService.BreathingStop(FrontedWindowType.BpWindow, "SurPickingBorder", index,
                        string.Empty);
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

    public Team MainTeam => _sharedDataService.MainTeam;
    public Team AwayTeam => _sharedDataService.AwayTeam;

    public ObservableCollection<bool> SurPickingBorderList { get; set; } =
        [.. Enumerable.Range(0, 4).Select(_ => false)];

    [ObservableProperty] private bool _hunPickingBorder;

    public ObservableCollection<SurPickViewModel> SurPickViewModelList { get; set; }
    public HunPickViewModel HunPickVm { get; set; }
    public ObservableCollection<MainSurGlobalBanRecordViewModel> MainSurGlobalBanRecordViewModelList { get; set; }
    public ObservableCollection<MainHunGlobalBanRecordViewModel> MainHunGlobalBanRecordViewModelList { get; set; }
    public ObservableCollection<AwaySurGlobalBanRecordViewModel> AwaySurGlobalBanRecordViewModelList { get; set; }
    public ObservableCollection<AwayHunGlobalBanRecordViewModel> AwayHunGlobalBanRecordViewModelList { get; set; }

    //基于模板基类的VM实现
    public partial class SurPickViewModel : CharaSelectViewModelBase
    {
        private readonly IFrontedWindowService _frontedWindowService;
        public Player ThisPlayer => SharedDataService.CurrentGame.SurPlayerList[Index];

        public SurPickViewModel(ISharedDataService sharedDataService, IFrontedWindowService frontedWindowService, int index = 0) :
            base(sharedDataService, Camp.Sur, index)
        {
            _frontedWindowService = frontedWindowService;
            sharedDataService.CurrentGameChanged += (_, _) =>
            {
                ThisPlayer.PropertyChanged -= OnThisPlayerPropertyChanged;
                OnPropertyChanged(nameof(ThisPlayer));
                ThisPlayer.PropertyChanged += OnThisPlayerPropertyChanged;
            };
            sharedDataService.TeamSwapped += (_, _) => OnPropertyChanged(nameof(ThisPlayer));
            ThisPlayer.PropertyChanged += OnThisPlayerPropertyChanged;
        }

        private void OnThisPlayerPropertyChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(ThisPlayer.Character))
                ReverseSyncChara();
        }

        public override async Task SyncCharaAsync()
        {
            _frontedWindowService.FadeOutAnimation(FrontedWindowType.BpWindow, "SurPick", Index, string.Empty);
            await Task.Delay(250);
            ThisPlayer.Character = SelectedChara;
            _frontedWindowService.FadeInAnimation(FrontedWindowType.BpWindow, "SurPick", Index, string.Empty);
            PreviewImage = ThisPlayer.Character?.HeaderImage;
        }

        [RelayCommand]
        private async Task SwapCharacterInPlayersAsync(CharacterChangerCommandParameter parameter)
        {
            _frontedWindowService.FadeOutAnimation(FrontedWindowType.BpWindow, "SurPick", parameter.Source, string.Empty);
            _frontedWindowService.FadeOutAnimation(FrontedWindowType.BpWindow, "SurPick", parameter.Target, string.Empty);
            await Task.Delay(250);
            SharedDataService.CurrentGame.SwapCharactersInPlayers(parameter.Source, parameter.Target);
            _frontedWindowService.FadeInAnimation(FrontedWindowType.BpWindow, "SurPick", parameter.Source, string.Empty);
            _frontedWindowService.FadeInAnimation(FrontedWindowType.BpWindow, "SurPick", parameter.Target, string.Empty);
        }

        private void ReverseSyncChara()
        {
            SelectedChara = SharedDataService.CurrentGame.SurPlayerList[Index].Character;
            PreviewImage = SharedDataService.CurrentGame.SurPlayerList[Index].Character?.HeaderImage;
        }

        protected override void SyncIsEnabled() => throw new NotImplementedException();

        protected override bool IsActionNameCorrect(GameAction? action) => action == GameAction.PickSur;
    }

    public class HunPickViewModel : CharaSelectViewModelBase
    {
        private readonly IFrontedWindowService _frontedWindowService;

        public HunPickViewModel(ISharedDataService sharedDataService, IFrontedWindowService frontedWindowService) : base(
            sharedDataService, Camp.Hun)
        {
            _frontedWindowService = frontedWindowService;
        }

        public override async Task SyncCharaAsync()
        {
            _frontedWindowService.FadeOutAnimation(FrontedWindowType.BpWindow, "HunPick", -1, string.Empty);
            await Task.Delay(250);
            SharedDataService.CurrentGame.HunPlayer.Character = SelectedChara;
            _frontedWindowService.FadeInAnimation(FrontedWindowType.BpWindow, "HunPick", -1, string.Empty);
            PreviewImage = SharedDataService.CurrentGame.HunPlayer.Character?.HeaderImage;
        }

        protected override void SyncIsEnabled()
        {
            throw new NotImplementedException();
        }

        protected override bool IsActionNameCorrect(GameAction? action) => action == GameAction.PickHun;
    }

    public class MainSurGlobalBanRecordViewModel : CharaSelectViewModelBase
    {
        private Character? _recordedChara;

        public Character? RecordedChara
        {
            get => _recordedChara;
            set
            {
                _recordedChara = value;
                SharedDataService.MainTeam.GlobalBannedSurRecordArray[Index] = _recordedChara;
            }
        }

        public MainSurGlobalBanRecordViewModel(ISharedDataService sharedDataService, int index = 0) : base(
            sharedDataService, Camp.Sur, index)
        {
        }

        public override Task SyncCharaAsync() => throw new NotImplementedException();

        protected override void SyncIsEnabled()
        {
            throw new NotImplementedException();
        }

        protected override bool IsActionNameCorrect(GameAction? action) => false;
    }

    public class MainHunGlobalBanRecordViewModel : CharaSelectViewModelBase
    {
        private Character? _recordedChara;

        public Character? RecordedChara
        {
            get => _recordedChara;
            set
            {
                _recordedChara = value;
                SharedDataService.MainTeam.GlobalBannedHunRecordArray[Index] = _recordedChara;
            }
        }

        public MainHunGlobalBanRecordViewModel(ISharedDataService sharedDataService, int index = 0) : base(
            sharedDataService, Camp.Hun, index)
        {
        }

        public override Task SyncCharaAsync() => throw new NotImplementedException();

        protected override void SyncIsEnabled() => throw new NotImplementedException();

        protected override bool IsActionNameCorrect(GameAction? action) => false;
    }

    public class AwaySurGlobalBanRecordViewModel : CharaSelectViewModelBase
    {
        private Character? _recordedChara;

        public Character? RecordedChara
        {
            get => _recordedChara;
            set
            {
                _recordedChara = value;
                SharedDataService.AwayTeam.GlobalBannedSurRecordArray[Index] = _recordedChara;
            }
        }

        public AwaySurGlobalBanRecordViewModel(ISharedDataService sharedDataService, int index = 0) : base(
            sharedDataService, Camp.Sur, index)
        {
        }

        public override Task SyncCharaAsync() => throw new NotImplementedException();

        protected override void SyncIsEnabled() => throw new NotImplementedException();

        protected override bool IsActionNameCorrect(GameAction? action) => false;
    }

    public class AwayHunGlobalBanRecordViewModel : CharaSelectViewModelBase
    {
        private Character? _recordedChara;

        public Character? RecordedChara
        {
            get => _recordedChara;
            set
            {
                _recordedChara = value;
                SharedDataService.AwayTeam.GlobalBannedHunRecordArray[Index] = _recordedChara;
            }
        }

        public AwayHunGlobalBanRecordViewModel(ISharedDataService sharedDataService, int index = 0) : base(
            sharedDataService, Camp.Hun, index)
        {
        }

        public override Task SyncCharaAsync() => throw new NotImplementedException();

        protected override void SyncIsEnabled() => throw new NotImplementedException();

        protected override bool IsActionNameCorrect(GameAction? action) => false;
    }
}