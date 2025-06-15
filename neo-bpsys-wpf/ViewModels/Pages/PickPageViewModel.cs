using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using neo_bpsys_wpf.CustomControls;
using neo_bpsys_wpf.Messages;
using neo_bpsys_wpf.Models;
using neo_bpsys_wpf.Services;
using System.Collections.ObjectModel;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Views.Windows;
using System.Collections.Specialized;

namespace neo_bpsys_wpf.ViewModels.Pages
{
    public partial class PickPageViewModel : ObservableRecipient, IRecipient<HighlightMessage>
    {
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        public PickPageViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }

        private readonly ISharedDataService _sharedDataService;
        private readonly IFrontService _frontService;

        public PickPageViewModel(ISharedDataService sharedDataService, IFrontService frontService)
        {
            _sharedDataService = sharedDataService;
            _frontService = frontService;
            SurPickViewModelList = [.. Enumerable.Range(0, 4).Select(i => new SurPickViewModel(sharedDataService, frontService, i))];
            HunPickVm = new HunPickViewModel(sharedDataService, frontService);
            MainSurGlobalBanRecordViewModelList = [.. Enumerable.Range(0, 9).Select(i => new MainSurGlobalBanRecordViewModel(sharedDataService, i))];
            MainHunGlobalBanRecordViewModelList = [.. Enumerable.Range(0, 3).Select(i => new MainHunGlobalBanRecordViewModel(sharedDataService, i))];
            AwaySurGlobalBanRecordViewModelList = [.. Enumerable.Range(0, 9).Select(i => new AwaySurGlobalBanRecordViewModel(sharedDataService, i))];
            AwayHunGlobalBanRecordViewModelList = [.. Enumerable.Range(0, 3).Select(i => new AwayHunGlobalBanRecordViewModel(sharedDataService, i))];
            IsActive = true;
        }

        [RelayCommand]
        private void PickingBorderSwitch(string index)
        {
            switch (index)
            {
                case "0":
                    if (SurPickingBorderList[0])
                        _frontService.BreathingStart<BpWindow>("SurPickingBorder", 0, string.Empty);
                    else
                        _frontService.BreathingStop<BpWindow>("SurPickingBorder", 0, string.Empty);
                    break;
                case "1":
                    if (SurPickingBorderList[1])
                        _frontService.BreathingStart<BpWindow>("SurPickingBorder", 1, string.Empty);
                    else
                        _frontService.BreathingStop<BpWindow>("SurPickingBorder", 1, string.Empty);
                    break;
                case "2":
                    if (SurPickingBorderList[2])
                        _frontService.BreathingStart<BpWindow>("SurPickingBorder", 2, string.Empty);
                    else
                        _frontService.BreathingStop<BpWindow>("SurPickingBorder", 2, string.Empty);
                    break;
                case "3":
                    if (SurPickingBorderList[3])
                        _frontService.BreathingStart<BpWindow>("SurPickingBorder", 3, string.Empty);
                    else
                        _frontService.BreathingStop<BpWindow>("SurPickingBorder", 3, string.Empty);
                    break;
                case "0and1":
                    if (SurPickingBorderList[0] && SurPickingBorderList[1])
                    {
                        _frontService.BreathingStart<BpWindow>("SurPickingBorder", 0, string.Empty);
                        _frontService.BreathingStart<BpWindow>("SurPickingBorder", 1, string.Empty);
                    }
                    else
                    {
                        _frontService.BreathingStop<BpWindow>("SurPickingBorder", 0, string.Empty);
                        _frontService.BreathingStop<BpWindow>("SurPickingBorder", 1, string.Empty);
                    }
                    break;
                case "Hun":
                    if (HunPickingBorder)
                        _frontService.BreathingStart<BpWindow>("HunPickingBorder", -1, string.Empty);
                    else
                        _frontService.BreathingStop<BpWindow>("HunPickingBorder", -1, string.Empty);
                    break;
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
                    PickingBorderSwitch(i.ToString());
                }
            }
            else
            {
                for (var i = 0; i < SurPickingBorderList.Count; i++)
                {
                    if (!SurPickingBorderList[i]) continue;
                    SurPickingBorderList[i] = false;
                    PickingBorderSwitch(i.ToString());
                }
            }

