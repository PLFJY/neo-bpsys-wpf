using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Events;
using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.ViewModels.Pages;

namespace neo_bpsys_wpf.ViewModels.Windows
{
    /// <summary>
    /// 比分窗口的视图模型，负责管理设计模式状态和共享数据服务
    /// </summary>
    public partial class ScoreWindowViewModel : ObservableObject
    {
        /// <summary>
        /// 设计时使用的构造函数（XAML设计器专用）
        /// </summary>
#pragma warning disable CS8618
        public ScoreWindowViewModel()
#pragma warning restore CS8618 
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }

        /// <summary>
        /// 共享数据服务接口，提供跨组件数据共享能力
        /// </summary>
        public ISharedDataService SharedDataService { get; }

        /// <summary>
        /// 主构造函数，初始化共享数据服务并订阅设计模式变更事件
        /// </summary>
        /// <param name="sharedDataService">共享数据服务实例，用于跨组件数据交互</param>
        public ScoreWindowViewModel(ISharedDataService sharedDataService)
        {
            // 订阅前端管理页面的设计模式状态变更通知
            FrontManagePageViewModel.DesignModeChanged += OnDesignModeChanged;
            SharedDataService = sharedDataService;
        }

        /// <summary>
        /// 标识当前是否处于设计模式状态（通过MVVM Toolkit自动生成属性）
        /// </summary>
        [ObservableProperty]
        private bool _isDesignMode = false;

        /// <summary>
        /// 处理设计模式状态变更事件，更新本地状态值
        /// </summary>
        /// <param name="sender">事件源对象</param>
        /// <param name="e">包含设计模式状态参数的事件参数</param>
        private void OnDesignModeChanged(object? sender, DesignModeChangedEventArgs e)
        {
            IsDesignMode = e.IsDesignMode;
        }
    }
}