using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using System.Collections.ObjectModel;
using CharaSelectViewModelBase = neo_bpsys_wpf.ViewModels.CharaSelectViewModelBase;

namespace neo_bpsys_wpf.ViewModels.Pages;

public partial class BanHunPageViewModel : ViewModelBase
{
    public BanHunPageViewModel()
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
        sharedDataService.TeamSwapped += (_, _) =>
        {
            for (var i = 0; i < _sharedDataService.CurrentGame.HunTeam.GlobalBannedHunRecordArray.Length; i++)
            {
                BanHunGlobalViewModelList[i].SelectedChara =
                    _sharedDataService.CurrentGame.HunTeam.GlobalBannedHunRecordArray[i];
                BanHunGlobalViewModelList[i].SyncCharaAsync();
            }
        };
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

        public override Task SyncCharaAsync()
        {
            SharedDataService.CurrentGame.CurrentHunBannedList[Index] = SelectedChara;
            PreviewImage = SharedDataService.CurrentGame.CurrentHunBannedList[Index]?.HeaderImageSingleColor;
            return Task.CompletedTask;
        }

        protected override void SyncIsEnabled()
        {
            SharedDataService.CanCurrentHunBannedList[Index] = IsEnabled;
        }

        protected override bool IsActionNameCorrect(GameAction? action) => action == GameAction.BanHun;
    }

    public class BanHunGlobalViewModel : CharaSelectViewModelBase
    {
        private readonly ISharedDataService _sharedDataService;

        public BanHunGlobalViewModel(ISharedDataService sharedDataService, int index = 0) : base(sharedDataService,
            Camp.Hun,
            index)
        {
            _sharedDataService = sharedDataService;
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

        public override Task SyncCharaAsync()
        {
            SharedDataService.CurrentGame.HunTeam.GlobalBannedHunList[Index] = SelectedChara;
            PreviewImage = SharedDataService.CurrentGame.HunTeam.GlobalBannedHunList[Index]?.HeaderImageSingleColor;
            return Task.CompletedTask;
        }

        protected override void SyncIsEnabled()
        {
            SharedDataService.CanGlobalHunBannedList[Index] = IsEnabled;
        }

        protected override bool IsActionNameCorrect(GameAction? action) => false;
    }
}