using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Events;
using System.Collections.ObjectModel;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class BanHunPageViewModel : ViewModelBase
{
#pragma warning disable CS8618 
    public BanHunPageViewModel()
#pragma warning restore CS8618 
    {
        //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
    }

    private readonly ISharedDataService _sharedDataService;

    public ObservableCollection<bool> CanCurrentHunBanned => _sharedDataService.CanCurrentHunBannedList;

    public BanHunPageViewModel(ISharedDataService sharedDataService)
    {
        _sharedDataService = sharedDataService;
        BanHunCurrentViewModelList =
        [
            .. Enumerable.Range(0, AppConstants.CurrentBanHunCount)
                .Select(i => new BanHunCurrentViewModel(_sharedDataService, i))
        ];
        BanHunGlobalViewModelList =
        [
            .. Enumerable.Range(0, AppConstants.GlobalBanHunCount)
                .Select(i => new BanHunGlobalViewModel(_sharedDataService, i))
        ];
    }

    public ObservableCollection<BanHunCurrentViewModel> BanHunCurrentViewModelList { get; set; }
    public ObservableCollection<BanHunGlobalViewModel> BanHunGlobalViewModelList { get; set; }

    //基于模板基类的VM实现
    public class BanHunCurrentViewModel : CharaSelectViewModelBase
    {
        private readonly ISharedDataService _sharedDataService;

        public BanHunCurrentViewModel(ISharedDataService sharedDataService, int index = 0) : base(sharedDataService,
            Camp.Hun, index)
        {
            _sharedDataService = sharedDataService;
            IsEnabled = sharedDataService.CanCurrentHunBannedList[index];
            SharedDataService.BanCountChanged += OnBanCountChanged;
        }

        private void OnBanCountChanged(object? sender, BanCountChangedEventArgs e)
        {
            if (e.BanListName == BanListName.CanCurrentHunBanned)
            {
                IsEnabled = SharedDataService.CanCurrentHunBannedList[Index];
            }
        }

        protected override Task SyncCharaToSourceAsync()
        {
            SharedDataService.CurrentGame.CurrentHunBannedList[Index] = SelectedChara;
            PreviewImage = SharedDataService.CurrentGame.CurrentHunBannedList[Index]?.HeaderImageSingleColor;
            return Task.CompletedTask;
        }

        protected override void SyncCharaFromSourceAsync()
        {
            SelectedChara = SharedDataService.CurrentGame.CurrentHunBannedList[Index];
            PreviewImage = SelectedChara?.HeaderImageSingleColor;
        }

        protected override void SyncIsEnabled()
        {
            SharedDataService.CanCurrentHunBannedList[Index] = IsEnabled;
        }

        protected override bool IsActionNameCorrect(GameAction? action) => action == GameAction.BanHun;
    }

    public class BanHunGlobalViewModel : CharaSelectViewModelBase
    {
        public BanHunGlobalViewModel(ISharedDataService sharedDataService, int index = 0) : base(sharedDataService,
            Camp.Hun,
            index)
        {
            IsEnabled = sharedDataService.CanGlobalHunBannedList[index];
            SharedDataService.BanCountChanged += OnBanCountChanged;
        }

        private void OnBanCountChanged(object? sender, BanCountChangedEventArgs e)
        {
            if (e.BanListName == BanListName.CanGlobalHunBanned)
            {
                IsEnabled = SharedDataService.CanGlobalHunBannedList[Index];
            }
        }

        protected override Task SyncCharaToSourceAsync()
        {
            SharedDataService.CurrentGame.HunTeam.GlobalBannedHunList[Index] = SelectedChara;
            PreviewImage = SharedDataService.CurrentGame.HunTeam.GlobalBannedHunList[Index]?.HeaderImageSingleColor;
            return Task.CompletedTask;
        }

        protected override void SyncCharaFromSourceAsync()
        {
            SelectedChara = SharedDataService.CurrentGame.HunTeam.GlobalBannedHunList[Index];
            PreviewImage = SelectedChara?.HeaderImageSingleColor;
        }

        protected override void SyncIsEnabled()
        {
            SharedDataService.CanGlobalHunBannedList[Index] = IsEnabled;
        }

        protected override bool IsActionNameCorrect(GameAction? action) => false;
    }
}