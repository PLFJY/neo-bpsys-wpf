using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Controls;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Team = neo_bpsys_wpf.Core.Models.Team;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class PickPageViewModel : ViewModelBase
{
#pragma warning disable CS8618
    public PickPageViewModel()
#pragma warning restore CS8618
    {
        // Decorative constructor for design-time only.
    }

    private readonly ISharedDataService _sharedDataService;
    private readonly ISettingsHostService _settingsHostService;

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
    }

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

    public Team HomeTeam => _sharedDataService.HomeTeam;
    public Team AwayTeam => _sharedDataService.AwayTeam;

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
        private readonly ISettingsHostService _settingsHostService;
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
            await _characterSelectionService.SelectSurvivorAsync(Index, SelectedChara, true, _settingsHostService.Settings.IsRecordGlobalBan);
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
        ICharacterSelectionService characterSelectionService,
        ISettingsHostService settingsHostService)
        : CharaSelectViewModelBase(sharedDataService, Camp.Hun)
    {
        private readonly ICharacterSelectionService _characterSelectionService1 = characterSelectionService;
        private readonly ISettingsHostService _settingsHostService = settingsHostService;

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
