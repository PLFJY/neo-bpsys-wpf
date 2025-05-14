using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Events;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Services;
using neo_bpsys_wpf.ViewModels.Pages;
using System.Windows.Media;

namespace neo_bpsys_wpf.ViewModels.Windows
{
    /// <summary>
    /// 小部件窗口的视图模型，用于管理窗口小部件的交互逻辑和设计模式状态
    /// </summary>
    public partial class WidgetsWindowViewModel : ObservableObject
    {
#pragma warning disable CS8618
        /// <summary>
        /// 设计模式专用构造函数，用于XAML设计器预览
        /// </summary>
        /// <remarks>
        /// 该构造函数仅在设计时（Blend/Visual Studio设计器）使用，配合IsDesignTimeCreatable=True特性，
        /// 避免在运行时调用时引发非空字段警告
        /// </remarks>
        public WidgetsWindowViewModel()
#pragma warning restore CS8618
        {
            //Decorative constructor, used in conjunction with IsDesignTimeCreatable=True
        }

        /// <summary>
        /// 获取或设置设计模式状态标识
        /// </summary>
        /// <value>
        /// true表示当前处于设计编辑模式，false表示正常使用模式
        /// </value>
        [ObservableProperty]
        private bool _isDesignMode = false;

        /// <summary>
        /// 获取共享数据服务实例
        /// </summary>
        public ISharedDataService SharedDataService { get; }

        /// <summary>
        /// 主构造函数，用于运行时初始化
        /// </summary>
        /// <param name="sharedDataService">共享数据服务依赖注入</param>
        public WidgetsWindowViewModel(ISharedDataService sharedDataService)
        {
            // 注册设计模式变更事件监听
            FrontManagePageViewModel.DesignModeChanged += OnDesignModeChanged;

            // 初始化共享数据服务
            SharedDataService = sharedDataService;
        }

        /// <summary>
        /// 处理设计模式变更事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">包含设计模式状态的事件参数</param>
        private void OnDesignModeChanged(object? sender, DesignModeChangedEventArgs e)
        {
            // 更新当前设计模式状态
            IsDesignMode = e.IsDesignMode;
        }
    }
}