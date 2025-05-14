using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.CustomControls;
using neo_bpsys_wpf.Models;
using neo_bpsys_wpf.Services;

namespace neo_bpsys_wpf.ViewModels.Pages
{
    /// <summary>
    /// 角色选择页面视图模型，负责管理玩家角色选择和确认逻辑。
    /// 实现INotifyPropertyChanged接口以支持数据绑定和属性通知。
    /// </summary>
    public partial class PickPageViewModel : ObservableObject
    {
        #region 构造函数

        /// <summary>
        /// 默认构造函数，用于设计时实例化。
        /// 此构造函数不执行任何实际初始化逻辑。
        /// </summary>
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        public PickPageViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }

        /// <summary>
        /// 使用共享数据服务初始化PickPageViewModel实例。
        /// </summary>
        /// <param name="sharedDataService">用于访问共享游戏数据的服务实例</param>
        public PickPageViewModel(ISharedDataService sharedDataService)
        {
            SharedDataService = sharedDataService;
            PickedSurList = new Character[4];
        }

        #endregion

        #region 属性

        /// <summary>
        /// 获取当前使用的共享数据服务实例。
        /// </summary>
        public ISharedDataService SharedDataService { get; }

        /// <summary>
        /// 存储已选择的4个求生者角色（可能包含null值）。
        /// 当数组元素变更时，会通过INotifyPropertyChanged通知UI更新。
        /// </summary>
        [ObservableProperty]
        private Character?[] _pickedSurList;

        /// <summary>
        /// 存储已选择的监管者角色（可能为null）。
        /// 当属性值变更时，会通过INotifyPropertyChanged通知UI更新。
        /// </summary>
        [ObservableProperty]
        private Character? _pickedHun;

        #endregion

        #region 命令方法

        /// <summary>
        /// 确认指定索引位置的求生者角色选择。
        /// </summary>
        /// <param name="parameter">包含目标索引位置的对象参数，必须为int类型</param>
        /// <remarks>
        /// 将PickedSurList中指定索引的角色保存到共享数据服务的对应位置，
        /// 并触发属性变更通知以更新UI绑定。
        /// </remarks>
        [RelayCommand]
        private void ConfirmPickedSur(object parameter)
        {
            if (parameter is not int index) return;

            SharedDataService.CurrentGame.SurPlayerList[index].Character = PickedSurList[index];
            OnPropertyChanged();
        }

        /// <summary>
        /// 确认监管者角色选择。
        /// </summary>
        /// <remarks>
        /// 将PickedHun属性值保存到共享数据服务的主控角色位置，
        /// 并触发属性变更通知以更新UI绑定。
        /// </remarks>
        [RelayCommand]
        private void ConfirmPickedHun()
        {
            SharedDataService.CurrentGame.HunPlayer.Character = PickedHun;
            OnPropertyChanged();
        }

        /// <summary>
        /// 交换两个玩家槽位的角色。
        /// </summary>
        /// <param name="parameter">包含源和目标索引的CharacterChangerCommandParameter参数</param>
        /// <remarks>
        /// 通过解构赋值交换指定源和目标索引位置的角色，
        /// 并触发属性变更通知以更新UI绑定。
        /// </remarks>
        [RelayCommand]
        private void SwapCharacterInPlayers(CharacterChangerCommandParameter parameter)
        {
            (SharedDataService.CurrentGame.SurPlayerList[parameter.Target].Character,
                SharedDataService.CurrentGame.SurPlayerList[parameter.Source].Character) =
                (SharedDataService.CurrentGame.SurPlayerList[parameter.Source].Character,
                SharedDataService.CurrentGame.SurPlayerList[parameter.Target].Character);

            OnPropertyChanged();
        }

        #endregion
    }
}