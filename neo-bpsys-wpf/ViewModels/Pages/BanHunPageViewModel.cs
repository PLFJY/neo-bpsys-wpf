using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Events;
using System.Collections.ObjectModel;

namespace neo_bpsys_wpf.ViewModels.Pages;

/// <summary>
/// 监管者禁用页面视图模型，管理当前局和全局监管者禁用列表。
/// </summary>
public partial class BanHunPageViewModel : ViewModelBase
{
#pragma warning disable CS8618
    /// <summary>
    /// 用于设计时预览的无参构造函数。
    /// </summary>
    public BanHunPageViewModel()
#pragma warning restore CS8618
    {
        // Decorative constructor for design-time only.
    }

    private readonly ISharedDataService _sharedDataService;

    /// <summary>
    /// 获取当前局监管者禁用位是否启用的列表。
    /// </summary>
    public ObservableCollection<bool> CanCurrentHunBanned => _sharedDataService.CanCurrentHunBannedList;

    /// <summary>
    /// 初始化监管者禁用页面视图模型。
    /// </summary>
    /// <param name="sharedDataService">共享数据服务</param>
    /// <param name="characterSelectionService">角色选择服务</param>
    public BanHunPageViewModel(ISharedDataService sharedDataService,
        ICharacterSelectionService characterSelectionService)
    {
        _sharedDataService = sharedDataService;
        BanHunCurrentViewModelList =
        [
            .. Enumerable.Range(0, AppConstants.CurrentBanHunCount)
                .Select(i => new BanHunCurrentViewModel(_sharedDataService, characterSelectionService, i))
        ];
        BanHunGlobalViewModelList =
        [
            .. Enumerable.Range(0, AppConstants.GlobalBanHunCount)
                .Select(i => new BanHunGlobalViewModel(_sharedDataService, i))
        ];
    }

    /// <summary>
    /// 当前局监管者禁用视图模型列表。
    /// </summary>
    public ObservableCollection<BanHunCurrentViewModel> BanHunCurrentViewModelList { get; set; }

    /// <summary>
    /// 全局监管者禁用视图模型列表。
    /// </summary>
    public ObservableCollection<BanHunGlobalViewModel> BanHunGlobalViewModelList { get; set; }

    //基于模板基类的VM实现
    /// <summary>
    /// 当前局监管者禁用视图模型。
    /// </summary>
    public class BanHunCurrentViewModel : CharaSelectViewModelBase
    {
        private readonly ISharedDataService _sharedDataService;
        private readonly ICharacterSelectionService _characterSelectionService;

        /// <summary>
        /// 初始化当前局监管者禁用视图模型。
        /// </summary>
        /// <param name="sharedDataService">共享数据服务</param>
        /// <param name="characterSelectionService">角色选择服务</param>
        /// <param name="index">序号</param>
        public BanHunCurrentViewModel(ISharedDataService sharedDataService,
            ICharacterSelectionService characterSelectionService,
            int index = 0)
            : base(sharedDataService, Camp.Hun, index)
        {
            _sharedDataService = sharedDataService;
            _characterSelectionService = characterSelectionService;
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

        protected override async Task SyncCharaToSourceAsync()
        {
            await _characterSelectionService.BanCharacterAsync(Camp.Hun, Index, SelectedChara);
            PreviewImage = SharedDataService.CurrentGame.CurrentHunBannedList[Index]?.HeaderImageSingleColor;
        }

        protected override void SyncCharaFromSourceAsync()
        {
            SelectedChara = SharedDataService.CurrentGame.CurrentHunBannedList[Index];
            PreviewImage = SelectedChara?.HeaderImageSingleColor;
        }

        protected override void SyncIsEnabled()
        {
            if (SharedDataService.CanCurrentHunBannedList[Index] != IsEnabled)
                SharedDataService.CanCurrentHunBannedList[Index] = IsEnabled;
        }

        protected override bool IsActionNameCorrect(GameAction? action) => action == GameAction.BanHun;
    }

    /// <summary>
    /// 全局监管者禁用视图模型。
    /// </summary>
    public class BanHunGlobalViewModel : CharaSelectViewModelBase
    {
        /// <summary>
        /// 初始化全局监管者禁用视图模型。
        /// </summary>
        /// <param name="sharedDataService">共享数据服务</param>
        /// <param name="index">序号</param>
        public BanHunGlobalViewModel(ISharedDataService sharedDataService, int index = 0) : base(sharedDataService,
            Camp.Hun,
            index)
        {
            IsEnabled = sharedDataService.CanGlobalHunBannedList[index];
            SharedDataService.BanCountChanged += OnBanCountChanged;
            SharedDataService.TeamSwapped += (sender, args) =>
                SyncCharaFromSourceAsync();
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
        {if (SharedDataService.CanGlobalHunBannedList[Index] != IsEnabled)
                
            SharedDataService.CanGlobalHunBannedList[Index] = IsEnabled;
        }

        protected override bool IsActionNameCorrect(GameAction? action) => false;
    }
}
