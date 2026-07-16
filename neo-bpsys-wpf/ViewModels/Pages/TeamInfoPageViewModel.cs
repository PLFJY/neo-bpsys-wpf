using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Controls;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.ProductTour;
using neo_bpsys_wpf.Tutorial;
using System.Collections.ObjectModel;
using Player = neo_bpsys_wpf.Core.Models.Player;

namespace neo_bpsys_wpf.ViewModels.Pages;

/// <summary>
/// 队伍信息页面视图模型，管理主客场队伍信息以及在场比赛选手。
/// </summary>
public partial class TeamInfoPageViewModel : ViewModelBase
{
#pragma warning disable CS8618 
    /// <summary>
    /// 用于设计时预览的无参构造函数。
    /// </summary>
    public TeamInfoPageViewModel()
#pragma warning restore CS8618 
    {
        // Decorative constructor for design-time only.
    }

    /// <summary>
    /// 初始化队伍信息页面视图模型。
    /// </summary>
    /// <param name="sharedDataService">共享数据服务</param>
    /// <param name="filePickerService">文件选择服务</param>
    /// <param name="imageSafetyService">前台图片安全校验服务</param>
    /// <param name="tutorialSignalService">教程信号服务</param>
    public TeamInfoPageViewModel(
        ISharedDataService sharedDataService,
        IFilePickerService filePickerService,
        IFrontedImageSafetyService imageSafetyService,
        ITutorialSignalService tutorialSignalService,
        IContentDialogService contentDialogService)
    {
        var sharedDataService1 = sharedDataService;
        HomeTeamInfoViewModel =
            new TeamInfoViewModel(sharedDataService1.HomeTeam, filePickerService, imageSafetyService, tutorialSignalService, contentDialogService);
        AwayTeamInfoViewModel =
            new TeamInfoViewModel(sharedDataService1.AwayTeam, filePickerService, imageSafetyService, tutorialSignalService, contentDialogService);
        OnFieldSurPlayerViewModels =
            [.. Enumerable.Range(0, 4).Select(i => new OnFieldSurPlayerViewModel(sharedDataService1, tutorialSignalService, i))];
        OnFieldHunPlayerVm = new OnFieldHunPlayerViewModel(sharedDataService1);
    }

    /// <summary>
    /// 主队信息视图模型。
    /// </summary>
    public TeamInfoViewModel HomeTeamInfoViewModel { get; }

    /// <summary>
    /// 客队信息视图模型。
    /// </summary>
    public TeamInfoViewModel AwayTeamInfoViewModel { get; }

    /// <summary>
    /// 在场求生者选手视图模型列表。
    /// </summary>
    public ObservableCollection<OnFieldSurPlayerViewModel> OnFieldSurPlayerViewModels { get; }

    /// <summary>
    /// 在场监管者选手视图模型。
    /// </summary>
    public OnFieldHunPlayerViewModel OnFieldHunPlayerVm { get; }

    /// <summary>
    /// 在场求生者选手视图模型，管理单个求生者选手信息。
    /// </summary>
    public partial class OnFieldSurPlayerViewModel : ObservableObjectBase
    {
        private readonly ISharedDataService _sharedDataService;
        private readonly ITutorialSignalService _tutorialSignalService;

        /// <summary>
        /// 初始化在场求生者选手视图模型。
        /// </summary>
        /// <param name="sharedDataService">共享数据服务</param>
        /// <param name="tutorialSignalService">教程信号服务</param>
        /// <param name="index">选手序号</param>
        public OnFieldSurPlayerViewModel(
            ISharedDataService sharedDataService,
            ITutorialSignalService tutorialSignalService,
            int index)
        {
            _sharedDataService = sharedDataService;
            _tutorialSignalService = tutorialSignalService;
            Index = index;
            sharedDataService.CurrentGameChanged += (_, _) => OnPropertyChanged(nameof(ThisPlayer));
            sharedDataService.TeamSwapped += (_, _) => OnPropertyChanged(nameof(ThisPlayer));
        }

        /// <summary>
        /// 获取当前选手数据。
        /// </summary>
        public Player ThisPlayer => _sharedDataService.CurrentGame.SurPlayerList[Index];

        /// <summary>
        /// 获取当前选手序号。
        /// </summary>
        public int Index { get; }

        [RelayCommand]
        private void SwapMembersInPlayers(CharacterChangerCommandParameter parameter)
        {
            _sharedDataService.CurrentGame.SwapMembersInPlayers(parameter.Source, parameter.Target);
            _tutorialSignalService.Publish(
                TutorialSignalIds.MemberPositionSwapped,
                new
                {
                    parameter.Source,
                    parameter.Target,
                    Index
                });
        }
    }

    /// <summary>
    /// 在场监管者选手视图模型，管理监管者选手信息。
    /// </summary>
    public class OnFieldHunPlayerViewModel : ObservableObjectBase
    {
        private readonly ISharedDataService _sharedDataService;

        /// <summary>
        /// 初始化在场监管者选手视图模型。
        /// </summary>
        /// <param name="sharedDataService">共享数据服务</param>
        public OnFieldHunPlayerViewModel(ISharedDataService sharedDataService)
        {
            _sharedDataService = sharedDataService;
            sharedDataService.CurrentGameChanged += (_, _) => OnPropertyChanged(nameof(ThisPlayer));
            sharedDataService.TeamSwapped += (_, _) => OnPropertyChanged(nameof(ThisPlayer));
        }

        /// <summary>
        /// 获取当前监管者选手数据。
        /// </summary>
        public Player ThisPlayer => _sharedDataService.CurrentGame.HunPlayer;
    }
}
