using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Events;
using System.Collections.ObjectModel;

namespace neo_bpsys_wpf.ViewModels.Pages;

/// <summary>
/// 求生者禁用页面视图模型，管理当前局和全局求生者禁用列表。
/// </summary>
public partial class BanSurPageViewModel : ViewModelBase
{
#pragma warning disable CS8618
    /// <summary>
    /// 用于设计时预览的无参构造函数。
    /// </summary>
    public BanSurPageViewModel()
#pragma warning restore CS8618
    {
        // Decorative constructor for design-time only.
    }

    private readonly ISharedDataService _sharedDataService;

    /// <summary>
    /// 获取当前局求生者禁用位是否启用的列表。
    /// </summary>
    public ObservableCollection<bool> CanCurrentHunBanned => _sharedDataService.CanCurrentSurBannedList;

    /// <summary>
    /// 初始化求生者禁用页面视图模型。
    /// </summary>
    /// <param name="sharedDataService">共享数据服务</param>
    /// <param name="characterSelectionService">角色选择服务</param>
    public BanSurPageViewModel(ISharedDataService sharedDataService,
        ICharacterSelectionService characterSelectionService)
    {
        _sharedDataService = sharedDataService;
        BanSurCurrentViewModelList =
        [
            .. Enumerable.Range(0, AppConstants.CurrentBanSurCount)
                .Select(i => new BanSurCurrentViewModel(_sharedDataService, characterSelectionService, i))
        ];
        BanSurGlobalViewModelList =
        [
            .. Enumerable.Range(0, AppConstants.GlobalBanSurCount)
                .Select(i => new BanSurGlobalViewModel(_sharedDataService, i))
        ];
    }

    /// <summary>
    /// 当前局求生者禁用视图模型列表。
    /// </summary>
    public ObservableCollection<BanSurCurrentViewModel> BanSurCurrentViewModelList { get; set; }

    /// <summary>
    /// 全局求生者禁用视图模型列表。
    /// </summary>
    public ObservableCollection<BanSurGlobalViewModel> BanSurGlobalViewModelList { get; set; }

    //基于模板基类的VM实现
    /// <summary>
    /// 当前局求生者禁用视图模型。
    /// </summary>
    public class BanSurCurrentViewModel : CharaSelectViewModelBase
    {
        private readonly ICharacterSelectionService _characterSelectionService;

        /// <summary>
        /// 初始化当前局求生者禁用视图模型。
        /// </summary>
        /// <param name="sharedDataService">共享数据服务</param>
        /// <param name="characterSelectionService">角色选择服务</param>
        /// <param name="index">序号</param>
        public BanSurCurrentViewModel(ISharedDataService sharedDataService,
            ICharacterSelectionService characterSelectionService,
            int index = 0) : base(sharedDataService, Camp.Sur, index)
        {
            _characterSelectionService = characterSelectionService;
            IsEnabled = sharedDataService.CanCurrentSurBannedList[index];
            SharedDataService.BanCountChanged += OnBanCountChanged;
            _characterSelectionService.CharacterBanned += (sender, e) =>
            {
                if(e.Camp == Camp.Sur && e.Index == index)
                {
                    SyncCharaFromSourceAsync();
                }
            };
        }

        private void OnBanCountChanged(object? sender, BanCountChangedEventArgs e)
        {
            if (e.BanListName == BanListName.CanCurrentSurBanned)
            {
                IsEnabled = SharedDataService.CanCurrentSurBannedList[Index];
            }
        }

        protected override async Task SyncCharaToSourceAsync()
        {
            await _characterSelectionService.BanCharacterAsync(Camp.Sur, Index, SelectedChara);
            PreviewImage = SharedDataService.CurrentGame.CurrentSurBannedList[Index]?.HeaderImageSingleColor;
        }

        protected override void SyncCharaFromSourceAsync()
        {
            SelectedChara = SharedDataService.CurrentGame.CurrentSurBannedList[Index];
            PreviewImage = SelectedChara?.HeaderImageSingleColor;
        }

        protected override void SyncIsEnabled()
        {
            if (SharedDataService.CanCurrentSurBannedList[Index] != IsEnabled)
                SharedDataService.CanCurrentSurBannedList[Index] = IsEnabled;
        }

        protected override bool IsActionNameCorrect(GameAction? action) => action == GameAction.BanSur;
    }

    /// <summary>
    /// 全局求生者禁用视图模型。
    /// </summary>
    public class BanSurGlobalViewModel : CharaSelectViewModelBase
    {
        /// <summary>
        /// 初始化全局求生者禁用视图模型。
        /// </summary>
        /// <param name="sharedDataService">共享数据服务</param>
        /// <param name="index">序号</param>
        public BanSurGlobalViewModel(ISharedDataService sharedDataService, int index = 0) : base(sharedDataService,
            Camp.Sur,
            index)
        {
            IsEnabled = sharedDataService.CanGlobalSurBannedList[index];
            SharedDataService.BanCountChanged += OnBanCountChanged;
            SharedDataService.TeamSwapped += (sender, args) =>
                SyncCharaFromSourceAsync();
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
            if (SharedDataService.CanGlobalSurBannedList[Index] != IsEnabled)
                SharedDataService.CanGlobalSurBannedList[Index] = IsEnabled;
        }

        protected override bool IsActionNameCorrect(GameAction? action) => false;
    }
}
