using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Enums;
using neo_bpsys_wpf.Models;
using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.ViewModels.Windows;
using System.Diagnostics;

namespace neo_bpsys_wpf.ViewModels.Pages
{
    /// <summary>
    /// 禁用角色页面视图模型，负责管理角色禁用相关的业务逻辑和数据绑定
    /// </summary>
    public partial class BanHunPageViewModel : ObservableObject
    {
        #region 构造函数

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        /// <summary>
        /// 设计时构造函数，用于XAML设计器可视化
        /// </summary>
        public BanHunPageViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        {
            // 仅用于设计时实例化，运行时由依赖注入容器处理
        }

        #endregion

        #region 属性

        /// <summary>
        /// 获取当前使用的共享数据服务实例
        /// </summary>
        public ISharedDataService SharedDataService { get; }

        #endregion

        #region 构造函数(带参)

        /// <summary>
        /// 初始化新的BanHunPageViewModel实例
        /// </summary>
        /// <param name="sharedDataService">共享数据服务接口实现</param>
        public BanHunPageViewModel(ISharedDataService sharedDataService)
        {
            // 订阅主窗口切换事件
            MainWindowViewModel.Swapped += MainWindowViewModel_Swapped;
            SharedDataService = sharedDataService;
        }

        #endregion

        #region 事件处理方法

        /// <summary>
        /// 主窗口切换事件处理方法，更新全局禁用角色数据
        /// </summary>
        /// <param name="sender">事件发送者对象</param>
        /// <param name="e">事件参数</param>
        private void MainWindowViewModel_Swapped(object? sender, EventArgs e)
        {
            // 从共享数据中获取最新的全局禁用角色记录
            GlobalBannedArray = SharedDataService.CurrentGame.HunTeam.GlobalBannedHunRecordArray;
            OnPropertyChanged();
        }

        #endregion

        #region 数据绑定属性

        /// <summary>
        /// 当前被禁用角色数组（2个槽位），用于绑定UI中的当前禁用选择
        /// </summary>
        [ObservableProperty]
        private Character?[] _currentBannedArray = new Character[2];

        /// <summary>
        /// 全局被禁用角色数组（3个槽位），用于绑定UI中的全局禁用选择
        /// </summary>
        [ObservableProperty]
        private Character?[] _globalBannedArray = new Character[3];

        #endregion

        #region 命令方法

        /// <summary>
        /// 确认当前禁用操作命令
        /// </summary>
        /// <param name="parameter">禁用槽位索引（0或1）</param>
        [RelayCommand]
        private void ConfirmCurrentBan(object parameter)
        {
            if (parameter is not int index) return;

            // 将指定槽位的禁用角色保存到共享数据中
            SharedDataService.CurrentGame.CurrentHunBannedList[index] = CurrentBannedArray[index];
            OnPropertyChanged();
        }

        /// <summary>
        /// 确认全局禁用操作命令
        /// </summary>
        /// <param name="parameter">禁用槽位索引（0-2）</param>
        [RelayCommand]
        private void ConfirmGlobalBan(object parameter)
        {
            if (parameter is not int index) return;

            // 将指定槽位的全局禁用角色保存到共享数据中
            SharedDataService.CurrentGame.HunTeam.GlobalBannedHunList[index] = GlobalBannedArray[index];
            OnPropertyChanged();
        }

        #endregion
    }
}