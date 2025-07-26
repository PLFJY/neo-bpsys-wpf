using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Abstractions.Services;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Messages;
using System.Collections.ObjectModel;
using neo_bpsys_wpf.Abstractions.ViewModels;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class BanHunPageViewModel : ViewModelBase, IRecipient<SwapMessage>
{
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
    public BanHunPageViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
    {
        //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
    }

    private readonly ISharedDataService _sharedDataService;

    public ObservableCollection<bool> CanCurrentHunBanned => _sharedDataService.CanCurrentHunBanned;

    public BanHunPageViewModel(ISharedDataService sharedDataService)
    {
        _sharedDataService = sharedDataService;
        BanHunCurrentViewModelList = [.. Enumerable.Range(0, 2).Select(i => new BanHunCurrentViewModel(_sharedDataService, i))];
        BanHunGlobalViewModelList = [.. Enumerable.Range(0, 3).Select(i => new BanHunGlobalViewModel(_sharedDataService, i))];
    }

    public void Receive(SwapMessage message)
    {
        if (message.IsSwapped)
        {
            for (int i = 0; i < _sharedDataService.CurrentGame.HunTeam.GlobalBannedHunRecordArray.Length; i++)
            {
                BanHunGlobalViewModelList[i].SelectedChara = _sharedDataService.CurrentGame.HunTeam.GlobalBannedHunRecordArray[i];
                BanHunGlobalViewModelList[i].SyncCharaAsync();
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

        public override void Receive(BanCountChangedMessage message)
        {
            if (message.ChangedList == BanListName.CanCurrentHunBanned)
            {
                IsEnabled = SharedDataService.CanCurrentHunBanned[Index];
            }
        }

        public override Task SyncCharaAsync()
        {
            SharedDataService.CurrentGame.CurrentHunBannedList[Index] = SelectedChara;
            PreviewImage = SharedDataService.CurrentGame.CurrentHunBannedList[Index]?.HeaderImageSingleColor;
            return Task.CompletedTask;
        }

        protected override void SyncIsEnabled()
        {
            SharedDataService.CanCurrentHunBanned[Index] = IsEnabled;
        }

        protected override bool IsActionNameCorrect(GameAction? action) => action == GameAction.BanHun;
    }

    public class BanHunGlobalViewModel : CharaSelectViewModelBase
    {
        public BanHunGlobalViewModel(ISharedDataService sharedDataService, int index = 0) : base(sharedDataService, index)
        {
            CharaList = sharedDataService.HunCharaList;
            IsEnabled = sharedDataService.CanGlobalHunBanned[index];
        }

        public override void Receive(BanCountChangedMessage message)
        {
            if (message.ChangedList == BanListName.CanGlobalHunBanned)
            {
                IsEnabled = SharedDataService.CanGlobalHunBanned[Index];
            }
        }

        public override Task SyncCharaAsync()
        {
            SharedDataService.CurrentGame.HunTeam.GlobalBannedHunList[Index] = SelectedChara;
            PreviewImage = SharedDataService.CurrentGame.HunTeam.GlobalBannedHunList[Index]?.HeaderImageSingleColor;
            return Task.CompletedTask;
        }

        protected override void SyncIsEnabled()
        {
            SharedDataService.CanGlobalHunBanned[Index] = IsEnabled;
        }

        protected override bool IsActionNameCorrect(GameAction? action) => false;
    }
}