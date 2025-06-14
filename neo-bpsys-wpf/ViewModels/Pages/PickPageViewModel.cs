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

namespace neo_bpsys_wpf.ViewModels.Pages
{
    public partial class PickPageViewModel : ObservableObject
    {
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        public PickPageViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }

        private ISharedDataService _sharedDataService;

        public PickPageViewModel(ISharedDataService sharedDataService)
        {
            _sharedDataService = sharedDataService;
            SurPickViewModelList = [.. Enumerable.Range(0, 4).Select(i => new SurPickViewModel(sharedDataService, i))];
            HunPickVm = new HunPickViewModel(sharedDataService);
            MainSurGlobalBanRecordViewModelList = [.. Enumerable.Range(0, 9).Select(i => new MainSurGlobalBanRecordViewModel(sharedDataService, i))];
            MainHunGlobalBanRecordViewModelList = [.. Enumerable.Range(0, 3).Select(i => new MainHunGlobalBanRecordViewModel(sharedDataService, i))];
            AwaySurGlobalBanRecordViewModelList = [.. Enumerable.Range(0, 9).Select(i => new AwaySurGlobalBanRecordViewModel(sharedDataService, i))];
            AwayHunGlobalBanRecordViewModelList = [.. Enumerable.Range(0, 3).Select(i => new AwayHunGlobalBanRecordViewModel(sharedDataService, i))];
        }

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
            public string PlayerName => SharedDataService.CurrentGame.SurPlayerList[Index].Member.Name;

            public SurPickViewModel(ISharedDataService sharedDataService, int index = 0) : base(sharedDataService, index)
            {
                CharaList = sharedDataService.SurCharaList;
            }

            public override void SyncChara()
            {
                SharedDataService.CurrentGame.SurPlayerList[Index].Character = SelectedChara;
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
            public HunPickViewModel(ISharedDataService sharedDataService) : base(sharedDataService)
            {
                CharaList = sharedDataService.HunCharaList;
            }

            public override void SyncChara()
            {
                SharedDataService.CurrentGame.HunPlayer.Character = SelectedChara;
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
