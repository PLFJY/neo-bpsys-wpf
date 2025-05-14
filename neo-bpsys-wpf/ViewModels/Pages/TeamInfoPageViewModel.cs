using System.Diagnostics;
using System.Security.Permissions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.CustomControls;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Services;

namespace neo_bpsys_wpf.ViewModels.Pages
{
    /// <summary>
    /// 提供战队信息页面的视图模型逻辑
    /// </summary>
    public partial class TeamInfoPageViewModel : ObservableObject
    {
        /// <summary>
        /// 设计时使用的构造函数（仅用于XAML预览）
        /// </summary>
#pragma warning disable CS8618
        public TeamInfoPageViewModel()
#pragma warning restore CS8618
        {
            // 设计器专用构造函数，配合IsDesignTimeCreatable=True使用
        }

        public ISharedDataService SharedDataService { get; }
        private readonly IFilePickerService _filePickerService;
        private readonly IMessageBoxService _messageBoxService;

        /// <summary>
        /// 初始化战队信息页面视图模型
        /// </summary>
        /// <param name="sharedDataService">共享数据服务</param>
        /// <param name="filePickerService">文件选择服务</param>
        /// <param name="messageBoxService">消息框服务</param>
        public TeamInfoPageViewModel(
            ISharedDataService sharedDataService,
            IFilePickerService filePickerService,
            IMessageBoxService messageBoxService
        )
        {
            SharedDataService = sharedDataService;
            _filePickerService = filePickerService;
            _messageBoxService = messageBoxService;

            // 初始化主队和客队的视图模型
            MainTeamInfoViewModel = new(
                SharedDataService.MainTeam,
                _filePickerService,
                _messageBoxService
            );

            AwayTeamInfoViewModel = new(
                SharedDataService.AwayTeam,
                _filePickerService,
                _messageBoxService
            );
        }

        /// <summary>
        /// 主队信息视图模型
        /// </summary>
        public TeamInfoViewModel MainTeamInfoViewModel { get; }

        /// <summary>
        /// 客队信息视图模型
        /// </summary>
        public TeamInfoViewModel AwayTeamInfoViewModel { get; }

        /// <summary>
        /// 交换两个选手的成员信息
        /// </summary>
        /// <param name="parameter">
        /// 包含交换源索引（Source）和目标索引（Target）的命令参数
        /// </param>
        [RelayCommand]
        private void SwapMembersInPlayers(CharacterChangerCommandParameter parameter)
        {
            // 使用元组语法交换两个位置的成员数据
            (SharedDataService.CurrentGame.SurPlayerList[parameter.Target].Member,
                SharedDataService.CurrentGame.SurPlayerList[parameter.Source].Member) =
                (SharedDataService.CurrentGame.SurPlayerList[parameter.Source].Member,
                SharedDataService.CurrentGame.SurPlayerList[parameter.Target].Member);

            // 通知属性变更以更新UI
            OnPropertyChanged();
        }
    }
}