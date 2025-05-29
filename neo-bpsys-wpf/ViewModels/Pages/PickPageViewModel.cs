using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using neo_bpsys_wpf.CustomControls;
using neo_bpsys_wpf.Messages;
using neo_bpsys_wpf.Models;
using neo_bpsys_wpf.Services;
using System.Collections.ObjectModel;

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

        public ISharedDataService SharedDataService { get; }

        public PickPageViewModel(ISharedDataService sharedDataService)
        {
            SharedDataService = sharedDataService;
            SurPickViewModelList = [.. Enumerable.Range(0, 4).Select(i => new SurPickViewModel(sharedDataService, i))];
            HunpickVM = new(sharedDataService);
            MainSurGlobalBanRecordViewModelList = [.. Enumerable.Range(0, 9).Select(i => new MainSurGlobalBanRecordViewModel(sharedDataService, i))];
            MainHunGlobalBanRecordViewModelList = [.. Enumerable.Range(0, 3).Select(i => new MainHunGlobalBanRecordViewModel(sharedDataService, i))];
            AwaySurGlobalBanRecordViewModelList = [.. Enumerable.Range(0, 9).Select(i => new AwaySurGlobalBanRecordViewModel(sharedDataService, i))];
            AwayHunGlobalBanRecordViewModelList = [.. Enumerable.Range(0, 3).Select(i => new AwayHunGlobalBanRecordViewModel(sharedDataService, i))];
        }

        public ObservableCollection<SurPickViewModel> SurPickViewModelList { get; set; }
        public HunPickViewModel HunpickVM { get; set; }
        public ObservableCollection<MainSurGlobalBanRecordViewModel> MainSurGlobalBanRecordViewModelList { get; set; }
        public ObservableCollection<MainHunGlobalBanRecordViewModel> MainHunGlobalBanRecordViewModelList { get; set; }
        public ObservableCollection<AwaySurGlobalBanRecordViewModel> AwaySurGlobalBanRecordViewModelList { get; set; }
        public ObservableCollection<AwayHunGlobalBanRecordViewModel> AwayHunGlobalBanRecordViewModelList { get; set; }

        //基于模板基类的VM实现
        public partial class SurPickViewModel : CharaSelectViewModelBase, IRecipient<CharacterSwapMessage>, IRecipient<ValueChangedMessage<string>>
        {
            public string PlayerName
            {
                get => _sharedDataService.CurrentGame.SurPlayerList[Index].Member.Name;
            }

            public SurPickViewModel(ISharedDataService sharedDataService, int index = 0) : base(sharedDataService, index)
            {
                CharaList = sharedDataService.SurCharaList;
            }

            public override void SyncChara()
            {
                _sharedDataService.CurrentGame.SurPlayerList[Index].Character = SelectedChara;
                PreviewImage = _sharedDataService.CurrentGame.SurPlayerList[Index].Character?.HeaderImage;
            }

            private void RevertSyncChara()
            {
                SelectedChara = _sharedDataService.CurrentGame.SurPlayerList[Index].Character;
                PreviewImage = _sharedDataService.CurrentGame.SurPlayerList[Index].Character?.HeaderImage;
            }

            [RelayCommand]
            private void SwapCharacterInPlayers(CharacterChangerCommandParameter parameter)
            {
                (_sharedDataService.CurrentGame.SurPlayerList[parameter.Target].Character,
                    _sharedDataService.CurrentGame.SurPlayerList[parameter.Source].Character) =
                    (_sharedDataService.CurrentGame.SurPlayerList[parameter.Source].Character,
                    _sharedDataService.CurrentGame.SurPlayerList[parameter.Target].Character);
                WeakReferenceMessenger.Default.Send(new CharacterSwapMessage(this));
                OnPropertyChanged();
            }

            public void Receive(CharacterSwapMessage message)
            {
                RevertSyncChara();
            }

            public void Receive(ValueChangedMessage<string> message)
            {
                if (message.Value == "PlayerName")
                {
                    OnPropertyChanged(nameof(PlayerName));
                }
            }

            public override void SyncIsEnabled()
            {
                throw new NotImplementedException();
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
                _sharedDataService.CurrentGame.HunPlayer.Character = SelectedChara;
                PreviewImage = _sharedDataService.CurrentGame.HunPlayer.Character?.HeaderImage;
            }

            public override void SyncIsEnabled()
            {
                throw new NotImplementedException();
            }
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
                    _sharedDataService.MainTeam.GlobalBannedSurRecordArray[Index] = _recordedChara;
                }
            }

            public MainSurGlobalBanRecordViewModel(ISharedDataService sharedDataService, int index = 0) : base(sharedDataService, index)
            {
                CharaList = sharedDataService.SurCharaList;
            }

            public override void SyncChara() => throw new NotImplementedException();

            public override void SyncIsEnabled()
            {
                throw new NotImplementedException();
            }
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
                    _sharedDataService.MainTeam.GlobalBannedHunRecordArray[Index] = _recordedChara;
                }
            }

            public MainHunGlobalBanRecordViewModel(ISharedDataService sharedDataService, int index = 0) : base(sharedDataService, index)
            {
                CharaList = sharedDataService.HunCharaList;
            }

            public override void SyncChara() => throw new NotImplementedException();

            public override void SyncIsEnabled()
            {
                throw new NotImplementedException();
            }
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
                    _sharedDataService.AwayTeam.GlobalBannedSurRecordArray[Index] = _recordedChara;
                }
            }

            public AwaySurGlobalBanRecordViewModel(ISharedDataService sharedDataService, int index = 0) : base(sharedDataService, index)
            {
                CharaList = sharedDataService.SurCharaList;
            }

            public override void SyncChara() => throw new NotImplementedException();

            public override void SyncIsEnabled()
            {
                throw new NotImplementedException();
            }
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
                    _sharedDataService.AwayTeam.GlobalBannedHunRecordArray[Index] = _recordedChara;
                }
            }

            public AwayHunGlobalBanRecordViewModel(ISharedDataService sharedDataService, int index = 0) : base(sharedDataService, index)
            {
                CharaList = sharedDataService.HunCharaList;
            }

            public override void SyncChara() => throw new NotImplementedException();

            public override void SyncIsEnabled()
            {
                throw new NotImplementedException();
            }

        }
    }
}
