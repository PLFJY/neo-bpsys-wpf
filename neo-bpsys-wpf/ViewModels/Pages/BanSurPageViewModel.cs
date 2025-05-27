using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Messages;
using neo_bpsys_wpf.Services;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

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

        public ISharedDataService SharedDataService { get; }

        public BanSurPageViewModel(ISharedDataService sharedDataService)
        {
            SharedDataService = sharedDataService;
            BanSurCurrentViewModelList = [.. Enumerable.Range(0, 4).Select(i => new BanSurCurrentViewModel(SharedDataService, i))];
            BanSurGlobalViewModelList = [.. Enumerable.Range(0, 9).Select(i => new BanSurGlobalViewModel(SharedDataService, i))];
            SharedDataService.CanCurrentSurBanned.CollectionChanged += CanCurrentSurBanned_CollectionChanged;
            SharedDataService.CanGlobalSurBanned.CollectionChanged += CanGlobalSurBanned_CollectionChanged;
            IsActive = true;
        }
        //刷新Ban位状态
        private void CanCurrentSurBanned_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Replace)
            {
                for (int i = 0; i < SharedDataService.CanCurrentSurBanned.Count; i++)
                {
                    BanSurCurrentViewModelList[i].IsEnabled = SharedDataService.CanCurrentSurBanned[i];
                }
            }
        }

        private void CanGlobalSurBanned_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Replace)
            {
                for (int i = 0; i < SharedDataService.CanGlobalSurBanned.Count; i++)
                {
                    BanSurGlobalViewModelList[i].IsEnabled = SharedDataService.CanGlobalSurBanned[i];
                }
            }
        }

        public void Receive(SwapMessage message)
        {
            if (message.IsSwapped)
            {
                for (int i = 0; i < SharedDataService.CurrentGame.SurTeam.GlobalBannedSurRecordArray.Length; i++)
                {
                    BanSurGlobalViewModelList[i].SelectedChara = SharedDataService.CurrentGame.SurTeam.GlobalBannedSurRecordArray[i];
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

            public override void SyncChara()
            {
                _sharedDataService.CurrentGame.CurrentSurBannedList[Index] = SelectedChara;
                PreviewImage = _sharedDataService.CurrentGame.CurrentSurBannedList[Index]?.HeaderImage_SingleColor;
            }
        }

        public class BanSurGlobalViewModel : CharaSelectViewModelBase
        {
            public BanSurGlobalViewModel(ISharedDataService sharedDataService, int index = 0) : base(sharedDataService, index)
            {
                CharaList = sharedDataService.SurCharaList;
                IsEnabled = sharedDataService.CanGlobalSurBanned[index];
            }

            public override void SyncChara()
            {
                _sharedDataService.CurrentGame.SurTeam.GlobalBannedSurList[Index] = SelectedChara;
                PreviewImage = _sharedDataService.CurrentGame.SurTeam.GlobalBannedSurList[Index]?.HeaderImage_SingleColor;
            }
        }
    }
}
