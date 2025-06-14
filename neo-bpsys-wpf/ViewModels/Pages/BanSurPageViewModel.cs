using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Messages;
using neo_bpsys_wpf.Services;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using neo_bpsys_wpf.Enums;

namespace neo_bpsys_wpf.ViewModels.Pages
{
    public partial class BanSurPageViewModel : ObservableRecipient, IRecipient<SwapMessage>
    {
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        public BanSurPageViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }

        private readonly ISharedDataService _sharedDataService;

        public ObservableCollection<bool> CanCurrentHunBanned => _sharedDataService.CanCurrentSurBanned;

        public BanSurPageViewModel(ISharedDataService sharedDataService)
        {
            _sharedDataService = sharedDataService;
            BanSurCurrentViewModelList = [.. Enumerable.Range(0, 4).Select(i => new BanSurCurrentViewModel(_sharedDataService, i))];
            BanSurGlobalViewModelList = [.. Enumerable.Range(0, 9).Select(i => new BanSurGlobalViewModel(_sharedDataService, i))];
            IsActive = true;
        }

        public void Receive(SwapMessage message)
        {
            if (message.IsSwapped)
            {
                for (int i = 0; i < _sharedDataService.CurrentGame.SurTeam.GlobalBannedSurRecordArray.Length; i++)
                {
                    BanSurGlobalViewModelList[i].SelectedChara = _sharedDataService.CurrentGame.SurTeam.GlobalBannedSurRecordArray[i];
                    BanSurGlobalViewModelList[i].SyncChara();
                }
                OnPropertyChanged();
            }
        }

        public ObservableCollection<BanSurCurrentViewModel> BanSurCurrentViewModelList { get; set; }
        public ObservableCollection<BanSurGlobalViewModel> BanSurGlobalViewModelList { get; set; }

        //基于模板基类的VM实现
        public class BanSurCurrentViewModel : CharaSelectViewModelBase
        {
            public BanSurCurrentViewModel(ISharedDataService sharedDataService, int index = 0) : base(sharedDataService, index)
            {
                CharaList = sharedDataService.SurCharaList;
                IsEnabled = sharedDataService.CanCurrentSurBanned[index];
            }

            public override void Receive(BanCountChangedMessage message)
            {
                if (message.ChangedList == BanListName.CanCurrentSurBanned)
                {
                    IsEnabled = SharedDataService.CanCurrentSurBanned[Index];
                }
            }

            public override void SyncChara()
            {
                SharedDataService.CurrentGame.CurrentSurBannedList[Index] = SelectedChara;
                PreviewImage = SharedDataService.CurrentGame.CurrentSurBannedList[Index]?.HeaderImageSingleColor;
            }

            protected override void SyncIsEnabled()
            {
                SharedDataService.CanCurrentSurBanned[Index] = IsEnabled;
            }

            protected override bool IsActionNameCorrect(GameAction? action) => action == GameAction.BanSur;
        }

        public class BanSurGlobalViewModel : CharaSelectViewModelBase
        {
            public BanSurGlobalViewModel(ISharedDataService sharedDataService, int index = 0) : base(sharedDataService, index)
            {
                CharaList = sharedDataService.SurCharaList;
                IsEnabled = sharedDataService.CanGlobalSurBanned[index];
            }

            public override void Receive(BanCountChangedMessage message)
            {
                if (message.ChangedList == BanListName.CanGlobalSurBanned)
                {
                    IsEnabled = SharedDataService.CanGlobalSurBanned[Index];
                }
            }

            public override void SyncChara()
            {
                SharedDataService.CurrentGame.SurTeam.GlobalBannedSurList[Index] = SelectedChara;
                PreviewImage = SharedDataService.CurrentGame.SurTeam.GlobalBannedSurList[Index]?.HeaderImageSingleColor;
            }

            protected override void SyncIsEnabled()
            {
                SharedDataService.CanGlobalSurBanned[Index] = IsEnabled;
            }

            protected override bool IsActionNameCorrect(GameAction? action) => false;
        }
    }
}