            if (message.GameAction == GameAction.PickHun)
            {
                HunPickingBorder = true;
                PickingBorderSwitch("Hun");
            }
            else
            {
                if (!HunPickingBorder) return;
                HunPickingBorder = false;
                PickingBorderSwitch("Hun");
            }
        }

        public ObservableCollection<bool> SurPickingBorderList { get; set; } = [.. Enumerable.Range(0, 4).Select(i => false)];

        [ObservableProperty]
        private bool _hunPickingBorder = false;
        
        public ObservableCollection<SurPickViewModel> SurPickViewModelList { get; set; }
        public HunPickViewModel HunPickVm { get; set; }
        public ObservableCollection<MainSurGlobalBanRecordViewModel> MainSurGlobalBanRecordViewModelList { get; set; }
        public ObservableCollection<MainHunGlobalBanRecordViewModel> MainHunGlobalBanRecordViewModelList { get; set; }
        public ObservableCollection<AwaySurGlobalBanRecordViewModel> AwaySurGlobalBanRecordViewModelList { get; set; }
        public ObservableCollection<AwayHunGlobalBanRecordViewModel> AwayHunGlobalBanRecordViewModelList { get; set; }

        //基于模板基类的VM实现
        public partial class SurPickViewModel : 
            CharaSelectViewModelBase, 
            IRecipient<CharacterSwappedMessage>, 
            IRecipient<PlayerSwappedMessage>, 
            IRecipient<MemberStateChangedMessage>,
            IRecipient<SwapMessage>
        {
            private readonly IFrontService _frontService;
            public string PlayerName => SharedDataService.CurrentGame.SurPlayerList[Index].Member.Name;

            public SurPickViewModel(ISharedDataService sharedDataService, IFrontService frontService, int index = 0) : base(sharedDataService, index)
            {
                _frontService = frontService;
                CharaList = sharedDataService.SurCharaList;
            }

            public override async void SyncChara()
            {
                _frontService.FadeOutAnimation<BpWindow>("SurPick" , Index, string.Empty);
                await Task.Delay(250);
                SharedDataService.CurrentGame.SurPlayerList[Index].Character = SelectedChara;
                _frontService.FadeInAnimation<BpWindow>("SurPick" , Index, string.Empty);
                PreviewImage = SharedDataService.CurrentGame.SurPlayerList[Index].Character?.HeaderImage;
            }

            private void RevertSyncChara()
            {
                SelectedChara = SharedDataService.CurrentGame.SurPlayerList[Index].Character;
                PreviewImage = SharedDataService.CurrentGame.SurPlayerList[Index].Character?.HeaderImage;
            }

            [RelayCommand]
            private void SwapCharacterInPlayers(CharacterChangerCommandParameter parameter)
            {
                (SharedDataService.CurrentGame.SurPlayerList[parameter.Target].Character,
                    SharedDataService.CurrentGame.SurPlayerList[parameter.Source].Character) =
                    (SharedDataService.CurrentGame.SurPlayerList[parameter.Source].Character,
                    SharedDataService.CurrentGame.SurPlayerList[parameter.Target].Character);
                WeakReferenceMessenger.Default.Send(new CharacterSwappedMessage(this));
                OnPropertyChanged();
            }

            public void Receive(CharacterSwappedMessage message)
            {
                RevertSyncChara();
            }

            protected override void SyncIsEnabled()
            {
                throw new NotImplementedException();
            }
            
            protected override bool IsActionNameCorrect(GameAction? action) => action == GameAction.PickSur;

            public void Receive(PlayerSwappedMessage message)
            {
                OnPropertyChanged(nameof(PlayerName));
            }

            public void Receive(MemberStateChangedMessage message)
            {
                OnPropertyChanged(nameof(PlayerName));
            }

            public void Receive(SwapMessage message)
            {
                if(message.IsSwapped)
                    OnPropertyChanged(nameof(PlayerName));
            }
        }

        public class HunPickViewModel : CharaSelectViewModelBase
        {
            private readonly IFrontService _frontService;

            public HunPickViewModel(ISharedDataService sharedDataService, IFrontService frontService) : base(sharedDataService)
            {
                _frontService = frontService;
                CharaList = sharedDataService.HunCharaList;
            }

            public override async void SyncChara()
            {
                _frontService.FadeOutAnimation<BpWindow>("HunPick", -1, string.Empty);
                await Task.Delay(250);
                SharedDataService.CurrentGame.HunPlayer.Character = SelectedChara;
                _frontService.FadeInAnimation<BpWindow>("HunPick", -1, string.Empty);
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

            public MainSurGlobalBanRecordViewModel(ISharedDataService sharedDataService, int index = 0) : base(sharedDataService, index)
            {
                CharaList = sharedDataService.SurCharaList;
            }

            public override void SyncChara() => throw new NotImplementedException();

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

            public MainHunGlobalBanRecordViewModel(ISharedDataService sharedDataService, int index = 0) : base(sharedDataService, index)
            {
                CharaList = sharedDataService.HunCharaList;
            }

            public override void SyncChara() => throw new NotImplementedException();
            
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

            public AwaySurGlobalBanRecordViewModel(ISharedDataService sharedDataService, int index = 0) : base(sharedDataService, index)
            {
                CharaList = sharedDataService.SurCharaList;
            }

            public override void SyncChara() => throw new NotImplementedException();
            
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

            public AwayHunGlobalBanRecordViewModel(ISharedDataService sharedDataService, int index = 0) : base(sharedDataService, index)
            {
                CharaList = sharedDataService.HunCharaList;
            }

            public override void SyncChara() => throw new NotImplementedException();

            protected override void SyncIsEnabled() => throw new NotImplementedException();
            
            protected override bool IsActionNameCorrect(GameAction? action) => false;
        }
    }
}
