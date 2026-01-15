using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Events;
using System.Collections.ObjectModel;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class BanSurPageViewModel : ViewModelBase
{
#pragma warning disable CS8618 
    public BanSurPageViewModel()
#pragma warning restore CS8618 
    {
        //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
    }

    private readonly ISharedDataService _sharedDataService;

    public ObservableCollection<bool> CanCurrentHunBanned => _sharedDataService.CanCurrentSurBannedList;

    public BanSurPageViewModel(ISharedDataService sharedDataService)
    {
        _sharedDataService = sharedDataService;
        BanSurCurrentViewModelList =
        [
            .. Enumerable.Range(0, AppConstants.CurrentBanSurCount)
                .Select(i => new BanSurCurrentViewModel(_sharedDataService, i))
        ];
        BanSurGlobalViewModelList =
        [
            .. Enumerable.Range(0, AppConstants.GlobalBanSurCount)
                .Select(i => new BanSurGlobalViewModel(_sharedDataService, i))
        ];
    }

    public ObservableCollection<BanSurCurrentViewModel> BanSurCurrentViewModelList { get; set; }
    public ObservableCollection<BanSurGlobalViewModel> BanSurGlobalViewModelList { get; set; }

    //基于模板基类的VM实现
    public class BanSurCurrentViewModel : CharaSelectViewModelBase
    {
        public BanSurCurrentViewModel(ISharedDataService sharedDataService, int index = 0) : base(sharedDataService,
            Camp.Sur, index)
        {
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

        protected override Task SyncCharaToSourceAsync()
        {
            SharedDataService.CurrentGame.CurrentSurBannedList[Index] = SelectedChara;
            PreviewImage = SharedDataService.CurrentGame.CurrentSurBannedList[Index]?.HeaderImageSingleColor;
            return Task.CompletedTask;
        }

        protected override void SyncCharaFromSourceAsync()
        {
            SelectedChara = SharedDataService.CurrentGame.CurrentSurBannedList[Index];
            PreviewImage = SelectedChara?.HeaderImageSingleColor;
        }

        protected override void SyncIsEnabled()
        {
            SharedDataService.CanCurrentSurBannedList[Index] = IsEnabled;
        }

        protected override bool IsActionNameCorrect(GameAction? action) => action == GameAction.BanSur;
    }

    public class BanSurGlobalViewModel : CharaSelectViewModelBase
    {
        public BanSurGlobalViewModel(ISharedDataService sharedDataService, int index = 0) : base(sharedDataService,
            Camp.Sur,
            index)
        {
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

        protected override Task SyncCharaToSourceAsync()
        {
            SharedDataService.CurrentGame.SurTeam.GlobalBannedSurList[Index] = SelectedChara;
            PreviewImage = SharedDataService.CurrentGame.SurTeam.GlobalBannedSurList[Index]?.HeaderImageSingleColor;
            return Task.CompletedTask;
        }

        protected override void SyncCharaFromSourceAsync()
        {
            SelectedChara = SharedDataService.CurrentGame.SurTeam.GlobalBannedSurList[Index];
            PreviewImage = SelectedChara?.HeaderImageSingleColor;
        }

        protected override void SyncIsEnabled()
        {
            SharedDataService.CanGlobalSurBannedList[Index] = IsEnabled;
        }

        protected override bool IsActionNameCorrect(GameAction? action) => false;
    }
}