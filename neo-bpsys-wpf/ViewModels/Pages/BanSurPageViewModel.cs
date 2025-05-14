using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neo_bpsys_wpf.Models;
using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.ViewModels.Windows;
using System;
using System.Diagnostics;
using System.Reflection;

namespace neo_bpsys_wpf.ViewModels.Pages
{
    /// <summary>
    /// BanSurPageViewModel 类负责管理求生者被禁用角色页面的业务逻辑
    /// 实现 MVVM 模式，处理当前对局禁用和全局禁用角色的交互逻辑
    /// </summary>
    public partial class BanSurPageViewModel : ObservableObject
    {
        #region 构造函数

        /// <summary>
        /// 默认构造函数（设计时使用）
        /// 用于 XAML 设计器实例化，实际运行时不执行业务逻辑
        /// </summary>
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        public BanSurPageViewModel()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }

        /// <summary>
        /// 带依赖注入的构造函数
        /// </summary>
        /// <param name="sharedDataService">共享数据服务接口实例</param>
        public BanSurPageViewModel(ISharedDataService sharedDataService)
        {
            MainWindowViewModel.Swapped += MainWindowViewModel_Swapped;
            SharedDataService = sharedDataService;
        }

        #endregion

        #region 属性

        /// <summary>
        /// 获取当前使用的共享数据服务
        /// 提供对游戏数据的统一访问入口
        /// </summary>
        public ISharedDataService SharedDataService { get; }

        /// <summary>
        /// 当前对局被禁用角色数组（4人槽位）
        /// 自动通知属性变更，绑定到界面显示
        /// </summary>
        [ObservableProperty]
        private Character?[] _currentBannedArray = new Character[4];

        /// <summary>
        /// 全局被禁用角色数组（9人槽位）
        /// 自动通知属性变更，绑定到界面显示
        /// </summary>
        [ObservableProperty]
        private Character?[] _globalBannedArray = new Character[9];

        #endregion

        #region 事件处理

        /// <summary>
        /// 主窗口切换事件处理方法
        /// 更新全局禁用角色数组并触发界面刷新
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void MainWindowViewModel_Swapped(object? sender, EventArgs e)
        {
            GlobalBannedArray = SharedDataService.CurrentGame.SurTeam.GlobalBannedHunRecordArray;
            OnPropertyChanged();
        }

        #endregion

        #region 命令方法

        /// <summary>
        /// 确认当前对局禁用角色命令
        /// 将指定槽位的禁用角色保存到共享数据
        /// </summary>
        /// <param name="parameter">槽位索引（int类型）</param>
        [RelayCommand]
        private void ConfirmCurrentBan(object parameter)
        {
            if (parameter is not int index) return;

            SharedDataService.CurrentGame.CurrentSurBannedList[index] = CurrentBannedArray[index];
            OnPropertyChanged();
        }

        /// <summary>
        /// 确认全局禁用角色命令
        /// 将指定槽位的禁用角色保存到全局配置
        /// </summary>
        /// <param name="parameter">槽位索引（int类型）</param>
        [RelayCommand]
        private void ConfirmGlobalBan(object parameter)
        {
            if (parameter is not int index) return;

            SharedDataService.CurrentGame.SurTeam.GlobalBannedSurList[index] = GlobalBannedArray[index];
            OnPropertyChanged();
        }

        #endregion
    }
}