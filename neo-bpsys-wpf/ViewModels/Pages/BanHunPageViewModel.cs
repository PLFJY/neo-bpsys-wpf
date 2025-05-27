using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Messages;
using neo_bpsys_wpf.Services;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace neo_bpsys_wpf.ViewModels.Pages
{
    public partial class BanHunPageViewModel : ObservableRecipient, IRecipient<SwapMessage>
    {
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        public BanHunPageViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }

        public ISharedDataService SharedDataService { get; }

        public BanHunPageViewModel(ISharedDataService sharedDataService)
        {
            SharedDataService = sharedDataService;
            BanHunCurrentViewModelList = [.. Enumerable.Range(0, 2).Select(i => new BanHunCurrentViewModel(SharedDataService, i))];
            BanHunGlobalViewModelList = [.. Enumerable.Range(0, 3).Select(i => new BanHunGlobalViewModel(SharedDataService, i))];
            SharedDataService.CanCurrentHunBanned.CollectionChanged += CanCurrentHunBanned_CollectionChanged;
            SharedDataService.CanGlobalHunBanned.CollectionChanged += CanGlobalHunBanned_CollectionChanged;
            IsActive = true;
        }
        //刷新Ban位状态
        private void CanCurrentHunBanned_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Replace)
            {
                for (int i = 0; i < SharedDataService.CanCurrentHunBanned.Count; i++)
                {
                    BanHunCurrentViewModelList[i].IsEnabled = SharedDataService.CanCurrentHunBanned[i];
                }
            }
        }

        private void CanGlobalHunBanned_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Replace)
            {
                for (int i = 0; i < SharedDataService.CanGlobalHunBanned.Count; i++)
                {
                    BanHunGlobalViewModelList[i].IsEnabled = SharedDataService.CanGlobalHunBanned[i];
                }
            }
        }

        public void Receive(SwapMessage message)
        {
            if (message.IsSwapped)
            {
                for (int i = 0; i < SharedDataService.CurrentGame.SurTeam.GlobalBannedHunRecordArray.Length; i++)
                {
                    BanHunGlobalViewModelList[i].SelectedChara = SharedDataService.CurrentGame.SurTeam.GlobalBannedHunRecordArray[i];
                    BanHunGlobalViewModelList[i].SyncChara();
                }
                OnPropertyChanged();
            }
        }

        public ObservableCollection<BanHunCurrentViewModel> BanHunCurrentViewModelList { get; set; }
        public ObservableCollection<BanHunGlobalViewModel> BanHunGlobalViewModelList { get; set; }

        //基于模板基类的VM实现
        public class BanHunCurrentViewModel : CharaSelectViewModelBase
        {
            public BanHunCurrentViewModel(ISharedDataService sharedDataService, int index = 0) : base(sharedDataService, index)
            {
                CharaList = sharedDataService.HunCharaList;
                IsEnabled = sharedDataService.CanCurrentHunBanned[index];
            }

            public override void SyncChara()
            {
                _sharedDataService.CurrentGame.CurrentHunBannedList[Index] = SelectedChara;
                PreviewImage = _sharedDataService.CurrentGame.CurrentHunBannedList[Index]?.HeaderImage_SingleColor;
            }
        }

        public class BanHunGlobalViewModel : CharaSelectViewModelBase
        {
            public BanHunGlobalViewModel(ISharedDataService sharedDataService, int index = 0) : base(sharedDataService, index)
            {
                CharaList = sharedDataService.HunCharaList;
                IsEnabled = sharedDataService.CanGlobalHunBanned[index];
            }

            public override void SyncChara()
            {
                _sharedDataService.CurrentGame.HunTeam.GlobalBannedHunList[Index] = SelectedChara;
                PreviewImage = _sharedDataService.CurrentGame.HunTeam.GlobalBannedHunList[Index]?.HeaderImage_SingleColor;
            }
        }
    }
}
