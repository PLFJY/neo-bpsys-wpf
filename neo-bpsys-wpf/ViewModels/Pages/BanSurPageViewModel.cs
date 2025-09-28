using CommunityToolkit.Mvvm.Messaging;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Abstractions.ViewModels;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Messages;
using System.Collections.ObjectModel;
using CharaSelectViewModelBase = neo_bpsys_wpf.Core.Abstractions.ViewModels.CharaSelectViewModelBase;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class BanSurPageViewModel : ViewModelBase
{
    public BanSurPageViewModel()
    {
        //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
    }

    private readonly ISharedDataService _sharedDataService;

    public ObservableCollection<bool> CanCurrentHunBanned => _sharedDataService.CanCurrentSurBannedList;

    public BanSurPageViewModel(ISharedDataService sharedDataService)
    {
        _sharedDataService = sharedDataService;
        BanSurCurrentViewModelList = [.. Enumerable.Range(0, AppConstants.CurrentBanSurCount).Select(i => new BanSurCurrentViewModel(_sharedDataService, i))];
        BanSurGlobalViewModelList = [.. Enumerable.Range(0, AppConstants.GlobalBanHunCount).Select(i => new BanSurGlobalViewModel(_sharedDataService, i))];
        sharedDataService.TeamSwapped += (_, _) =>
        {
            for (int i = 0; i < _sharedDataService.CurrentGame.SurTeam.GlobalBannedSurRecordArray.Length; i++)
            {
                BanSurGlobalViewModelList[i].SelectedChara = _sharedDataService.CurrentGame.SurTeam.GlobalBannedSurRecordArray[i];
                BanSurGlobalViewModelList[i].SyncCharaAsync();
            }
        };
    }

    public ObservableCollection<BanSurCurrentViewModel> BanSurCurrentViewModelList { get; set; }
    public ObservableCollection<BanSurGlobalViewModel> BanSurGlobalViewModelList { get; set; }

    //基于模板基类的VM实现
    public class BanSurCurrentViewModel : CharaSelectViewModelBase
    {
        public BanSurCurrentViewModel(ISharedDataService sharedDataService, int index = 0) : base(sharedDataService, index)
        {
            CharaList = sharedDataService.SurCharaList;
            IsEnabled = sharedDataService.CanCurrentSurBannedList[index];
            SharedDataService.BanCountChanged += OnBanCountChanged;
        }

        private void OnBanCountChanged(object? sender, BanCountChangedEventArgs e)
        {
            if (e.BanListName == BanListName.CanCurrentSurBanned)
            {
                IsEnabled = SharedDataService.CanCurrentSurBannedList[Index];
            }
        }

        public override Task SyncCharaAsync()
        {
            SharedDataService.CurrentGame.CurrentSurBannedList[Index] = SelectedChara;
            PreviewImage = SharedDataService.CurrentGame.CurrentSurBannedList[Index]?.HeaderImageSingleColor;
            return Task.CompletedTask;
        }

        protected override void SyncIsEnabled()
        {
            SharedDataService.CanCurrentSurBannedList[Index] = IsEnabled;
        }

        protected override bool IsActionNameCorrect(GameAction? action) => action == GameAction.BanSur;
    }

    public class BanSurGlobalViewModel : CharaSelectViewModelBase
    {
        public BanSurGlobalViewModel(ISharedDataService sharedDataService, int index = 0) : base(sharedDataService, index)
        {
            CharaList = sharedDataService.SurCharaList;
            IsEnabled = sharedDataService.CanGlobalSurBannedList[index];
            SharedDataService.BanCountChanged += OnBanCountChanged;
        }

        private void OnBanCountChanged(object? sender, BanCountChangedEventArgs e)
        {
            if (e.BanListName == BanListName.CanGlobalSurBanned)
            {
                IsEnabled = SharedDataService.CanGlobalSurBannedList[Index];
            }
        }

        public override Task SyncCharaAsync()
        {
            SharedDataService.CurrentGame.SurTeam.GlobalBannedSurList[Index] = SelectedChara;
            PreviewImage = SharedDataService.CurrentGame.SurTeam.GlobalBannedSurList[Index]?.HeaderImageSingleColor;
            return Task.CompletedTask;
        }

        protected override void SyncIsEnabled()
        {
            SharedDataService.CanGlobalSurBannedList[Index] = IsEnabled;
        }

        protected override bool IsActionNameCorrect(GameAction? action) => false;
    }
}